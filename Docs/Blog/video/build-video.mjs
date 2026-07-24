/*
  build-video.mjs — pipeline de video narrado LANDSCAPE multi-escena (Modelador Studio).

  Generaliza build-short.mjs (WikiIllustrationKit) a:
    - 1280x720 horizontal (tutorial),
    - varias escenas (cada una: HTML animado O clip mp4 de b-roll),
    - por escena: narrar (Marcela) -> grabar/tomar clip por el largo de la voz -> mp4 uniforme,
    - concatena todas + tarjeta intro -> final.mp4,
    - subtitulo global .srt con offsets acumulados (frase por frase).

  Reutiliza SIN instalar nada:
    - narrate() + getKey() de WikiIllustrationKit/shorts/narrate.mjs  (voz Marcela, lee su propia .elevenlabs.key)
    - cuesProportional()/toSrt() de .../shorts/captions.mjs
    - Playwright/Chromium de Xari/CoreReportsSolution/tools/video-generator/node_modules

  uso:   node build-video.mjs guion.video.json           (todas las escenas)
         node build-video.mjs guion.video.json 01-gancho  (solo una escena, para probar)
         SILENT=1 node build-video.mjs ...                 (fuerza mudo, no gasta ElevenLabs)
*/
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { execFileSync } from 'node:child_process';

const HERE = path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1'));
const KIT = 'C:/Proyecto/SISTEMA/Tools/WikiIllustrationKit/shorts';
const PW  = process.env.PW_PKG ||
  'C:/Proyecto/Xari/CoreReportsSolution/tools/video-generator/node_modules/playwright/index.js';

const W = 1280, H = 720, FPS = 30;

// ---- imports dinámicos (rutas absolutas de otros proyectos) ----
const { narrate, getKey } = await import(pathToFileURL(path.join(KIT, 'narrate.mjs')).href);
const { cuesProportional, toSrt, srtStamp } = await import(pathToFileURL(path.join(KIT, 'captions.mjs')).href);
const pw = await import(pathToFileURL(PW).href);
const chromium = pw.chromium || pw.default?.chromium;
if (!chromium) { console.error('no pude cargar chromium de playwright'); process.exit(1); }

// ---- args ----
const guionFile = process.argv[2] || 'guion.video.json';
const onlyId = process.argv[3] || null;                 // renderiza una sola escena
const SILENT = process.env.SILENT === '1' || !getKey();
const guion = JSON.parse(fs.readFileSync(path.resolve(HERE, guionFile), 'utf8'));

const outDir = path.resolve(HERE, 'out');
const workDir = path.resolve(HERE, '_work');
const sceneMp4Dir = path.join(workDir, 'scenes');
fs.mkdirSync(outDir, { recursive: true });
fs.mkdirSync(sceneMp4Dir, { recursive: true });

// ---------- helpers ----------
function probe(file) {
  const out = execFileSync('ffprobe', ['-v','error','-show_entries','format=duration','-of','default=nw=1:nk=1', file]).toString().trim();
  const d = parseFloat(out); return isNaN(d) ? 0 : d;
}

// graba un HTML animado a webm por `sec` segundos (landscape)
async function recordHtml(htmlPath, sec, outWebm) {
  const recDir = path.join(workDir, 'rec_' + path.basename(outWebm, '.webm'));
  fs.rmSync(recDir, { recursive: true, force: true });
  fs.mkdirSync(recDir, { recursive: true });
  const browser = await chromium.launch();
  const ctx = await browser.newContext({
    viewport: { width: W, height: H }, deviceScaleFactor: 1,
    recordVideo: { dir: recDir, size: { width: W, height: H } },
  });
  const page = await ctx.newPage();
  const errs = [];
  page.on('pageerror', e => errs.push(e.message));
  await page.goto(pathToFileURL(path.resolve(htmlPath)).href, { waitUntil: 'load' });
  await page.waitForTimeout(Math.ceil((sec + 0.35) * 1000));
  const video = page.video();
  await page.close(); await ctx.close(); await browser.close();
  fs.copyFileSync(await video.path(), outWebm);
  fs.rmSync(recDir, { recursive: true, force: true });
  if (errs.length) console.warn(`    ! JS errors: ${errs.slice(0,3).join(' | ')}`);
}

