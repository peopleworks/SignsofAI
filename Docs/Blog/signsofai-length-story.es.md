---
title: "Mi detector de IA le puso 94 sobre 100 a un pasaje, y le prohibí decir nada"
description: "La frontera con la que juzga se midió sobre documentos de 662 palabras en adelante. Se estaba aplicando a un párrafo pegado, y el error va en una sola dirección: la misma escritura marca 0 de 32 entera y 6 de 32 como extractos de sí misma. El arreglo no fue un modelo mejor. Fue callar."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/length-story-cover.png"
tags: [ia, estadistica, integridadacademica, dotnet]
author: "Pedro Hernández (PeopleWorks)"
lang: es
---

# Mi detector de IA le puso 94 sobre 100 a un pasaje, y le prohibí decir nada

La semana pasada alguien me escribió por un número de versión. Creía que la app de Windows estaba desactualizada, y tenía razón, por un motivo que ninguno de los dos esperaba. Persiguiéndolo acabé mirando qué dice mi propia herramienta sobre lo que la gente hace con ella más que ninguna otra cosa: pegar un párrafo y no tocar nada más.

Dice bastante. No debería.

Esto es lo que hace ahora, con un pasaje cargado de todas las señales que el catálogo conoce:

> **Sin veredicto a esta longitud**
> 23 señales encontradas · analizado como Inglés
> Este texto tiene 66 palabras. La frontera se midió solo sobre textos de 662 palabras o más, así que no se da veredicto. La puntuación no es prueba de que lo escribiera una máquina ni de que lo escribiera una persona.

La puntuación sigue ahí, en gris: **94 sobre 100**. Las 23 señales siguen listadas, cada una con la frase que la disparó. Lo que desapareció es la oración que acusa.

## Sobre qué se midió la frontera

Este proyecto publica con qué frecuencia se equivoca sobre una persona. Noventa textos, todos publicados antes de que existieran los modelos generativos, y con un umbral de 25 sobre 100 no marca ninguno, con un intervalo del 95% que llega al 4,1%. Ese número es la razón por la que alguien debería tomarse en serio todo lo demás.

Nunca había mirado la *forma* de esos noventa textos. Así que la miré.

```
 el más corto      662 palabras
 mediana         2.772 palabras
 el más largo    9.328 palabras
 por debajo de 600       ninguno
```

Ahí está. La frontera se ajustó sobre documentos y se estaba aplicando a párrafos. Cada veredicto sobre un extracto pegado era una extrapolación hacia una población que el corpus no contiene.

## Y la extrapolación va en una sola dirección

Si el error fuera simétrico, un lector podría descontarlo. No lo es, y lo sé porque lo medí una semana antes estudiando otra cosa.

Toma los mismos documentos. Recorta ventanas de 400 palabras. No cambia el autor, ni el tema, ni una sola oración. Solo las tijeras.

```
 Posición de la ventana   Ventanas   Marcadas   Tasa     Intervalo 95%
 apertura                 32         6          18,8%    8,9% – 35,3%
 medio                    30         4          13,3%    5,3% – 29,7%
 final                    27         3          11,1%    3,9% – 28,1%
```

Enteros, esos mismos documentos marcan cero de treinta y dos.

La objeción evidente es que las ventanas son más cortas y también más limpias: un artículo completo arrastra pies de figura y texto de trámite que un extracto ya se dejó por el camino. Por eso hay una fila de control. Aplica el mismo filtro, no recortes nada, y sigue siendo 0 de 32. La diferencia es la longitud.

## La frase a la que sigo volviendo

Contado por documento y no por ventana, de los 30 documentos que dieron más de una ventana: **11 quedan marcados en una posición y no en otra. Ninguno queda marcado en todas.**

Que uno de esos autores acabe acusado depende de qué cuatrocientas palabras le tocó pegar a alguien.

Todos son humanos. Todos se publicaron antes de que nada de esto existiera.

## Por qué pasa, que no tiene misterio

La señal más fuerte de esta herramienta es la variabilidad del ritmo: cuánto se separan entre sí las longitudes de las oraciones. La prosa humana es desigual. La salida de un modelo sin instrucciones tiende a encontrar un ancho y quedarse ahí, y para verlo no hace falta ninguna lista de palabras.

Una ventana de 400 palabras contiene quizá veinte oraciones. La larga de tres cláusulas y el fragmento de dos palabras que juntos hacen que un párrafo suene a persona pueden no caer las dos dentro. Así que la estimación no se vuelve solo más ruidosa cuando el texto se acorta. Se mueve, en una dirección, hacia la máquina.

## El arreglo fue callar

El instrumento es un suelo. Por debajo, la puntuación aparece con una declaración explícita de que la frontera nunca se midió a esa longitud, y no se da veredicto alguno.

La decisión interesante era dónde ponerlo, y quiero ser preciso aquí porque el primer diseño estaba mal y una revisión lo cazó.

