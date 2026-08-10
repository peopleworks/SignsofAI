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
| 8 | Artefactos | ✅ | ✅ | ✅ 34,5 s · 30,0 s |
| 9 | Bibliografía inventada | ✅ | ✅ | ✅ 36,3 s · 35,5 s |
| 10 | 61% / línea base | ✅ | ✅ | ✅ 40,2 s · 37,4 s |
| 11 | El veredicto que nunca se dio | ✅ | ✅ | ✅ 35,5 s · 32,4 s |

Los renders están en `out/<id>.mp4` + `.srt`.

**Los 8–11 duran más que los 1–7** (29–40 s frente a 23–30). Siguen dentro del límite de
Shorts, pero el 10 es un tercio más largo que nada publicado antes: si la retención cae, el guion
es lo que hay que recortar, no la animación.

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



## Short 11 — "Un `if` que siempre era falso" (el veredicto que nunca se dio)
**Visual:** un documento arriba que se bifurca en dos tarjetas — igual que la portada del séptimo
artículo, para que quien vio una reconozca la otra. Izquierda: el contador sube a `90/100` y aparece
una barra ámbar sólida (el veredicto dicho). Derecha: el mismo `90/100` y una barra punteada
**tachada** (el veredicto ausente). Entra el `≠` entre las dos barras, no entre los números —
los números coinciden, ese es el chiste. Corte a la línea de código con
`RecommendedThreshold is { } threshold`, y `null` cayendo sobre ella en rojo, dos veces: `en`, `es`.
Cierre: `340 pruebas` y ninguna mirando ahí; luego la frase nueva, «Sin señales por encima del
umbral medido», apareciendo bajo las dos tarjetas ya iguales.
- **ES:** "Mi detector le puso noventa sobre cien a este texto y dijo: señales fuertes de escritura con inteligencia artificial. El informe que un profesor imprime y lleva a un comité, del mismo texto y en la misma ejecución, no dijo nada. Y no era un caso raro: no había emitido un veredicto nunca, en ningún idioma. Una condición pedía un umbral que ningún idioma tenía. Yo tenía trescientas cuarenta pruebas. Ninguna comparaba las dos caras entre sí."
- **EN:** "My detector scored this text ninety out of a hundred and called it strong signs of A I writing. The report a teacher prints and carries to a committee, same text, same run, said nothing at all. And that was not a rare case: it had never given a verdict, not once, in any language. One condition asked for a threshold that no language had. I had three hundred and forty tests. Not one of them compared the two faces."

**Por qué este short y no otro:** es el único de la serie donde la herramienta queda mal, y por eso
funciona. Los diez anteriores explican algo que hace bien; este cuenta que se contradecía a sí misma
y que lo publicamos igual.

## Short 12 — "La acusación que mi propio código se negaba a hacer" (el titular que contaba mal)
**Visual:** una bibliografía limpia de dos entradas, en blanco, con aire de estar bien. Encima cae el
titular real del informe en rojo: «1 contradicción de fuentes», y debajo la nota «el documento se
contradice a sí mismo». Corte al código: el campo `IsContradiction` con su propio comentario
resaltado —*«una entrada listada que nadie cita no lo es, porque la gente lista lectura adicional con
toda legitimidad»*— y al lado la línea del titular contando `Issues.Count` en vez de
`ContradictionCount`. Las dos, en pantalla, a la vez. Cierre: el informe nuevo diciendo «las dos
concuerdan» y, debajo, `5 días · v0.4.0 · 0 usuarios`.
- **ES:** "Esta bibliografía está bien. Dos referencias, una citada, y la otra es lectura adicional: la gente lista lectura adicional. Mi informe la anunció así: una contradicción de fuentes. El documento se contradice a sí mismo. En la página que un profesor imprime y lleva a un comité. Y lo peor no es el fallo: mi propio código sabía la diferencia. Hay un campo que la distingue, con un comentario que explica exactamente por qué. La línea que escribía el titular nunca le preguntó. Estuvo publicado cinco días, en una versión. No lo encontró ningún usuario, porque todavía no hay usuarios."
- **EN:** "This bibliography is fine. Two references, one cited, and the other is further reading: people list further reading. My report announced it like this: one source contradiction. The document disagrees with itself. On the page a teacher prints and carries to a committee. And the bug is not the worst part: my own code knew the difference. There is a field that draws the line, with a comment explaining exactly why. The line printing the headline never asked it. It shipped for five days, in one release. No user found it, because there are no users yet."

