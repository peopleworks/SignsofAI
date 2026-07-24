# Security Policy

## Reporting a vulnerability

Use GitHub's private reporting:
**[Report a vulnerability](https://github.com/peopleworks/SignsofAI/security/advisories/new)**
(Security → Advisories → Report a vulnerability). It's private until we publish it together.

Please don't open a public issue for a vulnerability. If GitHub advisories don't work for you, email
`peopleworks@gmail.com` with `SECURITY` in the subject.

Expect a first reply within a week. This is a small project maintained alongside a day job, so a fix
may take longer than an acknowledgement — you'll hear where it stands either way. If you'd like credit
in the advisory, say so; if you'd rather stay anonymous, that's fine too.

## Supported versions

The latest release on NuGet and the deployed web app. There are no long-term support branches.

## What the attack surface actually looks like

Most of this project has no server to attack, which changes what "vulnerability" means here.

| Component | Where it runs | Notes |
| --- | --- | --- |
| Web app | Your browser, WebAssembly | Static hosting, no backend, no accounts, no cookies, no analytics. Documents are never uploaded. |
| `SignsOfAI.Core`, `.Cli`, `.Mcp` | Your machine | Local processing only. |
| `SignsOfAI.Perplexity.Api` | A server | The only component that receives text — and only from the two features that say so. |

We're especially interested in reports about:

- **Anything that causes text to leave the device from a feature documented as local.** That's the
  project's central promise; a bug there is the most serious thing you could find.
- **Credential handling in the optional "Humanize" feature.** Bring-your-own-key credentials are
  stored in the browser and sent directly to the provider you chose. A path that leaks them anywhere
  else is a vulnerability.
- **Catastrophic regex backtracking in the rule packs.** Rules run on every keystroke; a pattern that
  hangs the tab is a denial of service on the person typing.
- **Anything in the optional API** — injection, resource exhaustion, or a way to make it return
  someone else's text.

## What isn't a vulnerability

- **A false positive or a missed tell.** The tool is a heuristic linter, not a verdict machine, and
  being wrong is a known property, not a security bug. Please file those as regular issues — they're
  genuinely welcome, just through the other door.
- **Being able to write text that scores low.** You can, deliberately, and the README says so. The
  tool measures signals; it doesn't claim to be unfoolable.