Lo tentador es *ajustarlo*: recortar ventanas a 150, 300, 600 y 1200 palabras, medir la tasa en cada una y elegir la longitud donde baja del objetivo. Todas las versiones de esa idea fallaban por el mismo motivo de fondo. Una ventana rebanada de un documento largo no es la población que el suelo debe proteger. La respuesta de cuatrocientas palabras de un estudiante fue *compuesta* a esa longitud, y sus oraciones forman una distribución entera y no una truncada. Un suelo ajustado sobre truncamientos y aplicado contra composiciones repite, en otra dimensión, justo el error que viene a prevenir.

Así que el suelo no se ajusta. Es una *observación*: el texto más corto sobre el que se midió la frontera. 662 palabras. La herramienta de calibración lo calcula y lo escribe en la instantánea que el motor lleva dentro, al lado de la tasa de error.

Eso hace la afirmación más débil de lo que parece, y a propósito. No dice que la herramienta falle por debajo de 662 palabras. Dice que nada tan corto se midió nunca, que es lo único que la evidencia sostiene. No hay rejilla, ni ventanas recortadas, ni subconjunto elegido hasta que saliera un número, y no queda nada que discutir salvo un dato que cualquiera puede recalcular.

Tampoco hay techo, y la asimetría está medida y no supuesta. Acortar un texto mueve su puntuación hacia la máquina. Nada sugiere que una tesis más larga que el corpus corra peligro, así que silenciar el extremo largo por simetría sería retirar un veredicto por un motivo del que nadie tiene pruebas.

## El color también es el veredicto

Un detalle que estuvo a punto de salir mal, y que sospecho está mal ahora mismo en herramientas de otros.

Cuando el veredicto se retira, ¿de qué color queda la puntuación? En mi código un veredicto retirado caía en el mismo estado que "por debajo del umbral", que todas las superficies pintan de verde. Así que la página se habría negado a acusar con palabras y habría certificado el pasaje como limpio en el canal más ruidoso que tiene.

Un 94 sobre 100 en verde es una afirmación. Es la contraria de la que se estaba reteniendo, y más grande. Ahora la puntuación sale en gris, y hay un test que lo vigila.

## Lo que esto no arregla

Un ensayo de 900 palabras está por encima del suelo y sigue muy por debajo de la mediana del corpus. El suelo es una compuerta de cobertura, no una corrección. Que la frontera dependa de la longitud necesita textos cortos completos publicados antes de 2022, escritura que alguien compuso a esa longitud y no un documento largo recortado, y ese corpus todavía no existe. Está abierto como issue, y es ahora la contribución más buscada del proyecto.

Y el titular honesto: **esto no hace la herramienta más precisa. La hace más callada.** Responde menos preguntas que la semana pasada. Yo creo que es una mejora, y entiendo por qué un responsable de producto opinaría lo contrario.

## La parte que enlaza con la semana pasada

Me pasé la semana anterior midiendo si la marca de agua de Anthropic deja obsoleto a este proyecto, y no lo deja. Enterrada en ese trabajo había una línea que entonces no asimilé del todo: la marca es poco fiable en muestras cortas, porque pocas elecciones de palabra llevan poca información.

Mi herramienta es poco fiable en muestras cortas, porque pocas oraciones llevan poco ritmo.

Dos métodos sin relación, construidos por gente con recursos muy distintos, llegando al mismo suelo desde direcciones opuestas. Un profesor con un párrafo del trabajo de un alumno está por debajo de ese suelo en los dos casos. Eso no es una casualidad de implementaciones. Es lo que pasa cuando lo que mides es una distribución y te entregan demasiado poca.

## La versión general, para quien no le interese la detección de IA

Un umbral vale sobre la población en la que se ajustó, y en ninguna otra.

Esa frase no es polémica. Lo llamativo es lo poco que alguien escribe cuál fue su población. Todos los detectores de esta categoría te darán un porcentaje sobre un párrafo. Ninguno te dice las longitudes, los idiomas ni los géneros de la escritura con la que calibró ese porcentaje, lo que significa que no puedes saber si el número aplica a lo que acabas de pegar.

Yo tenía ese mismo defecto, en un proyecto cuyo argumento entero es que esta categoría promete de más. Estuvo meses publicado. El corpus llevaba todo ese tiempo en el repositorio con su texto más corto ahí escrito en el manifiesto, y yo nunca le había hecho la pregunta.

Publicar el método es lo que lo hizo encontrable. Al final no lo encontró nadie de fuera. Lo encontré yo, un año después, leyendo mis propios números con otra pregunta en la cabeza.

---

*Señales de escritura IA es libre y de código abierto: reglas, corpus de calibración y método en el repositorio. Funciona en el navegador, en la línea de comandos, como servidor MCP y como app de Windows. El suelo que describe este artículo sale en la 0.5.0. Si quieres lo más corto que puedes ponerle delante a un profesor, la tasa de falsos positivos vive en `Docs/CALIBRATION.md`, y el rango de longitudes que cubre ya está impreso en esa misma página.*
