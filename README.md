# ✍︎ Signs of AI Writing

A free, privacy-first toolkit for **academic and writing integrity**. It does two things:

1. **De-AI-ify linter** — flags the tells of AI-generated writing (overused vocabulary, rhetorical
   crutches, robotic sentence rhythm) and, for **every** finding, tells you *how to fix it*.
2. **Originality checker** — *"did they write it, or copy it?"* Compares documents against each other
   and surfaces the passages they share — verbatim copies, **reworded paraphrases** (even across
   languages), and a whole-cohort overview — as **evidence a human judges**. Not a black-box verdict.

> 🔒 **Almost everything runs 100% in your browser. Your documents never leave your device.**
> The only exception is the optional paraphrase check, which is strictly opt-in and clearly disclosed.

Built with **.NET 10** and **Blazor WebAssembly** by **Pedro Hernández (PeopleWorks)**, Microsoft MVP
for .NET — for the .NET and Microsoft developer community, *por y para la comunidad educativa*.

Repo: https://github.com/peopleworks/SignsofAI

Both **English** and **Spanish** are supported throughout (auto-detected or selectable). The Spanish
rule-pack is an original derivation of AI-writing markers for Spanish.

---

## 1. The AI-writing linter ("Analyze")

Unlike black-box detectors that only spit out a score, this is an **explainable, actionable, educational**
linter. Paste, upload (`.docx` / `.txt` / `.md`), or **just start typing** — the 0–100 score, highlights,
statistics, and per-finding fixes update **as you write**.

| Category       | Examples |
|----------------|----------|
| **Lexical**     | *delve, tapestry, multifaceted, nuanced, pivotal, underscore, showcase, testament…* (weighted by post-ChatGPT excess frequency) |
| **Rhetorical**  | Negative parallelisms (*"it's not just X, it's Y"*), cliché openers (*"in today's digital age"*), hedging (*"it's worth noting that"*), false ranges, rule-of-three |
| **Syntactic**   | Copula avoidance (*"serves as a…"*, *"a testament to…"*), inflated constructions (*"plays a crucial role"*) |
| **Statistical** | **Burstiness** — sentence-length uniformity. Machine text hovers at 0.0–0.2; human prose 0.6–0.8 |

- **Sentence-rhythm visualization** — a per-sentence bar chart that makes *burstiness* visible.
- **Per-finding recommendations** — every flagged tell carries a concrete fix and the research behind it.
- **Humanize (optional, BYOK)** — connect an AI provider and rewrite the flagged text in one click.
  Anthropic (`claude-opus-4-8`, works from the browser), OpenAI / DeepSeek, Azure OpenAI, or **Ollama**
  (local, no key). Credentials live only in your browser and are sent **directly** to the provider.
- **Before/after diff** and a **shareable result card** (a PNG summary that never includes your text).
- **Custom catalogs (BYO rules)** — paste banned words or import a rule-pack JSON; merges live.
- **Catalog page** — a searchable library of every AI-writing sign, in both languages, ranked with an
  in-browser BM25 index.

## 2. The Originality checker ("Originality")

*"¿Lo escribió la IA, lo copiaste, o lo parafraseaste para esconderlo?"* Drop in two or more documents —
a thesis and its sources, a batch of student submissions — and see exactly what they share. The guiding
principle is honest: **we surface the evidence and highlight it; a human judges. We never accuse.** This is
**not** a whole-internet index like Turnitin.

| Phase | What it catches | How | Where it runs |
|-------|-----------------|-----|---------------|
| **A — Literal copy** | verbatim shared passages, resistant to changed capitalization/accents | accent/case-folded word *k*-shingles + greedy longest-match tiling, verified token-by-token | 🔒 **in your browser** |
| **B — Paraphrase** | *reworded* copies — same idea, different words — **even across languages** | sentence embeddings (Google **EmbeddingGemma-300M**, ONNX) + cosine similarity | 🌐 optional server (**opt-in**) |
| **C — Cohort** | who copied whom across a whole class, at a glance | batch upload + an N×N **overlap heatmap**; click a cell to inspect the pair | 🔒 **in your browser** |
| **D — Web spot-check** | whether a passage already exists online | extracts a document's most **distinctive passages** and hands you one-click exact-phrase searches (Google/Bing/DuckDuckGo) | 🔒 **in your browser** |

