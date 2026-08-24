# signs-of-ai — the bilingual de-slop skill

A drop-in **Claude Code / Codex / agent skill** that detects and removes the tells of AI-generated
writing — in **English and Spanish**. Paste a draft, get it back sounding human, with a summary of what
changed. Ask "is this AI slop?" and get the tells it carries, quoted, with what that does and does not
support — never a claim about who wrote it.

It is the fast, human-judgment front end of **[SignsOfAI](https://github.com/peopleworks/SignsofAI)** — a
real, explainable, privacy-first writing-integrity engine. The skill edits; the engine *measures*.

## Install

The skill itself is [`SKILL.md`](../SKILL.md) in the repository root, which is where every installer
looks for it.

```bash
# one command, and it offers Claude Code, Codex, Gemini CLI, Cursor and the rest
npx skills add peopleworks/SignsofAI -g
```

As a Claude Code plugin, from the marketplace manifest in this repository:

```
/plugin marketplace add peopleworks/SignsofAI
/plugin install signs-of-ai
```

Or copy the one file yourself:

```bash
mkdir -p ~/.claude/skills/signs-of-ai && cp SKILL.md ~/.claude/skills/signs-of-ai/
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
- **MCP server** — `dnx SignsOfAI.Mcp --yes` connects `signs-of-ai` as ten tools, so an agent calls
  the real engine directly: `analyze_ai_writing`, `check_originality`, `inspect_characters`,
  `check_citations`, `compare_to_baseline`, `search_catalog`, `extract_distinctive_phrases`,
  `write_report`, and — the two that send text to a server, and say so — `measure_predictability`
  and `check_paraphrase`.

See the [main README](https://github.com/peopleworks/SignsofAI) for the web app, CLI, and MCP setup.

## Credits
By **Pedro Hernández — PeopleWorks**, [Microsoft MVP for .NET](https://mvp.microsoft.com/en-US/mvp/profile/24060a02-dbc6-44ec-bca5-c213ff9835c5). MIT licensed. Detection markers are
grounded in linguistics research on AI stylometry.
