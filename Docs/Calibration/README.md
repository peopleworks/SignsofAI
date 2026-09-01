# The calibration corpus

`Docs/CALIBRATION.md` says how often this tool flags writing that a machine did not produce. This
folder is what that number is made of.

## Why there is no accuracy figure here

"What is your accuracy?" is the first question every teacher asks, and no tool in this category
answers it. This one does not answer it either — it answers a narrower question that can be answered
honestly.

An accuracy figure needs a collection of machine-written text to measure against. Any such collection
is a sample of whichever models were convenient in whichever month, so the number ages badly and
flatters whoever assembled it. Worse, it is the wrong number: a detector that is 95% accurate and
achieves it by flagging every formal writer has done more harm than one that catches less.

A **false-positive rate** needs only writing known to be human. It does not go stale. And it measures
the harm this category actually causes: detectors flag 61% of essays by non-native English speakers,
and not one of them publishes that figure about itself.

## What makes a text admissible

**It was published before generative models could have written it.** That is the whole test. A paper
with a 2019 DOI and a Wikipedia revision stamped 2021 were not produced by something that did not
exist, which is a stronger guarantee than any classifier can offer about anything.

No text is admitted on the grounds that it "reads human". That judgement is the thing being measured
and cannot also be the thing doing the measuring.

## What the corpus does not cover, and what it costs

Every text here is **649 words or longer** as the engine counts them — that is the shortest one; the
Wikipedia fetcher skips anything under 700 by design and the learner selection anything under 662 by
a naive split. The threshold is therefore supported over that range and nowhere else, so since #59
the engine **withholds its verdict below that length** rather than extrapolating onto a population it
never sampled.

That is not a small exclusion. It is most of how the tool is used: somebody pastes a paragraph. And
the direction of the error is known — the same documents flag 0 of 32 whole and 6 of 32 as 400-word
excerpts of themselves (`Docs/PARAPHRASE.md`, section *Length*), so short text drifts toward the
machine rather than merely getting noisier.

**The most wanted contribution is therefore short complete texts published before 2022**: encyclopedia
stubs, short news pieces, abstracts — writing somebody *composed* at that length. A window cut out of
a longer document is not the same population and must not be used: it has the sentence rhythm of a
fragment, which is the very thing being measured. See issue #66.

## The texts are not in this repository

`Docs/Calibration/texts/` is git-ignored. Licences differ per source, the bulk would dwarf the code,
and neither problem is solved by committing it anyway. What is committed is `corpus.json`: what each
text is, where it came from, its licence, its year, and the SHA-256 of the extracted text.

That hash proves a run measured the file the manifest names. It does **not** prove that two people
extracting the same article independently produce identical text — they will not, because PDF and
HTML extraction differ. Reproducing a published figure exactly needs the extracted texts shared, not
just the manifest.

## Assembling it

```bash
# ~40 open-access research articles, 2018–2020, grouped by author affiliation
dotnet run --project tools/SignsOfAI.Calibration -- fetch --source plos --count 40

# pre-2022 encyclopedia prose, both languages, same register on purpose
dotnet run --project tools/SignsOfAI.Calibration -- fetch --source wikipedia --lang en --count 25
dotnet run --project tools/SignsOfAI.Calibration -- fetch --source wikipedia --lang es --count 25

# 206 essays by adult learners of English, 2006–2012, one per student — a fixed rule, no --count,
# so anyone running it gets the same texts and the same hashes (about 180 MB downloaded once)
dotnet run --project tools/SignsOfAI.Calibration -- fetch --source pelic

# measure, and rewrite Docs/CALIBRATION.md
dotnet run --project tools/SignsOfAI.Calibration -- run
```

The fetcher pauses between requests and honours `Retry-After`. These are free public APIs run by
people who owe this project nothing, and a corpus assembled by hammering them is not one worth
having.

## The groups, and what is wrong with them

