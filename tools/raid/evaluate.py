"""Evaluate the engine's RAID scores with RAID's own code, and break the result down.

The threshold search and the hit counting are RAID's `run_evaluation`, called unchanged. Everything
this script adds is bookkeeping on top of RAID's output, plus three things RAID does not report:
the product's own boundary, the length floor, and the prediction table in Docs/Detection/prediction.md.

Scores reach RAID divided by 100. RAID's threshold search assumes a 0-1 range (it starts at the
mean and moves in steps of 0.5), and dividing by a constant changes no ranking and no AUROC.
"""

import argparse
import hashlib
import json
import math
from pathlib import Path

import numpy as np
import pandas as pd
from raid import run_evaluation
import raid

CHAT = ["chatgpt", "gpt4", "llama-chat", "cohere-chat", "mpt-chat", "mistral-chat"]
BASE = ["gpt2", "mpt", "mistral", "cohere"]
BETWEEN = ["gpt3"]
GROUPS = {"chat": CHAT, "base": BASE, "between": BETWEEN}
LENGTH_BUCKETS = [(0, 199, "under 200"), (200, 399, "200-399"), (400, 648, "400-648"), (649, 10**9, "649 or more")]


def load(sample_csv: Path, scores_jsonl: Path, texts_jsonl: Path) -> pd.DataFrame:
    meta = pd.read_csv(sample_csv)
    scores = pd.read_json(scores_jsonl, lines=True)
    if len(scores) != len(meta) or set(scores["id"]) != set(meta["id"]):
        raise SystemExit("The scores do not cover exactly the sampled ids.")
    df = meta.merge(scores, on="id", how="left", validate="one_to_one")
    if df["score"].isnull().any():
        raise SystemExit(f"{df['score'].isnull().sum()} rows have no score.")
    # RAID's human set repeats some texts verbatim. RAID counts rows and so does the headline; the
    # checks this project adds on top count each distinct text once.
    hashes = {}
    with open(texts_jsonl, encoding="utf-8") as f:
        for line in f:
            row = json.loads(line)
            hashes[row["id"]] = hashlib.sha256(row["text"].encode("utf-8")).hexdigest()
    df["textHash"] = df["id"].map(hashes)
    if df["textHash"].isnull().any():
        raise SystemExit("Some sampled ids have no text.")
    return df


def lowest_counted(threshold: float) -> float:
    """The lowest one-decimal engine score that RAID's cutoff counts, using RAID's own comparison
    (score / 100 >= threshold). Printing the cutoff rounded can move a whole tie across it."""
    v = math.floor(threshold * 1000) / 10
    while (v / 100.0) < threshold:
        v = round(v + 0.1, 1)
    while v >= 0.1 and (round(v - 0.1, 1) / 100.0) >= threshold:
        v = round(v - 0.1, 1)
    return v


def pick(scores, **want):
    """The one record RAID produced for this split, with every unnamed axis at 'all'."""
    keys = {"domain": "all", "model": "all", "decoding": "all", "repetition_penalty": "all", "attack": "all"}
    keys.update(want)
    for s in scores:
        if all(s[k] == v for k, v in keys.items()):
            return s
    return None


def rate(record, fpr="0.05"):
    if record is None or record["accuracy"].get(fpr) is None:
        return None
    return record["accuracy"][fpr]["accuracy"]


def counts(record, fpr="0.05"):
    a = record["accuracy"][fpr]
    return a["tp"], a["fn"]


def evaluate(df: pd.DataFrame, per_domain: bool) -> dict:
    predictions = [{"id": i, "score": s / 100.0} for i, s in zip(df["id"], df["score"])]
    raid_df = df[["id", "model", "decoding", "repetition_penalty", "attack", "domain"]].copy()
    return run_evaluation(predictions, raid_df, target_fpr=[0.05, 0.01], per_domain_tuning=per_domain)


