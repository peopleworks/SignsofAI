---
title: "Metí mi detector de IA dentro de Word, y lo primero que hizo fue negarse a contestar"
description: "Un panel de tareas es un navegador, así que el motor corre en la máquina y el documento no se sube a ningún sitio — que es justo lo contrario de como funciona cualquier otro add-in de esta categoría. Después leyó un documento real, se negó a dar veredicto porque tenía 357 palabras, y aun así encontró seis caracteres invisibles."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/word-story-cover.png"
tags: [ia, dotnet, webassembly, integridadacademica]
author: "Pedro Hernández (PeopleWorks)"
lang: es
---

# Metí mi detector de IA dentro de Word, y lo primero que hizo fue negarse a contestar

La petición más repetida de este proyecto lleva meses siendo la misma frase: *mételo en Word*. Y es razonable. Nadie escribe en una pestaña del navegador; se escribe en un procesador de textos, y pedirle a alguien que copie dos mil palabras del documento que está escribiendo, las pegue en una web, y así averigüe algo sobre el documento que está escribiendo, es un flujo de trabajo que solo puede querer quien lo diseñó.

Así que el add-in ya existe. Lo interesante no es que funcione. Es lo que hizo la primera vez que se lo puse delante a un documento real.

## Leyó el documento y se negó a dar un veredicto

357 palabras, una nota técnica sobre una API, abierta en Word dentro del navegador. El panel la leyó, devolvió una puntuación de 0 sobre 100 y, debajo, en lugar de una conclusión, esto:

> **Sin veredicto a esta longitud.** Este texto tiene 357 palabras. La frontera se midió solo sobre textos de 649 palabras o más, así que aquí no se da veredicto: la puntuación no es prueba de que lo escribiera una máquina ni prueba de que lo escribiera una persona. Todo lo de abajo no se ve afectado.

Ese párrafo es el proyecto entero, impreso dentro de Word.

El número con el que esta herramienta juzga, 30 sobre 100, no se eligió. Se midió, sobre 296 documentos escritos antes de 2022, humanos por su fecha y no por la opinión de nadie. En esa frontera se marcan 2 de los 296: una tasa de falsos positivos del 0,7 %, con un intervalo del 95 % que llega al 2,4 %. Esa cifra está publicada, se mueve cuando se mueve el corpus, y dejó de ser cero el día en que entraron 206 redacciones de adultos aprendiendo inglés.

El más corto de esos 296 documentos tiene 649 palabras. Por debajo de ahí no hay nada sobre lo que la frontera se haya ajustado. La puntuación se puede calcular igual —la aritmética no deja de funcionar— pero no hay ninguna evidencia sobre qué significa, porque nunca se midió un texto tan corto. Así que la herramienta lo dice y se calla.

## La tentación aquí es enorme, y conviene nombrarla

Un detector que contesta a todo sienta mejor. Se demuestra más fácil, se vende más fácil, y nadie escribe para quejarse de que le diste una respuesta.

Pero esa respuesta sería inventada. La versión honesta de «esto no lo he medido» no es un número más pequeño ni un adjetivo más suave. Es el silencio, con el motivo al lado. En el add-in eso cuesta algo real: casi todo lo que la gente pega en un detector es un párrafo, y casi ningún párrafo llega a 649 palabras. La herramienta va a callarse muchísimo.

Prefiero que se calle a que adivine en una barra lateral, al lado del nombre de un alumno.

## Lo que sí encontró

El mismo panel, sobre esas mismas 357 palabras, informó de seis espacios de no separación (`U+00A0`) con el punto de código, la cuenta y un botón para enseñar todas sus posiciones en el documento.

Eso no es un juicio sobre la prosa y no lleva umbral, así que vale a cualquier longitud. Es un hecho sobre el archivo: esos caracteres están ahí, y tecleando no salen. Copiando de una página web o de un PDF sí, y también los dejan algunas herramientas que reescriben texto para esquivar detectores.

El panel dice el resto en voz alta, porque un hecho que insinúa algo sin decirlo es peor que una opinión:

> Esto no dice nada sobre quién escribió el texto, y no es prueba de deshonestidad. Es una pregunta sobre por dónde ha pasado el archivo: pídale a quien lo escribió que abra el documento y le cuente cómo lo produjo.

Un porcentaje se discute media hora en una reunión. «Aquí hay seis caracteres invisibles, y estas son sus posiciones» se resuelve con una pregunta.

## La parte que más importa es de arquitectura

Cualquier otro add-in de esta categoría manda tu documento a un servidor. No le queda otra: el análisis *es* el servidor, así que el texto tiene que viajar hasta donde vive el análisis.