- **Shared-passage evidence** — matches are highlighted in both documents, side by side; the headline
  overlap number equals exactly what you see highlighted (the evidence *is* the score).
- **Phase B is the one feature that leaves the device.** It's opt-in, disclosed in the UI, and sends only
  the sentences you choose to check to the PeopleWorks server. Everything else stays on your machine.
- **Phase D** is deliberately honest: we can't index the whole web, so instead of pretending to, we surface
  the passages worth checking and prepare the searches — nothing is sent anywhere until *you* click one.
  An **optional automatic web search** can be enabled by the server operator (see *Optional server* below).

## 3. The predictability meter (optional server)

An honest reframing of perplexity. A small language model (Qwen2.5-0.5B or Microsoft Phi-4-mini, int8 ONNX)
measures how *predictable / generic* a text's phrasing is. **This is not an AI-vs-human verdict** — on a
labelled corpus the two overlap badly (memorized human text scores *predictable* too). We surface
predictability honestly as one signal among many, calibrated per language. Opt-in; runs on the PeopleWorks
server. The model lazily loads and idle-unloads to keep the server light.

## 4. Use it from other apps — MCP server

Everything above is also available to **Claude Desktop and any [MCP](https://modelcontextprotocol.io)
client** through `SignsOfAI.Mcp`, a Model Context Protocol server (built on the official
[`ModelContextProtocol`](https://www.nuget.org/packages/ModelContextProtocol) SDK, stdio transport). Because
the engine lives in `SignsOfAI.Core` — pure .NET, no browser — the server just exposes it as tools:

| Tool | What it does | Where it runs |
|------|--------------|---------------|
| `analyze_ai_writing` | score + verdict + findings (with fixes) + statistics | 🔒 on-device |
| `check_originality` | overlap % and shared passages across 2+ documents | 🔒 on-device |
| `search_catalog` | search the catalog of AI-writing signs (EN/ES) | 🔒 on-device |
| `extract_distinctive_phrases` | distinctive phrases + ready-made web-search links | 🔒 on-device |
| `measure_predictability` | perplexity via the optional server | 🌐 server (**opt-in**) |
| `check_paraphrase` | reworded/translated matches via EmbeddingGemma | 🌐 server (**opt-in**) |

The first four run entirely on the machine; the last two disclose that they send text to the server
(endpoint via the `SIGNSOFAI_API_ENDPOINT` environment variable). Point Claude Desktop at it:

```jsonc
// %APPDATA%\Claude\claude_desktop_config.json
{ "mcpServers": { "signs-of-ai": {
  "command": "dotnet",
  "args": ["…/src/SignsOfAI.Mcp/bin/Release/net10.0/SignsOfAI.Mcp.dll"]
}}}
```

It also packs as a global tool (`signsofai-mcp`). See `src/SignsOfAI.Mcp/README.md` for details.

---

## Architecture

```
SignsOfAI.slnx
├─ src/
│  ├─ SignsOfAI.Core            # Pure C# engines (no UI/server deps)
│  │  ├─ Analyzers/             # Lexical, Pattern, Burstiness (IAnalyzer)
│  │  ├─ Originality/           # OriginalityChecker (shingles+tiling), ParaphraseFinder,
│  │  │                         #   DistinctivePhraseExtractor
│  │  ├─ Rules/Packs/           # rules.en.json, rules.es.json (embedded, community-extensible)
│  │  ├─ Text/                  # Tokenizer, sentence splitter, language detector, statistics
│  │  └─ AiWritingAnalyzer      # Public facade: Analyze(text, language)
│  ├─ SignsOfAI.Web             # Blazor WebAssembly front end (Analyze, Originality, Catalog)
│  ├─ SignsOfAI.Cli             # `dotnet tool` for CI pipelines
│  ├─ SignsOfAI.Mcp             # MCP server (stdio): the engine as tools for Claude Desktop / any client
│  └─ SignsOfAI.Perplexity.Api  # Optional ASP.NET Core server: predictability + embeddings
│     ├─ Engine/                #   OnnxPerplexityEngine, OnnxEmbeddingEngine (lazy-load + idle-unload)
│     └─ Config/                #   model profiles, calibration, embedding + web-search options
└─ tests/
   └─ SignsOfAI.Core.Tests      # xUnit (40+)
```

The Core engines are decoupled from the UI and server — the CLI, the Blazor app, and the API all reuse them.

## Run it

```bash
dotnet run --project src/SignsOfAI.Web
# then open http://localhost:5019
```

## Test

```bash
dotnet test
```

## Command line & CI (`dotnet tool`)

The linter ships as a global tool so you can gate prose in CI:

```bash
dotnet tool install --global SignsOfAI.Cli

signsofai check README.md                 # pretty report
signsofai check article.docx --lang en    # Word documents too
signsofai check post.md --json            # machine-readable
signsofai check post.md --max-score 40    # exit 1 if it reads too much like AI → fails CI
signsofai check post.md --rules my-style.json   # your custom catalog
```

The analysis engine is also a library — `dotnet add package SignsOfAI.Core`:

```csharp
var result = new SignsOfAI.Core.AiWritingAnalyzer().Analyze(text, "auto");
Console.WriteLine($"{result.OverallScore}/100 — {result.Verdict}");
```

## Optional server (`SignsOfAI.Perplexity.Api`)

The client works fully on its own; this server only powers the **opt-in** features (the predictability meter
and the Phase B paraphrase check). It's ASP.NET Core (.NET 10) hosting ONNX models with lazy-load and
idle-unload so it stays light. Model files are **not** in git — they download on first use.

The client points at a hosted instance by default; to run your own, set the endpoint in the app's server
settings and configure CORS for your origin.

### Enabling the optional automatic web search (Phase D)

By default Phase D is the on-device, one-click-search experience (no key, nothing sent until you click).
An operator can additionally enable an **automatic** web search — useful for presentations — by configuring
a search provider **on the server** (the key never touches the browser). It stays **off unless configured**:

```jsonc
// appsettings.json (or environment variables)
"WebSearch": {
  "Enabled": true,
  "Provider": "brave",              // Brave Search API (free tier); provider-abstracted
  "ApiKey": "",                     // prefer the BRAVE_API_KEY environment variable
  "MaxPhrasesPerDoc": 8,
  "MaxResultsPerPhrase": 5
}
```

When enabled, the server advertises the capability and the client offers an automatic "search the web"
action that reports pages containing a passage **verbatim**. If it's off, quota-exhausted, or errors, the UI
falls back to the manual one-click searches — it never breaks.

## Extending the rules

Add entries to `src/SignsOfAI.Core/Rules/Packs/rules.<lang>.json` — **lexical** rules match single word
tokens, **pattern** rules are regexes for multi-word tells. Each sets a `weight`, `severity`, and `suggestion`.

## Deploy

The Blazor client is a static bundle (hosts anywhere free). Included GitHub Actions:

- **GitHub Pages** (`deploy-pages.yml`) — Settings → Pages → Source: "GitHub Actions". The workflow rewrites
  the base href and writes an SPA `404.html` fallback.
- **Azure Static Web Apps** (`azure-static-web-apps.yml`) — add the deployment token as a repo secret.

The optional server is a normal ASP.NET Core app (`dotnet publish` the `SignsOfAI.Perplexity.Api` project).

## Credits

Created by **Pedro Hernández — PeopleWorks**, Microsoft MVP for .NET. Detection markers are grounded in
linguistics research on AI stylometry — see `Docs/GoogleResearch.md`.
