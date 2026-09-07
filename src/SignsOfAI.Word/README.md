# Signs of AI Writing — Word task pane

**Status: a spike.** It builds, it boots, and it analyses. It has not yet been loaded inside Word by
anybody — that is the one question left, and the section below is how to answer it in a few minutes.

## What this is

The third host. Every rule, every component and the engine arrive through `SignsOfAI.UI`, the same
Razor class library the web app and the desktop window render. This project is the task pane shell
and about sixty lines of Office.js glue.

That matters more here than it does anywhere else the project runs. **A task pane is a browser**, so
the WebAssembly engine runs inside Word, on the machine, and the document is never uploaded. Every
other add-in in this category posts your document to an API. The manifest asks for `ReadDocument`
rather than `ReadWriteDocument`, so Word itself enforces that this add-in only reads — rather than
asking anyone to trust a sentence on a website.

## What it does with a short document

The honest thing, and it is worth seeing before deciding what to build next. A 92-word paste scores
90/100 and the pane says:

> **No verdict at this length.** This text is 92 words. The boundary was measured only on texts of
> 649 words and longer, so no verdict is given — the score is neither evidence that a machine wrote
> this nor evidence that a person did. Everything below is unaffected.

The named signals, the character scan and the citation cross-check all still appear, because those
carry no threshold. An essay clears 649 words comfortably; this is mostly a note about what a
PowerPoint deck would get, and why a PowerPoint add-in is a different product rather than the same
one with a different manifest.

## Trying it

### In a browser, without Word

The pane detects that Office.js is absent and offers a paste box instead of showing a broken panel.
That is how it was developed and how the screenshots were taken.

```
dotnet publish src/SignsOfAI.Word -c Release -o out
cd out/wwwroot && python -m http.server 8731
```

Then open <http://localhost:8731/index.html> in a window about 340px wide.

### In Word

Word will not load a task pane over plain HTTP from localhost without a certificate, so point the
manifest at a deployed copy, or serve the published folder over HTTPS.

**Word on the web** — the quickest path:

1. Open a document on <https://www.office.com>
2. **Home** → **Add-ins** → **More Add-ins** → **My Add-ins** → **Upload My Add-in**
3. Choose `manifest.xml`
4. **Home** → **Read the signs**

**Word for Windows** has no upload button; it reads a shared-folder catalogue. The full walkthrough
lives in `C:\Proyecto\PowerPointWebViewer\README.md`, which solved this once already — the steps are
identical, only the Trust Center list is per-application.

## What the spike proved, and what it did not

Proved, by running it:

- Blazor WebAssembly boots inside a 340px pane and the engine runs there.
- The shared components render in one narrow column — the score, the withheld verdict, the artifact
  and citation panels, the findings list, the EN/ES switch.
- Office.js and Blazor coexist: `Office.onReady` fires before the module finishes starting, so the
  bridge parks the answer in a promise the .NET side awaits, rather than a callback registered too
  late to hear it.
- The absence of Word is a state, not a crash.

Not proved, because it needs Word:

- That Word accepts the manifest and shows the ribbon button.
- That `Word.run` returns the body text of a real document — the call is written but has only ever
  run against nothing.

Found while building, and worth keeping:

- The published page must reference `_framework/blazor.webassembly#[.{fingerprint}].js`. Without the
  placeholder the file name only exists during development and the pane never starts.
- The shared stylesheet assumes a page with room. In a pane, anything with a minimum width pushes
  content off the right edge, where there is no way to scroll to it.

## Before this could ship

- **The boot retry.** `boot.js` — the work from #73 and #74 that survives a transient 503 — lives in
  `SignsOfAI.Web/wwwroot` and is not used here. A pane that hangs inside Word is worse than a tab
  that hangs, because there is no obvious way to open developer tools. It should move into the
  shared library first.
- **Where it is hosted.** The manifest points at `peopleworks.github.io/SignsofAI/word/`, which the
  Pages workflow does not publish yet.
- **The Office Store, or sideloading.** The store wants a privacy policy, a support page and review;
  sideloading wants none of that and reaches nobody who has not been told about it.
