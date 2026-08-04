---
title: "Construí el detector de IA que hace otra pregunta"
description: "Los detectores marcan el 61% de los ensayos de quienes no tienen el inglés como lengua materna. El arreglo no es un clasificador mejor: es una pregunta mejor. No «¿se parece a una máquina?» sino «¿se parece a quien escribió los otros?». Esto es lo que costó, y el umbral que borré."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/baseline-story-cover.png"
tags: [ia, estilometria, integridadacademica, dotnet]
author: "Pedro Hernández (PeopleWorks)"
lang: es
---

# Construí el detector de IA que hace otra pregunta

Sesenta y uno por ciento.

Esa es la proporción de ensayos de quienes no tienen el inglés como lengua materna que los detectores de IA marcan como escritos por máquina. No el sesenta y uno por ciento de los tramposos. El sesenta y uno por ciento de los ensayos.

El mecanismo no tiene misterio. Estas herramientas aprendieron que la prosa formal, cuidada y un poco rígida se parece a un modelo de lenguaje. Y la prosa formal, cuidada y un poco rígida es exactamente lo que uno escribe en un idioma que aprendió después. La herramienta no está detectando IA. Está detectando una segunda lengua, y reportándola como deshonestidad.

Eso no se arregla con un clasificador mejor, porque el clasificador está contestando la pregunta que le hicieron. Se arregla haciendo otra pregunta.

## La pregunta

No: *¿se parece esto a una máquina?*

Sino: *¿se parece a la persona que escribió los otros?*

Esa inversión lo cambia todo. Un estudiante cuyo registro habitual es formal tiene una **línea base formal**. Medido contra sí mismo, lo formal no es una señal de alarma: es un martes. El mismo rasgo que lo condena bajo la primera pregunta lo exonera bajo la segunda.

El método no es nuevo. La Delta de Burrows, publicada en 2002, cuenta con qué frecuencia alguien echa mano de palabras pequeñas y corrientes —«de», «que», «aunque»— y compara esas tasas entre textos. La lingüística forense usa las palabras función precisamente porque ignoran el tema: una persona las emplea a su propio ritmo esté escribiendo sobre hidrología o sobre ópera.

Lo distinto es qué hice con el resultado.

## La Delta clásica hace una pregunta que yo no tengo autoridad para contestar

El montaje de manual le da a la Delta un conjunto de autores candidatos y pregunta a cuál se parece un texto. Llevado a un aula, eso se convierte en «¿es el estudiante, u otra persona?», y contestarlo exige un umbral: una raya pasada la cual el software dice *no es él*.

Alguien tiene que inventar esa raya. Sería yo. Y un número elegido por mí acabaría citado en una reunión disciplinaria como si hubiera bajado de una montaña.

Así que el reporte no fija ningún umbral. Pone dos cosas una al lado de la otra:

- cuánto se separa del centro el texto en cuestión;
- cuánto se separa de ese mismo centro **cada uno de los propios trabajos** de la persona, medido igual.

La escala es suya. Nadie tiene que aceptar mi idea de «demasiado lejos», porque no hay ninguna: solo está su propia variación, y dónde cae este trabajo dentro de ella.

Esta es salida real, uno de mis propios artículos apartado y comparado contra otros tres:

```
  0.654  dentro del propio rango de esta persona
  Distancia 0.654. Sus propios trabajos se separan hasta 1.18 de su centro,
  así que este queda dentro del rango que ya cubren.
  sus propios trabajos: 0.66 · 0.78 · 0.84 · 0.85 · 0.86 · 0.96 · 0.98 · 1.03 · 1.18
```

Eso se lee sin saber qué es una Delta.

## El sesgo que tuve que volver a quitar

La primera versión calculaba la dispersión propia midiendo cada trabajo contra las estadísticas de todos los trabajos, **incluido él mismo**.

Eso está mal, y mal en una dirección que importa. Una pieza incluida en las estadísticas contra las que luego se puntúa queda arrastrada hacia el centro. El rango de la persona sale artificialmente estrecho. Y un rango estrecho hace que el texto en cuestión parezca más fuera de lo que está.

El sesgo va contra la persona por la que se pregunta. Dejar uno fuera lo arregla: cada trabajo se mide contra los *otros*, exactamente como el texto revisado. Cuesta un bucle.

Lo cuento porque es la clase de error que nunca se anuncia. No se rompe nada. Los números parecen razonables. Simplemente alguien recibe un trato un poco peor del que la evidencia justifica, siempre, para siempre.

## El umbral que borré

Aquí viene la parte que no esperaba escribir.

