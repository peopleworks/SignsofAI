---
title: "Construí un detector de escritura con IA que muestra sus pruebas — y habla español"
description: "Casi todos los detectores de IA son cajas negras que escupen un número. Construí uno que te muestra la evidencia, corre entero en tu navegador y trata el español como idioma de primera. Aquí está cómo, y por qué."
canonical_url: "https://peopleworks.com.do/2026/07/24/detector-de-escritura-con-ia/"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/maker-story-cover.png"
tags: [dotnet, blazor, ia, webassembly]
author: "Pedro Hernández (PeopleWorks)"
lang: es
---

# Construí un detector de escritura con IA que muestra sus pruebas — y habla español

Casi todos los detectores de IA son cajas negras. Pegas un párrafo, vuelve un número ("87% IA") y se supone que confíes. Un profesor no puede actuar con eso. Un escritor no puede aprender de eso. Y si escribiste en español, el número muchas veces es peor que lanzar una moneda.

Así que construí lo contrario. Se llama **SignsOfAI**, es gratis, corre 100% en tu navegador, y por cada señal que marca te dice *cuál* es la pista y *cómo arreglarla*. Pruébalo: [peopleworks.github.io/SignsofAI](https://peopleworks.github.io/SignsofAI/).

Esta es la historia de cómo funciona y las decisiones detrás.

![El puntaje sube en vivo de 76 a 87 mientras se acumulan las pistas de IA al escribir, y luego cada pista queda resaltada con su arreglo](https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/screenshots/analyze-live.gif)

*El puntaje se actualiza mientras escribes. Cada resaltado trae su sugerencia de arreglo.*

## La comezón: un puntaje que no puedes discutir es un puntaje que no puedes creer

Dos cosas me molestaban de los detectores que todo el mundo enlaza.

Primero, esconden su razonamiento. Un porcentaje no es evidencia. Si acusan a un estudiante de usar IA, "la herramienta dijo 87%" no es algo que puedas defender, apelar o corregir. El número se siente preciso y objetivo. No es ninguna de las dos.

Segundo, piensan en inglés. La investigación, los datos de entrenamiento, las pistas: todo en inglés. El español recibe una traducción mecánica de reglas gringas, que se pierde cómo *suena* de verdad la IA en español: "sumérgete en el vasto mundo de", "cabe destacar que", "no solo… sino también". Media humanidad escribe en otra lengua, y las herramientas tratan a esa mitad como una idea de último minuto.

Quería una herramienta **explicable, accionable y bilingüe**, que nunca fingiera ser un detector de mentiras.

## Qué hace, en concreto

SignsOfAI hace dos trabajos.

**1. Revisa el texto en busca de pistas de IA.** Pega, sube un `.docx`, o solo empieza a escribir. Mientras escribes, puntúa el texto de 0 a 100 y resalta las señales en cuatro familias:

- **Léxico** — vocabulario sobreusado. `sumérgete`, `panorama`, `multifacético`, `resaltar`, `aprovechar`. Cada palabra pesa según cuánto más común se volvió después de ChatGPT.
- **Retórico** — las muletillas. Paralelismo negativo ("no solo X, sino Y"), aperturas cliché ("en la era digital actual"), relleno ("cabe destacar que").
- **Sintáctico** — las estructuras. Evasión de cópula ("se erige como…" en vez de "es…"), construcciones infladas ("juega un papel crucial").
- **Estadístico** — el ritmo. De esto hablo abajo, porque es lo interesante.

Cada marca trae un arreglo concreto y la razón detrás. Es un linter, no un veredicto.

![El texto anotado con cada pista de IA resaltada, junto a la lista de recomendaciones que explica y arregla cada una](https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/screenshots/evidence.png)

*Todo el argumento en una captura: no "87% IA", sino cuáles palabras, por qué se marcaron, y qué escribir en su lugar.*

**2. Revisa originalidad.** Suelta dos o más documentos (una tesis y sus fuentes, o los trabajos de una clase entera) y resalta los pasajes que comparten: copias literales, y *paráfrasis reescritas, incluso entre idiomas*. El número que ves es exactamente lo que está resaltado. La evidencia **es** el puntaje. Un humano juzga; la herramienta nunca acusa.

![Matriz de solapamiento de la cohorte que muestra qué documentos comparten texto, con los pares más similares ordenados debajo](https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/screenshots/originality.png)

*Una clase entera de un vistazo: cada documento contra todos los demás, y luego los pasajes compartidos.*

## La señal en la que más confío: burstiness

Esta es la pista más difícil de falsificar y más fácil de medir. Los humanos escribimos con un ritmo desparejo. Una frase larga, con tres cláusulas y un inciso, y luego una corta. Después un fragmento. Las máquinas no. A su aire, un modelo se acomoda en unas 15 a 25 palabras por frase y ahí se queda, párrafo tras párrafo.

Eso se puede cuantificar como **burstiness**: el coeficiente de variación del largo de las frases. La prosa humana suele dar 0.6–0.8. La salida por defecto de un modelo se queda en 0.0–0.2. No necesita lista de palabras ni modelo; es pura estadística sobre los largos de frase. SignsOfAI lo calcula, lo muestra como un gráfico de barras por frase, y lo integra en el puntaje.

Por cierto: pasé este artículo por su propio linter. Da 33/100. Señales leves. Casi todas las marcas son las palabras que el propio texto cita, `sumérgete` y `tapiz` entre ellas, y la burstiness queda en 0.86, bien dentro del rango humano. La herramienta me mostró dónde me estaba desviando y lo corregí. Un porcentaje no hace eso.

## Por qué reglas, no una red neuronal

No entrené un clasificador. Fue a propósito.

Un motor de reglas y estadística es **explicable por construcción**. Cuando marca `sumérgete`, te puede decir *sumérgete*, mostrarte dónde, y darte tres reemplazos. Una red neuronal te da una probabilidad y un encogimiento de hombros. Para una herramienta cuya promesa entera es "muestro mis pruebas", la transparencia le gana a un par de puntos de precisión.

También significa que todo corre en el navegador. Sin servidor, sin subida, sin cuenta. Tus documentos nunca salen de tu equipo. Eso importa mucho cuando los documentos son ensayos de estudiantes o un manuscrito inédito. El motor es una librería pequeña de .NET puro (`SignsOfAI.Core`); el sitio es Blazor WebAssembly sobre .NET 10.

## El español como ciudadano de primera

El pack de reglas en español no está traducido. Lo derivé desde cero, porque las pistas son distintas. La IA en inglés ama `delve` y `tapestry`; la IA en español ama `sumérgete`, `cabe destacar`, `un rico tapiz de`, `se erige como`. Los patrones retóricos riman entre idiomas, pero las palabras no. El idioma se autodetecta, y ambos packs cargan los mismos pesos, severidades y evidencia.

Esta es la parte que ninguna herramienta solo-inglés puede copiar traduciendo una lista de palabras.

## El giro: convertir a un competidor en ventaja

Un tiempo después del lanzamiento encontré un proyecto llamado *no-ai-slop*, un skill viral para editar escritura de IA, miles de estrellas. Mi primera reacción fue la honesta: *ellos tienen miles, yo tengo tres.*

Luego miré de cerca. Es un solo archivo Markdown de reglas. Solo inglés. Sin puntaje, sin estadística, sin revisión de originalidad. Se volvió viral porque no tenía fricción y montó la ola de los "agent skills", no porque hiciera algo que mi motor no pudiera.

Así que no competí. **Extraje su taxonomía**, una veintena de patrones de escritura de IA, y la metí en mis packs de reglas (bilingües, ponderados, con evidencia), agregué un detector de abuso de guiones largos, y publiqué mi propio skill, `/signs-of-ai`, que hace la misma edición rápida pero delega al motor real para un veredicto medido. Misma ola. Mejor barco.

La lección: cuando el formato de alguien está ganando, no necesitas su formato. Necesitas su taxonomía y un cimiento más fuerte debajo.

## Pruébalo, rómpelo, extiéndelo

SignsOfAI es de licencia MIT y está hecho para la comunidad educativa y la de .NET.

- **Demo en vivo:** [peopleworks.github.io/SignsofAI](https://peopleworks.github.io/SignsofAI/) — corre en tu navegador, nada sale de tu equipo.
- **Código:** [github.com/peopleworks/SignsofAI](https://github.com/peopleworks/SignsofAI)
- **CLI:** `dotnet tool install --global SignsOfAI.Cli` y luego `signsofai check borrador.md` — filtra prosa en CI con `--max-score`.
- **Servidor MCP + skill de agente:** llama al motor desde Claude Desktop o cualquier cliente MCP, o suelta `/signs-of-ai` en tu editor.
- **Trae tus propias reglas:** pega una lista de palabras vetadas o un rule-pack JSON; se fusiona al vuelo.

Si enseñas, escribes o calificas, o solo quieres que tu prosa deje de sonar a máquina, dale un párrafo y mira qué dice. Y si encuentras una pista que se le escapa, los packs de reglas son dos archivos JSON. Los pull requests son bienvenidos.

*Hecho por Pedro Hernández — PeopleWorks, [Microsoft MVP para .NET](https://mvp.microsoft.com/en-US/mvp/profile/24060a02-dbc6-44ec-bca5-c213ff9835c5). Por y para la comunidad educativa.*
