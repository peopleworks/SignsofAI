# Measuring detection on RAID

The scripts behind [`Docs/DETECTION.md`](../../Docs/DETECTION.md): how much machine-written text the
score separates, measured on RAID (Dugan et al., ACL 2024), a benchmark nobody here assembled.

The division of labour is the point. **This repository only produces scores.** RAID's own code,
`run_evaluation` from `raid-bench`, chooses every threshold and counts every hit, so nothing here can
shape the number in the engine's favour.

## What you need

- .NET 10, for `tools/SignsOfAI.Calibration`.
- **Python 3.12.** `raid-bench` pins numpy 1.26, which has no wheels for newer Pythons.
- About 1 GB of disk for the dataset.
- Network access: the dataset downloads from `dataset.raid-bench.xyz`, and RAID's
  `alternative_spelling` attack fetches a JSON word list from GitHub when it starts.

Keep the data, RAID's code and your scripts in separate directories, and run Python with `-I` so it
never imports a file from the directory it happens to be in. The layout used for the published run:

```
temp/raid/data/    the downloaded CSV
temp/raid/src/     RAID's repository (sparse: raid/ and generation/adversarial/)
temp/raid/venv/    Python 3.12 with tools/raid/requirements.txt
temp/raid/out/     everything this produces
```

`temp/` is ignored by git.

## The run, step by step

```sh
# 0. Environment
uv venv temp/raid/venv --python 3.12
git clone --depth 1 --filter=blob:none --sparse https://github.com/liamdugan/raid.git temp/raid/src
git -C temp/raid/src sparse-checkout set raid generation/adversarial
VIRTUAL_ENV=temp/raid/venv uv pip install -e temp/raid/src -r tools/raid/requirements.txt
PY=temp/raid/venv/Scripts/python.exe          # bin/python outside Windows

# 1. Data: RAID-train without attacks (802 MB)
curl -L -o temp/raid/data/train_none.csv https://dataset.raid-bench.xyz/train_none.csv

# 2. The sample: every human text, 200 machine texts per generator and domain, seed 20261117
$PY -I tools/raid/sample.py --data temp/raid/data/train_none.csv --out temp/raid/out

# 3. Scores from the shipped engine, unchanged
dotnet run --project tools/SignsOfAI.Calibration -c Release -- \
    raid --in temp/raid/out/sample.jsonl --out temp/raid/out/scores.jsonl

# 4. RAID's evaluation, plus the breakdowns
$PY -I tools/raid/evaluate.py --sample temp/raid/out/sample.csv --scores temp/raid/out/scores.jsonl \
    --manifest temp/raid/out/sample.json \
    --calibration src/SignsOfAI.Core/Calibration/published-calibration.json \
    --engine 0.8.1 --out Docs/Detection/results.json

# 5. Attacks: RAID's own attack code, the eight that need no language model
$PY -I tools/raid/attack.py make --sample-csv temp/raid/out/sample.csv \
    --sample-jsonl temp/raid/out/sample.jsonl --adversarial temp/raid/src/generation/adversarial \
    --nltk-data temp/raid/nltk_data --out temp/raid/out/attacks
dotnet run --project tools/SignsOfAI.Calibration -c Release -- \
    raid --in temp/raid/out/attacks/attacked.jsonl --out temp/raid/out/attacks/scores.jsonl
$PY -I tools/raid/attack.py evaluate --attacked-csv temp/raid/out/attacks/attacked.csv \
    --attacked-scores temp/raid/out/attacks/scores.jsonl --sample-csv temp/raid/out/sample.csv \
    --scores temp/raid/out/scores.jsonl --results Docs/Detection/results.json \
    --setup temp/raid/out/attacks/attacked.json --out Docs/Detection/attacks.json

# 6. For scale: the leaderboard's own numbers, from leaderboard/submissions/<name>/results.json
$PY -I tools/raid/leaderboard.py --dir <folder of downloaded results.json> --commit <raid commit> \
    --out Docs/Detection/leaderboard-scale.json

# 7. The page
cp temp/raid/out/sample.json Docs/Detection/sample.json
$PY -I tools/raid/report.py --commit <engine commit> --run-date <YYYY-MM-DD>
```

Step 2 prints an id fingerprint. If yours matches the one in `Docs/Detection/sample.json`, you scored
the same texts.

## Things that are easy to get wrong

- **Scores go to RAID divided by 100.** RAID's threshold search starts at the mean of the human scores
  and moves in steps of 0.5, which assumes a 0–1 range. On 0–100 it never reaches the cutoff, gives up
  after 50 steps, and discards the domain.
- **RAID tunes a cutoff per domain by default** (`per_domain_tuning=True`). `evaluate.py` reports that
  first, as the leaderboard does, and one cutoff for every domain second, which is closer to the
  product.
- **RAID's `attack.py` cannot be imported without a GPU stack**: it imports every attacker at the top,
  including the three that load a model. `attack.py` here imports the eight light ones one by one and
  reads their strengths from RAID's `attack.py` source, so they cannot drift from what RAID uses.
- **The language is fixed to English**, as the calibration does. The web app would detect it instead.
- `--threads 1` on the `raid` verb scores in order. It exists to check that the parallel run gives
  identical output, which it did.

## What not to do with the result

Do not change a rule or a threshold because of a RAID number inside the same measurement: that is
training on the test. A rule RAID suggests goes through the usual path, measured against the human
corpus before it is written. And nothing is submitted to the RAID leaderboard without the review
committee and the maintainer's decision: a merged `results.json` there stays public.
