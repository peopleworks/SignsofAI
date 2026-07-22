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
| `measure_predictability` | Perplexity — how predictable/generic a model finds the phrasing | ☁️ server |
| `check_paraphrase` | Reworded/translated copies via sentence embeddings (EmbeddingGemma) | ☁️ server |

The first four run **entirely on the machine** — the text never leaves it. The last two **send the text**
to the SignsOfAI server (their descriptions disclose this); see [Server tools](#server-tools-optional).

## Run it

```bash
# From the repo root — for development:
dotnet run --project src/SignsOfAI.Mcp

# …or build once and point Claude Desktop at the DLL (see below):
dotnet build src/SignsOfAI.Mcp -c Release
```

To install as a global command (`signsofai-mcp`):

```bash
dotnet pack src/SignsOfAI.Mcp -c Release
dotnet tool install --global --add-source src/SignsOfAI.Mcp/bin/Release SignsOfAI.Mcp
```

## Claude Desktop

Add one of these to `claude_desktop_config.json`
(Windows: `%APPDATA%\Claude\claude_desktop_config.json`), then restart Claude Desktop.

Using the built DLL:

```json
{
  "mcpServers": {
    "signs-of-ai": {
      "command": "dotnet",
      "args": ["C:\\Proyecto\\AI\\SignsofAI\\src\\SignsOfAI.Mcp\\bin\\Release\\net10.0\\SignsOfAI.Mcp.dll"]
    }
  }
}
```

Or, if installed as a global tool:

```json
{
  "mcpServers": {
    "signs-of-ai": { "command": "signsofai-mcp" }
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