// codifica una escena a mp4 uniforme (v: h264 1280x720 30fps ; a: aac SIEMPRE, silencio si no hay voz)
function encodeScene(baseVideo, audioFile, totalSec, tail, outMp4) {
  const vf = `[0:v]scale=${W}:${H}:force_original_aspect_ratio=decrease,` +
             `pad=${W}:${H}:(ow-iw)/2:(oh-ih)/2,fps=${FPS},format=yuv420p[v]`;
  const args = ['-y', '-i', baseVideo];
  if (audioFile) {
    // La voz de ElevenLabs es MONO. Forzamos mono en TODAS las escenas (incluida la intro,
    // abajo) para que el concat -c copy tenga layouts uniformes; mezclar mono+stereo produce
    // un "eco" de fase. apad rellena el silencio de cola. Sin loudnorm: el nivel crudo (~-20 dB)
    // es el que Pedro validó en 01-gancho.
    args.push('-i', audioFile,
      '-filter_complex', `${vf};[1:a]apad=pad_dur=${tail},aformat=channel_layouts=mono[a]`,
      '-map', '[v]', '-map', '[a]');
  } else {
    args.push('-f', 'lavfi', '-i', `anullsrc=r=44100:cl=mono`,
      '-filter_complex', vf,
      '-map', '[v]', '-map', '1:a:0');
  }
  args.push(
    '-t', totalSec.toFixed(2),
    '-c:v', 'libx264', '-crf', '20', '-preset', 'medium',
    '-c:a', 'aac', '-b:a', '160k', '-ar', '44100', '-ac', '1',
    '-movflags', '+faststart', outMp4);
  execFileSync('ffmpeg', args, { stdio: ['ignore','ignore','inherit'] });
}

// tarjeta intro simple (HTML -> hold -> mp4)
async function buildIntro(intro) {
  const html = path.resolve(HERE, intro.html || 'scenes/intro.html');
  if (!fs.existsSync(html)) return null;
  const sec = intro.seconds ?? 4;
  const webm = path.join(sceneMp4Dir, '00-intro.webm');
  const mp4  = path.join(sceneMp4Dir, '00-intro.mp4');
  console.log(`  intro (${sec}s)`);
  await recordHtml(html, sec, webm);
  encodeScene(webm, null, sec, 0, mp4);
  return { id: '00-intro', mp4, dur: sec, narration: null, start: 0 };
}

// ---------- main ----------
console.log(`\n=== ${guion.title || 'video'} ${SILENT ? '[MUDO]' : '[narrado: Marcela]'} ===`);
const rendered = [];   // { id, mp4, dur, narration, speech }
let scenes = guion.scenes;
if (onlyId) scenes = scenes.filter(s => s.id === onlyId);

// intro solo cuando renderizamos todo
if (guion.intro && !onlyId) { const it = await buildIntro(guion.intro); if (it) rendered.push({ ...it, speech: 0 }); }

for (const sc of scenes) {
  const tail = sc.tailSeconds ?? 1.2;
  const minSec = sc.minSeconds ?? 6;
  console.log(`\n-- escena ${sc.id} --`);

  // 1) voz
  let audio = null;
  if (!SILENT && sc.narration) {
    audio = await narrate(sc.narration, path.join(workDir, `${sc.id}.mp3`), sc.voice || 'Marcela');
  } else if (sc.narration) {
    console.log('   (mudo)');
  }
  const speech = audio ? audio.seconds : minSec;
  const totalSec = Math.max(minSec, speech + (audio ? tail : 0));

  // 2) base de video: clip de b-roll o HTML animado
  const outMp4 = path.join(sceneMp4Dir, `${sc.id}.mp4`);
  if (sc.clip) {
    const clip = path.resolve(HERE, sc.clip);
    if (!fs.existsSync(clip)) { console.warn(`   ! clip no existe, ESCENA OMITIDA: ${sc.clip}`); continue; }
    encodeScene(clip, audio ? audio.file : null, totalSec, tail, outMp4);
  } else {
    const webm = path.join(sceneMp4Dir, `${sc.id}.webm`);
    await recordHtml(path.resolve(HERE, sc.html), totalSec, webm);
    encodeScene(webm, audio ? audio.file : null, totalSec, tail, outMp4);
  }
  const dur = probe(outMp4);
  console.log(`   -> ${sc.id}.mp4  ${dur.toFixed(1)}s`);
  rendered.push({ id: sc.id, mp4: outMp4, dur, narration: sc.narration, speech });
}

// ---------- concat + srt global ----------
const baseName = onlyId ? `${guion.id || 'video'}--${onlyId}` : (guion.id || 'video');
if (rendered.length === 1) {
  fs.copyFileSync(rendered[0].mp4, path.join(outDir, `${baseName}.mp4`));
} else {
  const listFile = path.join(workDir, 'concat.txt');
  fs.writeFileSync(listFile, rendered.map(r => `file '${r.mp4.replace(/\\/g,'/')}'`).join('\n'));
  execFileSync('ffmpeg', ['-y','-f','concat','-safe','0','-i',listFile,'-c','copy','-movflags','+faststart',
    path.join(outDir, `${baseName}.mp4`)], { stdio: ['ignore','ignore','inherit'] });
}

// SRT global con offsets acumulados
let off = 0; const allCues = [];
for (const r of rendered) {
  if (r.narration) {
    for (const c of cuesProportional(r.narration, r.speech)) allCues.push({ start: c.start + off, end: c.end + off, text: c.text });
  }
  off += r.dur;
}
if (allCues.length) fs.writeFileSync(path.join(outDir, `${baseName}.srt`), toSrt(allCues));

const total = rendered.reduce((a, r) => a + r.dur, 0);
console.log(`\nLISTO -> out/${baseName}.mp4  (${total.toFixed(1)}s, ${rendered.length} clip(s), ${SILENT ? 'MUDO' : 'narrado'})`);
