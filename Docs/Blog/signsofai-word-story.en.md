---
title: "I put my AI detector inside Word, and the first thing it did was refuse to answer"
description: "A task pane is a browser, so the engine runs on the machine and the document is never uploaded — which is the opposite of how every other add-in in this category works. Then it read a real document, declined to give a verdict because the document was 357 words, and reported six invisible characters anyway."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/word-story-cover.png"
tags: [ai, dotnet, webassembly, academicintegrity]
author: "Pedro Hernández (PeopleWorks)"
lang: en
---

# I put my AI detector inside Word, and the first thing it did was refuse to answer

The most common request for this project has been the same sentence for months: *put it in Word*. It is a fair request. People do not write in a browser tab; they write in a word processor, and asking someone to copy 2,000 words out of the document they are working on, into a website, to find out something about the document they are working on, is a workflow only its author could love.

So the add-in exists now. The interesting part is not that it works. It is what it did the first time it ran on a real document.

## It read the document and declined to give a verdict

357 words, a technical note about an API, open in Word on the web. The pane read it and returned a score of 0 out of 100 — and under the score, instead of a conclusion, this:

> **No verdict at this length.** This text is 357 words. The boundary was measured only on texts of 649 words and longer, so no verdict is given — the score is neither evidence that a machine wrote this nor evidence that a person did. Everything below is unaffected.

That paragraph is the entire project, printed inside Word.

The number this tool judges by, 30 out of 100, was not chosen. It was measured on 296 documents that were written before 2022 and are therefore human by their dates rather than by anyone's opinion. At that boundary, 2 of the 296 are flagged: a false-positive rate of 0.7%, with a 95% interval reaching 2.4%. That figure is published, it moves when the corpus moves, and it stopped being zero the day 206 essays by adults learning English joined the corpus.

The shortest of those 296 documents is 649 words. Below that length, there is nothing the boundary was fitted on. A score can still be computed — arithmetic does not stop working — but there is no evidence about what it means, because no text that short was ever measured. So the tool says so and stops.

## The temptation here is enormous, and worth naming

A detector that answers every question feels better. It is easier to demo, easier to sell, and nobody writes to complain that you gave them an answer.

But the answer would be invented. The honest version of "I have not measured this" is not a smaller number or a softer adjective. It is silence, with the reason attached. In the add-in this costs something real: most of what people paste into a detector is a paragraph, and most paragraphs are under 649 words. The tool will decline a great deal.

I would rather it decline than guess in a sidebar next to a student's name.

## What it found anyway

The same panel, on the same 357 words, reported six no-break spaces (`U+00A0`) with the codepoint, the count, and a button to show every position in the document.

That is not a judgement about prose, and it carries no threshold, so it holds at any length. It is a fact about the file: those characters are in it, and typing does not produce them. Copying from a web page or a PDF does, and so do some tools that rewrite text to evade detectors.

The panel says the rest out loud, because a fact left to imply something is worse than an opinion:

> This says nothing about who wrote the text, and it is not evidence of dishonesty. It is a question about where the file has been: ask the writer to open the document and describe how it was produced.

A percentage gets argued about for half an hour in a meeting. "There are six invisible characters here, and here is where they are" gets resolved with a question.

## The part that matters most is architectural

Every other add-in in this category sends your document to a server. It has to: the analysis *is* the server, so the text must go where the analysis lives.

This one cannot, and the reason is boring in the best way. **A task pane is a browser**: an embedded WebView with the same rules as a tab. The engine is WebAssembly, it is downloaded once with the pane, and it runs there. There is no endpoint in this add-in, so there is nothing for the document to be sent to. The guarantee the web app already made now applies in the application where the document already lives.

And the manifest asks Word for `ReadDocument`, not `ReadWriteDocument`. That is a two-word difference with real teeth: **Word enforces it.** The add-in is not able to change your document, whatever a website might claim about its intentions. If you are a teacher deciding whether to let a tool near coursework, that line in the manifest is worth more than any paragraph on a landing page — including this one.

## It is a third host, not a second product

The pane is not a smaller engine. Every rule, the character scan, the citation cross-check and the rules about what may be claimed arrive from the same shared library the web app and the Windows app render. Word is a third place to run it.

The class holding what each host can do had this comment in it long before there was a third one:

> The interface should branch on what is possible, not on who is asking — the day a third host appears, or a browser stops blocking a thing, the components do not need revisiting.

That turned out to be true, with one exception worth mentioning because it is the sort of thing that rots quietly. The shared footer says *"runs 100% in your browser."* Inside Word that is false, in the same way it was false inside the Windows app, which shipped with that line in a WPF window for weeks before anyone noticed. So the new host got its own sentence. A tool that asks people to show evidence cannot be careless about the claims it makes about itself.

## It did not work the first time, and the reason is instructive

The first sideload put the pane in Word, showed the ribbon button, opened the panel — and displayed the wrong page, with Word warning that the add-in might not load properly.

It looked like a flaky environment. It was a 404. The manifest points at a URL the deploy had never published, so the host answered with the site's single-page fallback; the app that fell out of that has no route for the pane's address, and correctly said the page did not exist. Word's warning was the 404, three steps removed from its cause.

The deploy now publishes the pane, and refuses to finish if its entry point is missing. A task pane is loaded by URL from inside a word processor, where a missing file surfaces as "this add-in may not load properly" — an error message that points at nothing. Better to break the deploy than to debug that twice.

## PowerPoint is a different product, and I am not shipping it yet

The same manifest can declare more hosts, and it would be one line. It would also be dishonest.

A slide deck rarely reaches 649 words. Pointed at one, this add-in would do what it did above — withhold the verdict — for almost every deck, correctly and to nobody's benefit. What *does* work at slide length is the part with no threshold: the character scan, and the named tells shown without a score.

So the honest question in PowerPoint is not "did AI write this deck". It is "does this deck carry the fingerprints of a tool that rewrites text to evade detection", which is a different product with a different promise. It may well be worth building. It is not this one with an extra line in a file.

## What this does not fix

The add-in does not make short documents measurable. It surfaces the limit in the place where people will meet it most often, which is progress of a kind, but the gap is still there: the corpus contains no student essay under 649 words, and until it does, everything below that length gets a score and a refusal.

It does not read comments, footnotes or tracked changes — only the body.

And it is not in the Office Store, which means installing it is still a manifest and a menu rather than a button. That is next.

## The general version, for anyone who does not care about AI detection

The feature people asked for was "put it in Word". What shipped is mostly a set of refusals: it will not give a verdict it cannot support, it will not upload your document because it has nowhere to upload it to, and it asked for permission to read and deliberately not to write.

None of that is what a feature request looks like. All of it is what the feature is worth.
