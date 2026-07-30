# Translating the interface

The interface of Signs of AI Writing is translated by whoever wants to translate it. You do not need
to know C#, .NET or Blazor, and you do not need to build anything: a language is **two files' worth
of edits**, both plain JSON.

If you speak a language this tool doesn't, you can add it. That's the whole idea.

---

## What you'll be editing

Everything lives in one folder:

```
src/SignsOfAI.UI/wwwroot/i18n/
├── locales.json     ← the list of available languages
├── en.json          ← English (the reference)
└── es.json          ← Spanish
```

Each language file is a flat map of **key → text**. The key is an internal name that never appears
on screen; only the text does.

```json
{
  "nav.analyze": "Analyze",
  "home.stat.words": "Words",
  "home.recommendations": "Recommendations ({0})"
}
```

`en.json` is the reference. Every other file is a translation of it.

---

## Adding a new language

Say you want French (`fr`).

**1. Copy the reference.**

```bash
cp src/SignsOfAI.UI/wwwroot/i18n/en.json src/SignsOfAI.UI/wwwroot/i18n/fr.json
```

**2. Translate the values in `fr.json`.** Change only the text on the right of each colon. Never
change the keys on the left.

```json
"nav.analyze": "Analyser",
"home.stat.words": "Mots",
```

**3. Add one line to `locales.json`:**

```json
{
  "fallback": "en",
  "locales": [
    { "code": "en", "name": "English", "endonym": "English", "credit": "" },
    { "code": "es", "name": "Spanish", "endonym": "Español", "credit": "PeopleWorks" },
    { "code": "fr", "name": "French",  "endonym": "Français", "credit": "Your Name" }
  ]
}
```

| Field | What it is |
|---|---|
| `code` | Short language code. Must match the filename: `fr` → `fr.json`. |
| `name` | The language's English name, for maintainers reading this file. |
| `endonym` | The language's name **in that language** — `Français`, not `French`. A French speaker looks for `Français`. |
| `credit` | You. Shown when hovering the language switch, so contributors get named. Leave `""` to stay anonymous. |

**4. Open a pull request.** That's it — the switch picks the new language up automatically, no code
change anywhere.

---

## You don't have to finish

**A partial translation is welcome.** Any key you leave out falls back to English at run time, so
half a translation ships as half-translated — not as a page full of blanks. Translate the navigation
and the main page, open the PR, come back for the rest whenever.

You can also simply **delete** any key you're unsure about. Deleting is safer than guessing: a
deleted key shows English, while a wrong translation shows something wrong.

---

## Four rules that matter

**1. `{0}`, `{1}` … must survive.** They're slots where a number or a name gets inserted. Keep every
one that appears in the English, in whatever order your language needs.

```json
"home.recommendations": "Recommendations ({0})"     ← English
"home.recommendations": "Recommandations ({0})"     ← good
"home.recommendations": "Recommandations"           ← the count disappears from the page
```

**2. `.one` / `.other` are singular and plural.** `{0}` is the count.

```json
"home.signals.one":   "{0} signal found",
"home.signals.other": "{0} signals found",
```

If your language doesn't split this way, write both slots so they read correctly either way.

**3. Keep the HTML tags.** Some strings contain markup — `<strong>`, `<em>`, `<code>`, `<a href>`.
Translate the words around the tags and leave the tags themselves intact.

```json
"home.tagline": "Paste, upload, or just start typing … <strong>live, as you write</strong> …"
```

Because these strings are rendered as markup, treat a translation PR like a code change and read it
as such before merging.

**4. Don't translate names.** `Signs of AI Writing`, `PeopleWorks`, `Blazor`, `.NET`, `Ollama`,
`Anthropic`, `GitHub`, `jaccard`, `Tokens` stay as they are.

---

## Checking your work

```bash
dotnet test
```

The locale tests will tell you, by name, if a key is missing, misspelled, duplicated, blank, or lost
a placeholder. They run automatically on every pull request too. They deliberately **do not** fail
for an incomplete translation.

To see it in the browser:

```bash
dotnet run --project src/SignsOfAI.Web
```

Your language appears in the switch in the top-right corner.

---

## What is *not* translated here

The **findings** — the explanations of each AI-writing tell ("`delve` is heavily overused in AI
writing") — do not live in these files. They come from the rule packs, and they are written in the
language of the text being analyzed: advice about English prose is given in English, even when the
interface is in French. That is intentional.

Rule packs are their own contribution path, and also plain JSON — see
[`src/SignsOfAI.Core/Rules`](../src/SignsOfAI.Core/Rules) and the **Catalogs** panel on the Analyze
page, which lets you load your own without touching the repository at all.
