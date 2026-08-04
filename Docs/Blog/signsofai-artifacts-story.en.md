---
title: "Someone could switch off my AI detector with find-and-replace"
description: "Swapping a few letters for identical-looking ones from another alphabet drops seven published AI detectors below chance. It worked on mine too. The fix took 200 lines, no model, and one decision I want to argue for: it does not touch the score."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/artifacts-story-cover.png"
tags: [dotnet, unicode, security, ai]
author: "Pedro Hernández (PeopleWorks)"
lang: en
---

# Someone could switch off my AI detector with find-and-replace

A post went by on Reddit: *Best AI Humanizer for Students in 2026*. Seven tools, scored across five categories, a blind reviewer, fifty university drafts. It read like a study.

It is an advert. Two upvotes, one comment — the author, linking their own previous "studies". Fifty texts that were never published. Scores to one decimal place. The tool being sold wins every category.

But it sent me to the actual research, and the research had something to say that I did not enjoy hearing.

## The arms race is already over, and I lost it

The numbers are not close.

Paraphrasing a text with DIPPER takes DetectGPT from 70.3% accuracy to 4.6%, at a fixed 1% false positive rate, without changing what the text means. A 2026 reinforcement-learning attack reaches near-zero detection on three of four detectors. Across six commercial detectors, average accuracy is 39.5%, and falls to 17.4% under light modification.

And then the one that should end the conversation. A COLING 2025 paper called SilverSpeak swaps characters for **homoglyphs** — letters from other alphabets that render identically. A Cyrillic "а" (U+0430) next to a Latin "a" (U+0061). Same shape, different codepoint.

Seven detectors went from a Matthews correlation of 0.64 to **−0.01**. Worse than a coin.

Meanwhile, detectors flag 61% of essays by non-native English speakers as AI-written. So the category has landed in the worst possible place: easy to defeat if you know the trick, and punishing if you happen to write in your second language.

I build one of these tools. I had to sit with that for a while.

## Then I checked whether it worked on me

SignsOfAI does not use a classifier. It matches a catalog: overused vocabulary, rhetorical crutches, syntactic tells, sentence rhythm. Every rule points at a word or a pattern.

Which means every rule matches on text. And if the text says `dеlvе` — with two Cyrillic letters — the rule looking for `delve` sees a word it has never heard of.

I measured it on one AI-flavoured paragraph. Intact, the engine reports 17 signals and scores 94/100. With the vocabulary made unrecognisable, the same paragraph reports **6 signals and 80/100**. Eleven findings gone, and nothing visibly changed on screen.

That is not a subtle degradation. That is a published catalog anyone can switch off with find-and-replace.

## The thing about those characters

Here is what turns this from a problem into the most useful feature I have shipped in months.

A Cyrillic "а" inside an English word is not a style. It is not a probability. It is not a reading of how someone writes.

**It is a physical artifact of a tool.**

Nobody types U+200B. A zero-width space does not come out of a keyboard. A Cyrillic letter does not land in the middle of "analysis" by accident. These characters get there because a program put them there.

So the check that defeats the attack is also the only check in the product that returns a **fact** instead of a judgement — and it is by far the cheapest thing on my roadmap. A character walk. No model, no dependencies, no network. It runs in the browser, it runs offline, and it works below the level of language, so it is bilingual for free.

## Normalize first, and never clean silently

The fix is two pieces.

First, a scanner that walks the text by Unicode scalar and reports what it finds: zero-width and joining characters, direction controls, private-use codepoints, hidden tag characters, and letters standing in for Latin ones — with the codepoint, the line and the column of every single occurrence.

Second, the analyzers stop reading the raw string. They read a cleaned copy, with the impostor letters replaced by the letters they were impersonating, alongside a map back to the original so every finding still points at the reader's actual document.

```
● [Lexical] dеlvе
    "delve" is heavily overused in AI writing.
```

The rule fires again. The word is shown the way it appears in the file, Cyrillic letters and all, because sending a reader to look for a word that is not there is its own kind of failure.

Two rules I would defend anywhere:

- **The cleaning never happens quietly.** Anything the normalizer removes, the report names. A tool that silently corrects its input is a tool that hides evidence from the person it is supposed to be helping.
- **The normalizer does not decide what an artifact is.** It consumes the scanner's report and acts on it. One definition, one place. They cannot drift into disagreeing about the same document.

That second one matters more than it looks. This same substitution is used to attack **authorship attribution** — there is a 2025 paper that targets Burrows's Delta specifically, using zero-width steganography. Per-author comparison is the most promising answer to the false-positive problem, because it asks "does this look like *this* writer" instead of "does this look like AI", and it is the answer that helps second-language writers most. Normalization is not a nice-to-have for that feature. It is a prerequisite. You cannot compare someone against their own baseline if the baseline can be poisoned.