def summarise(result: dict, df: pd.DataFrame) -> dict:
    scores = result["scores"]
    machine = df[df["model"] != "human"]
    out = {
        "overall": {
            "tpr05": rate(pick(scores)),
            "tpr01": rate(pick(scores), "0.01"),
            "auroc": pick(scores)["auroc"],
        },
        "thresholds": result["thresholds"],
        "lowestCounted": ({d: lowest_counted(t) for d, t in result["thresholds"]["0.05"].items()}
                          if isinstance(result["thresholds"]["0.05"], dict)
                          else lowest_counted(result["thresholds"]["0.05"])),
        "trueFpr": result["fpr"],
        "byModel": {},
        "byDomain": {},
        "byDecoding": {},
        "byRepetitionPenalty": {},
        "byGroup": {},
    }
    for m in sorted(machine["model"].unique()):
        r = pick(scores, model=m)
        out["byModel"][m] = {"tpr05": rate(r), "tpr01": rate(r, "0.01"), "auroc": r["auroc"], "n": int((machine["model"] == m).sum())}
    for d in sorted(machine["domain"].unique()):
        r = pick(scores, domain=d)
        out["byDomain"][d] = {"tpr05": rate(r), "tpr01": rate(r, "0.01"), "auroc": r["auroc"]}
    for s in sorted(machine["decoding"].dropna().unique()):
        r = pick(scores, decoding=s)
        out["byDecoding"][s] = {"tpr05": rate(r), "auroc": r["auroc"]}
    for p in sorted(machine["repetition_penalty"].dropna().unique()):
        r = pick(scores, repetition_penalty=p)
        out["byRepetitionPenalty"][p] = {"tpr05": rate(r), "auroc": r["auroc"]}

    # A group's rate is pooled from RAID's own per-model counts, not averaged, so a generator with
    # more texts counts for more, exactly as it would in RAID's 'all'.
    for name, models in GROUPS.items():
        tp = fn = 0
        for m in models:
            r = pick(scores, model=m)
            if r is None or r["accuracy"].get("0.05") is None:
                continue
            a, b = counts(r)
            tp, fn = tp + a, fn + b
        out["byGroup"][name] = {"tpr05": tp / (tp + fn) if tp + fn else None, "models": models, "n": tp + fn}
    return out


def by_length(df: pd.DataFrame, thresholds05, boundary: float) -> list:
    """RAID does not split by length. Same per-domain thresholds, applied by hand, and the product's
    own boundary beside them."""
    rows = []
    t = df["domain"].map(thresholds05) if isinstance(thresholds05, dict) else thresholds05
    flagged = (df["score"] / 100.0) >= t
    at_boundary = df["score"] >= boundary
    for lo, hi, label in LENGTH_BUCKETS:
        in_bucket = df["words"].between(lo, hi)
        human = in_bucket & (df["model"] == "human")
        machine = in_bucket & (df["model"] != "human")
        rows.append({
            "bucket": label,
            "machine": int(machine.sum()),
            "human": int(human.sum()),
            "tpr05": float(flagged[machine].mean()) if machine.any() else None,
            "fpr05": float(flagged[human].mean()) if human.any() else None,
            "humanAtBoundary": float(at_boundary[human].mean()) if human.any() else None,
            "machineAtBoundary": float(at_boundary[machine].mean()) if machine.any() else None,
        })
    return rows


def product_view(df: pd.DataFrame, boundary: float, floor: int) -> dict:
    """What the shipped product would say: one boundary, and no verdict under the length floor."""
    human = df["model"] == "human"
    machine = ~human
    over = df["score"] >= boundary
    measured = df["words"] >= floor
    return {
        "boundary": boundary,
        "lengthFloorWords": floor,
        "ignoringFloor": {
            "fpr": float(over[human].mean()),
            "tpr": float(over[machine].mean()),
        },
        "shareAtOrAboveFloor": {
            "human": float(measured[human].mean()),
            "machine": float(measured[machine].mean()),
        },
        "withFloor": {
            "humanFlagged": float((over & measured)[human].mean()),
            "machineFlagged": float((over & measured)[machine].mean()),
        },
        "medianWords": {
            "human": float(df.loc[human, "words"].median()),
            "machine": float(df.loc[machine, "words"].median()),
        },
    }


