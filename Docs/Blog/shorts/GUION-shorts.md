# Guion — Shorts verticales "SignsOfAI" (9:16, 1080×1920)

Pipeline: WikiKit `../../../../SISTEMA/Tools/WikiIllustrationKit/shorts/build-short.mjs`.
Cada short: HTML 1080×1920 (CSS keyframes) + `scenes/<id>.json` (narración ElevenLabs) → `out/<id>.mp4` + `.srt`.
Paleta SignsOfAI: `--bg:#0b1020` · léxico `#db2777` · retórico `#f59e0b` · sintáctico `#22b8cf` · estadístico `#f0556f` · brand `#6d7cff`.
3 shorts, ~25–35 s. Se hacen versiones ES y EN (mismo HTML, narración distinta).

---

## Short 1 — "87%, ¿y ahora qué?" (el gancho)
**Visual:** sello grande "87% IA" que late; se agrieta; abajo aparece la pregunta "¿Y ahora qué?"; cierre con marca + "muestra la evidencia".
- **ES:** "Un detector de inteligencia artificial te dice: ochenta y siete por ciento IA. ¿Y ahora qué? Un porcentaje no es evidencia. No puedes acusar, ni apelar, ni aprender de un número. Por eso construí SignsOfAI: te muestra cada pista y cómo arreglarla. Gratis, en tu navegador."
- **EN:** "An A I detector tells you: eighty-seven percent A I. Now what? A percentage is not evidence. You can't accuse, appeal, or learn from a number. So I built SignsOfAI: it shows you every tell and how to fix it. Free, in your browser."

## Short 2 — "El truco que delata a la IA" (burstiness — el más viral)
**Visual:** dos filas de barras: arriba parejas ("MÁQUINA · 0.1"), abajo desiguales ("HUMANO · 0.7"); resalta la diferencia; cierre marca.
- **ES:** "¿Quieres saber si algo lo escribió una IA? Mira el ritmo. Las máquinas hacen frases todas del mismo largo, quince a veinticinco palabras, una tras otra. Los humanos variamos: una larga, luego una corta. Eso se mide, se llama burstiness, y es la pista más difícil de falsificar. SignsOfAI te la muestra en un gráfico."
- **EN:** "Want to know if an A I wrote something? Look at the rhythm. Machines make sentences all the same length, fifteen to twenty-five words, one after another. Humans vary: a long one, then a short one. It's measurable, it's called burstiness, and it's the hardest tell to fake. SignsOfAI shows it as a chart."

## Short 3 — "Los detectores de IA no hablan español" (la cuña)
**Visual:** bandera "EN" tachando "ES"; luego dos packs de reglas lado a lado; palabras: sumérgete, cabe destacar, un rico tapiz de; cierre marca.
- **ES:** "Casi todos los detectores de IA piensan en inglés. Al español le dan una traducción tosca, y se pierden cómo suena la IA de verdad en nuestro idioma: sumérgete en el vasto mundo de, cabe destacar que, un rico tapiz de. SignsOfAI trae un pack de español derivado desde cero. Detecta la IA en tu idioma. Gratis y abierto."
- **EN:** "Almost every A I detector thinks in English. Spanish gets a clumsy translation, and they miss how A I really sounds in Spanish. SignsOfAI ships a Spanish rule-pack derived from scratch. Detect A I in your language. Free and open source."

## Short 8 — "Buscar y reemplazar apaga un detector" (el artefacto)
**Visual:** la palabra `delve` grande; una letra gira y se vuelve cirílica con marca roja; el contador
de señales cae 17 → 6; aparece el reporte de caracteres con `U+0435 · línea 37, col 64`; cierre con
marca y la frase "un hecho, no un porcentaje".
- **ES:** "Puedes apagar casi cualquier detector de inteligencia artificial con buscar y reemplazar. Cambia la e latina por la e cirílica: se ven idénticas, en pantalla no cambia nada, y la regla deja de encontrar la palabra. Siete detectores cayeron por debajo del azar con ese truco. Con el mío también funcionaba. Ahora SignsOfAI te da el código, la línea y la columna de cada carácter raro. Un porcentaje se discute; un carácter en la línea catorce está o no está."
- **EN:** "You can switch off almost any A I detector with find and replace. Swap the Latin e for the Cyrillic e: identical on screen, nothing looks different, and the rule stops finding the word. Seven detectors dropped below chance with that trick. Mine did too. Now SignsOfAI gives you the codepoint, the line and the column of every one. A percentage is arguable; a character at line fourteen either is there or is not."

> Los shorts 4 a 7 ya existen como HTML sin entrada aquí (traducción, dos idiomas, reescritura,
> escritorio); su copy está en `PUBLICACION.md`. **Orden del pipeline:** narrar primero
> (`narrate-all.mjs`), sacar los tiempos de cue (`cue-times.mjs`) y **después** escribir el HTML
> alrededor de la duración medida, porque los retardos de animación son absolutos.


---

## Build (una vez existan los HTML de cada short)
```bash
cd ../../../../SISTEMA/Tools/WikiIllustrationKit/shorts
node snap.mjs <html> _work/snap.png 20          # validar visual (sin gasto de voz)
node build-short.mjs scenes/short1-gancho-es.json   # voz + record + mux + srt
```
Requiere: Node, ffmpeg/ffprobe, Playwright, clave ElevenLabs. **Ver nota de seguridad sobre la clave.**
