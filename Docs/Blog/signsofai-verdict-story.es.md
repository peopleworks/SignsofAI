---
title: "Mi detector de IA no emitió un veredicto ni una sola vez, y nada lo notó durante semanas"
description: "El mismo texto, el mismo motor, la misma ejecución: 90/100 y «Señales fuertes de escritura con IA» en pantalla, y ningún veredicto en el documento que un profesor imprime. Una condición era falsa para todos los documentos, y ninguna de 340 pruebas comparaba las dos caras."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/verdict-story-cover.png"
tags: [ia, testing, integridadacademica, dotnet]
author: "Pedro Hernández (PeopleWorks)"
lang: es
---

# Mi detector de IA no emitió un veredicto ni una sola vez, y nada lo notó durante semanas

Este es el mismo texto, pasado por la misma compilación de mi propia herramienta, en el mismo minuto.

La línea de comandos:

```
  90/100  Strong signs of AI writing   (23 signals, English)
```

El informe — el documento que un profesor exporta, imprime y lleva a una comisión de integridad académica:

```
  **89.9/100**

  *Por debajo del umbral que esta compilación puede respaldar, no se emite ningún veredicto.*
```

Un motor. Dos respuestas. Y la segunda no era un caso raro: el informe exportado **no había emitido un veredicto nunca, para ningún documento, en ningún idioma, desde que la función existe**.

## Un `if` que siempre era falso

El informe retiene el veredicto por debajo del umbral que la compilación puede respaldar. Eso es deliberado y sigo pensando que está bien: una puntuación sin su tasa de error al lado es exactamente lo que este proyecto existe para criticar.

La comprobación era esta:

```csharp
return c.For(result.Language)?.RecommendedThreshold is { } threshold
    && result.OverallScore >= threshold;
```

Léala despacio. Pide el umbral medido **para ese idioma concreto**.

Mi corpus de calibración tiene 90 textos publicados antes de que existiera la escritura con IA: 65 en inglés, 25 en español. Para acotar una tasa de falsos positivos por debajo del 5% sin nada marcado, la estadística necesita unos 75 textos en el grupo. Ninguno de los dos grupos llega a 75. Así que el umbral por idioma es `null` en inglés, `null` en español, y la condición que viene después es falsa. Siempre. Para todo el mundo.

Yo había escrito una regla cuidadosa —*nunca cites una tasa medida sobre otro idioma*— y luego la apliqué a una pregunta que esa regla no gobernaba. El resultado fue un informe que se negaba a hablar de nada, jamás, mientras la pantalla de al lado hablaba de todo sin problema.

## Por qué no lo detectó nada

Dos razones, y las dos son más interesantes que el fallo.

**Las bandas vivían en nueve sitios.** Los umbrales del veredicto estaban escritos a mano en el resultado del análisis, en el informe, en el localizador de la interfaz, en el color del CLI, en dos condicionales de la página web, en la página de lote, en el panel de reescritura. Sincronizados por un comentario: *«refleja las bandas de AnalysisResult.Verdict»*.

Un comentario no es un mecanismo. Ya se habían desviado. La página de lote cortaba en 40 donde todo lo demás cortaba en 45, así que una misma compilación podía colorear el mismo documento de dos formas según en qué página lo abrieras.

**Nada comparaba las caras entre sí.** Tenía 340 pruebas. Todas comprobaban una superficie contra sus propias expectativas. Ninguna preguntaba si el CLI, la página web y el informe exportado decían lo mismo sobre el mismo texto. La contradicción no estaba escondida en un rincón: era lo más ruidoso del producto, y era invisible porque ninguna prueba apuntaba ahí.

Esa es la lección aprovechable, y me salió gratis solo porque nadie usaba todavía el informe. Si publica un motor detrás de varias caras, escriba la prueba que pasa una entrada por todas y falla cuando no coinciden. Son diez líneas. La mía falla en el commit anterior, que es como sé que comprueba algo.

## La parte que sí daba vergüenza

Debajo del fallo había algo peor, y llevaba dos días sabiéndolo sin conectar ambas cosas.

Este proyecto publica un umbral medido. De la página de calibración: **en 25/100, la herramienta marca como mucho el 5% de la escritura que se sabe humana** — 0 de 90 textos, con un intervalo del 95% que llega al 4,1%.

El producto trazaba su línea en **20**, y en prosa: *«Señales leves de escritura con IA»*. Ese número lo elegí a mano, pronto, antes de que existiera nada contra lo que elegirlo. Un texto humano del corpus puntuaba por encima. El texto humano más alto de todo el corpus llegó a 23,4, cómodamente dentro del rango que el producto llamaba «señales leves de escritura con IA».

Publicar una cifra calibrada y entregar otra sin calibrar es exactamente el fallo que le critico a otros detectores. Lo tenía en mi propio repositorio, en público, durante semanas, en la misma página que la medición.

## Dos revisores, un desacuerdo, y la distinción que lo resolvió

Ahora hago revisiones de diseño antes de escribir código, con dos modelos independientes a los que doy el mismo encargo y la orden de atacarlo. Los dos encontraron por su cuenta la condición siempre falsa, sin que nadie se la señalara. Ese es el argumento entero a favor de la práctica: yo había mirado ese método y no lo había visto.

Y luego discreparon, que fue más útil que si hubieran coincidido.

Uno dijo: cuando un idioma no tenga umbral propio, usa el agregado. Si no, la herramienta espera años por el español y no ayuda a nadie.

El otro dijo: no — el código tiene una regla explícita contra tomar prestado el agregado, y o la respetas o la borras, pero no la rompes en silencio.

