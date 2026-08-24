---
title: "My AI detector scored a passage 94 out of 100, and I made it refuse to say anything"
description: "The boundary it judges by was measured on documents of 662 words and up. It was being applied to a pasted paragraph, and the error runs one way: the same writing flags 0 of 32 whole and 6 of 32 as excerpts of itself. The fix was not a better model. It was to say less."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/length-story-cover.png"
tags: [ai, statistics, academicintegrity, dotnet]
author: "Pedro Hernández (PeopleWorks)"
lang: en
---

# My AI detector scored a passage 94 out of 100, and I made it refuse to say anything

Someone wrote to me last week about a version number. They thought the Windows app was out of date, and they were right, for a reason neither of us expected. Chasing it, I ended up looking at what my own tool says about the thing people do with it more than anything else: paste a paragraph in and press nothing.

It says a lot. It should not.

Here is what it does now, on a passage stuffed with every tell the rule pack knows:

> **No verdict at this length**
> 23 signals found · analysed as English
> This text is 66 words. The boundary was measured only on texts of 662 words and longer, so no verdict is given. The score is neither evidence that a machine wrote this nor evidence that a person did.

The score is still there, in grey: **94 out of 100**. All 23 signals are still listed with the phrase that matched each one. What disappeared is the sentence that accuses.

## What the boundary was measured on

This project publishes how often it is wrong about a human. Ninety texts, all published before generative models existed, and at a threshold of 25 out of 100 it flags none of them, with a 95% interval reaching 4.1%. That number is the reason anyone should take the rest of it seriously.

I had never looked at the *shape* of those ninety texts. So I did.

```
 shortest      662 words
 median      2,772 words
 longest     9,328 words
 below 600         none
```

There it is. The boundary was fitted on documents, and it was being applied to paragraphs. Every verdict about a pasted excerpt was extrapolation onto a population the corpus does not contain.

## And the extrapolation runs one way

If the error were symmetric, a reader could allow for it. It is not, and I know that because I measured it a week earlier while studying something else.

Take the same documents. Cut 400-word windows out of them. No author changes, no subject, not a single sentence rewritten. Only the scissors.

```
 Window position   Windows   Flagged   Rate     95% interval
 opening           32        6         18.8%    8.9% – 35.3%
 middle            30        4         13.3%    5.3% – 29.7%
 late              27        3         11.1%    3.9% – 28.1%
```

Whole, those same documents flag zero of the thirty-two.

The obvious objection is that the windows are shorter and also cleaner: a whole article carries figure captions and boilerplate that an excerpt has stripped. So there is a control row. Apply the same filter, cut nothing, and it is still 0 of 32. The difference is length.

## The sentence I keep coming back to

Counted by document rather than by window, of the 30 documents that produced more than one window: **11 are flagged at one position and not at another. None is flagged wherever the window falls.**

Whether one of those authors gets accused depends on which four hundred words somebody happened to paste.

They are all human. Every one of them was published before any of this existed.

## Why it happens, which is not mysterious

The strongest signal this tool has is burstiness: the spread of sentence lengths. Human prose is uneven. Unprompted model output tends to find a width and stay there, and no word list is needed to see it.

A 400-word window holds perhaps twenty sentences. The long one with three clauses and the two-word fragment that together make a paragraph read like a person may not both fall inside it. So the estimate does not merely get noisier as the text gets shorter. It moves, in one direction, toward the machine.

## The fix was to say less

The instrument is a floor. Below it the score appears with an explicit statement that the boundary was never measured at that length, and no verdict is given at all.

The interesting decision was where to put it, and I want to be exact about this because the first design was wrong and a review caught it.

The tempting move is to *fit* one: cut windows at 150, 300, 600, 1200 words, measure the flag rate at each, and pick the length where it drops under the target. Every version of that idea failed for the same underlying reason. Windows sliced out of a long document are not the population the floor is meant to protect. A student's four-hundred-word answer was *composed* at that length, and its sentence lengths are a whole distribution rather than a truncated one. A floor fitted on truncations and enforced against compositions repeats, in a new dimension, exactly the mistake it exists to prevent.

So the floor is not fitted at all. It is an *observation*: the shortest text the boundary was measured on. 662 words. The calibration tool computes it and writes it into the snapshot the engine carries, next to the error rate.

That makes the claim weaker than it looks, and deliberately so. It does not say the tool breaks below 662 words. It says nothing that short was ever measured, which is the only thing the evidence supports. There is no grid, no sliced windows, no subset chosen until a number came out, and nothing to argue about except a fact anyone can recompute.

There is also no ceiling, and the asymmetry is measured rather than assumed. Shortening a text moves its score toward the machine. Nothing suggests a thesis longer than the corpus is at risk, so silencing the long end for the sake of symmetry would withhold a verdict for a reason nobody has evidence for.

## Colour is part of the verdict

One detail that nearly shipped wrong, and that I suspect is wrong in other people's tools right now.

When the verdict is withheld, what colour is the score? In my code a withheld verdict fell through to the same state as "below the threshold", which every surface paints green. So the page would have refused to accuse in words, and certified the passage as clean in the loudest channel it has.

A 94 out of 100 in green is a claim. It is the opposite of the claim being withheld, made larger. The score is grey now, and there is a test that says so.

## What this does not fix

A 900-word essay is above the floor and still far below the median of the corpus. The floor is a coverage gate, not a correction. Making the boundary depend on length needs short complete texts published before 2022, writing that somebody composed at that length rather than a long document cut down, and that corpus does not exist yet. It is open as an issue, and it is now the most wanted contribution to this project.

And the honest headline: **this does not make the tool more accurate. It makes it quieter.** It answers fewer questions than it did last week. I think that is an improvement, and I understand why a product manager would disagree.

## The part that connects to last week

I spent the previous week measuring whether Anthropic's watermark makes this project obsolete, and it does not. Buried in that work was a line I did not fully absorb at the time: the watermark is unreliable on short samples, because few word choices carry little information.

My tool is unreliable on short samples, because few sentences carry little rhythm.

Two unrelated methods, built by people with very different resources, arriving at the same floor from opposite directions. A teacher holding one paragraph of a student's work is below it either way. That is not a coincidence about implementations. It is what happens when the thing you are measuring is a distribution and you are handed too little of it.

## The general version, for people who do not care about AI detection

A threshold is valid over the population it was fitted on, and nowhere else.

That sentence is not controversial. What is remarkable is how rarely anyone writes down what their population was. Every detector in this category will give you a percentage about a paragraph. Not one of them tells you the lengths, the languages, or the genres of the writing that percentage was calibrated on, which means you cannot tell whether the number applies to the thing you pasted.

I had that defect too, in a project whose entire argument is that this category overclaims. It shipped for months. The corpus was sitting in the repository the whole time with its shortest text right there in the manifest, and I had never asked it the question.

Publishing the method is what made it findable. Not by anybody else, in the end. By me, a year later, reading my own numbers with a different question in my head.

---

*Signs of AI Writing is free and open source: rules, calibration corpus and method in the repository. It runs in the browser, on the command line, as an MCP server, and as a Windows app. The floor described here ships in 0.5.0. If you want the shortest thing you can hand a teacher, the false-positive rate lives in `Docs/CALIBRATION.md`, and the length range it covers is now printed on the same page.*