Éste no puede, y el motivo es aburrido en el mejor sentido. **Un panel de tareas es un navegador**: un WebView incrustado, con las mismas reglas que una pestaña. El motor es WebAssembly, se descarga una vez con el panel y corre ahí. En este add-in no hay ningún endpoint, así que no hay adónde mandar el documento. La garantía que ya daba la aplicación web se aplica ahora en el programa donde el documento ya vive.

Y el manifest le pide a Word permiso de `ReadDocument`, no de `ReadWriteDocument`. Es una diferencia de dos palabras con dientes de verdad: **lo impone Word**. El add-in no puede cambiar tu documento, diga lo que diga una web sobre sus intenciones. Si eres profesor y estás decidiendo si dejas que una herramienta se acerque a los trabajos de tus alumnos, esa línea del manifest vale más que cualquier párrafo de una página de aterrizaje. Éste incluido.

## Es un tercer anfitrión, no un segundo producto

El panel no es un motor reducido. Todas las reglas, el escáner de caracteres, el contraste de la bibliografía y las normas sobre lo que se puede afirmar llegan de la misma biblioteca compartida que renderizan la web y la aplicación de Windows. Word es un tercer sitio donde ejecutarlo.

La clase que guarda lo que puede hacer cada anfitrión llevaba este comentario dentro mucho antes de que hubiera un tercero:

> La interfaz debe ramificar por lo que es posible, no por quién pregunta: el día que aparezca un tercer anfitrión, o el día que un navegador deje de bloquear algo, los componentes no hay que revisarlos.

Resultó ser verdad, con una excepción que merece contarse porque es de las cosas que se pudren en silencio. El pie compartido dice *«funciona 100 % en tu navegador»*. Dentro de Word eso es falso, igual que era falso dentro de la aplicación de Windows, que estuvo semanas enseñando esa frase dentro de una ventana WPF sin que nadie lo notara. Así que el anfitrión nuevo tiene su propia frase. Una herramienta que le exige evidencia a los demás no puede ser descuidada con lo que afirma sobre sí misma.

## No funcionó a la primera, y el motivo enseña algo

El primer intento metió el panel en Word, enseñó el botón en la cinta, abrió la barra lateral… y mostró la página equivocada, con Word avisando de que el complemento podría no cargar bien.

Parecía un entorno inestable. Era un 404. El manifest apunta a una URL que el despliegue nunca había publicado, así que el servidor respondió con el fallback de la aplicación de una sola página; la aplicación que salió de ahí no tiene ninguna ruta para la dirección del panel y dijo, correctamente, que esa página no existe. El aviso de Word era el 404, a tres pasos de su causa.

Ahora el despliegue publica el panel y se niega a terminar si su punto de entrada no está. Un panel se carga por URL desde dentro de un procesador de textos, donde un archivo que falta aparece como «este complemento podría no cargar bien»: un mensaje de error que no señala a nada. Mejor romper el despliegue que depurar eso dos veces.

## PowerPoint es otro producto, y no lo estoy publicando

El mismo manifest puede declarar más anfitriones, y sería una línea. También sería deshonesto.

Una presentación casi nunca llega a 649 palabras. Apuntado a una, este add-in haría lo mismo que arriba —retener el veredicto— con casi cualquier presentación, correctamente y sin servirle a nadie. Lo que sí funciona a la longitud de una diapositiva es la parte sin umbral: el escáner de caracteres y las señales con nombre, enseñadas sin puntuación.

Así que la pregunta honesta en PowerPoint no es «¿escribió esto una IA?». Es «¿lleva esta presentación las huellas de una herramienta que reescribe texto para esquivar detectores?», que es otro producto con otra promesa. Puede que valga la pena construirlo. No es éste con una línea de más en un archivo.

## Lo que esto no arregla

El add-in no vuelve medibles los documentos cortos. Saca el límite a la superficie donde la gente se lo va a encontrar más a menudo, que ya es algo, pero el hueco sigue ahí: el corpus no contiene ni una redacción de estudiante por debajo de 649 palabras, y mientras no la contenga, todo lo que baje de esa longitud recibe una puntuación y una negativa.

No lee comentarios, ni notas al pie, ni control de cambios. Solo el cuerpo.

Y no está en la tienda de Office, así que instalarlo sigue siendo un manifest y un menú en vez de un botón. Eso es lo siguiente.

## La versión general, para quien no le interese la detección de IA

Lo que la gente pedía era «mételo en Word». Lo que salió es sobre todo un conjunto de negativas: no da un veredicto que no puede sostener, no sube tu documento porque no tiene adónde subirlo, y pidió permiso para leer y deliberadamente no para escribir.

Nada de eso se parece a una petición de función. Todo eso es lo que la función vale de verdad.
