# The Glama build configuration

Glama lists this MCP server and verifies it by **building and starting it themselves**. That build
is not driven by the repository's `Dockerfile`: it is a form under *Admin → Dockerfile* on
<https://glama.ai/mcp/servers/@peopleworks/SignsofAI>, and Glama generates a Dockerfile from the
fields. The repository's own `Dockerfile` is a separate, simpler thing for anyone who wants to run
the server in a container by hand.

**This file exists because those form values lived nowhere else.** On 2026-08-10 they were
overwritten with another project's, and the only surviving copy was a screenshot somebody happened
to have taken. A configuration that a third party stores for you is a configuration you do not have.

## The values

**Base image** `debian:trixie-slim` — **Node.js** `26` — **Python** `3.14`

**Build steps**

```json
[
  "apt-get update && apt-get install -y --no-install-recommends libicu-dev && rm -rf /var/lib/apt/lists/*",
  "curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh",
  "bash dotnet-install.sh --channel 10.0 --install-dir ./dotnet",
  "./dotnet/dotnet publish src/SignsOfAI.Mcp/SignsOfAI.Mcp.csproj -c Release -o ./out"
]
```

**CMD arguments**

```json
["mcp-proxy", "--", "./dotnet/dotnet", "./out/SignsOfAI.Mcp.dll"]
```

**Environment variables JSON schema**

```json
{
  "properties": {
    "SIGNSOFAI_API_ENDPOINT": {
      "description": "Endpoint for the optional server features (predictability and paraphrase check). If not set, those tools are unavailable.",
      "type": "string"
    }
  },
  "required": [],
  "type": "object"
}
```

**Placeholder parameters** `{}` — the server starts with no credentials, so there is nothing to fake.

**Pinned commit SHA** — leave **empty**. See below.

## Three things that are easy to get wrong

**`libicu-dev` is the first build step, and it is not optional.** `debian:trixie-slim` ships no ICU
and .NET dies at startup without it. The tempting fix is
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`, which makes the build pass and **silently changes how
Spanish text is compared and cased** — in a product whose whole differentiator is that it treats
Spanish as a first language. Install the library; never set that variable.

**Leave the pinned SHA empty.** A pin is a promise to keep publishing whatever that commit said. On
2026-08-10 the form was pinned to `020554d`, from the day before, so a rebuild would have shipped a
report that announced a tidy bibliography as a source contradiction — a defect fixed that morning.
If a pin is ever needed, it is a temporary measure with a date attached, not a default.

**stdio, so there is no port.** The client speaks JSON-RPC over stdin and stdout. `mcp-proxy` in the
CMD is Glama's own shim that fronts a stdio server for their checks. Nothing listens on a socket,
so any field asking for a port or a health endpoint stays blank.

## Related

- `Dockerfile` in the repository root — the hand-run container, unrelated to this form.
- `src/SignsOfAI.Mcp/.mcp/server.json` — the MCP registry manifest, a third and separate place the
  version has to be raised on release.
