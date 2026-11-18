"""RAID's cheap attacks, applied to a subsample, and what they do to the score and to the scanner.

  make      draws a seeded subsample from the RAID sample and attacks it with RAID's own attack code
  evaluate  reads the engine's scores for the attacked texts and reports two things kept apart:
            how detection moves under each attack, and how often the character scanner reports it

Only the attacks that need no language model are run. RAID's `attack.py` imports every attacker at
the top, including the ones that load a model, so the attackers are imported one by one and their
strength is read from `attack.py` itself rather than copied here, where it could drift.
"""

import argparse
import importlib
import json
import random
import re
import sys
from pathlib import Path

import pandas as pd

# The attackers RAID ships that run without a model or a GPU. paraphrase, synonym and
# perplexity_misspelling need one and are left out; the results page says so.
LIGHT = {
    "homoglyph": ("homoglyph", "HomoglyphAttack"),
    "zero_width_space": ("zero_width_space", "ZeroWidthSpaceAttack"),
    "whitespace": ("whitespace", "WhiteSpaceAttack"),
    "upper_lower": ("upper_lower", "UpperLowerFlipAttack"),
    "number": ("alter_number", "AlterNumbersAttack"),
    "article_deletion": ("article_deletion", "ArticleDeletionAttack"),
    "insert_paragraphs": ("insert_paragraphs", "InsertParagraphsAttack"),
    "alternative_spelling": ("alternative_spelling", "AlternativeSpellingAttack"),
}


def strengths(attack_py: Path) -> dict:
    """{ClassName: N} exactly as RAID's get_attack() constructs each attacker."""
    source = attack_py.read_text(encoding="utf-8")
    return {cls: float(n) for cls, n in re.findall(r"return (\w+)\(([\d.]+)\)", source)}


def build(adversarial_dir: Path) -> dict:
    sys.path.insert(0, str(adversarial_dir))
    n = strengths(adversarial_dir / "attack.py")
    attackers = {}
    for name, (module, cls) in LIGHT.items():
        mod = importlib.import_module(f"attackers.{module}")
        attackers[name] = (getattr(mod, cls)(n[cls]), n[cls])
    return attackers


def make(args) -> None:
    import nltk

    nltk_dir = Path(args.nltk_data)
    nltk_dir.mkdir(parents=True, exist_ok=True)
    nltk.data.path.insert(0, str(nltk_dir))
    for pkg in ("punkt", "punkt_tab"):
        nltk.download(pkg, download_dir=str(nltk_dir), quiet=True)

    random.seed(args.seed)
    meta = pd.read_csv(args.sample_csv)
    texts = {}
    with open(args.sample_jsonl, encoding="utf-8") as f:
        for line in f:
            row = json.loads(line)
            texts[row["id"]] = row["text"]

    human = meta[meta["model"] == "human"].sample(n=args.per_class, random_state=args.seed)
    machine = meta[meta["model"] != "human"].sample(n=args.per_class, random_state=args.seed)
    base = pd.concat([human, machine]).sort_values("id")

    attackers = build(Path(args.adversarial))
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    rows = []
    with open(out / "attacked.jsonl", "w", encoding="utf-8", newline="\n") as f:
        for name, (attacker, n) in attackers.items():
            for r in base.itertuples(index=False):
                attacked = attacker.attack(texts[r.id])["generation"]
                aid = f"{r.id}::{name}"
                f.write(json.dumps({"id": aid, "text": attacked}, ensure_ascii=False) + "\n")
                rows.append({"id": aid, "source": r.id, "model": r.model, "decoding": r.decoding,
                             "repetition_penalty": r.repetition_penalty, "domain": r.domain, "attack": name})
    pd.DataFrame(rows).to_csv(out / "attacked.csv", index=False)
    (out / "attacked.json").write_text(json.dumps({
        "seed": args.seed,
        "perClass": args.per_class,
        "attacks": {k: v[1] for k, v in attackers.items()},
        "leftOut": ["paraphrase", "synonym", "perplexity_misspelling"],
    }, indent=2) + "\n", encoding="utf-8")
    print(f"{len(rows)} attacked texts from {len(base)} originals, {len(attackers)} attacks -> {out}")


