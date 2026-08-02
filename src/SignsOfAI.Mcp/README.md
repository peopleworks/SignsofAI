# SignsOfAI.Mcp — Model Context Protocol server

Exposes the **Signs of AI Writing** engine as [MCP](https://modelcontextprotocol.io) tools, so Claude
Desktop (or any MCP client) can analyze text, compare documents for copying, browse the sign catalog, and —
optionally — measure perplexity and find cross-language paraphrases.

Built on the official [`ModelContextProtocol`](https://www.nuget.org/packages/ModelContextProtocol) SDK
(stdio transport) and a project reference to `SignsOfAI.Core`.

## Tools

| Tool | What it does | Runs |
| --- | --- | --- |
| `analyze_ai_writing` | Score 0–100 + verdict + findings (overused vocabulary, rhetorical crutches, syntactic tells, burstiness), each with a fix | 🖥️ offline |
| `check_originality` | Compares 2+ documents against **each other** → overlap % + the actual shared passages | 🖥️ offline |
| `search_catalog` | Searches the catalog of AI-writing signs (EN/ES), filter by keyword / language / category | 🖥️ offline |
| `extract_distinctive_phrases` | Distinctive phrases + ready-made exact-phrase web-search links | 🖥️ offline |
| `inspect_characters` | Characters typing cannot produce — zero-width, homoglyphs, hidden tag characters — with the exact line and column of each | 🖥️ offline |
| `measure_predictability` | Perplexity — how predictable/generic a model finds the phrasing | ☁️ server |
| `check_paraphrase` | Reworded/translated copies via sentence embeddings (EmbeddingGemma) | ☁️ server |

The first five run **entirely on the machine** — the text never leaves it. The last two **send the text**
to the SignsOfAI server (their descriptions disclose this); see [Server tools](#server-tools-optional).

## Install

It's on NuGet as [`SignsOfAI.Mcp`](https://www.nuget.org/packages/SignsOfAI.Mcp), a .NET tool. You need
the [.NET 10 SDK](https://dotnet.microsoft.com/download); nothing else to build.

```bash
# Run it on demand — no install step:
dnx SignsOfAI.Mcp --yes

# …or install the `signsofai-mcp` command once:
dotnet tool install --global SignsOfAI.Mcp
```

To hack on it instead, run it straight from the repo: `dotnet run --project src/SignsOfAI.Mcp`.

## Claude Desktop

Add one of these to `claude_desktop_config.json`
(Windows: `%APPDATA%\Claude\claude_desktop_config.json`), then restart Claude Desktop.

```json
{
  "mcpServers": {
    "signs-of-ai": {
      "command": "dnx",
      "args": ["SignsOfAI.Mcp", "--yes"]
    }
  }
}
```

Or, if you installed the global tool:

```json
{
  "mcpServers": {
    "signs-of-ai": { "command": "signsofai-mcp" }
  }
}
```

Working from a clone instead? Point it at the built DLL:

```json
{
  "mcpServers": {
    "signs-of-ai": {
      "command": "dotnet",
      "args": ["C:\\path\\to\\SignsofAI\\src\\SignsOfAI.Mcp\\bin\\Release\\net10.0\\SignsOfAI.Mcp.dll"]
    }
  }
}
```

## Server tools (optional)

`measure_predictability` and `check_paraphrase` call the SignsOfAI API. By default they use the
PeopleWorks-hosted endpoint; override it with the `SIGNSOFAI_API_ENDPOINT` environment variable:

```json
{
  "mcpServers": {
    "signs-of-ai": {
      "command": "signsofai-mcp",
      "env": { "SIGNSOFAI_API_ENDPOINT": "https://your-server" }
    }
  }
}
```

Unlike the offline tools, these two **send the text off the device** to run the model — the same disclosed,
opt-in behavior as the web app's Predictability and Paraphrase features. `check_paraphrase` also needs the
embedding feature enabled on the server.

## Protocol note

MCP speaks JSON-RPC over **stdout**, so this server logs everything to **stderr** — never write to stdout
from a tool.

## MCP registry

Listed in the [official MCP registry](https://registry.modelcontextprotocol.io) under the name below.
The registry reads that line out of this README to verify that whoever publishes the registry entry also
owns the NuGet package, so **don't remove or reword it** — publishing would start failing.

mcp-name: io.github.peopleworks/signs-of-ai
