---
title: "My AI detector never once gave a verdict, and nothing noticed for weeks"
description: "The same text, the same engine, the same run: 90/100 and 'Strong signs of AI writing' on screen, and no verdict at all in the document a teacher would print. One condition was false for every document ever analysed, and nothing in 340 tests compared the two."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/verdict-story-cover.png"
tags: [ai, testing, academicintegrity, dotnet]
author: "Pedro Hernández (PeopleWorks)"
lang: en
---

# My AI detector never once gave a verdict, and nothing noticed for weeks

Here is the same text, run through the same build of my own tool, in the same minute.

The command line:

```
  90/100  Strong signs of AI writing   (23 signals, English)
```

The report — the document a teacher exports, prints, and carries to an academic integrity meeting:

```
  **89.9/100**

  *Below the threshold this build can support, so no verdict is given.*
```

One engine. Two answers. And the second one was not a rare edge case: the exported report had **never printed a verdict, for any document, in any language, since the feature shipped**.

## An `if` that was always false

The report withholds its verdict below the threshold the build can support. That is deliberate and I still think it is right — a score with no error rate beside it is the thing this whole project exists to complain about.

The check looked like this:

```csharp
return c.For(result.Language)?.RecommendedThreshold is { } threshold
    && result.OverallScore >= threshold;
```

Read it slowly. It asks for the threshold measured **for that specific language**.

My calibration corpus has 90 texts published before generative models existed: 65 English, 25 Spanish. To bound a false-positive rate under 5% with nothing flagged, the statistics need roughly 75 texts in a group. Neither group has 75. So the per-language threshold is `null` for English, `null` for Spanish, and the condition after it is false. Always. For everyone.

I had written a careful rule — *never quote a rate measured on another language* — and then applied it to a question it did not govern. The result was a report that refused to speak about anything, ever, while the screen next to it spoke freely about everything.

## Why nothing caught it

Two reasons, and both are more interesting than the bug.

**The bands lived in nine places.** The verdict thresholds were written out in the analysis result, the report, the interface's localiser, the CLI's colour picker, two switches in the web page, the batch page, the live-rewrite panel. Kept in step by a comment: *"mirrors the bands in AnalysisResult.Verdict"*.

A comment is not a mechanism. They had already drifted. The batch page cut at 40 where everything else cut at 45, so a single build could colour the same document two different ways depending on which page you opened it in.

**Nothing compared the surfaces.** I had 340 tests. Every one of them checked a surface against its own expectations. Not one asked whether the CLI, the web page and the exported report said the same thing about the same text. The disagreement was not hiding in a corner; it was the loudest thing in the product, and it was invisible because no test was pointed at it.

That is the transferable lesson, and it cost me nothing to learn only because nobody was using the report yet. If you ship one engine behind several faces, write the test that runs one input through all of them and fails when they disagree. It is ten lines. Mine fails on the parent commit, which is how I know it tests something.

## The part that was actually embarrassing

Under the bug sat something worse, and I had known about it for two days without connecting them.

This project publishes a measured threshold. From the calibration page: **at 25/100, the tool flags at most 5% of writing known to be human** — 0 of 90 texts, with a 95% interval reaching 4.1%.

The product drew its line at **20**, in prose: *"Light signs of AI writing"*. That number was picked by hand, early, before there was anything to pick it against. One human text in the corpus scored above it. The highest-scoring human text in the whole corpus reached 23.4, comfortably inside the range the product was calling "light signs of AI writing".

Publishing a calibrated figure and shipping an uncalibrated one is precisely the failure I criticise other detectors for. I had done it in my own repository, in public, for weeks, on the same page as the measurement.

## Two reviewers, one disagreement, and the distinction that resolved it

I now run design reviews before writing code, with two independent models given the same brief and told to attack it. Both found the always-false condition on their own, without being told. That is the entire argument for the practice: I had stared at that method and not seen it.

They then disagreed, which was more useful than agreement.

One said: fall back to the pooled threshold when a language has none of its own. Otherwise the tool waits years for Spanish and helps nobody.

