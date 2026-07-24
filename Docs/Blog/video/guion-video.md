# Guion — Video explainer "SignsOfAI" (EN + ES)

**Formato:** 1280×720 (16:9), escenas HTML animadas + narración ElevenLabs (voz *Marcela* ES / voz EN a elegir) + ffmpeg. Pipeline: `../../../../Phoenix/Modelador/Docs/Blog/video/build-video.mjs`.
**Duración objetivo:** 3:00–3:40. **Dos manifiestos:** `guion.video.es.json` / `guion.video.en.json` (mismas escenas HTML, narración traducida).
**Paleta escenas:** tokens de SignsOfAI — `--bg:#0b1020` · léxico `#db2777` · retórico `#f59e0b` · sintáctico `#22b8cf` · estadístico `#f0556f` · brand `#6d7cff` · ok `#2fd27a`.
**Convención narración:** números/acrónimos en fonético ("cero a cien", "quince a veinticinco", "ce ele i", "punto siete dos").

---

## Escaleta (10 escenas incl. intro)

| # | id | Visual (escena HTML) | Narración ES | Narración EN |
|---|----|----------------------|--------------|--------------|
| 0 | `intro` | Marca SignsOfAI: el cuadro cónico de 4 colores late; tagline "Detecta la IA. Muestra la evidencia." | *(sin voz, 3.5 s)* | *(no voice, 3.5 s)* |
| 1 | `01-gancho` | Un párrafo se pega; aparece un sello "87% IA" que se agrieta y cae | "Pegas un párrafo en un detector de IA, y vuelve un número: ochenta y siete por ciento. ¿Y ahora qué? Un profesor no puede acusar con eso. Un escritor no puede aprender de eso." | "You paste a paragraph into an AI detector, and back comes a number: eighty-seven percent. Now what? A teacher can't accuse with that. A writer can't learn from it." |
| 2 | `02-dos-problemas` | Split: izquierda "caja negra" (candado), derecha bandera con "EN" tachando "ES" | "Los detectores tienen dos problemas. Esconden su razonamiento: un porcentaje no es evidencia. Y piensan en inglés; al español le dan una traducción tosca de reglas gringas." | "Detectors have two problems. They hide their reasoning: a percentage is not evidence. And they think in English; Spanish gets a clumsy translation of English rules." |
| 3 | `03-que-hace` | Editor con texto; cuatro chips se encienden: Léxico, Retórico, Sintáctico, Estadístico | "Así que construí lo contrario. SignsOfAI marca las pistas de la IA en cuatro familias, y por cada una te dice cuál es y cómo arreglarla. Es un linter, no un veredicto." | "So I built the opposite. SignsOfAI flags the tells of AI in four families, and for each one it tells you what it is and how to fix it. It's a linter, not a verdict." |
| 4 | `04-lexico` | Palabras resaltadas: delve, tapestry / sumérgete, cabe destacar → flechas a reemplazos | "Léxico: vocabulario sobreusado. Sumérgete, cabe destacar, un rico tapiz de. Cada palabra pesa según cuánto creció después de ChatGPT." | "Lexical: overused vocabulary. Delve, tapestry, multifaceted. Each word is weighted by how much it grew after ChatGPT." |
| 5 | `05-burstiness` | Gráfico de barras: barras parejas (máquina, 0.1) vs barras desiguales (humano, 0.7) | "Pero la señal en la que más confío es el ritmo. Las máquinas se acomodan en quince a veinticinco palabras por frase y ahí se quedan. Los humanos varían. Eso se mide: burstiness. Humano, cero punto siete; máquina, cero punto uno." | "But the signal I trust most is rhythm. Machines settle into fifteen to twenty-five words per sentence and stay there. Humans vary. You can measure it: burstiness. Human, zero point seven; machine, zero point one." |
| 6 | `06-privacidad` | Navegador con candado; "0 servidores · 0 subidas"; engranaje ".NET 10 · Blazor WASM" | "Y todo corre en tu navegador. Sin servidor, sin subir nada, sin cuenta. Tus documentos nunca salen de tu equipo. Reglas y estadística, no una caja negra neuronal." | "And it all runs in your browser. No server, no upload, no account. Your documents never leave your device. Rules and statistics, not a neural black box." |
| 7 | `07-espanol` | Dos packs de reglas lado a lado (EN/ES) con la misma estructura, sello "derivado desde cero" | "El español no está traducido; lo derivé desde cero, porque las pistas son distintas. Esa es la parte que ninguna herramienta solo-inglés puede copiar." | "The Spanish pack isn't translated; I derived it from scratch, because the tells are different. That's the part no English-only tool can copy." |
| 8 | `08-originalidad` | Dos documentos, pasajes compartidos resaltados; "evidencia, no acusación" | "Y hay más: un comparador de originalidad. Resalta los pasajes que dos textos comparten, incluso paráfrasis entre idiomas. La evidencia es el puntaje. Un humano juzga." | "And there's more: an originality checker. It highlights passages two texts share, even paraphrases across languages. The evidence is the score. A human judges." |
| 9 | `09-cierre` | Marca + URL grande; "Gratis · MIT · Pruébalo" | "Gratis, código abierto, para la comunidad educativa. Pruébalo en el enlace. Dale un párrafo y mira qué te dice." | "Free, open source, for the education community. Try it at the link. Give it a paragraph and see what it says." |

