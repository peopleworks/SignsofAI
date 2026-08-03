---
title: "Medí cuántas veces se equivoca mi propio detector de IA, y lo publiqué"
description: "Noventa textos escritos antes de que existieran los modelos generativos. Cero marcados al umbral recomendado — con un intervalo del 95% que llega al 4,1%, porque noventa textos no pueden prometer más. Y una regla mía se dispara en la mitad de toda la escritura académica humana."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/social-preview.png"
tags: [ia, estadistica, integridadacademica, dotnet]
author: "Pedro Hernández (PeopleWorks)"
lang: es
---

# Medí cuántas veces se equivoca mi propio detector de IA, y lo publiqué

Todo profesor que agarra un detector de IA hace la misma pregunta al minuto: *¿cuántas veces te equivocas?*

Nadie la contesta. Ni Turnitin, ni GPTZero, ni Originality.ai, ni ninguna de las herramientas que le dirán tan campantes que el ensayo de un estudiante es 87% IA. El número que permitiría juzgarlas es justo el que ninguna publica.

Así que medí el mío, y el resultado está en el repositorio: `Docs/CALIBRATION.md`, regenerado por un comando que cualquiera puede correr.

## Lo que no medí, y por qué

No medí exactitud.

La exactitud necesita un montón de texto escrito por máquina contra el cual comparar, y cualquier montón que yo arme es una muestra de los modelos que me vinieran bien ese mes. Ese número envejece mal, y favorece a quien lo armó: yo elijo los prompts, yo elijo los modelos, yo elijo cuándo dejar de recolectar.

Peor aún, es el número equivocado. Un detector con 95% de exactitud que lo consigue marcando a todo el que escribe formal ha hecho más daño que uno que caza menos y no acusa a nadie.

Lo que medí es la **tasa de falsos positivos**: cuántas veces la herramienta marca escritura que ninguna máquina produjo. Eso solo necesita texto humano, que no caduca. Y mide el daño que esta categoría causa de verdad: los detectores marcan el 61% de los ensayos de quienes no tienen el inglés como lengua materna, y ninguno publica esa cifra sobre sí mismo.

## Cómo se sabe que el texto es humano

No leyéndolo. Leerlo es lo que se está midiendo; no puede ser además lo que mide.

Por su fecha. Cada texto del corpus se publicó antes de que los modelos generativos pudieran haberlo escrito: artículos de acceso abierto de 2018 a 2020, y revisiones de Wikipedia tal como estaban en 2020 y 2021, sacadas del historial. Un artículo con un DOI de 2019 no lo escribió algo que todavía no existía.

Esa es una garantía más fuerte que la que ofrece cualquier clasificador sobre cualquier cosa, y está al alcance de cualquiera con conexión a internet.

## El número

Noventa textos, unas 380.000 palabras.

**A un umbral de 25 sobre 100, no marcó ninguno.**

```
 Puntuación desde   Textos humanos marcados   Tasa    Intervalo 95%
 10                 18 / 90                   20%     13%   – 29,4%
 15                  7 / 90                   7,8%    3,8%  – 15,2%
 20                  1 / 90                   1,1%    0,2%  – 6%
 25                  0 / 90                   0%      0%    – 4,1%
```

Lea la última columna, no la tercera. Cero de noventa no es una tasa de falsos positivos del 0%; es una tasa entre 0% y 4,1%, y noventa textos no pueden decir nada más estrecho.

Por eso la recomendación se hace desde el extremo **superior**. Esa es la disciplina entera: con un corpus pequeño el límite es ancho y el umbral recomendado sale prudente, y se estrecha solo a medida que el corpus crece. Leer la estimación puntual me dejaría afirmar una tasa del 0% a partir de noventa documentos, lo cual sería falso y es exactamente la clase de cosa que la gente cita.

Un dato que conviene interiorizar si alguna vez hace esto: **sin marcar absolutamente nada, todavía hacen falta unos setenta y cinco textos para que el intervalo por sí solo baje del 5%.** Eso es lo que significa «corpus suficiente», y es bastante más de lo que nadie reúne por accidente.

## El resultado que más me inquietaba

El argumento entero de este proyecto es que preguntar «¿se parece a una máquina?» castiga a quien escribe formal, y que la mayoría de esa gente aprendió el idioma después. Si mi propia herramienta hiciera eso, habría construido justo aquello que critico en cada artículo.

Así que el corpus va partido. Artículos de PLOS con al menos un autor afiliado en un país anglófono, contra artículos sin ninguno. Wikipedia en inglés contra Wikipedia en español — misma fuente, mismo registro, para poder distinguir un efecto de idioma de un efecto de género textual.

```
 Grupo                       Textos   Mediana   Percentil 90   Máximo
 en-anglophone-affiliation   21       7,0        9,8           15,4
 en-other-affiliation        19       6,4       14,1           18,3
 en-wikipedia                25       5,2       10,4           23,4
 es-wikipedia                25       7,7       15,4           18,9
```

