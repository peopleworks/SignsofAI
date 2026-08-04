---
title: "Cualquiera podía apagar mi detector de IA con buscar y reemplazar"
description: "Cambiar unas letras por otras idénticas de otro alfabeto hunde a siete detectores publicados por debajo del azar. Con el mío también funcionaba. El arreglo son 200 líneas, sin modelo, y una decisión que quiero defender: no toca la puntuación."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/artifacts-story-cover.png"
tags: [dotnet, unicode, seguridad, ia]
author: "Pedro Hernández (PeopleWorks)"
lang: es
---

# Cualquiera podía apagar mi detector de IA con buscar y reemplazar

Pasó por Reddit un post: *Best AI Humanizer for Students in 2026*. Siete herramientas, cinco categorías, un revisor ciego, cincuenta borradores universitarios. Tenía toda la forma de un estudio.

Es publicidad. Dos votos, un comentario —del propio autor, enlazando sus "estudios" anteriores—, cincuenta textos que nunca publicó, notas con un decimal, y la herramienta que vende ganando todas las categorías.

Pero me mandó a la investigación de verdad, y la investigación tenía algo que decir que no me gustó nada.

## La carrera armamentista ya terminó, y la perdí

Los números no están cerca.

Parafrasear un texto con DIPPER lleva la exactitud de DetectGPT de 70.3% a 4.6%, con una tasa fija de 1% de falsos positivos, sin cambiar lo que el texto significa. Un ataque de 2026 con aprendizaje por refuerzo llega a detección casi nula en tres de cuatro detectores. En seis detectores comerciales, la exactitud media es de 39.5%, y cae a 17.4% con modificaciones ligeras.

Y luego el que debería cerrar la discusión. Un paper de COLING 2025 llamado SilverSpeak cambia caracteres por **homoglifos**: letras de otros alfabetos que se dibujan igual. Una «а» cirílica (U+0430) junto a una «a» latina (U+0061). Misma forma, código distinto.

Siete detectores pasaron de una correlación de Matthews de 0.64 a **−0.01**. Peor que una moneda.

Mientras tanto, los detectores marcan como IA el 61% de los ensayos de quienes tienen el inglés como segunda lengua. Así que la categoría entera aterrizó en el peor sitio posible: fácil de burlar si conoces el truco, y castigadora si te toca escribir en tu segundo idioma.

Yo construyo una de estas herramientas. Me tomó un rato digerirlo.

## Después comprobé si conmigo también funcionaba

SignsOfAI no usa un clasificador. Compara contra un catálogo: vocabulario sobreusado, muletillas retóricas, tics sintácticos, ritmo de las frases. Cada regla apunta a una palabra o a un patrón.

Lo que significa que **cada regla busca sobre el texto**. Y si el texto dice `sumеrgirse` —con una letra cirílica dentro—, la regla que busca `sumergirse` ve una palabra que no conoce.

Lo medí sobre un párrafo con sabor a IA. Intacto, el motor reporta 17 señales y puntúa 94/100. Con el vocabulario vuelto irreconocible, el mismo párrafo reporta **6 señales y 80/100**. Once hallazgos desaparecidos, y en pantalla no cambió nada.

Eso no es una degradación sutil. Es un catálogo público que cualquiera apaga con buscar y reemplazar.

## Lo que pasa con esos caracteres

Y aquí es donde el problema se convierte en la función más útil que he publicado en meses.

Una «а» cirílica dentro de una palabra en español no es un estilo. No es una probabilidad. No es una lectura de cómo escribe alguien.

**Es un artefacto físico de una herramienta.**

Nadie teclea un U+200B. De un teclado no sale un espacio de ancho cero. Una letra cirílica no cae en medio de «análisis» por accidente. Esos caracteres están ahí porque un programa los puso.

Así que la comprobación que derrota al ataque es además la única del producto que devuelve un **hecho** en vez de un juicio — y de lejos la más barata de toda mi hoja de ruta. Un recorrido de caracteres. Sin modelo, sin dependencias, sin red. Corre en el navegador, corre sin conexión, y trabaja por debajo del idioma, así que es bilingüe sin trabajo extra.

