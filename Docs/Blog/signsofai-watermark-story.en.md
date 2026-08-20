---
title: "I tried to prove Claude's watermark didn't threaten my detector. I was wrong, and found a worse fault in my own tool"
description: "Anthropic started watermarking what Claude writes. I measured what happens to a text when somebody strips that mark — and the answer was nothing detectable. What I did find: my detector changes its mind depending on which four hundred words you paste."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/watermark-story-cover.png"
tags: [ai, statistics, academicintegrity, dotnet]
author: "Pedro Hernández (PeopleWorks)"
lang: en
---

# I tried to prove Claude's watermark didn't threaten my detector. I was wrong, and found a worse fault in my own tool

Claude models released on or after 2 August 2026 carry a machine-readable watermark, and Anthropic says earlier ones will follow during a transition period. The question arrived inside a week, in forums, on LinkedIn, in my inbox. *Doesn't that make a detector like yours obsolete?*

I built [SignsOfAI](https://github.com/peopleworks/SignsofAI) for teachers, and the short answer is uncomfortable for everybody. I wrote it by measuring rather than opining, and it came out the opposite of what I expected.

## Half the answer is in the vendor's own documentation

Before measuring anything, read. The mark is not a hidden character: it lives in **which word the model chose** among several equally valid ones, biasing that choice with a key. It is a version of SynthID-Text, published by Google DeepMind in 2024. This is not an Anthropic invention; it is an industry practice arriving at one more vendor.

Three things follow without measuring anything:

**Nobody outside the vendor can check anything today.** The key is theirs. A detection API is promised "soon". When it arrives, checking will mean uploading a student's essay to a company's server. That is precisely the line this project refuses to cross, since everything runs on the teacher's own machine.

**It marks only Claude.** Not GPT, not a local Llama, not any of the wrappers selling "humanisation". Which gives us the sentence that will be misused in both directions: **the absence of a mark is not evidence that a person wrote something**. Neither *"no mark, it's clean"* nor the worse inverse, *"no Claude mark, so they used something else, guilty"*.

**The vendor's own help centre lists what defeats it**: heavy editing, paraphrasing, translation, mixing into other writing, and very short passages. Those are, exactly, the conditions of a piece of student work.

## The half that had to be measured

One question is not settled by reading: **what does removing the mark do to the prose?**

Because the mark lives in word choice, no invisible-character cleaner touches it. Only a rewrite does. Services already sell that rewrite. My hypothesis was comfortable: a machine rewrite flattens sentence rhythm, so somebody stripping a watermark becomes *more* visible to my tool, not less.

The trouble with measuring that is you seem to need machine-written text to compare against, and this project has a whole page arguing why such a corpus should never be assembled: it is a sample of whichever models were convenient that month, it ages badly, and it flatters whoever put it together.

The way out was the **pair**. Each unit of the study is one passage measured twice: as its author wrote it, and after a model rewrote it. Same text, same author, same subject, nearly the same length. The baseline is not estimated from a population. *It is the text itself*. And the human halves come from the calibration corpus, every one published before generative models existed, which remains the only honest basis for calling writing human.

Thirty-two pairs. Eight from each stratum, in both languages.

## The answer: no

Five passages crossed the boundary that had not. Two crossed back. McNemar's exact test: **p = 0.453**.

That is not a result. With seven pairs changing side at all, only a clean sweep would have reached significance. The honest statement is that **this study cannot show that rewriting changes whether a passage is flagged, in either direction**. Not that rewriting is safe, and not that we catch it.

My hypothesis is withdrawn, and it says so in bold on the page.

What survives is narrower and still useful: a rewrite **does not repair a bibliography that contradicts itself**, and **does not return a student's prose to the shape of their own earlier work**. Neither of those checks runs on prose style, so paraphrasing leaves them alone.

## What I found without looking for it

Building the control turned up a number that did not fit. My calibration page publishes that **zero of ninety** human texts cross the boundary. Yet six of my thirty-two passages already crossed it before anybody touched them.

The difference was length. The passages run about four hundred words; the documents they came from, several thousand.

So I measured the same writing by the same people, three ways:

| The same writing, measured as | Flagged at 25/100 | 95% interval |
|---|---|---|
| whole documents | 0 / 32 | 0% – 10.7% |
| 400-word windows, three positions each | **13 / 89** | 8.7% – 23.4% |

And the number that says the most: **eleven of thirty documents are flagged at one position in the text and not at another, and none is flagged at all three.** Whether one of those authors gets accused depends on which four hundred words somebody happened to paste.

The mechanism is not mysterious. *Burstiness* is the spread of sentence lengths, and a short window holds few sentences: the long one with three clauses and the two-word fragment that together make a paragraph read as human may not both be inside it. The measurement does not become *uncertain*, which a reader could allow for. It **moves, in one direction, toward the machine**.

My boundary of 25/100 was measured on documents with a median of 3,241 words, and today it is applied to a pasted paragraph with nothing in the interface saying so. It is a defect, it is published on the page itself, and it is [issue #59](https://github.com/peopleworks/SignsofAI/issues/59).

There is a symmetry here I keep turning over: Anthropic says its mark is unreliable on short samples because there are too few word choices. Mine is unreliable on short samples because there are too few sentences. Two unrelated methods, the same floor, and a teacher holding a single paragraph is below both of them.

## Three reviewers and three false sentences

This project has a house rule: nothing that changes behaviour or a published number ships without adversarial review. This time it was three independent reviewers, each told explicitly **not to read the others' verdicts**, because reading one turns the second opinion into an echo.

The arithmetic survived intact. All three recomputed McNemar, the Wilson intervals, the quantiles, the counts; one regenerated the report byte-identical from the committed data.

The prose did not survive. Three published sentences were false:

**"A window from the middle of the document."** My code cuts from the beginning. I had described my own code wrongly. And it is not cosmetic: the beginning of a research article is its abstract, the beginning of an encyclopedia entry its lead, which is the most compressed and most formulaic prose either genre produces. I measured a genre effect and called it a length effect. Measured at three positions, the headline fell from 18.8% to 14.6%.

**"It is the only such case."** I had written by hand that one passage preserved a verbatim quotation, breaking the instruction's rule against leaving eight consecutive words alive. Measured: **twenty of thirty-two pairs**, the longest run 86 words. Nearly all are quotations — a constitutional court ruling, a political pamphlet, a published definition, and none can be reworded without falsifying it. The instruction demanded preserving every fact *and* replacing every word, and never said which wins.

**"It does flatten, in 20 of 32 pairs."** Sign test on 20 against 12: p = 0.215. Three paragraphs above, I had applied the exact test to another count and ruled it "not a result". I applied two different standards on the same page.

All three are the same failure: **machine-checked numbers beside a hand-waved method**. So the tool now measures its own compliance on every run and prints it, whether or not it flatters the study.

There was a fourth, caught only by the reviewer who read the Spanish line by line: **the rewrite silently corrected the originals**, which the instruction forbids in those words. *Intercesión* → *se cruza*. *Teoría de juego* → *teoría de juegos*. *Costa* → *consta*. The delivered treatment was not "paraphrase", it was "paraphrase and copy-edit", so part of every score change is error repair. And that lands squarely on the stratum which exists to measure the harm done to people writing in a second language.

## The conflict that has to be said out loud

The model that rewrote the passages is the same model that wrote the rules it was being rewritten against. Naming the model is provenance; it is not handling the conflict.

What argues in favour is in the data: the rewrite **introduced** signals as well as removing them, and pushed more passages over the boundary than it pulled back. A model gaming the measurement would not do that. But the objection is legitimate, it is the first one any hostile reader will raise, and the only real answer is another vendor's model repeating it. Until then the page says it plainly: **these numbers should not be quoted anywhere a teacher will act on them.**

## What a teacher takes away

Four things, and none of them is a reassuring headline:

1. **The watermark is no use to you today.** There is no public detector, and when there is, it will mean sending a student's work to somebody else's server.
2. **The absence of a mark means nothing.** In either direction.
3. **No tool, mine included, can tell apart** somebody who stripped a watermark from somebody who ran their own honest paragraph through a model for style, or because English is not their first language.
4. **Distrust any verdict on a short text**, mine included, until I fix #59.

All of it — the method, the data, the intervals and the faults — is published in [`Docs/PARAPHRASE.md`](https://github.com/peopleworks/SignsofAI/blob/main/Docs/PARAPHRASE.md), and regenerates with one command from the same repository.

A study that comes out the way you expected is pleasant. One that withdraws your hypothesis, finds three false sentences in your writing and uncovers a worse defect than the one you went looking for is worth considerably more. And when a project has spent eight articles demanding that everyone else publish their errors, publishing your own is not humility. It is the price.

*Built by Pedro Hernández — PeopleWorks, [Microsoft MVP for .NET](https://mvp.microsoft.com/en-US/mvp/profile/24060a02-dbc6-44ec-bca5-c213ff9835c5). By and for the education community.*
