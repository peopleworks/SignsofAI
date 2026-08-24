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
  'short12-residencia-es': {
    voice: 'Marcela',
    text: "La señal más fiable de que lo escribió una IA no es una palabra rara. Es esto, al pie de un ensayo: espero que esto te ayude. Es la otra mitad de una conversación, pegada sin querer. No dice quién escribe bien. Dice por dónde pasó el archivo. SignsOfAI las detecta, en español y en inglés.",
  },
  'short12-residencia-en': {
    voice: RACHEL,
    text: "The most reliable sign that an A I wrote something isn't a fancy word. It's this, at the foot of an essay: I hope this helps. That's the other half of a conversation, pasted in by mistake. It doesn't say who writes well. It says where the file has been. SignsOfAI catches them, in English and Spanish.",
  },
  'short13-longitud-es': {
    voice: 'Marcela',
    text: "Mi detector le puso noventa y cuatro sobre cien a este párrafo. Y se niega a decir nada. La frontera con la que juzga se midió sobre noventa textos, y el más corto tiene seiscientas sesenta y dos palabras. Este tiene sesenta y seis. Nunca medimos nada tan corto. Las veintitrés señales siguen ahí. Lo que se retira es la acusación.",
  },
  'short13-longitud-en': {
    voice: RACHEL,
    text: "My detector scored this paragraph ninety-four out of a hundred. And it refuses to say anything. The boundary it judges by was measured on ninety texts, and the shortest is six hundred and sixty-two words. This one is sixty-six. We never measured anything that short. All twenty-three signals are still there. What's withheld is the accusation.",
  },
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
  'short8-artefactos-es': {
    voice: 'Marcela',
    text: "Puedes apagar casi cualquier detector de inteligencia artificial con buscar y reemplazar. Cambia la e latina por la e cirílica: se ven idénticas, en pantalla no cambia nada, y la regla deja de encontrar la palabra. Siete detectores cayeron por debajo del azar con ese truco. Con el mío también funcionaba. Ahora SignsOfAI te da el código, la línea y la columna de cada carácter raro. Un porcentaje se discute; un carácter en la línea catorce está o no está.",
  },
  'short8-artefactos-en': {
    voice: RACHEL,
    text: "You can switch off almost any A I detector with find and replace. Swap the Latin e for the Cyrillic e: identical on screen, nothing looks different, and the rule stops finding the word. Seven detectors dropped below chance with that trick. Mine did too. Now SignsOfAI gives you the codepoint, the line and the column of every one. A percentage is arguable; a character at line fourteen either is there or is not.",
  },
  'short9-bibliografia-es': {
    voice: 'Marcela',
    text: "Mi propio detector le puso cero sobre cien a este ensayo. Cero señales. Vocabulario bien, ritmo humano. Y la bibliografía era inventada. Dos autores citados que no están en su propia lista de referencias. El mismo DOI en dos artículos distintos. Una fuente publicada en dos mil veintisiete. Nada de eso necesita internet: el documento se contradice a sí mismo. Y eso no es una acusación, es una pregunta que se contesta en una frase: ¿me manda el artículo?",
  },
  'short9-bibliografia-en': {
    voice: RACHEL,
    text: "My own detector scored this essay zero out of a hundred. Zero signals. Good vocabulary, human rhythm. And the bibliography was invented. Two authors cited that are nowhere in its own reference list. The same D O I on two different papers. A source published in twenty twenty-seven. None of that needs the internet: the document contradicts itself. And that is not an accusation, it is a question you answer in one sentence: can you send me the paper?",
  },
  'short10-linea-base-es': {
    voice: 'Marcela',
    text: "Sesenta y uno por ciento. Esa es la proporción de ensayos de estudiantes que escriben en su segunda lengua que los detectores marcan como inteligencia artificial. No de los tramposos: de los ensayos. Porque escribir formal y cuidado se parece a una máquina, y así es como escribes en un idioma que aprendiste después. Eso no se arregla con mejor detector. Se arregla con otra pregunta: no si se parece a una máquina, sino si se parece a quien escribió los otros trabajos. Y si escribes formal, tu propia línea base ya es formal.",
  },
  'short10-linea-base-en': {
    voice: RACHEL,
    text: "Sixty-one percent. That is the share of essays by students writing in their second language that AI detectors flag as machine-written. Not of the cheats: of the essays. Because formal, careful writing looks like a machine, and that is how you write in a language you learned second. You do not fix that with a better detector. You fix it with a different question: not does this look like a machine, but does this look like the person who wrote the others. And if you write formally, your own baseline is already formal.",
  },
  'short11-veredicto-es': {
    voice: 'Marcela',
    text: "Mi detector le puso noventa sobre cien a este texto y dijo: señales fuertes de escritura con inteligencia artificial. El informe que un profesor imprime y lleva a un comité, del mismo texto y en la misma ejecución, no dijo nada. Y no era un caso raro: no había emitido un veredicto nunca, en ningún idioma. Una condición pedía un umbral que ningún idioma tenía. Yo tenía trescientas cuarenta pruebas. Ninguna comparaba las dos caras entre sí.",
  },
  'short11-veredicto-en': {
    voice: RACHEL,
    text: "My detector scored this text ninety out of a hundred and called it strong signs of A I writing. The report a teacher prints and carries to a committee, same text, same run, said nothing at all. And that was not a rare case: it had never given a verdict, not once, in any language. One condition asked for a threshold that no language had. I had three hundred and forty tests. Not one of them compared the two faces.",
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
