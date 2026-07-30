---
title: "Un desconocido me pidió una app de escritorio y creí haber construido lo que no era"
description: "Llevar una app Blazor WebAssembly al escritorio tomó un día, no una reescritura. Exactamente una cosa se rompió de verdad. Aquí está esa cosa, la decisión de hace meses que me salvó, y los dos errores que cometí en el camino."
canonical_url: "https://github.com/peopleworks/SignsofAI"
cover_image: "https://raw.githubusercontent.com/peopleworks/SignsofAI/main/Docs/Blog/social/social-preview.png"
tags: [dotnet, blazor, webassembly, escritorio]
author: "Pedro Hernández (PeopleWorks)"
lang: es
---

# Un desconocido me pidió una app de escritorio y creí haber construido lo que no era

Alguien en Reddit andaba buscando una copia pirateada de un "humanizador de IA" de pago. Le contesté con el enlace al mío, que es gratis y de código abierto. Preguntó cómo se instalaba. Le dije que corre en el navegador y le pregunté si quería una versión de escritorio.

"Yes app."

Mi primera reacción no fue entusiasmo. Fue la sensación de haber elegido mal la arquitectura hace meses. SignsOfAI es Blazor WebAssembly. Vive en una pestaña. Convertir eso en algo que descargas y abres con doble clic sonaba a empezar de nuevo.

Estaba equivocado, y por qué lo estaba es lo único de todo esto que vale la pena escribir.

## El motor nunca fue la aplicación web

Antes de tocar nada abrí el archivo de proyecto del motor de análisis. No tiene ni una referencia de paquete. Ninguna.

Eso no fue suerte. Fue una regla que puse temprano y que después olvidé haber puesto: el motor se queda en .NET puro, sin navegador, sin entrada/salida, sin dependencias. Los paquetes de reglas van embebidos como recursos. El tokenizador, el separador de oraciones, el calificador, el reescritor: C# corriente operando sobre cadenas.

Cuando llegó la pregunta de Reddit, ese motor ya alimentaba tres frentes distintos: la app web, una herramienta de línea de comandos y un servidor MCP. Un cuarto no era un cambio de arquitectura. Era otro consumidor.

Así que el port nunca iba a ser una reescritura. Era una extracción: sacar páginas, componentes y servicios de interfaz del proyecto WebAssembly hacia una biblioteca de clases Razor, y dejar que un segundo anfitrión los dibuje.

Si te llevas una sola cosa, llévate esa. La decisión que me salvó se tomó meses antes de que el problema existiera, y ese día no me dio ningún crédito porque nada se veía mejor.

## Exactamente una cosa se rompió

Blazor Híbrido aloja tus componentes dentro de un WebView. Casi todo lo que hace una app Blazor pasa intacto. La interoperación con JavaScript funciona. El almacenamiento local funciona. Cada llamada HTTP a una API externa funciona, y mejor, porque no hay verificación previa de CORS estorbando.

Una cosa no.

Un `HttpClient` de .NET no alcanza el anfitrión virtual del WebView. En el navegador, leer tu propio `wwwroot` con un `HttpClient` atado a la dirección base de la app es lo normal. Dentro de un WebView, la app se sirve desde un origen virtual con el que solo la página misma puede hablar. El HTTP nativo sale a la red y no encuentra nada.

En mi caso eso era una sola línea: el cargador que lee las traducciones de la interfaz desde archivos JSON.

Mi primer instinto fue una interfaz con dos implementaciones, una por anfitrión. Después noté que el `fetch` del propio navegador sirve en los dos sitios. Así que el arreglo borró código en vez de agregarlo: el cargador ahora pasa por un ayudante de JavaScript, un solo camino, los dos anfitriones. El `HttpClient` atado a la dirección base desapareció entero, porque nadie más lo usaba.

La segunda sorpresa me costó más tiempo. La app de escritorio compiló limpia, arrancó, y murió en el primer dibujado:

```
System.IO.FileNotFoundException: Could not load file or assembly
'Microsoft.Windows.SDK.NET, Version=10.0.17763.10'
```

`BlazorWebView` aloja la página en el control de composición de WebView2, que va a buscar las proyecciones WinRT del SDK de Windows. Un marco de destino `net10.0-windows` no las trae. Hace falta una versión de plataforma: `net10.0-windows10.0.19041.0`. Compila de las dos formas, que es lo que lo vuelve molesto de diagnosticar.

## Tres agentes, un repositorio, cero conflictos

Tenía otros dos agentes de programación ociosos en otras terminales, así que repartí el trabajo en tres: el port de escritorio, extraer el motor ONNX de perplejidad a una biblioteca reutilizable, y un lector de documentos para PDF, ODT, EPUB y RTF.

Dos cosas lo hicieron posible, y ninguna es ingeniosa.

**Un árbol de trabajo de git por agente.** Las ramas comparten un solo árbol. Tres agentes en una carpeta significa que el `git checkout` del segundo le reescribe los archivos al primero a media edición, y parece un error tuyo. Con árboles separados, git también se niega a sacar una rama que ya tiene otro árbol, así que la protección deja de depender de que todos se porten bien.

