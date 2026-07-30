# Caza de estrellas — acciones pendientes (24 jul 2026)

Todo lo que sigue requiere tu cuenta o un formulario web. Copiar y pegar.

---

## 1. Tarjeta social del repo (2 minutos, la de mayor impacto inmediato)

Ahora mismo `usesCustomOpenGraphImage: false` — cada link compartido del repo en X, LinkedIn,
Reddit, HN, Discord o Slack sale con la tarjeta gris genérica de GitHub.

1. https://github.com/peopleworks/SignsofAI/settings
2. Sección **Social preview** → *Edit* → *Upload an image*
3. Sube `Docs/Blog/social/social-preview.png` (1280×640, ya renderizada)

Haz esto **antes** de publicar en HN/Reddit.

---

## 2. Publicar el MCP en NuGet (desbloquea el registro oficial)

El paquete ya está listo y validado: lleva `.mcp/server.json`, icono, tags `mcpserver`
y los dos package types (`DotnetTool` + `McpServer`).

```powershell
dotnet pack src/SignsOfAI.Mcp -c Release -o ./nupkg
dotnet nuget push ./nupkg/SignsOfAI.Mcp.0.1.0.nupkg --api-key <TU_API_KEY> --source https://api.nuget.org/v3/index.json
```

Consigue la key en https://www.nuget.org/account/apikeys (scope: *Push new packages*, glob `SignsOfAI.*`).

**Después de publicar** (avísame y lo hago):
- Actualizar `src/SignsOfAI.Mcp/README.md` y el README raíz: el install pasa a ser `dnx SignsOfAI.Mcp`
  o `dotnet tool install -g SignsOfAI.Mcp` en vez de compilar desde fuente.
- Registrar en el **registro oficial de MCP** (necesita login con GitHub, interactivo):
  ```
  mcp-publisher login github
  mcp-publisher publish
  ```
  (desde `src/SignsOfAI.Mcp`, que es donde está `.mcp/server.json`)

---

## 3. Directorios MCP que NO aceptan PR (formulario web)

| Sitio | Qué es | Cómo |
| --- | --- | --- |
| **mcpservers.org** | La web de wong2 (su repo tiene los PRs cerrados) | Formulario "Submit" en https://mcpservers.org/ |
| **Glama** | https://glama.ai/mcp/servers — indexa repos de GitHub | Suele auto-indexar; si no aparece en unos días, hay botón de submit |
| **Smithery** | https://smithery.ai | Conecta el repo de GitHub desde su UI |
| **PulseMCP** | https://www.pulsemcp.com | Formulario de submit |

Texto para todos (una línea):

> Explainable AI-writing detection and originality checking, in English and Spanish. Returns every
> tell it matched — overused vocabulary, rhetorical crutches, syntactic patterns, sentence-rhythm
> burstiness — each with the evidence and a fix, instead of a black-box percentage. 4 of its 6 tools
> run entirely on the machine. .NET 10, MIT.

---

## 4. AlternativeTo

https://alternativeto.net/manage/suggest-app/

- **Name:** Signs of AI Writing
- **URL:** https://peopleworks.github.io/SignsofAI/
- **Alternative to:** GPTZero, Originality.ai, Copyleaks, ZeroGPT, Turnitin, QuillBot
- **Licencia:** Open Source (MIT) · **Precio:** Free
- **Plataformas:** Web, Self-Hosted (marca también Windows/Mac/Linux: corre en cualquier navegador)
- **Tags:** ai-detection, academic-integrity, plagiarism-checker, writing-assistant, privacy, spanish, open-source

**Descripción:**

> Most AI detectors are black boxes: they hand you a number ("87% AI") and nothing else. A teacher
> can't accuse on that, and a writer can't learn from it. Signs of AI Writing does the opposite — it
> highlights every tell in the text (overused vocabulary, rhetorical crutches, syntactic patterns and
> the statistical sentence-rhythm known as burstiness) and tells you how to fix each one.
>
> It's bilingual: the Spanish rule pack is derived from scratch, not machine-translated, so it catches
> how AI actually writes in Spanish. It runs 100% in your browser — your documents never leave your
> device, no account, no upload. It also includes an originality checker that compares documents
> against each other and highlights the shared passages, even paraphrases across languages, as
> evidence a human judges.
>
> Free and open source (MIT). Built with .NET 10 and Blazor WebAssembly.

