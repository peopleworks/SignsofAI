# Retomar aquí — sesión guardada 2026-07-30

Estado congelado al cerrar la sesión. Nada quedó a medias: todo lo trabajado está mergeado en `main`.

---

## 1. Primero, al abrir la próxima sesión

```bash
cd C:\Proyecto\AI\SignsofAI
git checkout main && git pull        # la rama local quedó en feat/live-rewrite (ya mergeada)
dotnet test                          # deben pasar 125
dotnet run --project src/SignsOfAI.Web   # http://localhost:5219
```

`main` = `b2ba3e2`. El working tree está limpio; lo único sin trackear es esta carpeta `temp/`.

---

## 2. Qué se hizo (todo mergeado, CI verde)

**PR #17 — Interfaz EN/ES conmutable** (mergeado 2026-07-29)
- Switch segmentado en la barra de navegación, cambio instantáneo sin recargar, recordado por navegador, y en la primera visita respeta el idioma del navegador.
- **Traducciones como JSON que la comunidad puede aportar**: `src/SignsOfAI.Web/wwwroot/i18n/` (`en.json`, `es.json`, manifiesto `locales.json`). Un idioma nuevo = copiar un archivo + una línea. Sin C#, sin build.
- Claves faltantes caen a inglés → una traducción parcial es publicable.
- Guía: `Docs/TRANSLATING.md`. Créditos del traductor visibles en el switch.
- 6 tests que nombran el error exacto (clave inexistente, duplicada, vacía, `{0}` perdido) y **no** fallan por traducción incompleta.
- Se añadió `.github/workflows/ci.yml` (build + test en cada PR) para que esos tests corran en un PR de traducción.

**PR #18 — Reescritura en vivo, en el dispositivo** (mergeado 2026-07-30)
- Panel a dos columnas, reconstruido en cada tecla, score cayendo en vivo. Sin modelo, sin red, sin API key.
- Tres intensidades (Suave / Normal / A fondo), alternativas por cambio, y un clic para descartar uno.
- `src/SignsOfAI.Core/Rewriting/LocalRewriter.cs` + campos `replacements` / `delete` explícitos en los rule-packs.
- Resultados verificados: muestra EN 94 → 90, muestra ES 91 → 85, **todas** las sustituciones gramaticales.

---

## 3. ⚠ Lo que NO hay que "arreglar"

**El score baja menos de lo que podría, y es a propósito.** El reescritor declina cinco casos porque
al probar la salida real producía prosa rota. Si alguien reporta "no cambió esta palabra", lo más
probable es que el guard esté funcionando. Reproducir la mala salida **antes** de tocar nada.

1. **Partículas regidas** — `delve into` ≠ "examine into". Y las alternativas no salvan: "look into" daría "look into into".
2. **Flexiones** — una regla cubre delve/delves/delving; solo la forma canónica se auto-aplica.
3. **Construcciones marcadas** — nunca *borrar* una palabra dentro de un hallazgo retórico/sintáctico: quitar "just" de "it's not just a tool" **invierte el sentido**. Las sustituciones sí se permiten.
4. **Marco "a ___ of"** — "a plethora of" → "a many of" no es inglés.
5. **Género en español** — el género se lee **del artículo de la frase**, nunca de la terminación: "panorama" y "problema" son masculinos pese al -a, que es justo donde falla adivinar.

Además: la concordancia `a`/`an` se corrige en `Apply` porque depende de la alternativa elegida.

---

## 4. La decisión pendiente (era la pregunta abierta al cerrar)

**Opción A — Los dos shorts bilingües** ← mi recomendación
Las dos funcionalidades ya están publicables. Según la memoria del proyecto, el cuello de botella
nunca fue el pulido: es que nada en la web apunta al repo. El pase LLM añade capacidad; los shorts
añaden visitantes.

Ángulo de cada uno (esto es lo que se perdería si no queda escrito):

- **Short i18n** — no "ahora está en español", sino **"cualquiera puede añadir su idioma con un archivo JSON, sin saber programar"**. Es una llamada a la acción concreta para la comunidad.
- **Short humanizador** — no "humanizar", sino **"mira el score bajar mientras escribes: sin nube, sin API key"**, y cerrar con lo que ningún competidor puede decir: **"y cuando cambiarlo rompería la frase, no lo cambia"**. Esa negativa es el diferenciador, no una limitación.

**Opción B — El pase con LLM por oración** (lo estructural)
Es lo que el reescritor local honestamente no puede hacer: ritmo/variabilidad, paralelismos
negativos, evasión de cópula. El panel ya reporta cuántas señales son y no finge haberlas tocado.
Diseño previsto: reescribir **solo la oración** que contiene el hallazgo, a demanda, no el texto
completo → barato y rápido. Reutiliza el `HumanizerService` (BYOK) que ya existe.

---

## 5. Otros hilos abiertos (de la memoria del proyecto)

- **Directory PRs** — seguían aterrizando; lo manual está en `temp/caza-de-estrellas.md`.
- **skill-funnel de no-ai-slop** — pendiente (la paridad de taxonomía EN+ES ya está hecha).
- **Contenido listo sin publicar** — `Docs/Blog/PUBLICACION.md` tiene el copy para cada plataforma.
- **Servidor de perplejidad** — al correr en localhost da errores CORS en consola. Son preexistentes y esperados, no son un bug de estas funcionalidades.

---

## 6. Análisis de StealthWriter (conclusión, para no repetir el trabajo)

Tienen: humanizador + detector, niveles suave/medio/agresivo, "Deep Scan" frase por frase, y la
mecánica clave: **clic en una oración → eliges entre alternativas**. Precios $0–$400/mes. No prometen
por nombre burlar Turnitin ni GPTZero (cuidan el flanco legal).

Ventaja estructural nuestra: su negocio es un proxy medido a un LLM; nosotros hacemos buena parte
**en el dispositivo, gratis e instantáneo**. Eso no lo pueden ofrecer sin romper su modelo de negocio.

**Advertencia de marca:** no copiar su posicionamiento. La credibilidad de SignsOfAI está en ser el
detector honesto. El encuadre debe seguir siendo *"edita tu prosa para que no suene a máquina"*, nunca
*"indetectable"* ni *"burla Turnitin"*, y nada de trucos sucios (caracteres de ancho cero, homoglifos):
se detectan trivialmente y hacen que atrapen al usuario.

---

## 7. Detalle de diseño que parece un bug y no lo es

Hay **dos idiomas independientes**: el de la **interfaz** (el switch) y el del **texto analizado** (el
desplegable "Idioma del texto"). Los hallazgos se quedan en el idioma del texto analizado: con
interfaz en español analizando texto inglés, el consejo sale en inglés — correcto, porque el consejo
es sobre palabras inglesas. **No traducir los mensajes de los hallazgos al idioma de la interfaz.**
