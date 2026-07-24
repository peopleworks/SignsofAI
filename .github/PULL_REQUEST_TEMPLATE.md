## What this changes

<!-- One or two sentences. If it fixes an issue, link it: Fixes #123 -->

## Why

<!-- The reasoning, not the diff. For a rule change, show the text that was wrongly flagged or wrongly
     missed — that's the evidence a reviewer needs. -->

## Checklist

- [ ] `dotnet test` passes, with no new build warnings
- [ ] One idea per PR

If you touched the **rule packs** (`rules.en.json` / `rules.es.json`):

- [ ] The rule carries a `suggestion` — what the writer should do instead
- [ ] There's a test in `tests/SignsOfAI.Core.Tests` covering it
- [ ] `CleanHuman` in `ScoringTests` still scores exactly 0 (the new rule doesn't fire on ordinary prose)
- [ ] Any regex is bounded — no unbounded `.*`; this runs on every keystroke in the browser
- [ ] Spanish rules are derived from Spanish AI output, not translated from the English pack

If you touched the **articles in `Docs/Blog`**:

- [ ] Re-ran the CLI and updated the scores the articles quote about themselves
