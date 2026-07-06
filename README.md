# ✍︎ Signs of AI Writing

A free, privacy-first tool that flags the tells of AI-generated writing — overused vocabulary,
rhetorical crutches, robotic sentence rhythm — and, for **every** finding, tells you *how to fix it*.

Unlike black-box detectors that only spit out a score, this is a **de-AI-ifying linter**: explainable,
actionable, and educational. Built for students and professionals.

> 🔒 **Analysis runs 100% in your browser.** Your text never leaves your device.

Built with **.NET 10** and **Blazor WebAssembly** by **Pedro Hernández (PeopleWorks)**, Microsoft MVP
for .NET — for the .NET and Microsoft developer community.

Repo: https://github.com/peopleworks/SignsofAI

## What it detects

| Category       | Examples |
|----------------|----------|
| **Lexical**     | *delve, tapestry, multifaceted, nuanced, pivotal, underscore, showcase, testament…* (weighted by post-ChatGPT excess frequency) |
| **Rhetorical**  | Negative parallelisms (*"it's not just X, it's Y"*), cliché openers (*"in today's digital age"*), hedging (*"it's worth noting that"*), false ranges, rule-of-three |
| **Syntactic**   | Copula avoidance (*"serves as a…"*, *"a testament to…"*), inflated constructions (*"plays a crucial role"*) |
| **Statistical** | **Burstiness** — sentence-length uniformity. Machine text hovers at 0.0–0.2; human prose 0.6–0.8 |

Every finding carries a concrete suggestion and, where available, the research evidence behind it.

Both **English** and **Spanish** are supported (auto-detected or selectable). The Spanish rule-pack is
an original derivation of AI-writing markers for Spanish.

## Features

- **Analyze** — paste **or upload a document** (`.txt` / `.md`), get a 0–100 score with a per-category
  breakdown, statistics (burstiness, lexical diversity), highlighted tells, and a recommendation for
  every finding.
- **Humanize (optional, BYOK)** — connect an AI provider and rewrite the flagged text in one click.
  Two providers, chosen in ⚙ AI provider:
  - **Anthropic** (`claude-opus-4-8`) — works directly from the browser out of the box.
  - **Azure OpenAI** — for your own **Azure AI** resource (endpoint + deployment + key). Azure OpenAI
    must allow the site's origin via CORS, or sit behind API Management / a Function, for browser calls.
  Credentials are stored only in your browser and sent **directly** to the provider — never to us
  (there is no backend).
- **Catalog** — a searchable library of every AI-writing sign the analyzer knows, in both languages,
  with explanations and fixes. Ranked with an in-browser BM25 index. A study aid for students.

## Architecture

```
SignsOfAI.sln
├─ src/
│  ├─ SignsOfAI.Core        # Pure C# analysis engine (no UI/server deps)
│  │  ├─ Analyzers/         # LexicalAnalyzer, PatternAnalyzer, BurstinessAnalyzer (IAnalyzer)
│  │  ├─ Rules/Packs/       # rules.en.json, rules.es.json (embedded, community-extensible)
│  │  ├─ Text/              # Tokenizer, sentence splitter, language detector, statistics
│  │  ├─ Scoring/           # Transparent saturating density scorer
│  │  └─ AiWritingAnalyzer  # Public facade: Analyze(text, language)
│  └─ SignsOfAI.Web         # Blazor WebAssembly front end
└─ tests/
   └─ SignsOfAI.Core.Tests  # xUnit
```

The engine is decoupled from the UI, so a CLI / `dotnet tool` or Web API can reuse it later.

## Run it

```bash
dotnet run --project src/SignsOfAI.Web
# then open http://localhost:5019
```

## Test

```bash
dotnet test
```

## Extending the rules

Add entries to `src/SignsOfAI.Core/Rules/Packs/rules.<lang>.json`:

- **Lexical** rules match single word tokens (with all surface forms).
- **Pattern** rules are regexes for multi-word rhetorical/syntactic tells.

Each rule sets a `weight`, `severity`, and a human-friendly `suggestion`.

## Deploy (free static hosting)

The app is a static Blazor WebAssembly bundle, so it hosts anywhere for free. Two ready-made GitHub
Actions workflows are included in `.github/workflows/`:

- **Azure Static Web Apps** (`azure-static-web-apps.yml`) — provision a Static Web App in the Azure
  portal, add its deployment token as the `AZURE_STATIC_WEB_APPS_API_TOKEN` repo secret, push to `main`.
  SPA deep-links (e.g. `/catalog`) are handled by `wwwroot/staticwebapp.config.json`.
- **GitHub Pages** (`deploy-pages.yml`) — enable Pages (Settings → Pages → Source: "GitHub Actions").
  The workflow rewrites the base href to `/<repo>/` and writes a `404.html` SPA fallback automatically.

Manual publish:

```bash
dotnet publish src/SignsOfAI.Web -c Release -o publish
# serve publish/wwwroot with any static file server
```

## Roadmap ideas

- CLI / `dotnet tool` for CI pipelines (the Core engine is already UI-agnostic)
- Phase 2: contrastive-perplexity (Binoculars-style) scoring as an optional signal
- Shareable report export

## Credits

Created by **Pedro Hernández — PeopleWorks**, Microsoft MVP for .NET.

Detection markers are grounded in linguistics research on AI stylometry — see `Docs/GoogleResearch.md`
and the Wikipedia "Signs of AI writing" guidance.
