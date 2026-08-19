# The paraphrase study

`Docs/PARAPHRASE.md` reports what happens to a passage when a language model rewrites it. This folder
is what that report is made of.

## Why it exists

Claude models released on or after 2 August 2026 carry a machine-readable watermark, with earlier
models to follow during a transition period, and the question arrived within a week: does that make a
tool like this one redundant? The parts of the answer that can be settled by reading are settled by
reading — the key is Anthropic's, no third party can detect anything today, the mark is
vendor-specific, and the vendor's help centre lists heavy editing, paraphrasing, translation, mixing
into other writing and very short passages as conditions that leave it undetectable.

The part that cannot be settled by reading is what a *removal* does to prose. Since only a rewrite
can disturb a watermark that lives in word choice, every working remover is a paraphraser, and
paraphrasing is something this repository can measure without anybody's key.

Two details worth getting right, because the first version of this file got them wrong. The vendor's
help centre lists "heavily edited, paraphrased, translated, or mixed into other writing" among the
conditions that leave a mark undetectable — that is about somebody transforming Claude's output. Its
engineering write-up separately says that when *Claude itself* translates, the result is watermarked,
because Claude chose all the words. Both are true and they are about opposite directions. And the
technique is not Anthropic's: SynthID-Text was published by Google DeepMind in 2024, and Anthropic
adopted a version of it. This is one more vendor arriving at an existing industry practice.

## The design, and why it needs no machine-written corpus

`Docs/Calibration/README.md` argues at length against assembling a collection of machine-written text
to measure against: it is a sample of whichever models were convenient that month, it ages badly, and
it flatters whoever assembled it. That argument holds here and is not evaded.

What replaces it is a **pair**. Each unit of the study is one passage measured twice — as its author
wrote it, and after a model rewrote it. Both halves are the same passage, by the same author, on the
same subject, at nearly the same length. The baseline is not estimated from a population; it is the
text itself, so between-author variation is removed by construction.

What the pair does *not* remove is the tool's own sampling noise, and the length arm below shows that
noise is large at four hundred words. "Moved with the rewrite" is what this design measures; "moved
because of the rewrite" is a stronger claim and an earlier version of the report made it.

The human halves are drawn from the calibration corpus, so every one of them was published before
generative models existed. That remains the only basis for calling writing human, and it is a
stronger one than any classifier offers about anything.

## The controls, which were not planned

The first run produced a baseline that did not match the published one: six of thirty-two human
passages already sat above the verdict boundary, where the calibration page reports none of ninety.
The difference is length. The excerpts are around four hundred words; the documents they were cut
from run to several thousand.

Chasing that produced three controls, and each was added because a reviewer showed the study could
not tell two explanations apart without it.

1. **The whole source document**, so the scissors can be told from the model.
2. **The whole document with its apparatus stripped** by the same prose filter the excerpts pass
   through — otherwise the gap between arms could be figure captions and boilerplate rather than
   length. It is not: those documents still flag none of thirty-two.
3. **Windows at three positions** — opening, middle, late — because the pairs are cut from the
   opening, and the opening of a research article is its abstract while the opening of an
   encyclopedia entry is its lead. Cutting only there measures a genre effect and calls it a length
   effect. It changed the headline figure from 18.8% to 14.6%.

Together they produced the study's largest result, which is about this tool rather than about any
watermark. `Docs/PARAPHRASE.md` reports it under **Length**, and it is tracked as issue #59.

## What is committed, and what is not

`pairs.json` is the artefact: what each passage is, which corpus entry it came from, its year, and
the SHA-256 of both halves. The passages themselves are git-ignored, exactly as the calibration texts
are and for the same reason — they are derivatives of CC BY and CC BY-SA sources, licences differ,
and the bulk would dwarf the code.

`instruction.md` is committed and is the most important file here. It is the experimental treatment,
stored verbatim, and it deliberately says nothing about detectors in either direction.

The manifest also records **which model did the rewriting and on what date**. Unlike the human corpus,
this half of the study ages: a 2019 paper will still have been written in 2019 in ten years, whereas
a rewrite is one model's work on one day. Re-running with a newer model is the answer to "but models
have moved on", and the tool refuses to run without being told the model's name.

## Deviations from the protocol, measured rather than attested

The first version of this section was written by hand and was wrong. It claimed one deliberate breach
of the instruction's eight-word rule and there were twenty. Compliance is now checked by the tool on
every run and printed in `Docs/PARAPHRASE.md` under **Was the treatment actually applied**, because a
project that machine-checks its false-positive rate has no business attesting its own method by hand.

- **Twenty of the thirty-two pairs retain a verbatim run of eight words or more**, the longest 86
  words. The long runs are quotations — a court ruling, a political pamphlet, a published definition
  of a lek — which cannot be reworded without falsifying them. The instruction's requirement to
  preserve every fact and citation marker therefore conflicts with its no-eight-word-runs rule, and
  the protocol never said which wins. A future run should say so before it starts, not afterwards.
- **The rewriter silently corrected errors in the originals**, which the instruction forbids in as
  many words ("do not correct the original"). Verified cases in the Spanish arm: *intercesión* →
  *se cruza*, *teoría de juego* → *teoría de juegos*, *costa* → *consta*, and
  *participación en el mercado* → *reparto del mercado*, the last of which also changes the surface
  meaning. The English arm shows the same thing on the second-language passages. The delivered
  treatment was therefore *paraphrase plus copy-editing*, and some part of every score change is
  error repair rather than rewriting.
- **This bears directly on the fairness arm.** The `en-other-affiliation` stratum exists to measure
  the harm this category of tool does to people writing in a second language. A rewrite that repairs
  second-language features is not a neutral treatment on that stratum, and its numbers should be read
  with that in mind.
- **Two passages needed a second pass.** `wp-en-1025914326` and `wp-es-132146383` came back outside
  the ±10% the instruction sets and were rewritten once more. Across the final set the lengths run
  from −8.5% to +6.8%, median −1.0%.
- **The excerpts are cut from the opening of each document**, not from the middle. An earlier version
  of the report said otherwise. Because the opening of a research article is its abstract and the
  opening of an encyclopedia entry is its lead, this is not a neutral place to cut, and the report
  now measures windows at three positions rather than asserting it does not matter.

## Reproducing it

```bash
# 1. cut a stratified sample of the corpus into equal-length passages
dotnet run --project tools/SignsOfAI.Calibration -- excerpt --per-stratum 8 --words 400

# 2. rewrite each file in Docs/Paraphrase/human/ into Docs/Paraphrase/rewritten/ under the
#    same name, giving the model the instruction in instruction.md and nothing else

# 3. measure all three arms and rewrite Docs/PARAPHRASE.md
dotnet run --project tools/SignsOfAI.Calibration -- paraphrase \
    --paraphrased-by "<the model, named exactly>" --instruction Docs/Paraphrase/instruction.md
```

Step 2 is deliberately outside the tool. Wiring an API key into the calibration harness would make
the study reproducible only for people holding that key, and would tie a published number to one
vendor's availability. A folder of text files can be filled by any model, including one running on
the reader's own machine, and the manifest records which.

## What would improve this

In order:

- **More pairs.** Thirty-two cannot separate a small effect from noise, and the report says so in
  the one place it matters. Around a hundred would settle the direction.
- **A second rewriter.** Everything here is one model on one day. A local model and a competing
  frontier model would show how much of the result belongs to the rewriter rather than to rewriting.
- **Passages that look like coursework.** These are published articles and encyclopedia entries. A
  first-year essay is shorter, looser, and closer to the case a teacher actually faces — which, given
  what the length arm found, is the gap most worth closing.
