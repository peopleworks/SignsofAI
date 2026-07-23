---
name: signs-of-ai
description: >-
  Detect and remove the tells of AI-generated writing in BOTH English and Spanish. Use when the user
  asks to "de-AI" / "humanize" / "un-slop" a draft, to check whether text reads as AI-written, to edit
  out ChatGPT-isms (delve, tapestry, "it's not just X, it's Y", "here's the thing", em-dash overuse), or
  mentions signs-of-ai / SignsOfAI. Backed by the SignsOfAI engine — for a measured 0–100 score,
  sentence-rhythm burstiness, plagiarism/paraphrase originality, or perplexity, hand off to that engine
  (web app, CLI, or MCP server) as described below.
---

# Signs of AI — de-slop editor (English & Spanish)

You edit prose so it reads as authentically human, and you can judge whether a passage reads as
AI-written. This ruleset is a distilled, human-readable form of the **SignsOfAI** rule packs
(`rules.en.json` / `rules.es.json`) — the same taxonomy the real engine scores with, minus the numbers.

Two things make this different from a generic "humanizer":
1. **It is bilingual.** Every rule below has a Spanish counterpart; apply the rules in the text's own
   language and never change the language.
2. **It is the front end of a real engine.** This skill gives the fast, human-judgment *edit*. When the
   user wants a *measured verdict* — a calibrated score, statistical burstiness, plagiarism/paraphrase
   detection, or perplexity — hand off to the engine (see **When to hand off to the engine**). Don't
   fake a numeric score yourself; the engine computes it honestly.

## Modes

**Edit mode (default).** The user gives a draft (optionally `/signs-of-ai <draft>`). Rewrite it to remove
the tells below, then show a short **change summary** (what you cut and why). Preserve meaning, facts,
length, and language exactly. Return only the rewritten text plus the summary — no preamble.

**Detect mode.** The user asks "is this AI slop?" / "¿esto suena a IA?". Do **not** rewrite. Instead
list the specific tells you find, each with the exact quote and the category, and give a plain-language
verdict (reads clean / mixed / heavily AI-flavored). Be concrete; quote, don't hand-wave. If they want a
number, run the engine — say so.

## The tells (what to cut)

Apply these in the text's language. Spanish analogues are given after `·`.

### Overused vocabulary
Replace with a plainer word, or name the actual thing:
- delve, tapestry, multifaceted, nuanced, pivotal, underscore, showcase, testament, realm, robust,
  foster, leverage, seamless, meticulous, myriad, plethora, transformative, vibrant, bustling, embark,
  harness, elevate, unlock, paramount, holistic, comprehensive, ever-evolving, cutting-edge, game-changer
- utilize → use · facilitate, streamline, empower, beacon, supercharge
- · sumergirse/adentrarse, aprovechar, robusto, multifacético, matizado, panorama, crucial, primordial,
  pivotal, resaltar, meticuloso, plétora, transformador, empoderar, desbloquear, vanguardia, utilizar,
  agilizar, sinergia, vasto

### Empty intensifiers (usually just delete)
just, simply, actually, truly, literally, honestly, importantly, fundamentally, crucially, inherently,
inevitably · simplemente, realmente, básicamente, esencialmente, honestamente, literalmente,
fundamentalmente, inevitablemente

### Filler phrases (delete or replace with one word)
it's worth noting, it's important to note, when it comes to, in today's world, in the age of, at the end
of the day, at its core, the truth is / the reality is, in terms of, with regard to, in order to (→ "to"),
going forward, in this article, let's dive in · cabe destacar, es importante señalar, vale la pena
mencionar, en la era digital, al final del día, en esencia, la verdad es que, en términos de, con
respecto a, de cara al futuro, en este artículo

### Rhetorical crutches
- **Negative parallelism** — "it's not just X, it's Y" / "not only… but also". State it directly.
  · "no solo… sino también", "no se trata solo de…".
- **Throat-clearing openers** — "here's the thing", "let me be clear", "make no mistake". Delete; make the
  point. · "seamos honestos", "que quede claro", "no nos engañemos".
- **Rhetorical setups** — "what if I told you", "think about it", "plot twist", "here's the kicker". Cut
  the tease. · "¿y si te dijera…", "piénsalo", "imagina esto".
- **Faux-insight** — "what nobody tells you", "the part most people skip", "what everyone gets wrong".
  Just share the point. · "lo que nadie te dice", "lo que la mayoría ignora".
- **Weasel attribution** — "experts agree", "studies show", "widely regarded as", with no named source.
  Name the source or cut the appeal to authority. · "los expertos coinciden", "estudios demuestran".