def matched(df: pd.DataFrame, thresholds05: dict, column: str) -> dict:
    """A setting compared only across the generators that were run with both of its values, so a
    difference is not just a difference in which generators are in each pool. Same cutoffs."""
    machine = df[df["model"] != "human"]
    values = sorted(machine[column].dropna().unique())
    models = sorted(m for m, g in machine.groupby("model") if set(g[column].dropna().unique()) == set(values))
    sub = machine[machine["model"].isin(models)]
    flagged = (sub["score"] / 100.0) >= sub["domain"].map(thresholds05)
    out = {"models": models}
    for v in values:
        mask = sub[column] == v
        out[v] = {"tpr05": float(flagged[mask].mean()), "n": int(mask.sum())}
    return out


def wilson(k: int, n: int, z: float = 1.959964) -> tuple:
    """The interval docs/CALIBRATION.md publishes (2 of 296 -> 0.19%-2.43%), so the two compare."""
    if n == 0:
        return (None, None)
    p = k / n
    d = 1 + z * z / n
    centre = (p + z * z / (2 * n)) / d
    margin = z * ((p * (1 - p) / n + z * z / (4 * n * n)) ** 0.5) / d
    return (max(0.0, centre - margin), min(1.0, centre + margin))


def long_human_check(df: pd.DataFrame, boundary: float, floor: int, published: dict) -> dict:
    """RAID's human texts at or above the floor: long human writing from registers the calibration
    corpus does not contain (newswire, book summaries, film reviews, verse). Same boundary, same
    interval, nothing tuned. An outside sample of a different population, not a replication."""
    rows = df[(df["model"] == "human") & (df["words"] >= floor)]
    long_human = rows.drop_duplicates("textHash")
    over = long_human["score"] >= boundary
    k = int(over.sum())
    n = int(len(long_human))
    lo, hi = wilson(k, n)
    by_domain = {}
    for d, g in long_human.groupby("domain"):
        dk, dn = int((g["score"] >= boundary).sum()), int(len(g))
        by_domain[d] = {"texts": dn, "flagged": dk, "interval": list(wilson(dk, dn))}
    flagged_rules = long_human.loc[over, "rules"].explode().dropna().value_counts()
    has_burst = long_human["rules"].apply(lambda rs: "stat.burstiness" in rs)
    for d, g in long_human.groupby("domain"):
        by_domain[d]["burstiness"] = float(has_burst[g.index].mean())
    not_news = long_human["domain"] != "news"
    return {
        "rows": int(len(rows)),
        "texts": n,
        "flagged": k,
        "rate": k / n if n else None,
        "interval": [lo, hi],
        "byDomain": by_domain,
        "rulesInFlagged": {r: int(c) for r, c in flagged_rules.items()},
        "burstinessAll": float(has_burst.mean()),
        "withoutNews": {"texts": int(not_news.sum()), "flagged": int((over & not_news).sum())},
        "published": {"texts": published["texts"], "flagged": published["flaggedAtThreshold"],
                      "interval": [published["rateLow"], published["rateHigh"]]},
    }


