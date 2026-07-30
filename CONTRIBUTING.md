# Contributing to Signs of AI Writing

Thanks for being here. This project has one principle, and everything below follows from it:

> **We surface evidence a human can judge. We never hand down a verdict.**

If a change makes the tool more confident but less explainable, it's probably the wrong change.

The most useful contributions are usually **rules** — a tell we miss, or a false positive that punishes
honest writing. You do not need to know C# to report either one; the
[issue templates](https://github.com/peopleworks/SignsofAI/issues/new/choose) walk you through it.

## Getting set up

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). Nothing else.

```bash
git clone https://github.com/peopleworks/SignsofAI.git
cd SignsofAI
dotnet test                              # the whole suite
dotnet run --project src/SignsOfAI.Web   # the Blazor app, at https://localhost:5001
dotnet run --project src/SignsOfAI.Cli -- check some-file.md
```

## The layout

| Project | What it is |
| --- | --- |
| `src/SignsOfAI.Core` | The engine. Pure .NET, no browser, no I/O. Everything else is a shell around it. |
| `src/SignsOfAI.UI` | The interface itself — pages, components, UI services. Both hosts below render it, so a fix reaches web and desktop together. |
| `src/SignsOfAI.Web` | Host: the Blazor WebAssembly app in the browser. |
| `src/SignsOfAI.Desktop` | Host: the WPF + WebView2 app. Windows-only, so it lives in `SignsOfAI.Desktop.slnx` rather than the main solution, which is built on Linux. |
| `src/SignsOfAI.Cli` | `signsofai` — terminal reports and CI gating. |
| `src/SignsOfAI.Mcp` | MCP server over stdio. |
| `src/SignsOfAI.Perplexity.Api` | The optional server for perplexity and paraphrase. |
| `tests/SignsOfAI.Core.Tests` | xUnit. Every rule change lands with a test here. |

## Adding or changing a rule

Rules live in two JSON files: `src/SignsOfAI.Core/Rules/Packs/rules.en.json` and `rules.es.json`.
There are two kinds.

**Lexical** — single overused words. All inflections go in `terms`:

```json
{
  "id": "lex.delve",
  "terms": ["delve", "delves", "delving", "delved"],
  "weight": 6,
  "severity": "High",
  "suggestion": "examine, explore, look into, dig into",
  "evidence": "48× more frequent post-ChatGPT (excess ratio r=28)"
}
```

**Pattern** — .NET regex over the text, for rhetorical and syntactic tells:

```json
{
  "id": "rhet.not-just",
  "category": "Rhetorical",
  "regex": "\\bit'?s not (just|only|merely|about)\\b[^.?!\\n]{1,60}?,\\s*it'?s\\b",
  "weight": 6,
  "severity": "High",
  "message": "Negative parallelism (“it's not just X, it's Y”) — feigns depth.",
  "suggestion": "State the thing directly."
}
```

What we ask of a new rule:

- **A `suggestion` that tells the writer what to do instead.** A flag with no fix is a black box with
  extra steps. This is not optional.
- **`evidence` where you have it** — a frequency ratio, a paper, a corpus observation. "It feels
  AI-ish" is a fine reason to open an issue and a weak reason to merge a rule.
- **A weight that matches how much the tell actually tells you.** `weight` 1–3 for a word that merely
  drifted upward, 5–7 for a strong marker, 8–9 for something almost nobody writes by accident.
  Severity `Info` is for things a human might legitimately want (empty intensifiers like *just*);
  reserve `High` for real signals.
- **Bounded regexes.** Prefer `[^.?!\n]{1,60}` over `.*` — the analyzer runs on every keystroke in the
  browser, and a catastrophic backtrack freezes someone's tab.

### The calibration invariant

`ScoringTests` holds a paragraph called `CleanHuman` and asserts it scores **exactly 0**.

```csharp
Assert.Equal(0, _a.Analyze(CleanHuman, "en").OverallScore);
```

**A new rule must not fire on it.** This is the guardrail that keeps the tool from crying wolf on
ordinary prose, and it is the single easiest test to break — usually by writing a regex that is more
general than you meant. Run `dotnet test` before you push; if `CleanHuman` moved off 0, tighten the
rule rather than adjusting the test.

Also note: the **Statistical** category score comes from burstiness alone. A rule you file under
`Statistical` will show in the findings but won't move the score, which is rarely what you want.

### Spanish is derived, not translated

`rules.es.json` is not a translation of `rules.en.json`, and PRs that machine-translate English rules
into it will be asked for rework. Spanish AI writing has its own tells (*sumérgete en el vasto mundo
de*, *cabe destacar que*, *un rico tapiz de*). If you propose a Spanish rule, ground it in Spanish
text you have actually seen a model produce.

## Tests

Every rule change needs a test in `tests/SignsOfAI.Core.Tests`. The pattern is short:

```csharp
[Fact]
public void Flags_negative_parallelism() =>
    Assert.Contains(_a.Analyze("It's not just a tool, it's a revolution.", "en").Findings,
                    f => f.RuleId == "rhet.not-just");
```

`Findings` is an `IReadOnlyList`, so use `.Any(...)` / `Assert.Contains`, not `.Exists(...)`.

## Dogfooding

Run your own prose through the tool before you submit it — including the PR description:

```bash
dotnet run --project src/SignsOfAI.Cli -- check your-file.md
```

The blog articles in `Docs/Blog` state their own scores in their own text, and those numbers are
verified. If you touch them, re-run the CLI and update the figures; editing a self-referential
paragraph changes the score it reports.

## Pull requests

- One idea per PR. A rule pack change and a UI refactor are two PRs.
- Say **why**, not just what. If you fixed a false positive, show the sentence that was wrongly flagged.
- `dotnet test` green, and no new build warnings.
- Match the surrounding code. The codebase has a voice; comments explain *why*, not *what*.

## Releasing (maintainers)

Bump `<Version>` in the csproj files you're shipping, plus **both** version fields in
`src/SignsOfAI.Mcp/.mcp/server.json`, then publish a GitHub Release tagged `v<version>`. The workflow
runs the tests, packs, verifies the tag matches, and publishes to NuGet through trusted publishing —
there is no API key anywhere.

One landmine: `src/SignsOfAI.Mcp/README.md` contains the line `mcp-name: io.github.peopleworks/signs-of-ai`.
The MCP registry reads it out of the published package to verify ownership. It looks like decoration.
It is not. Removing it breaks registry publishing, silently, on the next release.

## Reporting something sensitive

Security issues go through [SECURITY.md](SECURITY.md), not public issues.
