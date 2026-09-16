# .NET Foundation eligibility — where this repository stands

A working record of the project against the [.NET Foundation eligibility
criteria](https://github.com/dotnet-foundation/projects#eligibility-criteria), kept in the repo so
the claims are checkable rather than asserted. Each row says where the evidence lives.

Application filed 2026-08-01: [dotnet-foundation/projects#545](https://github.com/dotnet-foundation/projects/issues/545).
Transfer model: **Contribution** — the copyright stays with PeopleWorks. Trademarks: **Licensed**.

Last reviewed: 2026-09-16, at version 0.8.1 and desktop 0.8.1. The figures below are dated to that review, not
kept live; the application itself was filed at 0.2.1 and has had no reply.

## Suitability

| Criterion | Status | Evidence |
|---|---|---|
| Built on .NET and/or creates value in the .NET ecosystem | Met | `net10.0` throughout. Ships a library (`SignsOfAI.Core`), a `dotnet tool` (`SignsOfAI.Cli`), an MCP server (`SignsOfAI.Mcp`), a Blazor WebAssembly site, a WPF desktop app and a Word task pane, all driven by the same rule packs. |
| Collaborative development philosophy | Met | `CONTRIBUTING.md`. The extension points that matter — the detection rules and the sign catalog — are JSON data files (`src/SignsOfAI.Core/Rules/Packs/rules.{en,es}.json`), not compiled C#, specifically so that a contributor with domain knowledge and no .NET can open a pull request. `Docs/TRANSLATING.md` covers adding a language. |

## Code

| Criterion | Status | Evidence |
|---|---|---|
| Source code distributed to the public at no charge | Met | MIT, no paid tier, no gated features. |
| Discoverable and publicly accessible | Met | <https://github.com/peopleworks/SignsofAI> |
| Build script produces artifacts identical to the official ones | Met | Everything the project ships is built by a workflow from the tagged commit on a clean runner, never from a developer machine: `.github/workflows/nuget.yml` for the packages, `desktop-release.yml` for the Windows installer and `.zip`, both built from the same staging folder, `deploy-pages.yml` for the site. Each desktop artifact gets a published SHA-256 sidecar. |
| Reproducible build settings | Met | `Directory.Build.props`: `Deterministic`, plus `ContinuousIntegrationBuild` under `GITHUB_ACTIONS` so CI builds normalize paths and local builds stay debuggable. |
| Source Link | Met | `Directory.Build.props`: `PublishRepositoryUrl` + `EmbedUntrackedSources`. The GitHub provider is in-box since the .NET 8 SDK, so no `PackageReference` is needed. Verified: the packed `.nuspec` carries `<repository … commit="…">` and the PDB carries the `raw.githubusercontent.com/.../<commit>/*` map. |
| Embedded PDBs or symbol packages | Met | `.snupkg` per package (`IncludeSymbols`, `SymbolPackageFormat=snupkg`), pushed to the NuGet symbol server alongside each `.nupkg`. `nuget.yml` fails the release if any package is missing its symbols. |
| Artifacts are code signed | **Not met** | See *Known gaps*. |

## Licenses and copyright

| Criterion | Status | Evidence |
|---|---|---|
| At least one permissive OSI-approved license | Met | MIT (`LICENSE`, and `PackageLicenseExpression` in every packed project). |
| Mandatory dependencies are permissively licensed | Met | `SignsOfAI.Core` has **no** package dependencies at all. Across the repository: `ModelContextProtocol` (Apache-2.0), `PdfPig` (Apache-2.0), `Tokenizers.DotNet` (MIT), `Microsoft.ML.OnnxRuntime` (MIT), `Microsoft.Extensions.*` and `Microsoft.AspNetCore.*` (MIT). |
| Committers bound by a CLA | Willing | No CLA today — every commit to date is by the project lead. The project will adopt the .NET Foundation CLA and its bot on onboarding. |
| Copyright ownership clearly defined | Met | `LICENSE`: Copyright (c) 2026 Pedro Hernández — PeopleWorks. `Authors`/`Company` set on every packed project. |

## Community

| Criterion | Status | Evidence |
|---|---|---|
| Public homepage with status and purpose | Met | <https://peopleworks.github.io/SignsofAI/> — the live application is the homepage. |
| Public issue tracker | Met | GitHub Issues, with templates under `.github/ISSUE_TEMPLATE`. |
| Published security policy | Met | `SECURITY.md` |
| Public communication channel with maintainers | Met | GitHub Issues, and GitHub Discussions — including a *False positives* category, which is where a rule that misfires gets reported. The tool has no server and no telemetry, so a reported false positive is the only signal that comes *from users*. The rate itself is measured independently: `Docs/CALIBRATION.md` runs the engine over 296 texts written before 2022 and publishes how many it flags — 2 of 296 at the recommended threshold, with the interval. |
| Publicly reviewable and contributable documentation | Met | `README.md`, `Docs/`, and the in-product explanations — every finding the analyzer reports links to the catalog entry that justifies it. |
| Code of Conduct | Met | `CODE_OF_CONDUCT.md` (Contributor Covenant); to be relinked to the .NET Foundation Code of Conduct on onboarding. |
| Account/organization 2FA | Met | Enabled on the PeopleWorks GitHub account, confirmed by the account owner. |

## Known gaps

**Code signing.** Nothing this project ships is signed. The NuGet packages are unsigned, and the
desktop app triggers a SmartScreen warning on first run. Since 0.8.1 it ships as a per-user
installer as well as a `.zip`, which removed every other step between a teacher and the app —
unblocking, extracting, keeping the folder together — and left this one: the installer is
unsigned too, and the download page says so rather than implying otherwise.

An Authenticode certificate is the fix, and it is the single concrete resource the project would
ask the Foundation for. The alternatives were checked before asking: Azure Artifact Signing does not
accept free, trial or sponsored subscriptions, and its individual identity validation is limited to
the United States and Canada; a commercial certificate is a recurring cost this project has no
funding for. Either way SmartScreen reputation accrues with download history rather than on the day a
file is signed. Each release publishes a SHA-256, so a download can be verified against what this
repository built — which is not a substitute for a signature.

**One contributor.** 245 commits on `main` as of 2026-09-16, all by the project lead, first commit
2026-07-05. The project can
show that it is *built* for contribution — JSON rule packs, a translation guide, issue and PR
templates — but it cannot yet show contributors it has promoted, so the reasonable outcome of an
application is Seed rather than Member.
