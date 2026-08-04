# Guion — Shorts verticales "SignsOfAI" (9:16, 1080×1920)

Pipeline: WikiKit `../../../../SISTEMA/Tools/WikiIllustrationKit/shorts/build-short.mjs`.
Cada short: HTML 1080×1920 (CSS keyframes) + `scenes/<id>.json` (narración ElevenLabs) → `out/<id>.mp4` + `.srt`.
Paleta SignsOfAI: `--bg:#0b1020` · léxico `#db2777` · retórico `#f59e0b` · sintáctico `#22b8cf` · estadístico `#f0556f` · brand `#6d7cff`.
~25–30 s cada uno. Se hacen versiones ES y EN (HTML distinto por idioma: `shortN-*.html` y `en/shortN-*.html`).

## Estado

| # | Short | HTML | Narración | Render ES · EN |
|---|-------|------|-----------|----------------|
| 1 | El gancho (87%) | ✅ | ✅ | ✅ 26 s · 25,7 s |
| 2 | Burstiness | ✅ | ✅ | ✅ 30 s · 30 s |
| 3 | No hablan español | ✅ | ✅ | ✅ 28 s · 28 s |
| 4 | Traducción | ✅ | ✅ | ✅ 26 s · 29 s |
| 5 | Dos idiomas | ✅ | ✅ | ✅ 25 s · 26 s |
| 6 | Reescritura | ✅ | ✅ | ✅ 25 s · 23 s |
| 7 | Escritorio | ✅ | ✅ | ✅ 25 s · 23 s |
| 8 | Artefactos | — | guion abajo | pendiente |
| 9 | Bibliografía inventada | — | guion abajo | pendiente |
| 10 | 61% / línea base | — | guion abajo | pendiente |

Los renders están en `out/<id>.mp4` + `.srt`. Los 8–10 necesitan HTML antes de narrar.

**Orden del pipeline:** narrar primero (`narrate-all.mjs`), sacar los tiempos de cue
(`cue-times.mjs`) y **después** escribir el HTML alrededor de la duración medida, porque los
retardos de animación son absolutos. `build-short.mjs` reutiliza el mp3 salvo `--revoice`.

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

## Short 4 — "Tu idioma, en un archivo" (la invitación a contribuir)
**Visual:** "Habla inglés · español · ¿Y el tuyo?"; se abre `es.json` y las claves se traducen a la
vista (`"home.upload"` → `"Subir documento"`); tres negaciones — sin C#, sin compilar, sin saber
programar; "¿Falta una clave? Cae al inglés. Una traducción a medias ya sirve"; cierre "Tu idioma,
en un archivo · Gratis · Open Source".
- **ES:** "SignsOfAI habla inglés y español. ¿Y el tuyo? Las traducciones no están compiladas: son un archivo JSON. Copias el inglés, traduces las frases y mandas un pull request. Sin C sharp, sin compilar, sin saber programar. Si falta una clave, cae al inglés: una traducción a medias ya sirve. Tu idioma, en un archivo."
- **EN:** "SignsOfAI speaks English and Spanish. What about yours? The translations aren't compiled. They're a JSON file. Copy the English one, translate the phrases, open a pull request. No C sharp, no build step, no programming. Miss a key and it falls back to English, so a half-finished translation still ships. Your language, in one file."

## Short 5 — "Dos idiomas, a propósito" (lo que parece un bug)
**Visual:** un clic cambia toda la interfaz; luego la pantalla en español con un consejo en inglés
(`"delve" is heavily overused in AI writing`) y el rótulo "¿Un fallo?"; se parte en dos columnas —
*Idioma de la app: Español* / *Idioma del texto: Inglés*; cierre "Dos idiomas, a propósito".
- **ES:** "Un clic, y toda la interfaz cambia. Sin recargar, y se acuerda. Pero mira esto: la interfaz en español, y el consejo sigue en inglés. ¿Un fallo? No. Estás analizando texto inglés, y el consejo habla de palabras inglesas. Son dos idiomas independientes: el de la aplicación, y el de tu texto. A propósito."
- **EN:** "One click, and the whole interface changes. No reload, and it remembers. But look at this: the interface in Spanish, and the advice still in English. A bug? No. You're analysing English text, and the advice is about English words. Two independent languages: the app's, and your text's. On purpose."

## Short 6 — "Y cuando rompería la frase, no la cambia" (la reescritura que se niega)
**Visual:** se teclea y el marcador `/100` baja en vivo; tres negaciones — sin nube, sin clave, sin
esperar; se resalta la frase «…no es **solo** una herramienta, es un cambio de paradigma» con la
nota "esta no la toca · quitar *solo* invierte el sentido"; cierre "Y cuando rompería la frase, no
la cambia".
- **ES:** "Escribe, y mira el número bajar. Cada cambio ocurre en tu dispositivo: sin nube, sin clave, sin esperar. Pero fíjate en esta palabra. No la toca. Porque cambiarla rompería la frase, y prefiere no tocarla antes que devolverte una frase rota. Eso ningún humanizador de pago te lo cuenta."
- **EN:** "Type, and watch the number fall. Every change happens on your device: no cloud, no key, no waiting. But look at this word. It leaves it alone. Because changing it would break the sentence, and it would rather refuse than hand you broken prose. No paid humanizer tells you that."

