/*
  build-short.mjs — pipeline de un short narrado (adaptado de WikiIllustrationKit).
  HERE = esta carpeta (self-contained): HTML + scenes + out/_work locales.
  La narración (ElevenLabs) y los captions se reusan del kit por ruta absoluta.

  uso:  node build-short.mjs scenes/<short>.json
  scene: { id, html, narration, voice?, minSeconds?, tailSeconds? }
*/
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { execFileSync } from 'node:child_process';
const KIT = 'file:///C:/Proyecto/SISTEMA/Tools/WikiIllustrationKit/shorts';
const { narrate, getKey } = await import(`${KIT}/narrate.mjs`);
const { cuesProportional, toSrt } = await import(`${KIT}/captions.mjs`);

const HERE = path.dirname(new URL(import.meta.url).pathname.replace(/^\//, ''));
const W = 1080, H = 1920;

const sceneFile = process.argv[2];
if (!sceneFile) { console.error('uso: node build-short.mjs scenes/<short>.json'); process.exit(1); }
const scene = JSON.parse(fs.readFileSync(sceneFile, 'utf8'));
const id = scene.id || path.basename(sceneFile, '.json');
const htmlPath = path.resolve(HERE, scene.html);
const outDir = path.resolve(HERE, 'out');
const workDir = path.resolve(HERE, '_work', id);
fs.mkdirSync(outDir, { recursive: true });
fs.mkdirSync(workDir, { recursive: true });

const tail = scene.tailSeconds ?? 1.3;
const minSec = scene.minSeconds ?? 18;

// ---------- 1) voz ----------
// Se reutiliza el mp3 ya generado si existe. Cuadrar los tiempos de las animaciones con la
// narración es, por naturaleza, iterativo: se renderiza, se mira, se ajustan los retardos y se
// vuelve a renderizar. Sin esto, cada uno de esos ajustes vuelve a llamar a ElevenLabs y a pagar
// por un audio idéntico. Con --revoice se fuerza a regenerarla (cambió el texto).
let audio = null;
const voicePath = path.join(workDir, 'voz.mp3');
const revoice = process.argv.includes('--revoice');

if (scene.narration && !revoice && fs.existsSync(voicePath)) {
  const { probeDuration } = await import(`${KIT}/narrate.mjs`);
  audio = { file: voicePath, seconds: probeDuration(voicePath) };
  console.log(`  narración reutilizada: voz.mp3 (${audio.seconds.toFixed(1)}s) — usa --revoice si cambió el texto`);
} else if (getKey() && scene.narration) {
  audio = await narrate(scene.narration, voicePath, scene.voice || process.env.ELEVENLABS_VOICE || 'Marcela');
} else {
  console.log('  (sin ELEVENLABS_API_KEY o sin narración) -> short SILENCIOSO');
}
const voiceSec = audio ? audio.seconds : minSec;
const totalSec = Math.max(minSec, voiceSec + tail);

// ---------- 2) grabar HTML ----------
const PW = process.env.PW_PKG ||
  'C:/Proyecto/Xari/CoreReportsSolution/tools/video-generator/node_modules/playwright/index.js';
const pw = await import(pathToFileURL(PW).href);
const chromium = pw.chromium || pw.default?.chromium;
if (!chromium) { console.error('no pude cargar chromium de playwright'); process.exit(1); }

const recDir = path.join(workDir, 'rec');
fs.mkdirSync(recDir, { recursive: true });
const browser = await chromium.launch();
const ctx = await browser.newContext({
  viewport: { width: W, height: H },
  deviceScaleFactor: 1,
  recordVideo: { dir: recDir, size: { width: W, height: H } },
});
const page = await ctx.newPage();
await page.goto(pathToFileURL(htmlPath).href);
await page.waitForTimeout(Math.ceil((totalSec + 0.4) * 1000));
const video = page.video();
await page.close(); await ctx.close(); await browser.close();
const webm = path.join(workDir, `${id}.webm`);
fs.copyFileSync(await video.path(), webm);
fs.rmSync(recDir, { recursive: true, force: true });
console.log(`  webm: ${(totalSec).toFixed(1)}s`);

// ---------- 3) mux + encode ----------
const outMp4 = path.join(outDir, `${id}.mp4`);
const args = ['-y', '-i', webm];
if (audio) args.push('-i', audio.file);
if (audio) {
  args.push(
    '-filter_complex', `[1:a]apad=pad_dur=${tail}[a]`,
    '-map', '0:v', '-map', '[a]',
    '-t', totalSec.toFixed(2),
    '-c:a', 'aac', '-b:a', '160k'
  );
} else {
  args.push('-t', totalSec.toFixed(2), '-an');
}
args.push(
  '-c:v', 'libx264', '-crf', '20', '-pix_fmt', 'yuv420p',
  '-r', '30', '-movflags', '+faststart', outMp4
);
execFileSync('ffmpeg', args, { stdio: 'inherit' });

if (scene.narration) {
  const speech = audio ? audio.seconds : totalSec;
  const cues = cuesProportional(scene.narration, speech);
  fs.writeFileSync(path.join(outDir, `${id}.srt`), toSrt(cues));
  console.log(`  srt: ${cues.length} cues (${speech.toFixed(1)}s de habla)`);
}

console.log(`\nLISTO -> ${outMp4}  (${totalSec.toFixed(1)}s, ${audio ? 'narrado' : 'silencioso'})`);
