# Brief: extract the ONNX engines into a reusable in-process library

**Your working directory:** `C:\Proyecto\AI\SignsofAI` — work only there.
Your branch `feat/onnx-lib` is already checked out there, so there is nothing to create: no
`git checkout`, no `git branch`, and **do not switch branches** — two other agents are working in
sibling folders (`SignsOfAI-docs`, `SignsOfAI-desktop`) on their own branches off the same repo.
The ONNX model weights live in `C:\Proyecto\AI\SignsofAI\models\` — that folder is yours, so you can
run the engine for real instead of skipping the model-dependent tests.

**Repo:** .NET 10, C#, MIT licensed.

## Context you need

The repo is "Signs of AI Writing": it detects the stylometric tells of AI-generated text. Today the
statistical part (perplexity / burstiness via a local Qwen2.5-0.5B int8 ONNX model, plus embeddings
via EmbeddingGemma-300m) only exists behind an **HTTP server** — `src/SignsOfAI.Perplexity.Api` —
which the Blazor WebAssembly front-end calls over the network.

We are now building a **desktop app**. On the desktop there is no reason to go over HTTP: the same
engine can run **in-process**, offline, with nothing uploaded anywhere. That is the whole point of
the desktop version, so the engine has to become a library that any host can reference.

## Goal

Create `src/SignsOfAI.Onnx/SignsOfAI.Onnx.csproj` — a plain class library (`Microsoft.NET.Sdk`,
`net10.0`) holding the ONNX inference code, and make the existing API project consume it instead of
owning it. **Move the code, do not rewrite it.** It works and it was validated; a rewrite risks
silently changing the scores.

### What moves into the library

- `Engine/IPerplexityEngine.cs`
- `Engine/OnnxPerplexityEngine.cs`
- `Engine/OnnxEmbeddingEngine.cs`
- `Scoring/PerplexityScorer.cs`
- `Config/PerplexityOptions.cs`, `Config/EmbeddingOptions.cs`

### What stays in `SignsOfAI.Perplexity.Api`

- `Program.cs` (the HTTP endpoints), `Model/Contracts.cs`, `Search/WebSearchService.cs`,
  `Config/WebSearchOptions.cs`. It adds a `ProjectReference` to the new library.

## Hard constraints — these are not negotiable

1. **Never add a `PackageReference` to `SignsOfAI.Core`.** Core is deliberately dependency-free
   (check its `.csproj`: zero package references). It compiles into Blazor WebAssembly and ships on
   NuGet; an ONNX dependency there would break both. All ONNX deps live in the new library only.

2. **No `RuntimeIdentifier` in the library.** The API pins `win-x64` because ONNX Runtime ships
   ~200 MB of natives for every OS. RID pinning is a **host** decision — leave it to host projects
   so a future Linux or macOS host can pick its own. Move the `PackageReference` lines
   (`Microsoft.ML.OnnxRuntime`, `Tokenizers.DotNet`) to the library, but leave
   `Tokenizers.DotNet.runtime.win-x64` **in the API** (it is RID-specific), and note in a comment
   that each host must reference its own runtime package.

3. **The model path must be injectable.** Today it comes from the API's configuration. The desktop
   will look somewhere completely different (next to the .exe, or a folder under `%LOCALAPPDATA%`
   after a first-run download). The options object must be constructible in code without
   `IConfiguration`, and the library must not read `appsettings.json` itself.

4. **Missing model must degrade, never crash.** Expose something like
   `bool IsAvailable { get; }` / `Task<bool> ProbeAsync(CancellationToken)` that reports "model not
   installed" cleanly. The desktop app must be able to show "statistical analysis unavailable —
   model not installed" and keep working, because the rest of the analysis is pure C# and needs no
   model at all. Do not let a missing file throw out of a constructor.

5. **Do not touch these paths at all:** `src/SignsOfAI.Web/`, `src/SignsOfAI.Core/`,
   `src/SignsOfAI.Cli/`, `src/SignsOfAI.Mcp/`, `SignsOfAI.slnx`, `README.md`. Another agent is
   refactoring the Web project in parallel and a third is adding a documents project; touching
   those, or the solution file, causes a conflict. **The solution file will be wired by the
   maintainer — just create the project, do not add it to `SignsOfAI.slnx`.**

## Tests

Add `tests/SignsOfAI.Onnx.Tests/`.

**Critical:** the `models/` folder is **git-ignored** (the weights are hundreds of MB and are not in
the repo). CI has no models. So every test that needs a model must **skip, not fail**, when the
model file is absent — e.g. check `File.Exists` and return early, or use a custom
`[SkippableFact]`-style guard. A red CI on a machine without weights is a bug in the test, not a
finding.

Do include tests that need **no** model: options validation, the "model missing → `IsAvailable ==
false`, no exception" path, and any pure-math helper inside `PerplexityScorer` you can exercise
directly.

## Definition of done

```bash
cd C:\Proyecto\AI\SignsofAI
dotnet build                    # whole solution builds
dotnet test                     # the 125 existing tests still pass, plus yours
dotnet run --project src/SignsOfAI.Perplexity.Api   # still starts and still answers
```

The API's behaviour must be **byte-identical** to before for the same input text. If you find
yourself changing a formula, stop — that is out of scope. Report it instead.

When done: commit on `feat/onnx-lib` with a clear message, do **not** merge, do **not** push to
`main`, and write a short summary of what moved and anything you had to decide.