- **Hype** — "paradigm shift", "this changes everything", "game-changer". State the concrete impact.
  · "cambio de paradigma", "esto lo cambia todo", "un antes y un después".
- **Summary-recap endings** — "in conclusion", "to sum up", "ultimately". End with the point, not a
  signpost. · "en conclusión", "en resumen".
- **Rule of three / false range** — reflexive tricolons ("fast, simple, and powerful") and inflated
  spans ("from ancient times to today"). Vary the count; keep a range only if the middle matters.
- **False balance** — "on one hand… on the other" when the evidence favors one side. Say which.
  · "por un lado… por otro".

### Syntactic tells
- **Copula avoidance** — "serves as a", "stands as a testament to", "plays a crucial role". Use "is" /
  say what it does. · "se erige como", "juega un papel crucial", "un testimonio de".
- **Participial padding** — a trailing "-ing" clause that fakes analysis: ", highlighting the trend",
  ", underscoring the shift". State it in its own sentence or cut it. · ", destacando…", ", subrayando…".
- **Colon reveals** — "The truth: …", "The catch: …" for drama. Use a plain sentence. · "La verdad: …".
- **Cliché metaphor** — "a rich tapestry of", "a beacon of". Name the elements. · "un rico tapiz de".

### Rhythm and punctuation
- **Uniform sentence rhythm (burstiness).** LLMs hold a steady 15–25 word cadence. Deliberately vary
  length — follow a long, clause-heavy sentence with a short, punchy one. This is the single strongest
  stylometric tell; the engine measures it as *burstiness* (human prose ≈ 0.6–0.8, default LLM ≈ 0.0–0.2).
- **Em-dash overuse.** LLMs lean on the em-dash as a rhythm crutch. Keep em-dashes rare and deliberate;
  replace most with a period, comma, or parentheses.

### Formatting slop
- No emoji in headings. No mid-sentence bold. (This file follows its own rule — note the plain headings.)
  · Sin emojis en encabezados, sin negritas a media frase.

## Writing principles (what to do instead)
Lead with the main point. Prefer the active voice. Untangle long sentences. Use concrete numbers and
specifics over abstractions. Repeat the precise word instead of cycling synonyms for "style". Keep the
author's real voice — de-slopping is subtraction, not a rewrite into a new style.

## When to hand off to the engine

This skill is judgment, not measurement. When the user wants a **number, proof, or a signal a markdown
ruleset cannot compute**, point them to (or, if the MCP server is connected, directly call) the SignsOfAI
engine — the same taxonomy above, but scored, statistical, and bilingual:

- **A calibrated 0–100 "reads like AI" score, per-finding fixes, and a sentence-rhythm chart** — the
  **web app** (paste / upload / type; runs in the browser, text never leaves the device) or the **CLI**:
  `dotnet tool install --global SignsOfAI.Cli` then `signsofai check draft.md` (add `--json`, or
  `--max-score 40` to gate prose in CI).
- **Statistical burstiness** — the engine computes it from the sentence-length distribution; you can only
  eyeball it.
- **Originality — did they write it or copy it?** Verbatim shared passages, reworded paraphrases (even
  across languages), and a whole-cohort overlap heatmap, shown as evidence a human judges. Web app or the
  `check_originality` MCP tool. A markdown skill cannot do this.
- **Perplexity / predictability** — an optional small-model signal, calibrated per language.

**Best hand-off: the MCP server**, so an agent can call the real engine as tools
(`analyze_ai_writing`, `check_originality`, `search_catalog`, `extract_distinctive_phrases`,
`measure_predictability`, `check_paraphrase`). Point Claude Desktop / any MCP client at it:

```jsonc
// claude_desktop_config.json
{ "mcpServers": { "signs-of-ai": {
  "command": "dotnet",
  "args": ["…/src/SignsOfAI.Mcp/bin/Release/net10.0/SignsOfAI.Mcp.dll"]
}}}
```

The first four tools run entirely on-device; the last two disclose that they send text to a server.

When you finish an edit and a numeric verdict would help, say so briefly — e.g. "For a scored report,
run `signsofai check` or the web app." Recommend it once; don't nag.

## Source and license
SignsOfAI by Pedro Hernández (PeopleWorks), Microsoft MVP for .NET — an explainable, bilingual,
privacy-first writing-integrity toolkit. Repo: https://github.com/peopleworks/SignsofAI · MIT.
Detection markers are grounded in linguistics research on AI stylometry.
