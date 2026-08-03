---
title: "Mi detector de IA le puso 0/100 a este ensayo. La bibliografía era inventada."
description: "La puntuación estilométrica no encontró nada. Comparar el documento contra su propia lista de referencias encontró cinco contradicciones en medio milisegundo, sin conexión y sin enviar nada a ninguna parte — y una de ellas era un DOI que aparecía en dos artículos distintos."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/social-preview.png"
tags: [ia, integridadacademica, dotnet, educacion]
author: "Pedro Hernández (PeopleWorks)"
lang: es
---

# Mi detector de IA le puso 0/100 a este ensayo. La bibliografía era inventada.

Este es el encabezado de un reporte que produjo mi propia herramienta la semana pasada.

```
  0/100  Reads mostly human   (0 signals, English)
  words 157 · sentences 21 · burstiness 0.75 · lexical diversity 0.66
```

Cero señales. El vocabulario estaba bien. El ritmo de las frases era variado y humano. Según todas las medidas que tiene el detector, ese ensayo lo escribió una persona.

Este es el resto del mismo reporte.

```
  Sources  5 contradiction(s)
    ! línea 5:  "Adeyemi, 2023" se cita en el texto pero no aparece en la lista de referencias.
    ! línea 10: "Delacroix-Barrios, 2025" se cita en el texto pero no aparece en la lista.
    ! línea 19: 2 referencias llevan el DOI 10.1080/aie.2022.4471. Un DOI identifica una sola
                obra, así que como mucho una de ellas está bien.
    ! línea 23: "10.55/pending" no puede ser un DOI.
    ! línea 23: Fechada en 2027, que todavía no ha ocurrido (2026).
```

Dos autores citados que no están en ninguna parte de la bibliografía del propio documento. Un DOI en dos artículos distintos. Un identificador mal formado. Una fuente publicada el año que viene.

La puntuación no dijo nada. Las fuentes lo dijeron todo.

## Por qué un porcentaje no le sirve a un profesor

Ya he escrito antes sobre por qué me niego a entregarle a nadie un número de confianza. La versión corta: los detectores marcan como IA el 61% de los ensayos de quienes no tienen el inglés como lengua materna, un ataque de paráfrasis baja un detector publicado de 70.3% de exactitud a 4.6%, y hasta Turnitin dice que su propia puntuación no debe ser la única base de una acción contra un estudiante.

Pero hay un problema más simple, y no tiene nada que ver con la exactitud. **Un profesor no puede actuar sobre un porcentaje.** Imagine la reunión. «El software dijo 87%.» ¿Dijo qué, exactamente? ¿Basándose en qué? Enséñeme la parte que es IA. No puede. Usted tiene un número y una mala sensación, y el estudiante tiene una carrera.

Ahora imagine la otra reunión. «Usted cita a Adeyemi 2023 tres veces. En su lista de referencias no hay ningún Adeyemi. ¿Me manda el artículo?»

Eso no es una acusación. Es una pregunta, y se contesta en una frase. Si el artículo existe, el estudiante reenvía un PDF y todos siguen con su tarde. Si no existe, nadie tuvo que discutir de estadística.

Esa diferencia es la razón entera de que esta función exista.

## Lo que no esperaba: no hace falta internet

Mi primer diseño mandaba cada referencia a Crossref para comprobar si resolvía. Es el enfoque obvio y me incomodaba, porque la promesa entera de esta herramienta es que nada sale de su equipo, y «salvo las bibliografías de sus estudiantes» es un asterisco de verdad.

Después miré lo que pasa de verdad cuando una bibliografía es inventada, y resultó que internet sobraba casi siempre. **Una lista de referencias inventada se contradice a sí misma antes de que a nadie le dé tiempo de preguntar si los artículos existen.**

Los fallos son estructurales, no factuales:

- Un apellido aparece en la prosa y en ninguna parte de la lista. Este es con diferencia el más frecuente. El texto y la bibliografía se producen en pasadas distintas, y se separan.
- El mismo DOI aparece en dos obras. Un DOI identifica una cosa; dos entradas con el mismo no pueden estar bien las dos. Esto es **muy** habitual en bibliografías generadas, que reutilizan patrones de identificador igual que reutilizan giros.
- Un DOI que no tiene forma de DOI. El estándar es estricto: `10.`, un registrante de cuatro a nueve dígitos, una barra, un sufijo no vacío. `10.55/pending` falla en el registrante.
- Un año de publicación que todavía no ha ocurrido.
- El texto cita `[7]` y la lista tiene cinco entradas.

Nada de eso requiere una búsqueda. Todo se decide desde el documento solo, en el navegador, sin conexión, sin que nada salga a ninguna parte. Es la misma forma que la comprobación de artefactos de caracteres que publiqué antes: **no una probabilidad, una contradicción.**