Los dos tenían razón sobre cosas distintas, y me costó vergonzosamente ver la diferencia:

- Tomar prestada la **tasa de error** agregada tergiversa la fiabilidad. Decirle a quien escribe en español que su ensayo lo juzgó una herramienta que se equivoca el 4,1% de las veces, cuando la medición solo para español respalda un 13,3%, es entregarle una cifra tres veces mejor que cualquier cosa medida sobre su idioma. Eso sigue prohibido.
- Tomar prestada la **frontera** agregada no afirma nada sobre fiabilidad. Decide cuándo la herramienta abre la boca. Está medida, está publicada, y se imprime en la página junto a la cifra propia del idioma.

El mismo número, dos actos distintos. Uno es una afirmación sobre cuánto me equivoco. El otro es una raya en el suelo.

Así que la frontera ahora se presta y la tasa nunca — y hay tres estados, no dos. Un idioma **presente** en el corpus toma la línea y lleva su propia cota. Un idioma **ausente** no recibe veredicto con ninguna puntuación, porque no habría nada en la página que corrigiera la impresión que deja un veredicto. Una compilación sin calibración propia no dice nada de nada.

El revisor que proponía el respaldo plano habría roto ese tercer caso. Comprobé en vez de asentir, y una prueba que estaba bien lo atrapó.

## Cuatro bandas se volvieron dos

La escala vieja decía «fuertes», «moderadas», «leves», «parece escrito mayormente por una persona». Cuatro grados con sonido de medición.

Exactamente una de esas fronteras estaba medida. Mi corpus puede situar la línea donde termina la escritura humana y **no dice absolutamente nada** sobre el 45 ni sobre el 70. Ningún texto conocidamente humano se acercó a menos de veinte puntos de ninguno de los dos. Graduar «moderadas» frente a «fuertes» exigiría un corpus de texto escrito por máquina, y esa misma página de calibración argumenta largamente contra reunir uno jamás: muestrea los modelos que estuvieran a mano ese mes, envejece mal y adula a quien lo montó.

El compromiso obvio era conservar las palabras y añadir una nota al pie admitiendo que no están medidas. Estuve a punto. Lo que lo mató: **la nota al pie se lee una vez y el titular se lee siempre.** Una página cuyo encabezado dice «Señales fuertes de escritura con IA» y cuya letra pequeña dice «esto no lo podemos medir» no ha sido honesta: ha sido honesta en un sitio donde nadie mira.

Así que por encima de la línea la herramienta ahora dice «Señales de escritura con IA» y deja que los hallazgos carguen con el peso, que es para lo que están los hallazgos.

Por debajo de la línea estaba lo más revelador. El informe decía *«Parece escrito mayormente por una persona»*. La interfaz decía *«Señales mínimas de escritura con IA»*. El mismo estado, dos afirmaciones distintas, y la primera nunca me correspondió hacerla. Un detector que no detecta nada también devuelve una puntuación baja, y yo deliberadamente nunca he medido cuánta escritura de máquina caza esta herramienta. Las dos dicen ahora: **«Sin señales por encima del umbral medido.»** Una afirmación sobre la herramienta, no sobre la persona.

Era el último sitio del producto donde yo decía algo sobre un ser humano en lugar de sobre mi propio instrumento.

## El que de verdad importaba

Después fui a buscar qué había dejado obsoleto el cambio, y encontré algo que no tenía nada que ver con él.

El paquete para el docente — texto para el programa de la asignatura, una hoja para el estudiante, un procedimiento para comisiones de integridad — incluye un párrafo para que el profesor lo copie en una resolución disciplinaria. Le entregaba la tasa agregada: *«inferior al 4,1% en agregado»*. Fuera cual fuera el idioma del trabajo.

Para un ensayo en español, la cifra honesta es 13,3%.

A una comisión que juzgaba a un estudiante hispanohablante se le estaba entregando una herramienta tres veces mejor que la que realmente se usó, de puño y letra del profesor, en un documento que se lee en apelación. La hoja del estudiante tenía el mismo defecto: **las dos** ediciones citaban el número agregado, incluida la española, escrita para los estudiantes con más probabilidades de salir perjudicados por una tasa medida sobre todo en inglés.

Yo había arreglado exactamente ese fallo dentro del informe dos días antes. Lo arreglé en el código y lo dejé intacto en el documento que lleva la salida del código a una sala donde puede terminar el semestre de alguien.

Cada edición cita ahora su propia cifra, y dice que la agregada es más favorable y que por eso no se usa.

## Qué me llevo de esto

El cambio del veredicto no movió ninguna puntuación. Re-ejecuté la calibración antes y después y el archivo publicado es idéntico byte a byte: misma huella, mismos 90 textos, mismo umbral, mismo intervalo. Esa comprobación existe para que nadie, yo el primero, pueda mover una frontera y presentar el número mejorado como un logro.

Tres cosas que me quedo:

**Una entrada, todas las caras, una prueba.** Si varias caras comparten un motor, algo tiene que fallar cuando se contradicen. Yo tenía 340 pruebas y ninguna miraba ahí.

**Un comentario no es un mecanismo.** Nueve copias de un número sincronizadas por una frase en un bloque de documentación se desvían, y ya se habían desviado.

**Compruebe si su producto hace lo que dice su medición.** La distancia entre una cifra publicada y el comportamiento entregado es justo lo que construí esta herramienta para señalar en el trabajo de otros. Estaba en el mío, en público, al lado de la medición, durante semanas.

El código es MIT, el manifiesto del corpus y la calibración están en el repositorio, y el comando que los regenera es una línea: [github.com/peopleworks/SignsofAI](https://github.com/peopleworks/SignsofAI).