**Repartir por archivo, no por tema.** Cada instrucción nombraba las rutas que ese agente poseía y prohibía el resto por nombre. Un archivo compartido, la solución, quedó reservado y vedado para todos. Las tres ramas se fusionaron sin un conflicto.

Las instrucciones llevaban el motivo detrás de cada restricción, no solo la regla. Esta biblioteca no puede agregarle una dependencia al motor, porque el motor se publica a WebAssembly y a NuGet. Usa PdfPig y no iText, porque iText es AGPL y este repositorio es MIT. Los pesos del modelo están fuera de git, así que las pruebas que los necesitan tienen que saltarse en vez de fallar.

## Lo que salió mal

Los dos agentes reportaron éxito. Los dos tenían pruebas en verde. Uno me estaba mintiendo, sin querer.

Enterrada entre 72 pruebas que pasaban estaba esta:

```csharp
Assert.True(string.IsNullOrEmpty(result.Text) || result.Warnings.Count > 0 || true);
```

Esa afirmación no puede fallar. Sumaba a una suite verde y no protegía nada. Revisé si estaba tapando un defecto real, y no: el código manejaba bien el caso, la prueba nunca lo decía. Una prueba que pasa siempre es peor que una que falta, porque la que falta es honesta sobre el hueco.

Sobrevivió por una razón concreta. Ninguno de los dos proyectos nuevos estaba todavía en la solución, así que ninguna compilación integrada corrió esas pruebas. Agregarlos fue el arreglo de verdad.

Mi propio error fue peor. Limpiando, corrí `git add -A` desde la raíz del repositorio, y eso arrastró una carpeta de notas de trabajo y capturas. Llegó a la rama principal, pública. Lo noté, lo quité, hice un empujón forzado, y después comprobé si eso había borrado algo:

```
$ gh api repos/.../commits/90ab44a
90ab44a82f40441dd1222bebc04ebb1bea955e0c
```

Ahí seguía. Un empujón forzado deja el commit huérfano; no lo borra. En un repositorio público, cualquiera con el hash lo sigue leyendo, y solo el soporte de GitHub puede purgarlo. En mi caso el contenido era inocuo. La lección no lo fue: el arreglo es `.gitignore`, y "voy a tener cuidado" no es un arreglo.

## La parte que lo hizo valer la pena

Una versión de escritorio que solo hace lo que ya hacía una pestaña es una descarga más grande sin motivo. Lo que la justifica es lo que una pestaña estructuralmente no puede.

Lee PDF, ODT, EPUB y RTF. No porque un navegador no pudiera interpretar un PDF, sino porque mandarle un intérprete de PDF a cada visitante le cuesta megabytes antes de analizar una sola palabra. Aquí ya está en el disco.

Alcanza a Ollama en `localhost:11434`. Desde una página servida por HTTPS esa llamada se rechaza a menos que el usuario reconfigure Ollama. Desde aquí es una petición HTTP corriente.

Analiza una carpeta entera. A una pestaña se le entregan archivos; nunca se le da la ruta de una carpeta.

Y mide previsibilidad con un modelo de lenguaje corriendo dentro de la aplicación, en vez de llamar a un servicio. Esa última traía un número, y el número es la razón por la que confío en el port. Misma oración, mismo modelo:

| | Endpoint alojado | En proceso |
| --- | --- | --- |
| Perplejidad | 27.33 | 27.3 |
| Tokens | 17 | 17 |
| Previsibilidad | 0.859 | 0.86 |
| Tiempo | 411 ms | 122 ms |

La misma lectura, tres veces más rápida, con el texto sin salir nunca de la máquina y sin que ningún servidor tenga que estar levantado.

Esa paridad no es casualidad. El motor se *movió* de la API a una biblioteca compartida, no se reimplementó para el escritorio. Si lo hubiera reescrito, los dos habrían derivado, y una herramienta cuyo argumento entero es la honestidad estaría reportando números distintos para el mismo párrafo según dónde la abriste.

## Lo que me diría a mí mismo por la mañana

El port tomó un día. Los primeros diez minutos me los pasé pidiéndome disculpas por una decisión de arquitectura que resultó ser el motivo de que el día fuera corto.

Mantén lo que hace el trabajo libre de lo que muestra el trabajo. Cuesta un poco de disciplina temprano, parece sobreingeniería mientras nada lo necesita, y una tarde cualquiera un desconocido te pide algo que nunca planeaste y la respuesta es una referencia de proyecto.

La app es gratis, MIT, y las dos versiones están aquí: [github.com/peopleworks/SignsofAI](https://github.com/peopleworks/SignsofAI).

---

*Escrito por un humano y revisado con la herramienta que describe: **5/100, se lee mayormente humano**, variabilidad 0.70, unas 1.650 palabras. En la primera pasada me marcó una muletilla y le hice caso, que es para lo que sirve.*

*Quedan dos avisos y los dos están mal: lee "PDF, ODT, EPUB y RTF" como regla de tres, y esa lista tiene cuatro elementos. Dejé la frase como estaba en vez de reescribirla para mejorar mi propio número. Un detector cuyo autor edita en silencio alrededor de sus falsos positivos no es un detector en el que debas confiar.*

*Editar esta nota cambiaba el puntaje que la nota misma declara, lo cual tiene su propia lección pequeña sobre medir aquello sobre lo que estás parado.*
