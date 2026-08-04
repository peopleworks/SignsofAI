---
title: "My AI detector scored this essay 0/100. Its bibliography was invented."
description: "The stylometric score found nothing. Comparing the document against its own reference list found five contradictions in half a millisecond, offline, with nothing sent anywhere — and one of them was a DOI that appeared on two different papers."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/citations-story-cover.png"
tags: [ai, academicintegrity, dotnet, education]
author: "Pedro Hernández (PeopleWorks)"
lang: en
---

# My AI detector scored this essay 0/100. Its bibliography was invented.

Here is the top of a report my own tool produced last week.

```
  0/100  Reads mostly human   (0 signals, English)
  words 157 · sentences 21 · burstiness 0.75 · lexical diversity 0.66
```

Zero signals. The vocabulary was fine. The sentence rhythm was varied and human. By every measure the detector has, that essay was written by a person.

Here is the rest of the same report.

```
  Sources  5 contradiction(s)
    ! line 5:  "Adeyemi, 2023" is cited in the text but appears nowhere in the reference list.
    ! line 10: "Delacroix-Barrios, 2025" is cited in the text but appears nowhere in the reference list.
    ! line 19: 2 references carry the DOI 10.1080/aie.2022.4471. A DOI identifies one work,
               so at most one of them is right.
    ! line 23: "10.55/pending" cannot be a DOI.
    ! line 23: Dated 2027, which has not happened yet (2026).
```

Two authors cited who are nowhere in the document's own bibliography. One DOI on two different papers. A malformed identifier. A source published next year.

The score said nothing. The sources said everything.

## Why a percentage is useless to a teacher

I have written before about why I refuse to hand anyone a confidence number. The short version: detectors flag 61% of essays by non-native English speakers as AI-written, a paraphrase attack drops one published detector from 70.3% accuracy to 4.6%, and even Turnitin tells you its own score should not be the sole basis for action against a student.

But there is a plainer problem, and it has nothing to do with accuracy. **A teacher cannot act on a percentage.** Imagine the meeting. "The software said 87%." Said what, exactly? Based on what? Show me the part that is AI. You cannot. You have a number and a bad feeling, and the student has a career.

Now imagine the other meeting. "You cite Adeyemi 2023 three times. There is no Adeyemi in your reference list. Can you send me the paper?"

That is not an accusation. It is a question, and it takes one sentence to answer. If the paper exists, the student forwards a PDF and everyone moves on with their afternoon. If it does not, nobody had to argue about statistics.

That difference is the whole reason this feature exists.

## The part I did not expect: you do not need the internet

My first design sent every reference to Crossref to check whether it resolved. That is the obvious approach and I was uneasy about it, because this tool's entire promise is that nothing leaves your machine, and "except your students' bibliographies" is a real asterisk.

Then I looked at what actually happens when a bibliography is invented, and the internet turned out to be mostly unnecessary. **An invented reference list contradicts itself before anyone gets round to asking whether the papers exist.**

The failures are structural, not factual:

- A name appears in the prose and nowhere in the list. This is by far the most common. Text and bibliography get produced in separate passes, and they drift.
- The same DOI turns up on two different works. A DOI identifies one thing; two entries carrying the same one cannot both be right. This is *very* common in generated bibliographies, which reuse identifier patterns the way they reuse phrasing.
- A DOI that is not shaped like a DOI. The standard is strict: `10.`, a four-to-nine digit registrant, a slash, a non-empty suffix. `10.55/pending` fails on the registrant.
- A publication year that has not happened yet.
- The text cites `[7]` and the list has five entries.

None of that requires a lookup. All of it is decidable from the document alone, in the browser, offline, with nothing sent anywhere. It is the same shape as the character-artifact check I shipped before it: **not a probability, a contradiction.**

Verifying that a well-formed, internally consistent reference is a *real* paper still needs a lookup. That is a separate, opt-in step, and by then there is a citation string to send instead of somebody's essay. I would rather ship the free half first and be honest about where it stops.

## Most of the work was in not accusing people

The engine is not the hard part. Cross-referencing two lists is undergraduate stuff. The hard part is that **a false "your reference is missing" is much worse than a missed one.** It sends someone hunting for something that is sitting right in front of them, and it only has to happen once before they stop believing anything the tool says.

Three cases cost me most of the day, and two of them were bugs I found by running the thing on realistic input rather than on my own tidy fixtures.

**Accents.** A student writes `(Martinez, 2020)` and the bibliography says `Martínez`. If that reports as a missing source, I have built a tool that singles out exactly the writers this project exists to stop singling out. Comparison is accent-folded, and there is a test named after it.

**Wrapped bibliographies.** Pull a reference list out of a PDF and the hanging indent is gone, so entries arrive across several lines. My first splitter treated any line starting with a capital and containing a comma as a new entry — which promoted `Journal of Educational Measurement, 59(4), 512-538.` to a reference in its own right, and then complained that nobody had cited it. A complaint about a line the author never wrote. The fix is to require a line to open with something shaped like an *author* before it can start an entry.

**Years hiding inside identifiers.** This one was nastier. The DOI `10.1080/aie.2022.4471` contains `2022`, which is not a publication year and has nothing to do with anything. That stray number split wrapped entries in the wrong place — and on a DOI ending `.2027.` it would have reported a perfectly ordinary reference as published in the future. An accusation assembled entirely out of an identifier. Links and DOIs are now stripped before any year is read, and the regression test says so.

There is also a stoplist for words that sit in front of a year without being anybody's name: Table, Figure, Section, March, Tabla, Figura, Capítulo. Without it, `(Figure 2019)` becomes a citation of someone called Figure, who is then reported as missing from the bibliography. Manufacturing an accusation out of a caption is not a bug I wanted to ship.

And the whole thing refuses to run when it cannot find a reference list. Guessing where a bibliography starts would produce complaints out of formatting, so a document that does not announce one gets its citations counted and no cross-checks at all.

## It does not touch the score

Same rule as the artifact check, and a test whose only job is to fail if it ever changes.

The temptation is obvious: an invented bibliography is damning, the number should go up. But a score is a judgement, and a judgement is arguable — as it should be, since it is a reading of prose. Whether the name "Adeyemi" appears in a list is not arguable. You look, and it is there or it is not.

Fold the second into the first and you have converted the only actionable thing on the page back into a percentage nobody can take to a meeting. So they sit in separate panels, they are stored separately, and the report says what it is not:

> A source missing from its own bibliography is usually a slip, and it is always the writer's to explain. Ask for the source itself: a real one can be produced in seconds, and an invented one cannot.

## Where I have landed

I no longer think the interesting question is "did a machine write this". I do not think that question can be answered reliably, I am fairly sure it cannot be answered fairly, and I am certain that answering it with a percentage helps nobody in the room.

The useful question is narrower and much easier: **does this document hold together?** Do its sources exist in its own pages. Do its characters come from a keyboard. Does its own bibliography agree with its own prose.

Those have answers. The answers are checkable by the person being asked about them. And they turn a confrontation into a conversation, which is what every academic integrity office has been asking for since 2023 and what almost no tool in this category actually delivers.

MIT and free, engine and rules and tests, on [GitHub](https://github.com/peopleworks/SignsofAI). If it reports a missing reference that is not missing, that is the bug report I want most — there is no server and no telemetry here, so a human telling me is the only way I find out.

---

*Written by a human and checked with the tool it describes: **5/100, reads mostly human**, burstiness 0.69, about 1,550 words. The example report in this article is real output, not a mock-up; the essay behind it is one I wrote to be wrong on purpose.*