## Normalizar primero, y no limpiar nunca en silencio

El arreglo tiene dos piezas.

Primero, un escáner que recorre el texto por escalar Unicode y reporta lo que encuentra: caracteres de ancho cero y de unión, controles de dirección, códigos de uso privado, caracteres de etiqueta ocultos, y letras que suplantan a las latinas — con el código, la línea y la columna de cada aparición.

Segundo, los analizadores dejan de leer la cadena cruda. Leen una copia limpia, con las letras impostoras sustituidas por las que fingían ser, junto a un mapa de vuelta al original para que cada hallazgo siga apuntando al documento real de quien lo lee.

```
● [Lexical] dеlvе
    "delve" is heavily overused in AI writing.
```

La regla vuelve a dispararse. Y la palabra se muestra tal como está en el archivo, con sus letras cirílicas incluidas, porque mandar a alguien a buscar una palabra que no está ahí es otra forma de fallar.

Dos reglas que defendería en cualquier parte:

- **La limpieza nunca ocurre a escondidas.** Todo lo que el normalizador quita, el reporte lo nombra. Una herramienta que corrige su entrada en silencio es una herramienta que le esconde evidencia a la persona a la que dice ayudar.
- **El normalizador no decide qué es un artefacto.** Consume el reporte del escáner y actúa sobre él. Una definición, un solo sitio. No pueden acabar describiendo documentos distintos.

Lo segundo importa más de lo que parece. Esta misma sustitución se usa para atacar la **atribución de autoría**: hay un paper de 2025 que apunta a la Delta de Burrows en concreto, con esteganografía de ancho cero. La comparación contra el propio autor es la respuesta más prometedora al problema de los falsos positivos, porque pregunta «¿se parece a *esta persona*?» en vez de «¿se parece a una IA?», y es la que más ayuda a quien escribe en segunda lengua. Para esa función la normalización no es un extra: es un prerrequisito. No se puede comparar a alguien contra su propia línea base si la línea base se puede envenenar.

## La decisión que quiero defender: no toca la puntuación

El reporte de artefactos aporta exactamente cero a la puntuación de 0 a 100. Hay una prueba cuyo único trabajo es fallar si eso cambia algún día.

La tentación es evidente. El texto pasó por una herramienta de reescritura, eso es sospechoso, el número debería subir. Sería una línea.

Y destruiría lo único que hace que la función valga la pena.

Una puntuación es un juicio, y un juicio es discutible — como debe ser, porque es una lectura de la prosa y la prosa se discute. Un carácter en la línea 14, columna 3 no se discute. Usted abre el archivo en cualquier editor y mira. No tiene que creerme a mí, ni a mis pesos, ni a mis umbrales, ni a mi opinión sobre la palabra «profundizar».

Si mezcla lo uno con lo otro, convirtió lo único comprobable del producto otra vez en una opinión. Por eso van separados, se guardan separados, y el panel lo dice en voz alta:

> Nada de esto afecta a la puntuación de arriba. Una puntuación es un juicio discutible; un carácter en una línea y una columna está o no está, y usted puede comprobarlo en cualquier editor sin creernos nada.

Turnitin publica algo parecido: un porcentaje del texto que pudo haber pasado por una herramienta de evasión. Un porcentaje. Mi versión le da el código y las coordenadas de cada aparición. Un número no se puede auditar. Una lista de posiciones sí.

## Las pruebas que más importan son las de no dispararse

Casi todo el archivo de pruebas trata de lo que **nunca** debe marcarse, porque un falso positivo aquí no es una opinión discutible sobre cómo escribe alguien. Es una afirmación falsa sobre lo que hay en su archivo.

