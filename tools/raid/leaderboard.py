"""For scale only: what RAID's own authors measured for open detectors, from the leaderboard files.

These numbers are on RAID-test; this project's are on a sample of RAID-train. Same distribution,
different texts, so they set a scale and are never a row in the same table.
"""

import argparse
import json
from pathlib import Path

ATTACKS = ["none", "homoglyph", "zero_width_space", "whitespace"]


def pick(scores, attack):
    for s in scores:
        if (s["domain"], s["model"], s["decoding"], s["repetition_penalty"], s["attack"]) == ("all", "all", "all", "all", attack):
            a = s["accuracy"].get("0.05")
            return a["accuracy"] if a else None
    return None


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True, help="leaderboard results.json files, one per detector")
    ap.add_argument("--commit", required=True, help="the RAID repository commit they were read at")
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    detectors = []
    for f in sorted(Path(args.dir).glob("*.json")):
        d = json.loads(f.read_text(encoding="utf-8"))
        scores = d.get("scores") or []
        row = {a: pick(scores, a) for a in ATTACKS}
        if row["none"] is None:
            continue
        detectors.append({
            "name": d["detector_name"],
            "submittedBy": "RAID authors" if "RAID Authors" in str(d.get("contact_info")) else "vendor",
            "tpr05": row,
        })
    detectors.sort(key=lambda r: -r["tpr05"]["none"])
    out = {"source": "github.com/liamdugan/raid leaderboard/submissions/*/results.json",
           "commit": args.commit, "split": "RAID-test", "detectors": detectors}
    Path(args.out).write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    for r in detectors:
        print(f"{r['name']:<20} {r['submittedBy']:<13} " + " ".join(f"{(v or 0) * 100:6.1f}" for v in r["tpr05"].values()))


if __name__ == "__main__":
    main()
