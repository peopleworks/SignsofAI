# Signs of AI Writing — Word task pane

**Status: it loads in Word.** Sideloaded into Word on the web on 7 September: the manifest is
accepted, the ribbon shows a **Signs of AI** group with a **Read the signs** button, and the pane
opens. What it showed was the wrong page — see below, it was a 404 and it is fixed.

Still untested: that `Word.run` returns the body of a real document. That call is written and has
only ever run against nothing.

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

## The first sideload, and what it found

The pane came up showing the **web app's** navigation and "Sorry, the content you are looking for
does not exist", and Word warned *"This add-in may not load properly."*

Neither was a bug in the add-in. `SourceLocation` points at
`https://peopleworks.github.io/SignsofAI/word/index.html`, the Pages workflow published only the web
app, and so that URL answered **404**. Pages then served its SPA fallback — the web app's
`index.html` — whose Blazor router has no route for `/word/index.html` and correctly said Not found.
Word's warning was the 404, not the machine it was running on.

Fixed in `deploy-pages.yml`, which now publishes this project into `/word/` with its own rewritten
`<base href>` and **fails the deploy** if that entry point is missing. A task pane is loaded by URL
from inside Word, where a 404 surfaces as "this add-in may not load properly" rather than as a
missing page, so it is worth failing the deploy instead of finding out in Word a second time.

## What the spike proved, and what it did not

Proved, by running it:

- Blazor WebAssembly boots inside a 340px pane and the engine runs there.
- The shared components render in one narrow column — the score, the withheld verdict, the artifact
  and citation panels, the findings list, the EN/ES switch.
- Office.js and Blazor coexist: `Office.onReady` fires before the module finishes starting, so the
  bridge parks the answer in a promise the .NET side awaits, rather than a callback registered too
  late to hear it.
- The absence of Word is a state, not a crash.

Proved by sideloading it:

- Word accepts the manifest and puts **Read the signs** on the Home tab.
- The pane opens and loads over HTTPS from Pages.

Not proved yet:

- That `Word.run` returns the body text of a real document — the call is written but has only ever
  run against nothing, because the page that contains it never loaded.

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
- **First load is about 3.4 MB** compressed — the runtime, and 2 MB of that is ICU data, which
  cannot be dropped with `InvariantGlobalization` because it would silently change how Spanish is
  handled. Trimming ICU to the locales this actually needs is the obvious next look.
- **The Office Store, or sideloading.** The store wants a privacy policy, a support page and review;
  sideloading wants none of that and reaches nobody who has not been told about it.
