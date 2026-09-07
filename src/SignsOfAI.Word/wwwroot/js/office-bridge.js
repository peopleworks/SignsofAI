// The whole of this add-in's contact with Word.
//
// It reads. It never writes, and the manifest asks only for ReadDocument so Word itself enforces
// that rather than asking anyone to take our word for it. The text it pulls out is handed straight
// to the WebAssembly module running in this same pane; nothing here opens a socket, and there is no
// endpoint in this file to open one to.
//
// Office.js signals readiness once, before Blazor has finished starting, so the result is parked in
// a promise the .NET side awaits instead of a callback it might register too late.

window.signsOfAiWord = (function () {
  "use strict";

  let readyResolve;
  const ready = new Promise((resolve) => { readyResolve = resolve; });

  // Set by index.html once Office.onReady has fired. Outside Word — a plain browser tab during
  // development — Office.js is absent and this resolves to false instead of hanging.
  function markReady(insideWord) {
    readyResolve(!!insideWord);
  }

  async function isInsideWord() {
    return await ready;
  }

  // The document body as plain text. Word's own extraction, so headers, footers, footnotes and
  // comments are excluded — the body is what a reader would call "the essay".
  async function readDocument() {
    if (!(await ready)) {
      throw new Error("not-inside-word");
    }

    return await Word.run(async (context) => {
      const body = context.document.body;
      body.load("text");
      await context.sync();
      return body.text ?? "";
    });
  }

  // The document's own name, so a report says which file it describes.
  async function documentName() {
    if (!(await ready)) return null;
    try {
      const url = Office.context.document.url;
      if (!url) return null;
      const parts = url.split(/[\\/]/);
      return parts[parts.length - 1] || null;
    } catch {
      return null; // a document that has never been saved has no url, which is not an error
    }
  }

  return { markReady, isInsideWord, readDocument, documentName };
})();
