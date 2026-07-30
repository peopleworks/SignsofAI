/*
  make-scenes.mjs — escribe los scenes/*.json de los shorts nuevos.

  Los guiones viven en narrate-all.mjs y se leen de ahí, no se copian: el .srt se genera a partir del
  texto de la escena, así que si la escena y el audio narrado dicen cosas distintas, los subtítulos
  quedan desincronizados con la voz y nadie se entera hasta verlo publicado.

  minSeconds sale de la duración medida del mp3 más la cola, redondeada hacia arriba.
*/
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const HERE = path.dirname(new URL(import.meta.url).pathname.replace(/^\//, ''));
const TAIL = 1.4;

const src = fs.readFileSync(path.join(HERE, 'narrate-all.mjs'), 'utf8');
const block = src.slice(src.indexOf('const SCRIPTS'), src.indexOf('const wanted'));

const ids = [...block.matchAll(/'([\w-]+)':\s*\{/g)].map((m) => m[1]);
let written = 0;

for (const id of ids) {
  const m = block.match(new RegExp(`'${id}':\\s*\\{[^}]*?text:\\s*(["'])([\\s\\S]*?)\\1,`));
  if (!m) { console.error(`  ! ${id}: no pude leer el texto`); continue; }
  const text = m[2].replace(/\\'/g, "'").replace(/\\"/g, '"');

  const mp3 = path.join(HERE, '_work', id, 'voz.mp3');
  if (!fs.existsSync(mp3)) { console.error(`  ! ${id}: falta la voz, corre narrate-all.mjs`); continue; }
  const seconds = parseFloat(execFileSync('ffprobe',
    ['-v', 'error', '-show_entries', 'format=duration', '-of', 'default=nw=1:nk=1', mp3]).toString().trim());

  const lang = id.endsWith('-en') ? 'en' : 'es';
  const base = id.replace(/-(en|es)$/, '');
  const scene = {
    id,
    html: lang === 'en' ? `en/${base}.html` : `${base}.html`,
    voice: lang === 'en' ? 'Rachel' : 'Marcela',
    minSeconds: Math.ceil(seconds + TAIL),
    tailSeconds: TAIL,
    narration: text,
  };

  const out = path.join(HERE, 'scenes', `${id}.json`);
  fs.writeFileSync(out, JSON.stringify(scene, null, 2) + '\n', 'utf8');
  console.log(`  ${id}: voz ${seconds.toFixed(1)}s -> video ${scene.minSeconds}s`);
  written++;
}

console.log(`\n${written} escenas escritas.`);
