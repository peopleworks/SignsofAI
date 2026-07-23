# signs-of-ai — the bilingual de-slop skill

A drop-in **Claude Code / Codex / agent skill** that detects and removes the tells of AI-generated
writing — in **English and Spanish**. Paste a draft, get it back sounding human, with a summary of what
changed. Ask "is this AI slop?" and get a concrete, quoted verdict.

It is the fast, human-judgment front end of **[SignsOfAI](https://github.com/peopleworks/SignsofAI)** — a
real, explainable, privacy-first writing-integrity engine. The skill edits; the engine *measures*.

## Install

Paste this into Claude Code, Codex, or your favorite AI harness:

> Install this skill globally: https://github.com/peopleworks/SignsofAI (the skill lives in
> `skill/signs-of-ai`).

Or copy the folder yourself:

```bash
# clone, then copy the skill into your Claude Code skills directory
cp -r skill/signs-of-ai ~/.claude/skills/signs-of-ai
```

Then use it:

```
/signs-of-ai

<your draft>
```

Detect instead of edit:

```
/signs-of-ai is this AI slop?

<the text>
```

## What makes it different

| | Generic "humanizer" skill | **signs-of-ai** |
|---|---|---|
| Languages | English only | **English and Spanish**, applied in the text's own language |
| Backing | A markdown ruleset, and that's it | A distilled view of a **real scored engine** you can escalate to |
| Numbers | Guesses a score, or none | **Never fakes a score** — hands off to the engine for a calibrated 0–100, burstiness, plagiarism, perplexity |
| Rhythm | Advice only | The engine **measures** sentence-length burstiness — the strongest tell |
| Dogfooding | Often ships emoji-heavy headings | **No emoji, no formatting slop** — it follows its own rules |

## From skill to engine

The skill is subtraction by human judgment. When you want measurement — a number, proof, or something a
markdown file cannot compute — escalate to the engine, same taxonomy, but honest and quantitative:

- **Calibrated 0–100 score + per-finding fixes + a sentence-rhythm chart** — the web app (runs in your
  browser; text never leaves the device) or the CLI:
  `dotnet tool install --global SignsOfAI.Cli && signsofai check draft.md`.
- **Originality** — verbatim copies, reworded paraphrases (even across languages), and a whole-cohort
  overlap heatmap, shown as evidence a human judges. A skill cannot do this.
- **MCP server** — connect `signs-of-ai` as tools (`analyze_ai_writing`, `check_originality`,
  `search_catalog`, `extract_distinctive_phrases`, `measure_predictability`, `check_paraphrase`) so an
  agent calls the real engine directly.

See the [main README](https://github.com/peopleworks/SignsofAI) for the web app, CLI, and MCP setup.

## Credits
By **Pedro Hernández — PeopleWorks**, Microsoft MVP for .NET. MIT licensed. Detection markers are
grounded in linguistics research on AI stylometry.