- `Spanish_is_never_flagged_for_being_Spanish` — «análisis», «señora», «pingüinera». Las letras latinas con tilde están deliberadamente ausentes de la tabla de sosias. Una herramienta que tratara la «á» como impostora sería exactamente el instrumento del que se acusa a toda esta categoría.
- `A_real_Greek_word_is_left_alone` — «α-helix» es una letra griega *al lado* de una palabra latina, no una escondida *dentro* de ella. La decisión se toma por racha de letras: una sosia solo cuenta cuando las latinas son mayoría en la racha donde está. Esa es la forma de una sustitución, y no la de una palabra real de otro alfabeto.
- `An_emoji_sequence_is_not_an_artifact` — un unidor de ancho cero es como se construye un emoji de varias personas.
- `Join_controls_are_left_alone_in_the_scripts_that_need_them` — en persa, árabe y las escrituras índicas ese mismo carácter es ortografía normal.

## La medida no es cuántos. Es cómo están repartidos.

Los documentos normales recogen estas cosas todo el tiempo. Copie de una página web y se lleva espacios duros. Extraiga de un PDF y se lleva guiones blandos. Escriba en dos idiomas y se lleva dos alfabetos.

Por eso el reporte separa *cuántos hay* de *cuán repartidos están*. El texto pegado lleva sus artefactos donde cayó el pegado. Una herramienta que reescribió el documento entero los deja por todas partes donde tocó. El documento se divide en secciones y el reporte dice cuántas contienen alguno:

> 47 caracteres que no se producen al escribir, repartidos por 8 de 10 secciones del documento. Ese reparto es lo que deja una herramienta cuando procesa un texto completo.

Usted puede no estar de acuerdo con ese razonamiento. Ve las secciones, el conteo y cada posición, y saca su propia conclusión. De eso se trata.

## Lo que no significa

Cada reporte que dice algo dice también lo que no significa, en el mismo panel y en el idioma del texto:

> Esto no dice nada sobre quién escribió el texto, y no es prueba de deshonestidad. Es una pregunta sobre por dónde ha pasado el archivo: pídale a quien lo escribió que abra el documento y le cuente cómo lo produjo.

Hasta Turnitin dice que su propia puntuación no debe ser la única base de una acción contra un estudiante, y se niega a reportar nada entre el 1% y el 19% por riesgo de falsos positivos. El líder del mercado le está diciendo que no lo use como prueba. Vale la pena repetirlo en una sala llena de profesores, justo antes de admitir que la herramienta propia tiene la misma limitación.

## Dónde me deja esto

No voy a ganar una carrera armamentista contra ataques de paráfrasis con aprendizaje por refuerzo. Nadie va a ganarla. Cada hora invertida en hacer más listo al detector estadístico es una hora invertida en perder más despacio, y el daño colateral le cae a quien aprendió el idioma después.

Lo que sobrevive es lo comprobable: citas alucinadas que no existen, metadatos que dicen que un ensayo de 3.000 palabras tomó noventa segundos, una comparación contra el trabajo anterior de la misma persona — y ahora, caracteres que un teclado no puede producir, en coordenadas que cualquiera verifica.

Nada de eso es un porcentaje. Todo eso es algo que una persona puede llevar a una reunión y defender.

El motor, las reglas y las pruebas están en [GitHub](https://github.com/peopleworks/SignsofAI) bajo licencia MIT. Si encuentra un documento donde se equivoque, ese es el reporte que más quiero — la herramienta no tiene servidor ni telemetría, así que un humano contándomelo es la única forma en que llego a enterarme.

---

*Escrito por un humano y revisado con la herramienta que describe: **11/100, mayormente humano**, variabilidad 0.68, unas 1.990 palabras.*

*Además dispara su propia comprobación nueva, que es mejor demostración que cualquiera que yo pudiera montar. Tres letras cirílicas —los ejemplos de arriba, en la línea 37 y la línea 64—. El reporte las llama **presentes, sin repartirse**, que es exactamente lo correcto: están en dos sitios porque las escribí ahí a propósito, y esa no es la forma de un documento por el que ha pasado una herramienta.*

*Las dejé. Borrar mis propios ejemplos para sacar un reporte más limpio sería la misma jugada que un autor de detectores reescribiendo en silencio alrededor de un falso positivo — y prefiero que usted pueda ver la cosa funcionando conmigo.*