Comprobar que una referencia bien formada y coherente corresponde a un artículo *real* sí necesita una búsqueda. Ese es un paso aparte y opcional, y para entonces hay una cadena de cita que enviar en lugar del ensayo de alguien. Prefiero publicar la mitad gratis primero y ser honesto sobre dónde se detiene.

## Casi todo el trabajo fue en no acusar a nadie

El motor no es lo difícil. Cruzar dos listas es cosa de primer año. Lo difícil es que **un falso «le falta esta referencia» es mucho peor que uno que se escapa.** Manda a alguien a buscar algo que tiene delante, y basta con que ocurra una vez para que deje de creerle a la herramienta.

Tres casos me costaron casi el día, y dos de ellos eran fallos que encontré ejecutando la cosa sobre entrada realista y no sobre mis propios ejemplos ordenaditos.

**Los acentos.** Un estudiante escribe `(Martinez, 2020)` y la bibliografía dice `Martínez`. Si eso se reporta como fuente faltante, he construido una herramienta que señala justo a los escritores que este proyecto existe para dejar de señalar. La comparación ignora los acentos, y hay una prueba que se llama así.

**Bibliografías envueltas.** Saque una lista de referencias de un PDF y la sangría francesa desaparece, así que las entradas llegan repartidas en varias líneas. Mi primer separador trataba cualquier línea que empezara con mayúscula y llevara una coma como entrada nueva — lo que ascendió `Journal of Educational Measurement, 59(4), 512-538.` a referencia por derecho propio, y luego se quejó de que nadie la había citado. Una queja sobre una línea que el autor nunca escribió. El arreglo es exigir que una línea abra con algo con forma de *autor* antes de poder empezar una entrada.

**Años escondidos dentro de identificadores.** Este fue peor. El DOI `10.1080/aie.2022.4471` contiene `2022`, que no es un año de publicación ni tiene que ver con nada. Ese número suelto partía las entradas envueltas en el sitio equivocado — y con un DOI terminado en `.2027.` habría reportado una referencia perfectamente normal como publicada en el futuro. Una acusación montada enteramente a partir de un identificador. Ahora los enlaces y los DOI se quitan antes de leer cualquier año, y la prueba de regresión lo dice.

Hay además una lista de palabras que se ponen delante de un año sin ser el apellido de nadie: Table, Figure, Section, March, Tabla, Figura, Capítulo. Sin ella, `(Figura 2019)` se convierte en una cita de alguien llamado Figura, a quien luego se reporta como ausente de la bibliografía. Fabricar una acusación a partir de un pie de foto no es un fallo que quisiera publicar.

Y todo esto se niega a ejecutarse cuando no encuentra una lista de referencias. Adivinar dónde empieza una bibliografía produciría quejas sacadas del formato, así que un documento que no anuncia una recibe el conteo de sus citas y ninguna comprobación cruzada.

## No toca la puntuación

La misma regla que la comprobación de artefactos, y una prueba cuyo único trabajo es fallar si eso cambia.

La tentación es evidente: una bibliografía inventada es demoledora, el número debería subir. Pero una puntuación es un juicio, y un juicio se discute — como debe ser, porque es una lectura de la prosa. Que el apellido «Adeyemi» esté o no en una lista no se discute. Usted mira, y está o no está.

Si mete lo segundo dentro de lo primero, convirtió lo único accionable de la página en un porcentaje que nadie puede llevar a una reunión. Por eso van en paneles separados, se guardan separados, y el reporte dice lo que no es:

> Una fuente que falta en su propia bibliografía suele ser un descuido, y siempre le toca explicarla a quien escribió. Pida la fuente en sí: una real se consigue en segundos, y una inventada no.

## Dónde he aterrizado

Ya no creo que la pregunta interesante sea «¿escribió esto una máquina?». No creo que esa pregunta se pueda contestar de forma fiable, estoy bastante seguro de que no se puede contestar de forma justa, y estoy seguro del todo de que contestarla con un porcentaje no ayuda a nadie en la sala.

La pregunta útil es más estrecha y mucho más fácil: **¿este documento se sostiene?** ¿Sus fuentes existen en sus propias páginas? ¿Sus caracteres salen de un teclado? ¿Su bibliografía concuerda con su prosa?

Esas tienen respuesta. La respuesta la puede comprobar la persona a quien se le pregunta. Y convierten una confrontación en una conversación, que es lo que toda oficina de integridad académica lleva pidiendo desde 2023 y lo que casi ninguna herramienta de esta categoría entrega de verdad.

MIT y gratis, motor, reglas y pruebas, en [GitHub](https://github.com/peopleworks/SignsofAI). Si reporta una referencia faltante que no falta, ese es el reporte de fallo que más quiero — aquí no hay servidor ni telemetría, así que un humano contándomelo es la única forma en que me entero.

---

*Escrito por un humano y revisado con la herramienta que describe: **5/100, mayormente humano**, variabilidad 0.69, unas 1.610 palabras. El reporte de ejemplo de este artículo es salida real, no una maqueta; el ensayo detrás lo escribí yo para que estuviera mal a propósito.*
