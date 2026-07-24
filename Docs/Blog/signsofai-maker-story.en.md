---
title: "I built an AI-writing detector that shows its work — and speaks Spanish"
description: "Most AI detectors are black boxes that spit out a number. I built one that shows you the evidence, runs entirely in your browser, and treats Spanish as a first-class language. Here's how, and why."
canonical_url: "https://peopleworks.github.io/SignsofAI/"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/social-preview.png"
tags: [dotnet, blazor, ai, webassembly]
author: "Pedro Hernández (PeopleWorks)"
lang: en
---

# I built an AI-writing detector that shows its work — and speaks Spanish

Most AI detectors are black boxes. You paste a paragraph, a number comes back — "87% AI" — and you're supposed to trust it. A teacher can't act on that. A writer can't learn from it. And if you wrote in Spanish, the number is often worse than a coin flip.

So I built the opposite. It's called **SignsOfAI**, it's free, it runs 100% in your browser, and for every signal it flags it tells you *what* the tell is and *how to fix it*. Try it: [peopleworks.github.io/SignsofAI](https://peopleworks.github.io/SignsofAI/).

This is the story of how it works and the decisions behind it.

![The score climbing live from 76 to 87 as AI tells accumulate while typing, then every tell highlighted with its fix](https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/screenshots/analyze-live.gif)

*The score updates as you type. Every highlight comes with a suggested fix.*

## The itch: a score you can't argue with is a score you can't trust

Two things bothered me about the detectors everyone links to.

First, they hide their reasoning. A percentage is not evidence. If a student is accused of using AI, "the tool said 87%" is not something you can defend, appeal, or learn from. The number feels precise and objective. It is neither.

Second, they think in English. The research, the training data, the tells — all English. Spanish gets a machine translation of English rules, which misses how AI actually *sounds* in Spanish: "sumérgete en el vasto mundo de", "cabe destacar que", "no solo… sino también". Half the planet writes in something other than English, and the tools treat that half as an afterthought.

I wanted a tool that was **explainable, actionable, and bilingual** — and that never pretended to be a lie detector.

## What it actually does

SignsOfAI does two jobs.

**1. It lints writing for the tells of AI.** Paste, upload a `.docx`, or just start typing. As you write, it scores the text 0–100 and highlights the signals in four families:

- **Lexical** — overused vocabulary. `delve`, `tapestry`, `multifaceted`, `underscore`, `leverage`. Each word is weighted by how much more common it became after ChatGPT shipped. `delve` alone is about 48× more frequent now.
- **Rhetorical** — the crutches. Negative parallelism ("it's not just X, it's Y"), cliché openers ("in today's digital age"), hedging ("it's worth noting that").
- **Syntactic** — the structures. Copula avoidance ("serves as a…" instead of "is a…"), inflated constructions ("plays a crucial role").
- **Statistical** — the rhythm. More on this below, because it's the interesting one.

Every flag carries a concrete fix and the reason behind it. It's a linter, not a verdict.

![The annotated text with every AI tell highlighted, beside the recommendation list explaining and fixing each one](https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/screenshots/evidence.png)

*This is the whole argument in one screenshot: not "87% AI", but which words, why they were flagged, and what to write instead.*

**2. It checks originality.** Drop in two or more documents — a thesis and its sources, or a whole class's submissions — and it highlights the passages they share: verbatim copies, and *reworded paraphrases, even across languages*. The number you see equals exactly what's highlighted. The evidence **is** the score. A human judges; the tool never accuses.

![Cohort overlap matrix showing which documents share text, with the most similar pairs ranked below](https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/screenshots/originality.png)

*A whole class at a glance: every document against every other, then the shared passages themselves.*

## The one signal I trust most: burstiness

Here's the tell that's hardest to fake and easiest to measure. Humans write with wildly uneven rhythm. A long, winding sentence with three clauses and an aside, then a short one. Then a fragment. Machines don't. Left alone, an LLM settles into a steady 15–25 words per sentence and holds it, paragraph after paragraph.

You can quantify that as **burstiness** — the coefficient of variation of sentence length. Human prose usually scores 0.6–0.8. Default LLM output sits at 0.0–0.2. It needs no word list and no model; it's just statistics on sentence lengths. SignsOfAI computes it, shows it as a per-sentence bar chart, and folds it into the score.

By the way — this article was written to pass its own linter. Short sentences next to long ones. No `tapestry`. That's the point.

## Why rules, not a neural net

I didn't train a classifier. That was deliberate.

A rules-and-statistics engine is **explainable by construction**. When it flags `delve`, it can tell you *delve*, show you where, and hand you three replacements. A neural net gives you a probability and a shrug. For a tool whose whole promise is "show your work," transparency beats a couple of points of accuracy.

It also means the whole thing runs in the browser. No server, no upload, no account. Your documents never leave your device. That matters a lot when the documents are student essays or an unpublished manuscript. The engine is a small, pure .NET library (`SignsOfAI.Core`); the site is Blazor WebAssembly on .NET 10.

## Spanish as a first-class citizen

The Spanish rule-pack isn't translated. I derived it from scratch, because the tells are different. English AI loves `delve` and `tapestry`; Spanish AI loves `sumérgete`, `cabe destacar`, `un rico tapiz de`, `se erige como`. The rhetorical patterns rhyme across languages but the words don't. Language is auto-detected, and both packs carry the same weights, severities, and evidence.

This is the part no English-only tool can copy by translating a word list.

## The plot twist: turning a competitor into an advantage

A while after launch, I found a project called *no-ai-slop* — a viral little skill for editing AI writing, thousands of stars. My first reaction was the honest one: *they have thousands, I have three.*

Then I looked closer. It's a single Markdown file of rules. English only. No score, no statistics, no originality check. It went viral because it was frictionless and rode the "agent skills" wave, not because it was doing something my engine couldn't.

So I didn't compete. I **mined its taxonomy** — 20-odd patterns of AI writing — folded them into my rule packs (bilingual, weighted, with evidence), added an em-dash-overuse detector, and shipped my own drop-in skill, `/signs-of-ai`, that does the same fast edit but hands off to the real engine for a measured verdict. Same wave. Better boat.

The lesson: when someone's format is winning, you don't need their format. You need their taxonomy and a stronger foundation underneath.

## Try it, break it, extend it

SignsOfAI is MIT-licensed and built for the education and .NET communities.

- **Live demo:** [peopleworks.github.io/SignsofAI](https://peopleworks.github.io/SignsofAI/) — runs in your browser, nothing leaves your device.
- **Code:** [github.com/peopleworks/SignsofAI](https://github.com/peopleworks/SignsofAI)
- **CLI:** `dotnet tool install --global SignsOfAI.Cli` then `signsofai check draft.md` — gate prose in CI with `--max-score`.
- **MCP server + agent skill:** call the engine from Claude Desktop or any MCP client, or drop `/signs-of-ai` into your editor.
- **Bring your own rules:** paste a banned-word list or a rule-pack JSON; it merges live.

If you teach, write, or grade — or you just want your own prose to stop sounding like a machine — give it a paragraph and see what it says. And if you find a tell it misses, the rule packs are two JSON files. Pull requests welcome.

*Built by Pedro Hernández — PeopleWorks, Microsoft MVP for .NET. Por y para la comunidad educativa.*
