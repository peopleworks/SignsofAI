/*
  narrate-all.mjs — genera la voz de los shorts nuevos y reporta su duración.

  Se narra ANTES de escribir el HTML a propósito. Los retardos de las animaciones son tiempos
  absolutos, así que hay que conocer cuánto dura de verdad cada narración para cuadrarlos; hacerlo al
  revés termina en un short que acaba en silencio mirando una pantalla quieta, o con la voz sonando
  encima de la tarjeta final.

  uso:  node narrate-all.mjs [id ...]
*/
import fs from 'node:fs';
import path from 'node:path';

const KIT = 'file:///C:/Proyecto/SISTEMA/Tools/WikiIllustrationKit/shorts';
const { narrate } = await import(`${KIT}/narrate.mjs`);
const HERE = path.dirname(new URL(import.meta.url).pathname.replace(/^\//, ''));

// La voz inglesa hay que fijarla por ID: "Rachel" no está en la lista de voces de la cuenta, así que
// buscarla por nombre falla. El español usa Marcela por nombre.
const RACHEL = '21m00Tcm4TlvDq8ikWAM';

const SCRIPTS = {
  'short4-traduccion-es': {
    voice: 'Marcela',
    text: 'SignsOfAI habla inglés y español. ¿Y el tuyo? Las traducciones no están compiladas: son un archivo JSON. Copias el inglés, traduces las frases y mandas un pull request. Sin C sharp, sin compilar, sin saber programar. Si falta una clave, cae al inglés: una traducción a medias ya sirve. Tu idioma, en un archivo.',
  },
  'short4-traduccion-en': {
    voice: RACHEL,
    text: "SignsOfAI speaks English and Spanish. What about yours? The translations aren't compiled. They're a JSON file. Copy the English one, translate the phrases, open a pull request. No C sharp, no build step, no programming. Miss a key and it falls back to English, so a half-finished translation still ships. Your language, in one file.",
  },
  'short5-dos-idiomas-es': {
    voice: 'Marcela',
    text: 'Un clic, y toda la interfaz cambia. Sin recargar, y se acuerda. Pero mira esto: la interfaz en español, y el consejo sigue en inglés. ¿Un fallo? No. Estás analizando texto inglés, y el consejo habla de palabras inglesas. Son dos idiomas independientes: el de la aplicación, y el de tu texto. A propósito.',
  },
  'short5-dos-idiomas-en': {
    voice: RACHEL,
    text: "One click, and the whole interface changes. No reload, and it remembers. But look at this: the interface in Spanish, and the advice still in English. A bug? No. You're analysing English text, and the advice is about English words. Two independent languages: the app's, and your text's. On purpose.",
  },
  'short6-reescritura-es': {
    voice: 'Marcela',
    text: 'Escribe, y mira el número bajar. Cada cambio ocurre en tu dispositivo: sin nube, sin clave, sin esperar. Pero fíjate en esta palabra. No la toca. Porque cambiarla rompería la frase, y prefiere no tocarla antes que devolverte una frase rota. Eso ningún humanizador de pago te lo cuenta.',
  },
  'short6-reescritura-en': {
    voice: RACHEL,
    text: 'Type, and watch the number fall. Every change happens on your device: no cloud, no key, no waiting. But look at this word. It leaves it alone. Because changing it would break the sentence, and it would rather refuse than hand you broken prose. No paid humanizer tells you that.',
  },
  'short7-escritorio-es': {
    voice: 'Marcela',
    text: 'Ya hay aplicación de escritorio. ¿Para qué, si la web funciona? Para esto: mide la previsibilidad con un modelo que corre dentro de la aplicación. Sin servidor, sin conexión, y tu texto no sale de la máquina. Y lee una carpeta entera: doscientas entregas, ordenadas de peor a mejor. Gratis, para Windows.',
  },
  'short7-escritorio-en': {
    voice: RACHEL,
    text: "There's a desktop app now. Why, if the web one works? For this: it measures predictability with a model running inside the app. No server, offline, and your text never leaves the machine. And it reads a whole folder: two hundred submissions, sorted worst first. Free, for Windows.",
  },
};

const wanted = process.argv.slice(2).filter((a) => !a.startsWith('-'));
const ids = wanted.length ? wanted : Object.keys(SCRIPTS);

const results = [];
for (const id of ids) {
  const s = SCRIPTS[id];
  if (!s) { console.error(`  ? ${id}: no existe`); continue; }

  const out = path.join(HERE, '_work', id, 'voz.mp3');
  if (fs.existsSync(out) && !process.argv.includes('--revoice')) {
    console.log(`  ${id}: ya existe, se salta (--revoice para rehacerla)`);
    continue;
  }

  // resolveVoiceId mira ELEVENLABS_VOICE_ID antes que el nombre, así que se fija sólo para inglés.
  if (s.voice === RACHEL) process.env.ELEVENLABS_VOICE_ID = RACHEL;
  else delete process.env.ELEVENLABS_VOICE_ID;

  const { seconds } = await narrate(s.text, out, s.voice === RACHEL ? 'Rachel' : s.voice);
  const words = s.text.split(/\s+/).length;
  results.push({ id, seconds, words });
}

if (results.length) {
  console.log('\n  id                              voz     palabras  palabras/s');
  for (const r of results) {
    console.log(
      `  ${r.id.padEnd(30)} ${r.seconds.toFixed(1).padStart(5)}s ${String(r.words).padStart(9)} ${(r.words / r.seconds).toFixed(2).padStart(11)}`
    );
  }
}
