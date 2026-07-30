// Interface-language helpers. Blazor owns the UI copy; these two touch the things only the
// browser can tell us or that live outside the Blazor root.
window.signsofai = window.signsofai || {};

// The visitor's preferred language, used only to pick a sensible default on a first visit.
window.signsofai.browserLang = function () {
    return (navigator.languages && navigator.languages[0]) || navigator.language || '';
};

// <html lang> sits outside #app, so Blazor can't render it. Screen readers pick their voice from
// it and browser translation UIs read it, so it has to follow the switch.
window.signsofai.setHtmlLang = function (lang) {
    document.documentElement.setAttribute('lang', lang);
};

// Reads a static asset shipped with the app and hands back its text, or null if it isn't there.
//
// This deliberately goes through the browser's fetch instead of a .NET HttpClient. The desktop host
// renders these same components inside a WebView, and a native HttpClient cannot reach a WebView's
// virtual host — only the page itself can. Routing through the page gives one code path that works
// in the browser and on the desktop alike.
window.signsofai.fetchText = async function (path) {
    try {
        // Resolve against <base href> explicitly: on GitHub Pages the app is served from /<repo>/,
        // so a bare relative path has to pick up that prefix rather than hitting the domain root.
        const response = await fetch(new URL(path, document.baseURI));
        return response.ok ? await response.text() : null;
    } catch {
        return null;
    }
};