Las medianas van de 5,2 a 7,7 — una dispersión de 2,5 puntos en una escala de cien. Al grupo en español no se le castiga por estar en español. Al grupo no anglófono no se le castiga por no ser anglófono, aunque su cola es más larga, y esa cola merece vigilancia según crezca el corpus.

Una herramienta con el defecto sobre el que llevo escribiendo mostraría un grupo claramente por encima del resto. Ninguno lo está.

Quiero ser preciso sobre qué es y qué no es esto. Es un **primer indicio**, sobre decenas de textos y no cientos, y sobre artículos publicados y no ensayos de estudiantes. No es un hallazgo. Puede moverse cuando el corpus crezca, y se moverá en la dirección en que se mueva — que es precisamente el motivo de publicar el método junto al número.

La división por afiliación es además un sustituto tosco. La lengua materna de nadie consta en un DOI, y mucha gente en una universidad de Londres aprendió inglés después. Se equivoca a propósito en una dirección: cualquier afiliación anglófona cuenta como anglófona, lo que encoge el grupo de segunda lengua y hace que cualquier brecha sea una subestimación y no una exageración.

## La parte que escoció

El reporte lista además cuáles de mis reglas se dispararon sobre esa escritura humana. Cada acierto es un falso positivo por construcción: ninguna máquina escribió nada de eso, así que no hay juicio que hacer.

```
 Regla                 Textos donde disparó   Proporción   Aciertos
 rhet.rule-of-three    45                     50%          100
 stat.burstiness       25                     27,8%         25
 lex.moreover          23                     25,6%         67
 lex.furthermore       23                     25,6%         55
 rhet.in-order-to      22                     24,4%         60
```

**Mi detector de regla de tres se dispara en la mitad de toda la escritura académica humana.** Cien veces a lo largo de cuarenta y cinco artículos que son anteriores a los modelos por completo.

Y «moreover» y «furthermore», marcados en la cuarta parte de los textos. Son conectores académicos normales. Los investigadores llevan escribiendo «furthermore» desde mucho antes de que nadie entrenara un transformer.

Una regla en lo alto de esa lista no está automáticamente mal; algunas pistas sí aparecen de verdad en prosa humana, y el catálogo lo dice. Pero una regla que dispara en la mitad de todo está midiendo el género, no la máquina. Sin hacer esto no habría sabido cuáles, y habría seguido publicándolas tan tranquilo.

Esa lista es lo más útil que produjo el ejercicio entero, y es la razón para publicarlo en vez de comprobarlo en privado y seguir adelante.

## Lo que no le dice

Cuatro cosas, y están en el propio reporte y no en una nota al pie.

**Nada sobre cuánta escritura de IA caza.** Esa es la otra mitad del cuadro y está deliberadamente ausente. Una herramienta que no marca nada tiene una tasa de falsos positivos perfecta.

**Nada sobre texto distinto a este corpus.** Son artículos publicados: largos, muy editados, escritos por gente que escribe para vivir. Un ensayo de primer año es otro animal. El arreglo honesto es que un centro calibre sobre los trabajos pre-2022 de sus propios estudiantes, cosa que la misma herramienta hace, y obtenga una tasa para *su* población en vez de la de otro.

**La agrupación es un sustituto**, discutido arriba.

**Los hashes prueban qué midió esta ejecución**, no que usted extrayendo los mismos artículos obtenga texto idéntico. No lo obtendría: la extracción de PDF y la de HTML difieren.

## Por qué es un archivo JSON y no una afirmación

El corpus es un manifiesto que cualquiera puede ampliar — fuente, licencia, año, hash y el razonamiento detrás de cada agrupación. Los textos en sí no están en el repositorio, porque las licencias difieren por fuente y el volumen aplastaría al código, pero sí está todo lo necesario para rearmarlos y comprobarlos.

Añada textos y el número publicado cambia. De eso se trata. **Un número que puede subir cuando el corpus crece es una medición que funciona. Uno que solo pudiera bajar sería publicidad.**

Lo que más quiero es escritura académica en español. SciELO y Redalyc son las fuentes obvias y ninguna era alcanzable desde donde armé esto, así que el lado español descansa hoy solo en prosa enciclopédica — y el español es la mitad de este problema que nadie más mide.

MIT, motor, manifiesto del corpus y la herramienta de medición: [github.com/peopleworks/SignsofAI](https://github.com/peopleworks/SignsofAI).

---

*Escrito por un humano y revisado con la herramienta que describe: **8/100, mayormente humano**, variabilidad 0.58, unas 1.530 palabras. Cada cifra citada arriba está copiada del reporte generado y no de memoria. Había escrito que la regla que dispara en la mitad de la escritura académica humana había disparado también aquí; lo comprobé antes de publicar y no era cierto. Sí dispararon otras dos de esa lista — `lex.moreover` y `lex.furthermore`, los conectores académicos normales de los que me quejo cuatro párrafos más arriba. Lo cual es evidencia a favor del argumento, o en contra de mi prosa.*