## The decision I want to argue for: it does not touch the score

The artifact report contributes exactly zero to the 0–100 score. There is a test whose entire job is to fail if that ever changes.

The temptation is obvious. Text has been through a rewriting tool, that is suspicious, the number should go up. It would take one line.

It would also destroy the only thing that makes the feature worth having.

A score is a judgement, and a judgement is arguable — as it should be, because it is a reading of prose and prose is arguable. A character at line 14, column 3 is not arguable. You can open the file in any editor and look. You do not have to trust me, my weights, my thresholds, or my opinion about the word "delve".

Fold one into the other and you have converted the only checkable thing in the product back into an opinion. So they are presented apart, they are stored apart, and the panel says so out loud:

> None of this affects the score above. A score is a judgement you can argue with; a character at a given line and column either is there or is not, and you can check it in any editor without trusting us.

Turnitin ships something adjacent — a percentage of text that may have been through a "bypasser" tool. A percentage. My version gives you the codepoint and the coordinates of every occurrence. A number cannot be audited. A list of positions can.

## The tests that matter most are the ones about not firing

Most of the test file is about what must **never** be flagged, because a false positive here is not a debatable opinion about someone's writing. It is a wrong statement about what is in their file.

- `Spanish_is_never_flagged_for_being_Spanish` — "análisis", "señora", "pingüinera". Accented Latin letters are deliberately absent from the lookalike table. A tool that treated "á" as an impostor would be exactly the instrument this whole category is criticised for being.
- `A_real_Greek_word_is_left_alone` — "α-helix" is a Greek letter *beside* a Latin word, not one hiding *inside* one. The decision is made per run of letters: a lookalike only counts when Latin letters are the majority of the run it sits in. That is the shape of a substitution, and not the shape of a real word from another alphabet.
- `An_emoji_sequence_is_not_an_artifact` — a zero-width joiner is how a multi-person emoji is built.
- `Join_controls_are_left_alone_in_the_scripts_that_need_them` — in Persian, Arabic and the Indic scripts, that same character is ordinary orthography.

## Count is not the measurement. Distribution is.

Ordinary documents pick these up all the time. Copy from a web page and you carry non-breaking spaces. Extract from a PDF and you carry soft hyphens. Write in two languages and you carry two alphabets.

So the report separates *how many* from *how spread out*. Pasted text carries its artifacts where the paste landed. A tool that rewrote the whole document leaves them everywhere it touched. The document is divided into sections, and the report states how many contain one:

> 47 characters that typing does not produce, spread across 8 of 10 sections of the document. That distribution is what a tool leaves behind when it processes a whole text.

You can disagree with that reasoning. You can see the sections, the count, and every position, and reach your own conclusion. That is the point.

## What it does not mean

Every report that says something also says what it does not mean, in the same panel, in the language of the text:

> This says nothing about who wrote the text, and it is not evidence of dishonesty. It is a question about where the file has been: ask the writer to open the document and describe how it was produced.

Even Turnitin says its own score should not be the sole basis for action against a student, and declines to report anything in the 1–19% range because of false positives. The market leader is telling you not to use it as proof. That is worth repeating in a room full of teachers, right before you concede that your own tool has the same limitation.

## Where this leaves me

I am not going to win an arms race against reinforcement-learning paraphrase attacks. Nobody is. Every hour spent making the statistical detector cleverer is an hour spent losing more slowly, and the collateral damage lands on the student who learned English second.

What survives is the stuff that is checkable: hallucinated citations that do not resolve, document metadata that says a 3,000-word essay took ninety seconds, a comparison against a writer's own earlier work — and now, characters that a keyboard cannot produce, at coordinates anyone can verify.

None of that is a percentage. All of it is something a person can take to a meeting and defend.

The engine, the rules and the tests are MIT on [GitHub](https://github.com/peopleworks/SignsofAI). If you find a document it gets wrong, that is the report I most want — the tool has no server and no telemetry, so a human telling me is the only way I ever find out.

---

*Written by a human and checked with the tool it describes: **14/100, reads mostly human**, burstiness 0.70, about 1,920 words.*

*It also trips its own new check, which is a better demonstration than anything I could have staged. Four Cyrillic "e" — the examples above, at line 37 and line 64. The report calls them **present, not spread**, which is exactly right: they sit in two places because I typed them there on purpose, and that is not the shape of a document a tool has been through.*

*I left them in. Editing my own examples away to get a cleaner report would be the same move as a detector author quietly rewording around a false positive — and I would rather you could see the thing working on me.*