La Delta es un promedio sobre todas las palabras medidas, y eso diluye. Lo probé contra un caso deliberadamente extremo —la Constitución de EE.UU. contra mis propios artículos— y quedó *en el borde* de mi rango en vez de fuera. Y sin embargo los rasgos de debajo estaban gritando: ese texto usa «of» **92 veces por cada mil palabras** donde yo la uso 13. Siete veces mi tasa. Un rasgo en z = 10.7, promediado hasta la invisibilidad por ochenta rasgos que casualmente coincidían.

Así que añadí una segunda medida que no promedia nada: **cuántas palabras usa el texto a un ritmo al que la persona nunca las ha usado**, en ninguno de sus trabajos. No una estadística. Un rango y un número, comprobables contando.

La separación fue limpia. Mis propios artículos, apartados de uno en uno: 0, 1, 1 y 3 palabras fuera de mi rango, sobre unas 80. La Constitución: 14 de 93.

Y entonces quise meterlo en el veredicto. Un cuarto de las palabras fuera del rango, digamos, y el promedio deja de tener la última palabra.

Lo borré.

Porque ¿de dónde salía «un cuarto»? De mí, mirando cinco documentos y eligiendo un número que los separaba. Eso no es calibrar. Es exactamente la jugada que este proyecto se niega a hacer en todo lo demás — y habría quedado enterrada en una constante arriba de un archivo, haciendo daño en silencio en casos que nunca probé.

El conteo se reporta. Se dice en el resumen con palabras normales. Una persona lo lee. La colocación la decide una sola regla que cualquiera puede repetir en voz alta: *¿está esto más lejos del centro de la persona que sus propios trabajos?*

Hay un comentario en el código diciéndolo, y una prueba llamada `That_count_does_not_decide_the_placement` para que nadie lo cablee más adelante como mejora. Calibrarlo con honestidad exigiría un corpus de textos con autoría conocida, que es un trabajo aparte y no algo que se finja con cinco archivos y una tarde.

## Se niega a contestar más veces de las que contesta

Unas 1.400 palabras de trabajo anterior repartidas en piezas suficientes, y 300 en la entrega. Por debajo de eso lo dice y no devuelve nada.

Es una limitación de verdad y será el resultado más frecuente en la práctica. Un profesor con un solo trabajo previo de un estudiante no puede usar esto.

Lo prefiero a la alternativa. Una distancia calculada sobre cuatrocientas palabras es ruido, y el ruido con un decimal puesto es justo lo que la gente se cree.

## Lo que el código no le deja decir

No hay ningún resultado que signifique *lo escribió otra persona*. Ni desactivado, ni escondido tras un flag: la enumeración tiene cuatro valores y ninguno es ese, y hay una prueba que verifica la lista.

```csharp
Assert.Equal(["Undetermined", "WithinRange", "AtTheEdge", "BeyondRange"], names);
```

El estilo cambia con la tarea. Con el género. Con el plazo, un coautor, un corrector, una buena noche de sueño, y con que alguien simplemente mejore escribiendo, que se supone que es el objetivo del ejercicio. Un texto fuera del rango es una razón para preguntar qué cambió. No es prueba, y de un tipo que no puede expresar la acusación no se puede leer la acusación por error.

El consejo que se imprime debajo de cada resultado dice lo mismo, en el idioma en que se escribió el texto:

> El estilo cambia con la tarea, con el género, con el plazo y con que una persona simplemente mejore. Un texto fuera del rango es una razón para preguntar qué cambió, nunca una conclusión sobre quién lo escribió — y un texto dentro del rango es el resultado más útil, porque es el que zanja una sospecha.

## El resultado por el que construí esto de verdad

Todo lo anterior va sobre la dirección acusatoria, porque ahí es donde vive el daño. Pero la dirección útil es la otra.

Hay un estudiante bajo sospecha. El profesor tiene tres ensayos suyos anteriores. El nuevo cae dentro del rango que esos tres cubren entre sí, y el reporte lo dice con los números al lado.

Eso no es una detección. Es una sospecha que termina en silencio, sobre evidencia, antes de convertirse en una reunión — y nadie tuvo que discutir de estadística.

Zanjar sospechas es la mayor parte de lo que debería hacer un proceso de integridad, y es lo único que casi ninguna herramienta de esta categoría está construida para hacer. Todas están optimizadas para encontrar algo. Esta da lo mejor de sí cuando no encuentra nada, y lo dice con claridad suficiente para que un profesor cierre la pestaña y se vaya a casa.

Las listas de palabras función viven en los packs de reglas como JSON, así que añadir un idioma es un pull request y ningún compilador. MIT, motor y pruebas incluidos, en [GitHub](https://github.com/peopleworks/SignsofAI).

---

*Escrito por un humano y revisado con la herramienta que describe: **7/100, mayormente humano**, variabilidad 0.67, unas 1.600 palabras. La comparación citada arriba es la propia familia de artículos de este blog, medida de verdad — y el que salió en 0.654 es el primer artículo que escribí sobre este proyecto.*