def evaluate(args) -> None:
    results = json.loads(Path(args.results).read_text(encoding="utf-8"))
    thresholds = results["perDomainThreshold"]["thresholds"]["0.05"]

    meta = pd.read_csv(args.attacked_csv)
    scores = pd.read_json(args.attacked_scores, lines=True)
    if len(scores) != len(meta) or set(scores["id"]) != set(meta["id"]):
        raise SystemExit("The attacked scores do not cover exactly the attacked ids.")
    df = meta.merge(scores, on="id", how="left", validate="one_to_one")
    sample = pd.read_csv(args.sample_csv)
    original_scores = pd.read_json(args.scores, lines=True)
    if len(original_scores) != len(sample) or set(original_scores["id"]) != set(sample["id"]):
        raise SystemExit("The original scores do not cover exactly the sampled ids.")
    original = sample.merge(original_scores, on="id", how="left", validate="one_to_one")
    for frame, name in ((df, "attacked"), (original, "original")):
        if frame["score"].isnull().any():
            raise SystemExit(f"{frame['score'].isnull().sum()} {name} rows have no score.")
    if not set(df["source"]).issubset(set(original["id"])):
        raise SystemExit("Some attacked texts have no original to compare with.")
    by_id = original.set_index("id")

    def flagged(frame):
        return (frame["score"] / 100.0) >= frame["domain"].map(thresholds)

    def scan(frame):
        return {
            "any": float((frame["artifacts"] > 0).mean()),
            "strong": float((frame["strongArtifacts"] > 0).mean()),
            "systematic": float((frame["artifactPattern"] == "Systematic").mean()),
        }

    setup = json.loads(Path(args.setup).read_text(encoding="utf-8"))
    report = {"setup": setup, "attacks": {}, "unattackedHumanScan": scan(original[original["model"] == "human"]),
              "unattackedMachineScan": scan(original[original["model"] != "human"])}
    for name, sub in df.groupby("attack"):
        machine = sub[sub["model"] != "human"]
        human = sub[sub["model"] == "human"]
        before = by_id.loc[machine["source"]]
        report["attacks"][name] = {
            # Paired: the same machine texts, before and after the attack, at RAID's thresholds.
            "tprBefore": float(flagged(before.reset_index()).mean()),
            "tprAfter": float(flagged(machine).mean()),
            "meanScoreBefore": float(before["score"].mean()),
            "meanScoreAfter": float(machine["score"].mean()),
            "scanMachine": scan(machine),
            "scanHuman": scan(human),
        }
    def wilson(k, n, z=1.959964):
        p = k / n
        d = 1 + z * z / n
        c = (p + z * z / (2 * n)) / d
        m = z * ((p * (1 - p) / n + z * z / (4 * n * n)) ** 0.5) / d
        return [max(0.0, c - m), min(1.0, c + m)]

    humans = original[original["model"] == "human"]
    alarms = int((humans["artifacts"] > 0).sum())
    hg, zw = report["attacks"]["homoglyph"], report["attacks"]["zero_width_space"]
    report["predictions"] = {
        "P5": {
            "homoglyphStrong": hg["scanMachine"]["strong"],
            "zeroWidthStrong": zw["scanMachine"]["strong"],
            "humanAlarms": alarms, "humanTexts": int(len(humans)),
            "humanAlarmInterval": wilson(alarms, int(len(humans))),
            "held": hg["scanMachine"]["strong"] >= 0.99 and zw["scanMachine"]["strong"] >= 0.99
                    and alarms / len(humans) <= 0.01,
        },
        "P6": {"scoreDrop": hg["meanScoreBefore"] - hg["meanScoreAfter"],
               "held": hg["meanScoreAfter"] < hg["meanScoreBefore"]},
    }
    # Which attacked texts the scanner did not report, by class, with a reason computed from the text:
    # a letter that imitates nothing means the text is written in another script; no Latin letter at
    # all means there was no Latin word left to disguise.
    sys.path.insert(0, str(Path(args.adversarial)))
    lookalikes = set()
    for targets in importlib.import_module("attackers.homoglyph").HomoglyphAttack().mapping.values():
        lookalikes.update(targets)
    texts = {}
    with open(args.attacked_jsonl, encoding="utf-8") as f:
        for line in f:
            row = json.loads(line)
            texts[row["id"]] = row["text"]

    def reason(text):
        foreign = any(c.isalpha() and not c.isascii() and c not in lookalikes for c in text)
        latin = sum(c.isascii() and c.isalpha() for c in text)
        if foreign:
            return "written in another script"
        return "no Latin letter left" if latin == 0 else "unexplained"

    report["scannerMisses"] = {
        name: [{"id": r.id, "class": "human" if r.model == "human" else "machine", "reason": reason(texts[r.id])}
               for r in sub.loc[sub["strongArtifacts"] == 0].sort_values("id").itertuples(index=False)]
        for name, sub in df.groupby("attack") if name in ("homoglyph", "zero_width_space")
    }
    Path(args.out).write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    print(f"{'attack':<22} {'TPR before':>10} {'after':>7} {'score':>13} {'scan any/strong/sys (machine)':>32}")
    for name, a in report["attacks"].items():
        s = a["scanMachine"]
        print(f"{name:<22} {a['tprBefore']:>10.3f} {a['tprAfter']:>7.3f} {a['meanScoreBefore']:>6.1f}->{a['meanScoreAfter']:<5.1f} "
              f"{s['any']:>10.3f} {s['strong']:>7.3f} {s['systematic']:>7.3f}")
    h = report["unattackedHumanScan"]
    print(f"unattacked humans flagged by the scan: any {h['any']:.4f}  strong {h['strong']:.4f}  systematic {h['systematic']:.4f}")


def main() -> None:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    m = sub.add_parser("make")
    m.add_argument("--sample-csv", required=True)
    m.add_argument("--sample-jsonl", required=True)
    m.add_argument("--adversarial", required=True, help="RAID's generation/adversarial directory")
    m.add_argument("--nltk-data", required=True)
    m.add_argument("--out", required=True)
    m.add_argument("--per-class", type=int, default=1000)
    m.add_argument("--seed", type=int, default=20261117)
    e = sub.add_parser("evaluate")
    e.add_argument("--attacked-csv", required=True)
    e.add_argument("--attacked-scores", required=True)
    e.add_argument("--sample-csv", required=True)
    e.add_argument("--scores", required=True)
    e.add_argument("--results", required=True)
    e.add_argument("--setup", required=True, help="attacked.json written by make")
    e.add_argument("--attacked-jsonl", required=True, help="attacked.jsonl, read to explain scanner misses")
    e.add_argument("--adversarial", required=True, help="RAID's generation/adversarial directory")
    e.add_argument("--out", required=True)
    args = ap.parse_args()
    make(args) if args.cmd == "make" else evaluate(args)


if __name__ == "__main__":
    main()