The other said: no — the codebase has an explicit rule against borrowing the aggregate, and either you honour it or you delete it, but you do not quietly break it.

Both are right, about different things, and the distinction took me an embarrassing while to see:

- Borrowing the aggregate **error rate** misstates reliability. Telling a Spanish writer their essay was judged by a tool wrong 4.1% of the time, when the measurement for Spanish alone supports only 13.3%, hands them a number three times better than anything measured on their language. That stays forbidden.
- Borrowing the aggregate **boundary** asserts nothing about reliability. It decides when the tool opens its mouth. It is measured, it is published, and it is printed on the page beside the language's own figure.

Same number, two different acts. One is a claim about how often I am wrong. The other is a line in the sand.

So the boundary is now borrowed and the rate never is — and there are three states, not two. A language **in** the corpus borrows the line and carries its own bound. A language **absent** from it gets no verdict at any score, because there would be nothing on the page to correct the impression a verdict leaves. A build with no calibration of its own says nothing about anything.

The reviewer who proposed the flat fallback would have broken that third case. I checked instead of agreeing, and a test that was right caught it.

## Four bands became two

The old scale read "Strong", "Moderate", "Light", "Reads mostly human". Four measured-sounding degrees.

Exactly one of those boundaries was measured. My corpus can locate the line where human writing stops and says **nothing whatever** about 45 or 70. No text known to be human came within twenty points of either. Grading "moderate" against "strong" would require a corpus of machine-written text, and the same calibration page argues at length against ever collecting one: it samples whichever models were convenient that month, ages badly, and flatters whoever assembled it.

The obvious compromise was to keep the words and add a footnote admitting they are unmeasured. I nearly did. What killed it: **the footnote is read once and the heading is read every time.** A page whose headline says "Strong signs of AI writing" and whose small print says "we cannot measure this" has not been honest — it has been honest in a place nobody looks.

So above the line the tool now says "Signs of AI writing" and lets the findings carry the weight, which is what findings are for.

Below the line was the more revealing one. The report said *"Reads mostly human"*. The interface said *"Minimal signs of AI writing"*. Same state, two different claims, and the first one was never mine to make. A detector that detects nothing also returns a low score, and I have deliberately never measured how much machine writing this catches. Both now read: **"No signs above the measured boundary."** A statement about the tool, not about the person.

It was the last place in the product where I said something about a human being rather than about my own instrument.

## The one that mattered

Then I went looking for what the change had made stale, and found something that had nothing to do with it.

The teacher package — syllabus language, a student sheet, a procedure for integrity committees — includes a paragraph for a teacher to copy into a disciplinary finding. It handed them the pooled rate: *"under 4.1% overall"*. Whatever language the work was in.

For a Spanish essay, the honest figure is 13.3%.

A committee judging a Spanish-speaking student was being handed a tool three times better than the one actually used, in the teacher's own handwriting, in a document that gets read at appeal. The student sheet had the same defect: **both** language editions quoted the pooled number, including the Spanish one written for the students most likely to be harmed by a rate measured mostly on English.

I had fixed exactly this bug inside the report two days earlier. I fixed it in the code and left it standing in the document that carries the code's output into a room where it can end someone's semester.

Every edition now quotes its own figure, and says the pooled one is more flattering and that is why it is not being used.

## What I would take from this

The verdict change moved no scores. I re-ran the calibration before and after and the published file is byte-identical: same fingerprint, same 90 texts, same threshold, same interval. That check exists so nobody, me included, can shift a boundary and present the improved number as an achievement.

Three things I would keep:

**One input, every surface, one test.** If several faces share an engine, something must fail when they disagree. Mine had 340 tests and none of them looked.

**A comment is not a mechanism.** Nine copies of a number synced by a sentence in a doc block drift, and they had.

**Check whether your product does what your measurement says.** The gap between a published figure and shipped behaviour is the exact thing I built this tool to point out in other people's work. It was in mine, in public, next to the measurement, for weeks.

The code is MIT, the corpus manifest and the calibration are in the repository, and the command that regenerates them is one line: [github.com/peopleworks/SignsofAI](https://github.com/peopleworks/SignsofAI).