def rule_lift(df: pd.DataFrame, min_texts: int = 50) -> list:
    """For each rule: in what share of human texts, chat-tuned texts and base-model texts it fires.
    Reported, never acted on inside this measurement."""
    groups = {
        "human": df["model"] == "human",
        "chat": df["model"].isin(CHAT),
        "base": df["model"].isin(BASE),
    }
    exploded = df[["id", "model", "rules"]].explode("rules").dropna(subset=["rules"])
    rows = []
    for rule, sub in exploded.groupby("rules"):
        ids = set(sub["id"])
        hit = df["id"].isin(ids)
        if hit.sum() < min_texts:
            continue
        share = {g: float(hit[mask].mean()) for g, mask in groups.items()}
        rows.append({
            "rule": rule,
            "texts": int(hit.sum()),
            "human": share["human"],
            "chat": share["chat"],
            "base": share["base"],
            "chatLift": share["chat"] / share["human"] if share["human"] > 0 else None,
        })
    rows.sort(key=lambda r: -(r["chatLift"] or 0))
    return rows


def domain_rules(df: pd.DataFrame, domain: str, top: int = 8) -> list:
    """Why a domain separates well or badly: the rules whose share differs most between the two classes."""
    d = df[df["domain"] == domain]
    human = d["model"] == "human"
    exploded = d[["id", "rules"]].explode("rules").dropna(subset=["rules"])
    rows = []
    for rule, sub in exploded.groupby("rules"):
        hit = d["id"].isin(set(sub["id"]))
        rows.append({"rule": rule, "human": float(hit[human].mean()), "machine": float(hit[~human].mean())})
    rows.sort(key=lambda r: -(r["machine"] - r["human"]))
    return rows[:top]


def rerun_summary(published: dict, rerun_path) -> dict | None:
    """Which fields of the published calibration moved when the current engine re-ran it."""
    if not rerun_path:
        return None
    rerun = json.loads(Path(rerun_path).read_text(encoding="utf-8"))
    return {
        "engine": rerun["engine"],
        "measuredOn": rerun["measuredOn"],
        "fieldsChanged": sorted(k for k in published if published[k] != rerun.get(k)),
        "file": "Docs/Detection/calibration-rerun.json",
    }


def raid_mix(per_domain: dict, dataset_by_model: dict) -> float:
    """The overall rate re-weighted to RAID's proportions instead of this sample's equal ones."""
    by_model = per_domain["byModel"]
    total = sum(dataset_by_model[m] for m in by_model)
    return sum(by_model[m]["tpr05"] * dataset_by_model[m] for m in by_model) / total


