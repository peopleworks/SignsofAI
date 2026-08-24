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

Every text here is **662 words or longer** — that is the shortest one, and the Wikipedia fetcher skips
anything under 700 by design. The threshold is therefore supported over that range and nowhere else,
so since #59 the engine **withholds its verdict below 662 words** rather than extrapolating onto a
population it never sampled.

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
| `en-other-affiliation` | PLOS articles with no anglophone affiliation | **Standing in for second-language English** — the population this whole category harms |
| `en-wikipedia` | Pre-2022 English Wikipedia revisions | Same register as the Spanish group, so a language effect can be told from a register effect |
| `es-wikipedia` | Pre-2022 Spanish Wikipedia revisions | The half nobody else measures at all |

**The affiliation grouping is a proxy and a crude one.** Nobody's first language is recorded in a DOI,
and plenty of people at a London university learned English second. It errs deliberately in one
direction: a paper with *any* anglophone affiliation counts as anglophone, which shrinks the
second-language group and makes any gap found an understatement rather than an exaggeration. Every
entry records the affiliation used, so each classification can be argued with individually.

**None of this is a student essay.** Published articles are longer, more heavily edited and written by
people who write for a living. A first-year essay is a different thing and the rate measured on one
does not transfer to the other. The honest fix is for a school to calibrate on its own students'
pre-2022 work — the same tool does it, and the result would be a false-positive rate for *its*
population instead of somebody else's.

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
- **Anything closer to a student essay.** Coursework released under an open licence, pre-2022 writing
  competition entries, open thesis repositories.
- **More of everything.** With nothing flagged it still takes roughly seventy-five texts in a group
  before the interval alone can bound a 5% rate. Most groups here are half that.

A number that goes up when the corpus grows is a working measurement, not a failure. Publishing one
that could only ever go down would be the failure.
