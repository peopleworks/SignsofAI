# The rewriting instruction

This is the treatment. Every number in `Docs/PARAPHRASE.md` is a measurement of what *this
instruction*, given to the model named in the manifest, does to a passage — not of what "AI
paraphrasing" does in general. Change a word here and the study measures something else.

It is written to imitate the only watermark-removal method that can work. A statistical text
watermark lives in which words the model chose, so stripping invisible characters cannot touch it
and only a rewrite can; Anthropic's own description says a complete rewrite where every word is
replaced will remove it. The instruction below therefore asks for exactly that, and asks for nothing
else.

## What it deliberately does not say

It does not say *make this sound human*, and it does not say *make this sound like AI*. Either
sentence would decide the result before the measurement: the first pushes the rewrite away from the
signals this project looks for, the second pushes it toward them, and both would produce a number
about the instruction rather than about paraphrasing. The rewriter is told to preserve meaning and
destroy wording, which is what somebody removing a watermark actually wants, and is told nothing
whatever about detectors.

It does not mention SignsOfAI, its rule packs, or any of the tells it looks for. A rewriter that
knew what was being counted would be gaming the measurement.

## The instruction, verbatim

```text
Rewrite the passage below so that none of its original wording survives. Replace the vocabulary,
recast the sentences, and change the order of clauses wherever the meaning allows. No run of eight
or more consecutive words from the original may remain.

Preserve, as closely as you can:
- the meaning, including every fact, figure, name and citation marker
- the language it is written in
- the register — an academic passage stays academic, an encyclopedic one stays encyclopedic
- the approximate length, within about ten per cent

Do not summarise, do not expand, do not add commentary, do not correct the original, and do not
address the reader. Return only the rewritten passage.
```

## Why the length constraint is there

Sentence-length variation is one of the things being measured. A rewrite free to compress a
four-hundred-word passage into two hundred would change that distribution by changing how much text
there is, and the study would not be able to tell that apart from the rewrite's own habits. Holding
length roughly constant is what makes the two halves comparable.

## Two conflicts inside this instruction, found after it had been used

Recorded rather than fixed. Editing the treatment after the fact would mean the numbers in
`Docs/PARAPHRASE.md` were produced by an instruction that no longer exists, which is worse than an
imperfect instruction honestly described. A future run should resolve both **before** it starts.

**Preserve every fact versus replace every word.** A passage quoting a court ruling, a political
pamphlet or a published definition cannot have those words replaced without falsifying the
quotation. The instruction demands both and does not say which wins, so the rewriter preserved the
quotations — reasonably, but the eight-word rule is breached in twenty of the thirty-two pairs as a
result. The tool now measures this on every run and the report prints it.

**Do not correct the original versus preserve the register.** Several passages contain errors:
*intercesión* for *intersección*, *teoría de juego* for *teoría de juegos*, and constructions a
first-language editor would change. The rewriter repaired them. That makes the delivered treatment
*paraphrase plus copy-editing*, so part of any measured change is error repair. It matters most on
the `en-other-affiliation` stratum, which exists precisely to measure the harm done to people
writing in a second language: a treatment that quietly repairs second-language features is not
neutral there.
