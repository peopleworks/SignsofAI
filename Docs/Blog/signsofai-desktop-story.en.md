---
title: "A stranger asked for a desktop app, and I thought I'd built the wrong thing"
description: "Porting a Blazor WebAssembly app to the desktop took a day, not a rewrite. Exactly one thing genuinely broke. Here's that thing, the decision months earlier that saved me, and the two mistakes I made along the way."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/social-preview.png"
tags: [dotnet, blazor, webassembly, desktop]
author: "Pedro Hernández (PeopleWorks)"
lang: en
---

# A stranger asked for a desktop app, and I thought I'd built the wrong thing

Someone on Reddit was hunting for a cracked copy of a paid "AI humanizer". I replied with a link to mine, which is free and open source. He asked how to install it. I said it runs in the browser, and asked whether he wanted a desktop version.

"Yes app."

My first reaction was not excitement. It was a sinking feeling that I had picked the wrong architecture months ago. SignsOfAI is Blazor WebAssembly. It runs in a tab. Turning that into something you download and double-click sounded like starting over.

I was wrong, and the reason I was wrong is the only part of this worth writing down.

## The engine was never the web app

Before touching anything, I looked at the project file for the analysis engine. It has no package references. Not one.

That was not luck. It was a rule I set early and then forgot I had set: the engine stays pure .NET, no browser, no I/O, no dependencies. Rule packs are embedded resources. The tokenizer, the sentence splitter, the scorer, the rewriter, all plain C# operating on strings.

By the time the Reddit question arrived, that engine was already feeding three different front ends: the web app, a command-line tool, and an MCP server. A fourth was not an architectural change. It was another consumer.

So the port was never going to be a rewrite. It was an extraction: pull the pages, components and UI services out of the WebAssembly project into a Razor class library, and let a second host render them.

If you take one thing from this, take that. The decision that saved me was made months before the problem existed, and I got no credit for it at the time because nothing was visibly better that day.

## Exactly one thing broke

Blazor Hybrid hosts your components in a WebView. Most of what a Blazor app does carries over untouched. JavaScript interop works. Local storage works. Every HTTP call to an outside API works, and better, because there is no CORS preflight in the way.

One thing did not.

A .NET `HttpClient` cannot reach the WebView's virtual host. In the browser, fetching your own `wwwroot` through an `HttpClient` bound to the app's base address is ordinary. In a WebView, the app is served from a virtual origin that only the page itself can talk to. Native HTTP goes out to the network and finds nothing.

In my case that was a single line: the loader that reads the interface translations from JSON files.

My first instinct was an interface with two implementations, one per host. Then I noticed the browser's own `fetch` works in both places. So the fix deleted code instead of adding it: the loader now goes through a small JS helper, one code path, both hosts. The `HttpClient` bound to the base address disappeared entirely, because nothing else was using it.

The second surprise cost me more time. The desktop app compiled cleanly, launched, and died at the first render:

```
System.IO.FileNotFoundException: Could not load file or assembly
'Microsoft.Windows.SDK.NET, Version=10.0.17763.10'
```

`BlazorWebView` hosts the page in WebView2's composition control, which reaches for the Windows SDK WinRT projections. A target framework of `net10.0-windows` does not bring those in. You need a platform version: `net10.0-windows10.0.19041.0`. It builds either way, which is what makes it annoying to diagnose.

## Three agents, one repository, no merge conflicts

I had two other coding agents idle in other terminals, so I split the work three ways: the desktop port, extracting the ONNX perplexity engine into a reusable library, and a document reader for PDF, ODT, EPUB and RTF.

Two things made that work, and neither is clever.

**One git worktree per agent.** Branches share a single working tree. Three agents in one folder means the second one's `git checkout` rewrites the first one's files mid-edit, and it looks like your own mistake. Separate worktrees also make git refuse to check out a branch another worktree already holds, so the protection stops depending on everyone behaving.