**Por qué este short:** es el segundo de la serie donde la herramienta queda mal, y es peor que el
once. El once contaba que no decía nada; este cuenta que dijo de más, y en la dirección que acusa.

## Short 13 — "Un informe que jura que nada se subió, pidiendo un píxel" (el escapado incompleto)
**Visual:** la última línea del informe, en cursiva y grande: «nada de esto se subió a ningún sitio».
Debajo, el ensayo del alumno con una línea suelta en monoespaciado: `![x](https://…/pixel.png)`.
El profesor pega el informe en el aula virtual —campo de comentario, botón enviar— y desde la
palabra sale una flecha hacia un servidor ajeno que devuelve `200 OK` y una hora exacta. Corte:
`\<` tachado en rojo, con el rótulo «esto bloquea HTML», y al lado `![](…)` intacto con «esto no es
HTML: es Markdown». Cierre: la línea escapada `!\[x](…)` y la frase final.
- **ES:** "Mi informe termina con esta frase: nada de esto se subió a ningún sitio. Y era verdad, hasta que el alumno escribe esto en su ensayo. No es HTML, es Markdown. Yo escapaba el HTML. El profesor pega el informe en el aula virtual, y el aula virtual va a buscar esa imagen al servidor de otro. Que ahora sabe que abrió el informe, y a qué hora. La promesa la rompía el propio documento analizado. Lo encontró un revisor al que le pedí una sola cosa: rómpelo."
- **EN:** "My report ends with this sentence: nothing here was uploaded anywhere. And it was true, until the student writes this in their essay. That is not HTML. That is Markdown. I was escaping the HTML. The teacher pastes the report into the learning platform, and the platform goes and fetches that image from somebody else's server. Which now knows they opened the report, and when. The promise was broken by the document being analysed. A reviewer found it, after I asked it one thing: break this."

## Short 14 — "Dos revisores, dos preguntas, cero solapamiento" (cómo se revisa con IA)
**Visual:** el mismo diff en el centro. Salen dos columnas: izquierda «¿afirma más de lo que midió?»,
derecha «encuentra una entrada que lo rompa». Van cayendo hallazgos en cada columna, cuatro y cinco,
en colores distintos. Al final las dos columnas se solapan sobre el centro y la intersección queda
**vacía**: un `0` grande. Cierre: dos briefs idénticos → una revisión; dos briefs distintos → nueve.
- **ES:** "Le pedí a dos modelos que revisaran el mismo código. A uno le pregunté: ¿esta página afirma más de lo que midió? Al otro: encuentra una entrada que la rompa. Encontraron nueve defectos entre los dos. Y ni uno solo coincidía. El de las afirmaciones no vio ningún problema de escapado. El que atacaba no vio ninguna sobreafirmación. Si les mando el mismo encargo a los dos, pago dos revisiones y recibo una. La lente importa más que el modelo."
- **EN:** "I asked two models to review the same change. One I asked: does this page claim more than it measured? The other: find an input that breaks it. Between them they found nine defects. Not one of them overlapped. The claims reviewer found no escaping bug. The attacker found no overclaim. Send both the same brief and you pay for two reviews and get one. The lens matters more than the model."

---

## Build (una vez existan los HTML de cada short)
```bash
cd ../../../../SISTEMA/Tools/WikiIllustrationKit/shorts
node snap.mjs <html> _work/snap.png 20          # validar visual (sin gasto de voz)
node build-short.mjs scenes/short1-gancho-es.json   # voz + record + mux + srt
```
Requiere: Node, ffmpeg/ffprobe, Playwright, clave ElevenLabs. **Ver nota de seguridad sobre la clave.**
