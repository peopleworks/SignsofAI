"""Draw the RAID sample this project measures, and fingerprint it.

Every human text in RAID-train without attacks, and a fixed number of machine-written texts for each
generator and domain, chosen with a fixed seed. Writes:

  <out>/sample.jsonl   {id, text} per line, for `signsofai-calibrate raid`
  <out>/sample.csv     the same rows with RAID's metadata and no text, for evaluation
  <out>/sample.json    what was drawn and from what: dataset hash, seed, counts, id fingerprint

The texts are not redistributed. Anyone with the same dataset file and this script gets the same ids,
and the fingerprint says whether they did.
"""

import argparse
import hashlib
import json
from pathlib import Path

import pandas as pd

META = ["id", "model", "decoding", "repetition_penalty", "attack", "domain"]


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", required=True, help="RAID train_none.csv")
    ap.add_argument("--out", required=True, help="directory for the sample files")
    ap.add_argument("--per-cell", type=int, default=200, help="machine texts per generator and domain")
    ap.add_argument("--seed", type=int, default=20261117)
    args = ap.parse_args()

    data = Path(args.data)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    df = pd.read_csv(data)
    if set(df["attack"].unique()) != {"none"}:
        raise SystemExit("Expected RAID-train without attacks (train_none.csv).")

    human = df[df["model"] == "human"]
    machine = df[df["model"] != "human"]

    # Sorting before sampling makes the draw depend on the rows, not on the order the file happens
    # to be in.
    machine = machine.sort_values("id")
    drawn = (
        machine.groupby(["model", "domain"], group_keys=False)
        .apply(lambda g: g.sample(n=min(args.per_cell, len(g)), random_state=args.seed))
    )

    sample = pd.concat([human, drawn]).sort_values("id").reset_index(drop=True)
    if sample["id"].duplicated().any():
        raise SystemExit("Duplicate ids in the sample.")

    with open(out / "sample.jsonl", "w", encoding="utf-8", newline="\n") as f:
        for row in sample.itertuples(index=False):
            f.write(json.dumps({"id": row.id, "text": row.generation}, ensure_ascii=False) + "\n")

    sample[META].to_csv(out / "sample.csv", index=False)

    fingerprint = hashlib.sha256("\n".join(sample["id"]).encode("utf-8")).hexdigest()
    manifest = {
        "dataset": "RAID-train without attacks (train_none.csv)",
        "datasetUrl": "https://dataset.raid-bench.xyz/train_none.csv",
        "datasetSha256": sha256_file(data),
        "seed": args.seed,
        "machinePerGeneratorAndDomain": args.per_cell,
        "human": int(len(human)),
        "machine": int(len(drawn)),
        "total": int(len(sample)),
        "humanByDomain": {k: int(v) for k, v in human["domain"].value_counts().sort_index().items()},
        "machineByModel": {k: int(v) for k, v in drawn["model"].value_counts().sort_index().items()},
        # RAID's own mix, which this sample does not follow: it weights generators run with and without
        # a repetition penalty twice. Kept so a rate can be re-weighted to RAID's proportions.
        "datasetMachineByModel": {k: int(v) for k, v in machine["model"].value_counts().sort_index().items()},
        "idFingerprintSha256": fingerprint,
        "pandas": pd.__version__,
    }
    (out / "sample.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
