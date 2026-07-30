# Claude for Open Source — Application draft

**Project:** Signs of AI Writing · **Repo:** peopleworks/SignsofAI · **License:** MIT
**Applicant:** Pedro Hernández (PeopleWorks) — Microsoft MVP for .NET

> Written to survive its own linter: plain words, varied sentence length, no rhetorical tics.
> Paste each block into the matching form field.

---

## Field 1 — "Tell us about the project's reach and impact…"

Signs of AI Writing is a free toolkit for writing integrity, built with .NET 10 and Blazor
WebAssembly and released under MIT. It is early. But what exists already works from start to finish.

The linter reads a piece of writing and points out the habits that give away AI text: overused
words and canned phrasing, plus sentences that all run to the same length. For every thing it flags,
it explains the fix and the reason behind it. The originality checker takes two or more documents and
shows what they share — exact copies, reworded passages, even matches that cross from Spanish to
English. A person reads that evidence and decides. The tool never hands down a verdict.

Almost all of it runs inside the browser, so a student's writing stays on their own machine.

Where does it fit? Most detectors sell a suspicion score. This one is built to help someone learn
instead. It works in two languages, and the Spanish marker set is my own research — few tools serve
that audience at all. Claude is wired in as a bring-your-own-key rewrite provider, and the whole
engine is exposed through an MCP server, so any Claude client can call its tools directly.

---

## Field 2 — "How will you use the subscription for your project?"

To move a working prototype toward something a university can actually adopt.

Day to day, the subscription pays for the Claude-facing work. I want to sharpen the bring-your-own-key
rewrite flow so its feedback teaches the writer rather than just scolding them. I want to harden the
MCP server and widen the cross-language paraphrase checks. And I am preparing talks for universities
on keeping a human in the loop — using AI to learn a subject, not to copy an answer and move on. That
material needs a lot of iteration against real writing, and cost is the thing that slows me down today.

The goal is small and stubborn: a calm, honest tool that reminds people why understanding a thing is
worth more than the applause for finishing it.

---

## Field 3 — "Other info" (optional)

I am a Microsoft MVP for .NET, and I build this in the open for the education community. Everything is
MIT, so a teacher or another maintainer can fork it, read every rule, and disagree with me in public.
That openness is the point — an integrity tool has no business being a black box.

The project is honest about its own limits. It surfaces evidence for a person to judge; it does not
accuse anyone. I would rather ship that carefully than chase a scary-sounding accuracy number.

If the hard eligibility bars are what matter most, I do not clear them yet. I am applying through the
door you left open for people building something the ecosystem may come to rely on. This is that.