**Split by file, not by topic.** Each brief named the paths that agent owned and forbade the rest by name. One shared file, the solution, was reserved and off limits to all of them. All three branches merged with no conflicts.

The briefs carried the reason behind each constraint, not only the rule. This library may not add a dependency to the engine, because the engine ships to WebAssembly and to NuGet. Use PdfPig, not iText, because iText is AGPL and this repository is MIT. The model weights are git-ignored, so tests that need them must skip rather than fail.

## What went wrong

Both agents reported success. Both had green tests. One of them was lying to me, without meaning to.

Buried in 72 passing tests was this:

```csharp
Assert.True(string.IsNullOrEmpty(result.Text) || result.Warnings.Count > 0 || true);
```

That assertion cannot fail. It counted toward a green suite and protected nothing. I checked whether it was covering a real defect, and it was not: the code handled the case correctly, the test never said so. A permanently passing test is worse than a missing one, because the missing one is honest about the gap.

It survived for a specific reason. Neither new project was in the solution yet, so no integrated build ever ran those tests. Adding them was the actual fix.

My own mistake was worse. Cleaning up, I ran `git add -A` from the repository root, which swept in a scratch folder of working notes and screenshots. It reached the public default branch. I noticed, removed it, force-pushed, and then checked whether that had deleted anything:

```
$ gh api repos/.../commits/90ab44a
90ab44a82f40441dd1222bebc04ebb1bea955e0c
```

Still there. A force-push orphans a commit; it does not delete it. On a public repository, anyone with the hash can still read it, and only GitHub Support can purge it. The content was harmless in my case. The lesson was not: `.gitignore` is the fix, and "I'll be careful" is not.

## The part that made it worth it

A desktop build that only does what a browser tab already does is a bigger download for no reason. What justifies it is the things a tab structurally cannot do.

It reads PDF, ODT, EPUB and RTF. Not because a browser could not parse a PDF, but because shipping a PDF parser to every visitor costs them megabytes before they analyse a single word. Here it is already on disk.

It reaches Ollama on `localhost:11434`. From a page served over HTTPS that call is refused unless the user reconfigures Ollama. From here it is an ordinary HTTP request.

It scans a whole folder. A browser tab is handed files; it is never given a folder path.

And it measures predictability with a language model running inside the application, instead of calling a service. That last one had a number attached, and the number is the reason I trust the port. Same sentence, same model:

| | Hosted endpoint | In-process |
| --- | --- | --- |
| Perplexity | 27.33 | 27.3 |
| Tokens | 17 | 17 |
| Predictability | 0.859 | 0.86 |
| Time | 411 ms | 122 ms |

The same reading, three times faster, with the text never leaving the machine and no server needing to be up.

That parity is not a coincidence. The engine was *moved* out of the API into a shared library, not reimplemented for the desktop. Had I rewritten it, the two would have drifted, and a tool whose whole claim is honesty would be quietly reporting different numbers for the same paragraph depending on where you opened it.

## What I would tell myself in the morning

The port took a day. I spent the first ten minutes of it apologising to myself for an architecture decision that turned out to be the reason the day was short.

Keep the thing that does the work free of the thing that shows the work. It costs a little discipline early, it looks like over-engineering while nothing needs it, and then one afternoon a stranger asks for something you never planned and the answer is a project reference.

The app is free, MIT, and both builds are here: [github.com/peopleworks/SignsofAI](https://github.com/peopleworks/SignsofAI).

---

*Written by a human and checked with the tool it describes: **5/100, reads mostly human**, burstiness 0.70, about 1,580 words. Its first pass caught three empty intensifiers of mine and I took the advice, which is what the tool is for.*

*Two flags survive, and both are wrong: it reads "PDF, ODT, EPUB and RTF" as a rule of three, and that list has four items. I left the sentence alone rather than reword my way to a cleaner number. A detector whose author quietly edits around its false positives is not one you should trust.*

*Editing this footnote kept changing the score it reported, which is its own small lesson about measuring the thing you are standing on.*
