---
title: "I built the AI detector that answers a different question"
description: "Detectors flag 61% of essays by non-native English speakers. The fix is not a better classifier — it is a better question: not \"does this look like a machine\" but \"does this look like the person who wrote the others\". Here is what that took, and the threshold I deleted."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/baseline-story-cover.png"
tags: [ai, stylometry, academicintegrity, dotnet]
author: "Pedro Hernández (PeopleWorks)"
lang: en
---

# I built the AI detector that answers a different question

Sixty-one percent.

That is the share of essays by non-native English speakers that AI detectors flag as machine-written. Not sixty-one percent of the cheats. Sixty-one percent of the essays.

The mechanism is not mysterious. These tools learned that formal, careful, slightly stiff prose looks like a language model. And formal, careful, slightly stiff prose is exactly what you write in a language you learned second. The tool is not detecting AI. It is detecting a second language, and reporting it as dishonesty.

You cannot fix that with a better classifier, because the classifier is answering the question it was asked. You fix it by asking a different question.

## The question

Not: *does this look like a machine?*

But: *does this look like the person who wrote the others?*

That inversion does everything. A student whose ordinary register is formal has a **formal baseline**. Measured against themselves, formal is not a red flag — it is Tuesday. The very trait that condemns them under the first question exonerates them under the second.

The method is not new. Burrows's Delta, published in 2002, counts how often a writer reaches for small ordinary words — "the", "of", "however" — and compares those rates between texts. Function words are what forensic linguistics uses precisely because they ignore the topic: a person uses them at their own rate whether they are writing about hydrology or opera.

What is different is what I did with the output.

## Classical Delta asks a question I do not have the standing to answer

The textbook setup gives Delta a set of candidate authors and asks which one a text resembles. Applied to a classroom, that becomes "is this the student, or someone else", and answering it requires a threshold — a line past which the software says *not them*.

Somebody has to invent that line. It would be me. And a number I picked would end up quoted in a disciplinary meeting as though it came down from a mountain.

So the report never states a threshold. It states two things side by side:

- how far the questioned text sits from the writer's centre;
- how far **each of the writer's own pieces** sits from that same centre, measured identically.

The scale belongs to the writer. Nobody has to accept my idea of "too far", because there isn't one — there is only their own variation, and where this piece falls against it.

Here is real output, one of my own articles held out and compared against three others:

```
  0.654  inside this writer's own range
  Distance 0.654. This writer's own pieces sit up to 1.18 from their centre,
  so this one is inside the range they already cover.
  their own pieces: 0.66 · 0.78 · 0.84 · 0.85 · 0.86 · 0.96 · 0.98 · 1.03 · 1.18
```

You can read that without knowing what Delta is.

## The bias I had to go back and remove

The first version computed the writer's own spread by measuring each of their pieces against the statistics of all their pieces — including itself.

That is wrong, and wrong in a direction that matters. A piece included in the statistics it is then scored against gets pulled toward the centre. The writer's range comes out artificially tight. And a tight range makes the questioned text look further outside it than it is.

The bias runs against the person being asked about. Leave-one-out fixes it: each of the writer's pieces is measured against the *others*, exactly as the questioned text is. It costs a loop.

I mention it because it is the kind of error that never announces itself. Nothing crashes. The numbers look plausible. Someone just gets treated slightly worse than the evidence warrants, every single time, forever.

## The threshold I deleted

Here is the part I did not expect to write.

Delta is a mean across every measured word, and that dilutes. I tested against a deliberately extreme case — the US Constitution, against my own articles — and it landed at *the edge* of my range rather than beyond it. Yet the underlying features were screaming: that text uses "of" at **92 per thousand words** where I use it at 13. Seven times my rate. One feature at z = 10.7, averaged into invisibility by eighty features that happened to match.

So I added a second measurement that does not average anything: **how many words the questioned text uses at a rate the writer has never used them at**, in any of their pieces. Not a statistic. A range and a number, checkable by counting.

The separation was clean. My own articles, held out one at a time: 0, 1, 1 and 3 words outside my range, out of roughly 80. The Constitution: 14 out of 93.

And then I wanted to wire it into the verdict. A quarter of the words outside the writer's range, say, and the aggregate stops having the last word.

I deleted it.

Because where did "a quarter" come from? From me, looking at five documents, picking a number that separated them. That is not calibration. That is the exact move this entire project refuses to make everywhere else — and it would have been buried in a constant near the top of a file, doing quiet damage in cases I never tested.

The count is reported. It is stated in the summary in plain words. A person reads it. The placement is decided by one rule anyone can restate out loud: *is this further from the writer's centre than their own pieces are*.

There is a comment in the source saying so, and a test named `That_count_does_not_decide_the_placement` so nobody wires it in later as an improvement. Calibrating it honestly would need a corpus of texts with known authorship, which is its own piece of work and not one I can fake with five files and an afternoon.

## It refuses to answer more often than it answers

Roughly 1,400 words of earlier work, spread across enough separate pieces, and 300 in the submission. Below that it says so and returns nothing.

That is a real limitation and it will be the most common outcome in practice. A teacher who has one prior assignment from a student cannot use this at all.

I would rather that than the alternative. A distance computed from four hundred words is noise, and noise with a decimal point on it is exactly what people believe.

## What the code will not let you say

There is no result meaning *someone else wrote this*. Not disabled, not hidden behind a flag — the enumeration has four values and none of them is that one, and there is a test asserting the list.

```csharp
Assert.Equal(["Undetermined", "WithinRange", "AtTheEdge", "BeyondRange"], names);
```

Style moves with the assignment. With the genre. With the deadline, a co-author, an editor, a good night's sleep, and with a person simply getting better at writing, which is supposedly the point of the exercise. A text outside the range is a reason to ask what changed. It is not evidence, and a type that cannot express the accusation cannot have the accusation read out of it by mistake.

The advice printed under every single result says the same thing, in whichever language the text was written in:

> Style moves with the assignment, the genre, the deadline, and with a person simply getting better. A text outside the range is a reason to ask what changed, never a conclusion about who wrote it — and a text inside the range is the more useful result, because it is the one that settles a suspicion.

## The result I actually built this for

Everything above is about the accusatory direction, because that is where the harm lives. But the useful direction is the other one.

A student is suspected. The teacher has three of their earlier essays. The new one lands inside the range those three cover between themselves, and the report says so with the numbers beside it.

That is not a detection. That is a suspicion ending quietly, on evidence, before it becomes a meeting — and nobody had to argue about statistics.

Settling suspicions is most of what an integrity process should be doing, and it is the one thing almost no tool in this category is built to do. Every one of them is optimised to find something. This one is at its best when it finds nothing, and says so clearly enough that a teacher can close the tab and go home.

Function-word lists live in the rule packs as JSON, so adding a language is a pull request and no compiler. MIT, engine and tests included, on [GitHub](https://github.com/peopleworks/SignsofAI).

---

*Written by a human and checked with the tool it describes: **8/100, reads mostly human**, burstiness 0.63, about 1,570 words. The held-out comparison quoted above is this article's own family of posts, measured for real — and the one that came out at 0.654 is the first article I ever wrote about this project.*
