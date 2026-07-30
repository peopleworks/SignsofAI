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