def predictions(per_domain: dict) -> list:
    g = per_domain["byGroup"]
    chat, base, between = g["chat"]["tpr05"], g["base"]["tpr05"], g["between"]["tpr05"]
    domains = {d: v["tpr05"] for d, v in per_domain["byDomain"].items()}
    top3 = sorted(domains, key=lambda d: -(domains[d] or 0))[:3]
    lo, hi = min(chat, base), max(chat, base)
    overall = per_domain["overall"]["tpr05"]
    return [
        {"id": "P1", "claim": "chat-tuned rate at least twice the base-model rate",
         "measured": f"chat {chat:.3f}, base {base:.3f}, ratio {chat / base:.2f}" if base else f"chat {chat:.3f}, base 0",
         "held": bool(base == 0 or chat / base >= 2)},
        {"id": "P2", "claim": "base-model rate under 20%", "measured": f"{base:.3f}", "held": bool(base < 0.20)},
        {"id": "P3", "claim": "text-davinci-003 between the two groups",
         "measured": f"{between:.3f} (range {lo:.3f}-{hi:.3f})", "held": bool(lo <= between <= hi)},
        {"id": "P4", "claim": "poetry and recipes not among the three best domains",
         "measured": "best three: " + ", ".join(f"{d} {domains[d]:.3f}" for d in top3),
         "held": bool("poetry" not in top3 and "recipes" not in top3)},
        {"id": "overall", "claim": "between 15% and 40% overall, without attacks",
         "measured": f"{overall:.3f}", "held": bool(0.15 <= overall <= 0.40)},
    ]


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--sample", required=True, help="sample.csv")
    ap.add_argument("--scores", required=True, help="scores.jsonl from `signsofai-calibrate raid`")
    ap.add_argument("--texts", required=True, help="sample.jsonl, read only to recognise repeated texts")
    ap.add_argument("--manifest", required=True, help="sample.json")
    ap.add_argument("--calibration", required=True, help="published-calibration.json")
    ap.add_argument("--calibration-rerun", help="published-calibration.json as regenerated by the current engine; "
                    "committed as Docs/Detection/calibration-rerun.json so the comparison has a source")
    ap.add_argument("--engine", required=True, help="engine version that produced the scores")
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    df = load(Path(args.sample), Path(args.scores), Path(args.texts))
    calibration = json.loads(Path(args.calibration).read_text(encoding="utf-8"))

    per_domain = summarise(evaluate(df, per_domain=True), df)
    single = summarise(evaluate(df, per_domain=False), df)

    results = {
        "engine": args.engine,
        "raidBench": raid.__version__,
        "sample": json.loads(Path(args.manifest).read_text(encoding="utf-8")),
        "language": "en",
        "detectedNotEnglish": {
            "human": float((df.loc[df["model"] == "human", "detectedLanguage"] != "en").mean()),
            "machine": float((df.loc[df["model"] != "human", "detectedLanguage"] != "en").mean()),
        },
        "perDomainThreshold": per_domain,
        "singleThreshold": single,
        "byLength": by_length(df, per_domain["thresholds"]["0.05"], calibration["recommendedThreshold"]),
        "matchedRepetitionPenalty": matched(df, per_domain["thresholds"]["0.05"], "repetition_penalty"),
        "matchedDecoding": matched(df, per_domain["thresholds"]["0.05"], "decoding"),
        "humanTexts": {"rows": int((df["model"] == "human").sum()),
                       "distinct": int(df.loc[df["model"] == "human", "textHash"].nunique())},
        "product": product_view(df, calibration["recommendedThreshold"], calibration["shortestWords"]),
        "calibration": {
            "engine": calibration["engine"],
            "measuredOn": calibration["measuredOn"],
            "burstinessShare": next((r["textShare"] for r in calibration["noisiestRules"]
                                     if r["ruleId"] == "stat.burstiness"), None),
            "rerun": rerun_summary(calibration, args.calibration_rerun),
        },
        "predictions": predictions(per_domain),
        "predictionsSingleThreshold": predictions(single),
        "raidMixTpr05": raid_mix(per_domain, json.loads(Path(args.manifest).read_text(encoding="utf-8"))["datasetMachineByModel"]),
        "longHumanCheck": long_human_check(df, calibration["recommendedThreshold"], calibration["shortestWords"], calibration),
        "rules": rule_lift(df),
        "domainRules": {d: domain_rules(df, d) for d in ("poetry", "reviews", "recipes", "reddit")},
        "scoreQuantiles": {
            "human": {q: float(np.quantile(df.loc[df["model"] == "human", "score"], q)) for q in (0.5, 0.9, 0.95, 0.99)},
            "machine": {q: float(np.quantile(df.loc[df["model"] != "human", "score"], q)) for q in (0.5, 0.9, 0.95, 0.99)},
        },
    }
    Path(args.out).write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")

    o = per_domain["overall"]
    print(f"RAID, per-domain threshold: TPR@5% {o['tpr05']:.3f}  TPR@1% {o['tpr01']}  AUROC {o['auroc']:.3f}")
    s = single["overall"]
    print(f"RAID, single threshold:     TPR@5% {s['tpr05']}  AUROC {s['auroc']:.3f}")
    for name, v in per_domain["byGroup"].items():
        print(f"  {name:<8} {v['tpr05']:.3f}  (n={v['n']})")
    lh = results["longHumanCheck"]
    print(f"  long humans: {lh['flagged']} of {lh['texts']} at the boundary, interval {lh['interval']}")
    for p in results["predictions"]:
        print(f"  {p['id']:<8} {'HELD   ' if p['held'] else 'FAILED '} {p['measured']}")


if __name__ == "__main__":
    main()