## Short 7 — "Ya hay app de escritorio" (lo que el navegador no puede)
**Visual:** "Ya hay app de escritorio" y debajo la objeción "¿Para qué, si la web funciona?";
aparece el medidor de **Previsibilidad 86%** con el pie "modelo corriendo dentro de la app · 122 ms"
y las dos negaciones — sin servidor, sin conexión; luego una carpeta soltándose sobre la ventana con
`entrega-041.pdf`, `entrega-118.docx`, `entrega-007.odt` y "…y 197 más, ordenadas de peor a mejor";
cierre "Gratis, para Windows".
- **ES:** "Ya hay aplicación de escritorio. ¿Para qué, si la web funciona? Para esto: mide la previsibilidad con un modelo que corre dentro de la aplicación. Sin servidor, sin conexión, y tu texto no sale de la máquina. Y lee una carpeta entera: doscientas entregas, ordenadas de peor a mejor. Gratis, para Windows."
- **EN:** "There's a desktop app now. Why, if the web one works? For this: it measures predictability with a model running inside the app. No server, offline, and your text never leaves the machine. And it reads a whole folder: two hundred submissions, sorted worst first. Free, for Windows."

## Short 8 — "Buscar y reemplazar apaga un detector" (el artefacto)
**Visual:** la palabra `delve` grande; una letra gira y se vuelve cirílica con marca roja; el contador
de señales cae 17 → 6; aparece el reporte de caracteres con `U+0435 · línea 37, col 64`; cierre con
marca y la frase "un hecho, no un porcentaje".
- **ES:** "Puedes apagar casi cualquier detector de inteligencia artificial con buscar y reemplazar. Cambia la e latina por la e cirílica: se ven idénticas, en pantalla no cambia nada, y la regla deja de encontrar la palabra. Siete detectores cayeron por debajo del azar con ese truco. Con el mío también funcionaba. Ahora SignsOfAI te da el código, la línea y la columna de cada carácter raro. Un porcentaje se discute; un carácter en la línea catorce está o no está."
- **EN:** "You can switch off almost any A I detector with find and replace. Swap the Latin e for the Cyrillic e: identical on screen, nothing looks different, and the rule stops finding the word. Seven detectors dropped below chance with that trick. Mine did too. Now SignsOfAI gives you the codepoint, the line and the column of every one. A percentage is arguable; a character at line fourteen either is there or is not."


## Short 9 — "0/100, y la bibliografía inventada" (las fuentes)
**Visual:** el marcador grande `0/100 · 0 señales` en verde, con aire de "todo bien"; se desliza
hacia arriba y debajo aparece el bloque de fuentes con cinco `!` en ámbar, uno a uno; se resalta
`2 referencias llevan el mismo DOI`; cierre con marca y "no un porcentaje: una pregunta".
- **ES:** "Mi propio detector le puso cero sobre cien a este ensayo. Cero señales. Vocabulario bien, ritmo humano. Y la bibliografía era inventada. Dos autores citados que no están en su propia lista de referencias. El mismo DOI en dos artículos distintos. Una fuente publicada en dos mil veintisiete. Nada de eso necesita internet: el documento se contradice a sí mismo. Y eso no es una acusación, es una pregunta que se contesta en una frase: ¿me manda el artículo?"
- **EN:** "My own detector scored this essay zero out of a hundred. Zero signals. Good vocabulary, human rhythm. And the bibliography was invented. Two authors cited that are nowhere in its own reference list. The same D O I on two different papers. A source published in twenty twenty-seven. None of that needs the internet: the document contradicts itself. And that is not an accusation, it is a question you answer in one sentence: can you send me the paper?"


## Short 10 — "61%" (la línea base del estudiante)
**Visual:** el número **61%** enorme, en rojo, con la leyenda «de los ensayos de quienes escriben en
segunda lengua, marcados como IA»; se rompe; aparecen dos preguntas enfrentadas — «¿se parece a una
máquina?» tachada, «¿se parece a quien escribió los otros?» resaltada; cierre con la barra de rango
y una marca cayendo dentro, en verde.
- **ES:** "Sesenta y uno por ciento. Esa es la proporción de ensayos de estudiantes que escriben en su segunda lengua que los detectores marcan como inteligencia artificial. No de los tramposos: de los ensayos. Porque escribir formal y cuidado se parece a una máquina, y así es como escribes en un idioma que aprendiste después. Eso no se arregla con mejor detector. Se arregla con otra pregunta: no si se parece a una máquina, sino si se parece a quien escribió los otros trabajos. Y si escribes formal, tu propia línea base ya es formal."
- **EN:** "Sixty-one percent. That is the share of essays by students writing in their second language that AI detectors flag as machine-written. Not of the cheats: of the essays. Because formal, careful writing looks like a machine, and that is how you write in a language you learned second. You do not fix that with a better detector. You fix it with a different question: not does this look like a machine, but does this look like the person who wrote the others. And if you write formally, your own baseline is already formal."


---

## Build (una vez existan los HTML de cada short)
```bash
cd ../../../../SISTEMA/Tools/WikiIllustrationKit/shorts
node snap.mjs <html> _work/snap.png 20          # validar visual (sin gasto de voz)
node build-short.mjs scenes/short1-gancho-es.json   # voz + record + mux + srt
```
Requiere: Node, ffmpeg/ffprobe, Playwright, clave ElevenLabs. **Ver nota de seguridad sobre la clave.**
