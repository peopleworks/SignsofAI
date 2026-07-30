/*
  cue-times.mjs — en qué segundo cae cada frase de una narración.

  Los retardos de las animaciones son absolutos, así que hay que colocarlos donde la voz llega a lo
  que enseñan. Esto reparte la duración medida del mp3 entre las palabras del guion y dice a qué
  segundo empieza cada trozo buscado. No es exacto —la voz no habla a ritmo constante— pero acierta
  dentro de unas décimas, que para revelar una tarjeta es de sobra.

  uso:  node cue-times.mjs <id> "trozo" "otro trozo" ...
*/
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import fs from 'node:fs';

const HERE = path.dirname(new URL(import.meta.url).pathname.replace(/^\//, ''));
const [id, ...needles] = process.argv.slice(2);
if (!id) { console.error('uso: node cue-times.mjs <id> "trozo" ...'); process.exit(1); }

// Se lee el guion del propio narrate-all.mjs para no tener dos copias del texto. Importarlo no
// serviría: es un script que narra al ejecutarse, no un módulo con exports.
const src = fs.readFileSync(path.join(HERE, 'narrate-all.mjs'), 'utf8');
const m = src.match(new RegExp(`'${id}':\\s*\\{[^}]*?text:\\s*(["'])([\\s\\S]*?)\\1,`));
if (!m) { console.error(`no encuentro el texto de ${id}`); process.exit(1); }
const text = m[2];

const mp3 = path.join(HERE, '_work', id, 'voz.mp3');
const seconds = parseFloat(execFileSync('ffprobe',
  ['-v', 'error', '-show_entries', 'format=duration', '-of', 'default=nw=1:nk=1', mp3]).toString().trim());

const words = text.split(/\s+/);
const perWord = seconds / words.length;

console.log(`${id}: ${seconds.toFixed(1)}s, ${words.length} palabras (${perWord.toFixed(3)}s/palabra)`);
for (const needle of needles) {
  const before = text.indexOf(needle);
  if (before < 0) { console.log(`  ?  "${needle}" no aparece`); continue; }
  const wordIndex = text.slice(0, before).split(/\s+/).filter(Boolean).length;
  console.log(`  ${(wordIndex * perWord).toFixed(1).padStart(5)}s  "${needle}"`);
}