---

## Miniatura (thumbnail 1280×720)
- Fondo `#0b1020` con glows rosa/índigo. Sello "87% IA" agrietado a la izquierda; a la derecha, texto resaltado con chips de colores + "MUESTRA LA EVIDENCIA".
- Overlay: título grande "¿IA o humano?" / subtítulo "Detector explicable y bilingüe" / esquina: marca SignsOfAI.

## Paquete de publicación YouTube (rellenar al renderizar)
- **Asset:** `video/out/signsofai-explainer-es.mp4` (y `-en.mp4`) · ~3:20 · 1280×720.
- **Título:** "Detector de IA que MUESTRA la evidencia (y habla español) — SignsOfAI, gratis y en tu navegador".
- **Capítulos:** 00:00 Intro · 00:05 El problema · 00:30 Qué hace · 01:00 Burstiness · 01:40 Privado · 02:10 Español · 02:40 Originalidad · 03:05 Pruébalo.
- **Descripción:** gancho + "no vendo nada / educativo" + "Lo que verás" (bullets) + enlaces (demo, repo) + hashtags `#IA #DotNet #Blazor #IntegridadAcadémica`.
- **Enlace demo:** https://peopleworks.github.io/SignsofAI/ · **Repo:** https://github.com/peopleworks/SignsofAI

## Cómo construir (una vez existan las escenas HTML en `scenes/`)
```bash
cd Docs/Blog/video
# prueba una escena (barata):
node ../../../../Phoenix/Modelador/Docs/Blog/video/build-video.mjs guion.video.es.json 01-gancho
# preview mudo (sin gasto de ElevenLabs):
SILENT=1 node ../../../../Phoenix/Modelador/Docs/Blog/video/build-video.mjs guion.video.es.json
# render final:
node ../../../../Phoenix/Modelador/Docs/Blog/video/build-video.mjs guion.video.es.json
```
Requiere: Node, ffmpeg/ffprobe en PATH, Playwright (del node_modules de Xari), y clave ElevenLabs.

**Voces:** ES → *Marcela*. EN → *Rachel* (la voz de Xari que ya nos gusta). Para fijarla exacta al renderizar el EN:
```bash
ELEVENLABS_VOICE_ID=21m00Tcm4TlvDq8ikWAM node ...build-video.mjs guion.video.en.json
```
Clave verificada: NO commiteada, cuenta sin eventos (revisado 2026-07-23).
