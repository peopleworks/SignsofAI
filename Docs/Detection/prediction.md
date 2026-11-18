# What we expected before measuring detection on RAID

Written on **17 November 2026**, before a single RAID text had been scored by this engine, and
committed before the measurement so the order can be checked in the history. The results page that
follows it must report each line below as it stands, including the ones that turn out wrong.

## Why this exists

`Docs/CALIBRATION.md` measures how often the tool is wrong about a human, and says plainly what it
leaves out: *nothing about how much AI writing it catches*, because a tool that flags nothing has a
perfect false-positive rate. RAID (Dugan et al., ACL 2024, arXiv 2405.07940) measures that other
half: the share of machine-generated text a detector catches at a fixed false-positive rate on RAID's
own human texts.

Two things about RAID shape what can be predicted:

- **RAID picks its own threshold.** It takes the raw score and, by default separately for each
  domain, finds the cutoff that flags 5% of its human texts. Our 30/100 boundary and the length floor
  from #59 play no part, and the cutoff RAID finds is not ours.
- **RAID's humans are not students.** News, Wikipedia, arXiv abstracts, Reddit, reviews, recipes,
  poetry and book summaries. Whatever this measures says nothing about second-language learners (#75),
  which is what the calibration is for.

## The predictions

RAID's generators fall into three groups. **Chat-tuned:** ChatGPT, GPT-4, Llama 2 70B Chat, Cohere
Chat, MPT-30B Chat, Mistral 7B Chat. **Base models:** GPT-2 XL, MPT-30B, Mistral 7B, Cohere.
**In between:** GPT-3 text-davinci-003, which is instruction-tuned.

The underlying claim: **the tells this engine looks for are fingerprints of chat-tuning, not of
machine text.**

| # | Prediction | What would refute it |
|---|---|---|
| P1 | The detection rate on chat-tuned generators is **at least twice** the rate on base models | A ratio below two |
| P2 | On base models the rate stays **under 20%**. A score that knew nothing would catch 5% | 20% or more |
| P3 | text-davinci-003 lands **between** the two groups | Outside that range |
| P4 | **Poetry and recipes**, short and formulaic, are worse than news, Reddit, reviews and books | Poetry or recipes among the three best domains |
| P5 | The character scanner catches **at least 99%** of texts under RAID's homoglyph and zero-width-space attacks, and flags **almost none** of the unattacked human texts | Under 99%, or false alarms on more than 1% of human texts |
| P6 | The homoglyph attack **lowers the score** (disguised words stop matching the rules), and it is exactly the attack the scanner exposes | The score does not drop |

**Overall guess, held with low confidence:** between **15% and 40%** detection at a 5% false-positive
rate, without attacks. Far below the commercial detectors on the leaderboard, which is consistent with
what this project has said from the start: it cannot tell who wrote a text, and it does not try.

## Note added later the same day, still before measuring

Reading `AiWritingAnalyzer.Analyze` while writing the scoring code: the analyzer **normalizes
character artifacts before any rule reads the text**, precisely so that a Cyrillic letter inside
"delve" cannot switch the catalog off. If that works as written, **P6 is wrong**: RAID's homoglyph and
zero-width attacks should leave the score where it was, while the scanner reports them. P6 stays above
exactly as it was written, and the results will say which of the two happened.

## What is measured, and how

- A stratified sample of **RAID-train without attacks** (labelled and public): 500 human texts per
  domain and 200 machine texts per generator and domain, fixed by a seed and recorded in a manifest.
- Scored by the shipped engine and packs, unchanged, through `tools/SignsOfAI.Calibration`, with the
  language set to English as the calibration does. The score is the 0–100 the reader sees, before the
  length floor, divided by 100 because RAID's threshold search assumes a 0–1 range.
- Evaluated by **RAID's own `run_evaluation`**, not a reimplementation of it, both per domain (RAID's
  default) and with a single threshold across domains (closer to how the product works).
- For attacks, the lightweight ones from RAID's own code are applied to a subsample. The scanner's
  catch rate is measured **separately** and never added to the score.

No rule or threshold will be changed in response to these numbers inside this measurement. A change
suggested by RAID goes through the usual path: measured against the human corpus before it is written.
