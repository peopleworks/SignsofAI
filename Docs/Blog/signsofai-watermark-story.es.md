---
title: "Quise demostrar que el watermark de Claude no nos afectaba. Me equivoqué, y encontré un fallo peor en mi propia herramienta"
description: "Anthropic empezó a marcar lo que Claude escribe. Medí qué le pasa a un texto cuando alguien borra esa marca — y la respuesta fue que no pasa nada detectable. Lo que sí encontré: mi detector cambia de opinión según qué cuatrocientas palabras le pegues."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/watermark-story-cover.png"
tags: [ia, estadistica, integridadacademica, dotnet]
author: "Pedro Hernández (PeopleWorks)"
lang: es
---

# Quise demostrar que el watermark de Claude no nos afectaba. Me equivoqué, y encontré un fallo peor en mi propia herramienta

Los modelos de Claude publicados desde el 2 de agosto de 2026 llevan una marca legible por máquina. Anthropic dice que los anteriores la tendrán durante un periodo de transición. Y la pregunta llegó en menos de una semana, en foros, en LinkedIn, en mi bandeja: *¿esto no deja obsoleto a un detector como el tuyo?*

Construí [SignsOfAI](https://github.com/peopleworks/SignsofAI) para profesores, y la respuesta corta es incómoda para todos los bandos. La escribí midiendo, no opinando, y salió lo contrario de lo que esperaba.

## La mitad de la respuesta está en la documentación del proveedor

Antes de medir nada conviene leer. La marca no es un carácter escondido: vive en **qué palabra eligió el modelo** entre varias igual de válidas, sesgando la elección con una clave. Es una versión de SynthID-Text, que Google DeepMind publicó en 2024 — o sea, esto no es una invención de Anthropic, es una práctica de industria llegando a un proveedor más.

De ahí salen tres cosas que ya se pueden afirmar sin medir:

**Hoy nadie fuera del proveedor puede comprobar nada.** La clave es suya. Prometen una API de detección "pronto". Cuando llegue, comprobar significará subir el ensayo de un estudiante al servidor de una empresa — justo la línea que este proyecto se niega a cruzar, porque todo corre en la máquina del profesor.

**Marca solo a Claude.** Ni GPT, ni un Llama local, ni ninguno de los envoltorios que venden "humanización". Y de ahí la frase que se va a malusar en las dos direcciones: **la ausencia de marca no es prueba de autoría humana**. Ni *"no tiene marca, está limpio"*, ni el error contrario y peor, *"no tiene marca de Claude, usó otra cosa, culpable"*.

**El propio centro de ayuda lista qué la derrota**: edición fuerte, parafraseo, traducción, mezclar con otro texto, y pasajes muy cortos. Esas son, exactamente, las condiciones de un trabajo de estudiante.

## La mitad que sí había que medir

Queda una pregunta que no se resuelve leyendo: **¿qué le hace a la prosa el borrado de la marca?**

Como la marca vive en la elección de palabras, ningún limpiador de caracteres invisibles la toca. Solo una reescritura. Y ya hay servicios que la venden. Mi hipótesis era cómoda: una reescritura de máquina aplana el ritmo de las frases, así que quien borra el watermark se vuelve *más* visible para mi herramienta, no menos.

El problema de medir eso es que hace falta texto de máquina para comparar, y este proyecto tiene una página entera argumentando por qué nunca hay que reunir un corpus así: es una muestra de los modelos que estaban de moda ese mes, envejece mal, y halaga a quien lo ensambla.

La salida fue el **par**. Cada unidad del estudio es un mismo pasaje medido dos veces: como lo escribió su autor, y después de que un modelo lo reescribiera. Mismo texto, mismo autor, mismo tema, casi la misma longitud. La línea base no se estima a partir de una población — *es el propio texto*. Y los pasajes humanos salen del corpus de calibración, todos publicados antes de que existieran los modelos generativos, que es la única base honesta para llamar humano a un texto.

Treinta y dos pares. Ocho de cada estrato, en los dos idiomas.

## La respuesta: no

Cinco pasajes cruzaron el umbral que antes no lo cruzaban. Dos lo cruzaron de vuelta. Test exacto de McNemar: **p = 0,453**.

Eso no es un resultado. Con siete pares cambiando de lado, solo un barrido limpio habría alcanzado significación. La afirmación honesta es que **este estudio no puede demostrar que reescribir cambie si un pasaje se marca, en ninguna de las dos direcciones**. Ni que reescribir sea seguro, ni que lo cacemos.

Mi hipótesis se retira, y así está escrito en negrita en la página.

Lo que sí sobrevive es más estrecho y sigue siendo útil: una reescritura **no repara una bibliografía que se contradice sola**, y **no devuelve la prosa de un estudiante a la forma de sus trabajos anteriores**. Esas dos comprobaciones no corren sobre el estilo, así que el parafraseo no las toca.

## Lo que encontré sin buscarlo

Al montar el control salió un número que no cuadraba. Mi página de calibración publica que **cero de noventa** textos humanos cruzan el umbral. Pero seis de mis treinta y dos pasajes ya lo cruzaban antes de tocarlos.

La diferencia era la longitud. Los pasajes tienen unas cuatrocientas palabras; los documentos de los que salieron, varios miles.

Así que medí lo mismo escrito por la misma gente, de tres formas:

| La misma escritura, medida como | Marcada a 25/100 | Intervalo 95% |
|---|---|---|
| documentos completos | 0 / 32 | 0% – 10,7% |
| ventanas de 400 palabras, tres posiciones | **13 / 89** | 8,7% – 23,4% |

Y el número que más dice: **once de treinta documentos están marcados en una posición del texto y no en otra, y ninguno lo está en las tres.** Que a uno de esos autores se le acuse depende de qué cuatrocientas palabras le tocó pegar a alguien.

El mecanismo no tiene misterio. La *burstiness* es la dispersión del largo de las frases, y una ventana corta contiene pocas frases: la larga de tres cláusulas y el fragmento de dos palabras que juntos hacen que un párrafo parezca humano puede que no quepan los dos dentro. La medida no se vuelve *incierta* — eso un lector lo podría compensar. Se **mueve, en una sola dirección, hacia la máquina**.

Mi umbral de 25/100 se midió sobre documentos de mediana 3.241 palabras y se aplica hoy a un párrafo pegado, sin que nada en la interfaz lo advierta. Es un defecto, está publicado en la propia página, y es el [issue #59](https://github.com/peopleworks/SignsofAI/issues/59).

Hay una simetría que me dejó pensando: Anthropic dice que su marca falla en muestras cortas porque hay pocas elecciones de palabra. La mía falla porque hay pocas frases. Dos métodos sin ninguna relación, el mismo suelo — y un profesor con un solo párrafo en la mano está por debajo de los dos.

## Tres revisores y tres frases falsas

En este proyecto hay una regla de la casa: nada que cambie el comportamiento o un número publicado se publica sin revisión adversarial. Esta vez fueron tres revisores independientes, cada uno con instrucción explícita de **no leer el veredicto de los otros** — si lo lee, la segunda opinión es un eco.

La aritmética sobrevivió entera. Los tres recalcularon McNemar, los intervalos de Wilson, los cuantiles, los recuentos; uno regeneró el informe byte a byte idéntico desde los datos.

La prosa no sobrevivió. Tres frases publicadas eran falsas:

**«Una ventana del medio del documento».** Mi código corta desde el principio. Describí mal mi propio código. Y no es cosmético: el principio de un artículo científico es su resumen, y el de una entrada de enciclopedia su entradilla — la prosa más comprimida y formulaica que produce cada género. Medí un efecto de género y lo llamé efecto de longitud. Al medirlo en tres posiciones, el titular cayó de 18,8% a 14,6%.

**«Es el único caso».** Había escrito a mano que un solo pasaje conservaba una cita literal, rompiendo la regla de la instrucción de no dejar sobrevivir ocho palabras seguidas. Al medirlo: **veinte de treinta y dos pares**, el tramo más largo de 86 palabras. Casi todos son citas — una sentencia del Tribunal Constitucional, un panfleto político, una definición publicada — que no se pueden reescribir sin falsificarlas. La instrucción exigía preservar cada dato *y* reemplazar cada palabra, y nunca dijo cuál mandaba.

**«Sí aplana el ritmo, en 20 de 32 pares».** Test de signos sobre 20 contra 12: p = 0,215. Tres párrafos más arriba yo mismo había aplicado el test exacto a otro recuento y dictaminado "no es un resultado". Apliqué dos raseros distintos en la misma página.

Las tres son el mismo fallo: **números comprobados a máquina junto a un método comprobado a mano**. Así que la herramienta ahora mide su propio cumplimiento en cada corrida y lo imprime, favorezca o no al estudio.

Hubo una cuarta, que solo vio el revisor que leyó el español línea por línea: **la reescritura corrigió los originales en silencio**, cosa que la instrucción prohíbe con esas palabras. *Intercesión* → *se cruza*. *Teoría de juego* → *teoría de juegos*. *Costa* → *consta*. El tratamiento entregado no fue "parafrasear", fue "parafrasear y corregir estilo", así que parte de cada cambio de puntuación es reparación de errores. Y eso golpea justo el estrato que existe para medir el daño a quien escribe en su segunda lengua.

## El conflicto que hay que decir en voz alta

El modelo que reescribió los pasajes es el mismo que escribió las reglas contra las que se reescribía. Nombrar el modelo es procedencia, no es tratar el conflicto.

Lo que juega a favor está en los datos: la reescritura **introdujo** señales además de quitarlas, y empujó más pasajes por encima del umbral que los que rescató. Un modelo haciendo trampa no haría eso. Pero la objeción es legítima, es la primera que levantará cualquier lector hostil, y la única respuesta real es que otro proveedor lo repita. Hasta entonces, la página lo dice sin rodeos: **estos números no deben citarse donde un profesor vaya a actuar sobre ellos.**

## Qué se lleva un profesor de aquí

Cuatro cosas, y ninguna es un titular tranquilizador:

1. **El watermark no le sirve a usted hoy.** No hay detector público, y cuando lo haya implicará mandar el trabajo del estudiante a un servidor ajeno.
2. **Que no haya marca no significa nada.** Ni a favor ni en contra.
3. **Ninguna herramienta, la mía incluida, sabe distinguir** a quien borró un watermark de quien pasó su propio párrafo honesto por un modelo para mejorar el estilo, o porque el inglés no es su lengua.
4. **Desconfíe de cualquier veredicto sobre un texto corto**, incluido el mío, hasta que arregle el #59.

Todo esto — el método, los datos, los intervalos y los fallos — está publicado en [`Docs/PARAPHRASE.md`](https://github.com/peopleworks/SignsofAI/blob/main/Docs/PARAPHRASE.md), y se regenera con un comando desde el mismo repositorio.

Un estudio que sale como esperabas es agradable. Uno que retira tu hipótesis, te encuentra tres frases falsas y te descubre un defecto peor del que ibas buscando vale bastante más. Y si un proyecto lleva ocho artículos exigiéndoles a los demás que publiquen sus errores, publicar los propios no es humildad. Es el precio.

*Hecho por Pedro Hernández — PeopleWorks, [Microsoft MVP para .NET](https://mvp.microsoft.com/en-US/mvp/profile/24060a02-dbc6-44ec-bca5-c213ff9835c5). Por y para la comunidad educativa.*
