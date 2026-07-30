# Brief: a document-extraction library (PDF, DOCX, RTF, ODT, TXT, MD, EPUB)

**Your working directory:** `C:\Proyecto\AI\SignsofAI-docs` — **`cd` there first, and work only there.**
It is a git worktree of the repo with your branch `feat/documents` already checked out, so there is
nothing to create: no `git checkout`, no `git branch`. Two other agents are working in sibling
folders on their own branches; if you run git commands in `C:\Proyecto\AI\SignsofAI` you will
disrupt them.

**Repo:** .NET 10, C#, MIT licensed.

## Context you need

The repo is "Signs of AI Writing": it detects the stylometric tells of AI-generated text and helps
the author edit them out. Today it is a Blazor WebAssembly web app where the user **pastes** text.

We are now building a **desktop app**, and the first thing a desktop user asked for is to feed it
**documents** — including dropping a whole folder of them at once (think: a teacher with 200
submissions, a writer with a book in chapters). So we need reliable plain-text extraction from the
formats people actually write in.

There is already prior art to follow: **read `src/SignsOfAI.Core/Documents/DocxTextExtractor.cs`
first.** It extracts DOCX with *zero* external dependencies — a .docx is a ZIP, so it uses the BCL's
`ZipArchive` plus LINQ-to-XML. Match its style: XML doc comments that explain *why*, lenient parsing
of real-world files (note how it handles ZIP writers that emit backslashes), and async with
`CancellationToken`.

## Goal

Create `src/SignsOfAI.Documents/SignsOfAI.Documents.csproj` — a class library (`Microsoft.NET.Sdk`,
`net10.0`) that turns a file into analysable plain text.

Suggested shape (adjust if you find something better, but explain why):

```csharp
public interface IDocumentExtractor
{
    bool CanHandle(string fileName);
    Task<ExtractionResult> ExtractAsync(Stream stream, CancellationToken ct = default);
}
```

`ExtractionResult` should carry the text **plus enough structure to map a finding back to where the
user can see it** — page number for PDF, paragraph index otherwise. A finding that says "sentence 4
of page 12" is useful; a 300 KB wall of text with a character offset is not. Also carry a
`Warnings` collection (e.g. "pages 3–4 are scanned images, no text layer").

Plus a registry/facade that picks the right extractor for a given file, and enumerates a directory.

## Formats and how to get them

| Format | How |
|---|---|
| `.txt`, `.md` | Plain read. Detect encoding (BOM, then UTF-8, then fall back). Strip Markdown syntax so the analyser sees prose, not `##` and link brackets. |
| `.docx` | **Reuse** the existing `DocxTextExtractor` from Core — do not duplicate it. |
| `.odt` | Same trick as DOCX: ZIP + `content.xml`. No dependency needed. |
| `.epub` | Also a ZIP: read the OPF spine, then the XHTML chapters in order. No dependency needed. |
| `.rtf` | A small hand-written control-word stripper is fine and avoids a dependency. |
| `.pdf` | Needs a library — see the licence rule below. |

## Hard constraints — these are not negotiable

1. **Never add a `PackageReference` to `SignsOfAI.Core`.** Core is deliberately dependency-free
   (check its `.csproj`: zero package references) because it compiles into Blazor WebAssembly and
   ships on NuGet. Your new project *may* have dependencies; Core may not. Your project references
   Core, never the reverse.

2. **Licence: MIT-compatible only.** This repo is MIT. For PDF use **PdfPig**
   (`UglyToad.PdfPig`, Apache-2.0) — it is the right pick and it is pure managed code.
   **Do not use iText / iTextSharp (AGPL)** or anything GPL/AGPL: it would force a licence change on
   the whole project. If you are unsure about a package's licence, do not add it — ask.

3. **A bad file must never kill a batch.** Encrypted PDFs, truncated ZIPs, a `.docx` that is really
   a renamed `.doc`, a 0-byte file, a PDF that is pure scanned images with no text layer — every one
   of these must come back as a *typed failure result* the caller can list in a UI ("12 of 200 files
   could not be read, here is why"). No exception may escape a single-file extraction and abort the
   loop. This is the single most important requirement in this brief.

4. **Guard against resource exhaustion.** Someone will drop a 400 MB PDF. Accept a max-size /
   max-pages option and stop cleanly when exceeded. Do not buffer an entire folder into memory at
   once.

5. **Do not touch these paths at all:** `src/SignsOfAI.Web/`, `src/SignsOfAI.Perplexity.Api/`,
   `src/SignsOfAI.Cli/`, `src/SignsOfAI.Mcp/`, `SignsOfAI.slnx`, `README.md`. Two other agents are
   working in parallel — one is refactoring the Web project, one is extracting an ONNX library.
   Touching their files, or the solution file, causes a conflict. **The solution file will be wired
   by the maintainer — just create the project, do not add it to `SignsOfAI.slnx`.**
   Inside `src/SignsOfAI.Core/` you may **read** but not modify.

## Tests

Add `tests/SignsOfAI.Documents.Tests/` with **small fixture files committed into the repo** — a
2-page PDF, a tiny ODT, an EPUB with two chapters, an RTF, a UTF-16 TXT with a BOM, and
deliberately broken ones (a truncated PDF, an encrypted PDF, a 0-byte file). Keep every fixture
under ~50 KB; generate them programmatically in the test if that is cleaner than committing a blob.

Tests must run in CI with **no network** and no external tools installed.

## Definition of done

```bash
cd C:\Proyecto\AI\SignsofAI
dotnet build                    # whole solution builds
dotnet test                     # the 125 existing tests still pass, plus yours
```

When done: commit on `feat/documents` with a clear message, do **not** merge, do **not** push to
`main`, and write a short summary — especially: which formats you got working, which you did not,
and any file that defeated the extractor. An honest "EPUB with nested navigation is not handled" is
worth more than a claim of full coverage.