| Group | What it is | What it is for |
|---|---|---|
| `en-anglophone-affiliation` | PLOS articles with at least one author affiliated in an anglophone country | The comparison baseline |
| `en-other-affiliation` | PLOS articles with no anglophone affiliation | Was standing in for second-language English until the learner group arrived; kept, because it is the same question asked of professional writers |
| `en-second-language-learner` | Classroom essays from PELIC — University of Pittsburgh's Intensive English Program, 2006–2012, first language recorded per writer | **The population this whole category harms**, measured directly rather than through a proxy |
| `en-wikipedia` | Pre-2022 English Wikipedia revisions | Same register as the Spanish group, so a language effect can be told from a register effect |
| `es-wikipedia` | Pre-2022 Spanish Wikipedia revisions | The half nobody else measures at all |

**The affiliation grouping is a proxy and a crude one.** Nobody's first language is recorded in a DOI,
and plenty of people at a London university learned English second. It errs deliberately in one
direction: a paper with *any* anglophone affiliation counts as anglophone, which shrinks the
second-language group and makes any gap found an understatement rather than an exaggeration. Every
entry records the affiliation used, so each classification can be argued with individually.

**The learner group is the one that is not a proxy.** [PELIC](https://github.com/ELI-Data-Mining-Group/PELIC-dataset)
records each writer's first language — Arabic, Korean, Chinese, Japanese, Spanish, Thai and Turkish
make up most of it — and every essay was written years before generative models, in a classroom,
under a prompt. The selection is a rule rather than a choice: first submitted version, writing
classes only, at least 662 words so the group enters at the floor the corpus already had, one text
per student so nobody prolific counts twice. Nothing is picked by score. The licence is
CC BY-NC-ND 4.0, which permits measuring and publishing the numbers and forbids redistributing the
texts — the same arrangement every other source here already has.

What it found is on `Docs/CALIBRATION.md` and it is the reason the boundary moved from 25 to 30: at
25, 9 of the 206 essays were flagged and none of the 90 published texts. The rules doing it are the
connectors an academic-English course teaches (`rhet.in-conclusion` fires in 40% of learner essays
and 14% of published ones) — see issue #75. It also caught a defect: `chat.eager-opener` had been
admitted on zero hits in the published texts and fired on eleven learner essays, because its pattern
accepted *"Of course, …"* with a comma, an ordinary concession, alongside *"Certainly!"*. Zero on one
register is not zero.

**Still no native-speaker student essay.** Published articles are written by people who write for a
living; the learner essays are by adults in a university language programme. A first-year essay by a
native speaker is a different population again, and the rate measured here does not transfer to it.
The honest fix is for a school to calibrate on its own students' pre-2022 work — the same tool does
it, and the result would be a false-positive rate for *its* population instead of somebody else's.

## Contributing texts

Add to the corpus and the published number changes. That is the point.

1. Fetch or assemble texts that are **provably pre-2022** and openly licensed.
2. Put the extracted prose in `Docs/Calibration/texts/`.
3. Add entries to `corpus.json` with the source, licence, year and reasoning.
4. Run with `--record-hashes` once, then `run`, and commit the manifest and the regenerated
   `Docs/CALIBRATION.md`.

What is most wanted, in order:

- **Spanish academic writing.** SciELO and Redalyc are the obvious sources and neither was reachable
  from where this was first assembled. Spanish is the half of this project nobody else measures, and
  it currently rests on encyclopedia prose alone.
- **Native-speaker student writing.** Coursework released under an open licence, pre-2022 writing
  competition entries, open thesis repositories. The learner group covers second-language writers;
  nothing yet covers a first-year student writing in their own language.
- **More of everything.** With nothing flagged it still takes roughly seventy-five texts in a group
  before the interval alone can bound a 5% rate. Most groups here are half that.

A number that goes up when the corpus grows is a working measurement, not a failure. Publishing one
that could only ever go down would be the failure.