---

## 5. Lanzamiento de contenido (el material ya está renderizado)

Orden sugerido, con un día entre pasos:

1. **Artículo** en tus dos blogs + Medium + dev.to (`Docs/Blog/signsofai-maker-story.*`)
2. **Shorts** en goteo diario (YouTube Shorts, Reels, TikTok) — textos en `Docs/Blog/PUBLICACION.md`
3. **Video grande** (ES y EN), enlazando al artículo
4. **Show HN** — con la tarjeta social ya subida y el artículo publicado

**Show HN (título):**
```
Show HN: An AI-writing detector that shows its work, and speaks Spanish
```

**Primer comentario (el que hace o rompe un Show HN):**
```
Author here. I built this because the AI detectors I could point students at all return a bare
number. "87% AI" is unfalsifiable: a teacher can't accuse on it, a student can't appeal it, and a
writer can't learn anything from it. And in Spanish they get noticeably worse, because almost all of
them think in English first.

So this one shows the evidence instead. It highlights the specific tells it found — overused
vocabulary, rhetorical crutches ("it's not just X, it's Y"), participial padding, and the statistical
rhythm of sentence lengths (burstiness: humans vary a lot, models don't) — and for each one it says
what to do about it. The Spanish rule pack is derived from scratch rather than translated.

It runs entirely in the browser (Blazor WebAssembly, .NET 10) — nothing is uploaded, no account. The
honest caveat: this is a heuristic linter, not a verdict machine. It's meant to give a human
something concrete to look at and argue with. I'd rather it be wrong in a way you can see than right
in a way you can't.

Code is MIT: https://github.com/peopleworks/SignsofAI
There's also an MCP server so you can run the same analysis from Claude.
```

**Reddit** (uno por día, no todos de golpe): r/dotnet y r/Blazor (ángulo técnico: Blazor WASM,
sin backend), r/Professors y r/Teachers (ángulo: evidencia en vez de porcentaje — leer las reglas
de cada sub antes, varios prohíben self-promotion), r/programacion y r/españa (ángulo: el español
como ciudadano de primera).

---

## 6. Shorts en inglés — bloqueados por la red

Los tres shorts EN están escritos y con layout validado (`Docs/Blog/shorts/en/*.html` +
`scenes/short*-en.json`, voz Rachel). No se pudieron renderizar porque el appliance de SSL-decrypt
de Waubonsee intercepta `api.elevenlabs.io` y Node rechaza el certificado.

Fuera de esa red (hotspot del teléfono), son 3 minutos:

```bash
cd Docs/Blog/shorts
node build-short.mjs scenes/short1-gancho-en.json
node build-short.mjs scenes/short2-burstiness-en.json
node build-short.mjs scenes/short3-espanol-en.json
```

---

## 7. Estado de los PRs de directorios (8 abiertos)

| Repo | ⭐ | PR |
| --- | ---: | --- |
| punkpeye/awesome-mcp-servers | 91k | [#10839](https://github.com/punkpeye/awesome-mcp-servers/pull/10839) |
| TensorBlock/awesome-mcp-servers | 790 | [#1377](https://github.com/TensorBlock/awesome-mcp-servers/pull/1377) |
| AdrienTorris/awesome-blazor | — | [#713](https://github.com/AdrienTorris/awesome-blazor/pull/713) |
| quozd/awesome-dotnet | — | [#1480](https://github.com/quozd/awesome-dotnet/pull/1480) |
| karanb192/awesome-claude-skills | — | [#168](https://github.com/karanb192/awesome-claude-skills/pull/168) |
| travisvn/awesome-claude-skills | — | [#1032](https://github.com/travisvn/awesome-claude-skills/pull/1032) |
| ComposioHQ/awesome-claude-skills | — | [#1423](https://github.com/ComposioHQ/awesome-claude-skills/pull/1423) |
| mingrath/awesome-claude-skills | — | [#28](https://github.com/mingrath/awesome-claude-skills/pull/28) |

Cerrados por el camino: **wong2** y **appcypher** tienen PRs deshabilitados en el repo → van por
formulario web (sección 3).
