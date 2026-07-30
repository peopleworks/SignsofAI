// Renders a shareable result card onto a <canvas>. Privacy-preserving: only the summary,
// never the analyzed text.
window.signsofai = window.signsofai || {};

window.signsofai.drawCard = function (canvas, d) {
    if (typeof d === 'string') d = JSON.parse(d);
    const W = 1200, H = 630;
    canvas.width = W; canvas.height = H;
    const ctx = canvas.getContext('2d');

    // Background
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, W, H);
    ctx.fillStyle = '#2563eb';
    ctx.fillRect(0, 0, 10, H); // brand spine

    const sans = "700 42px system-ui, 'Segoe UI', Roboto, Arial, sans-serif";
    // Header
    ctx.fillStyle = '#2563eb';
    ctx.font = sans;
    ctx.textBaseline = 'alphabetic';
    ctx.fillText('Signs of AI Writing', 60, 92);
    ctx.fillStyle = '#5b6470';
    ctx.font = "400 22px system-ui, 'Segoe UI', Roboto, Arial, sans-serif";
    ctx.fillText('AI-writing analysis · runs in your browser', 60, 124);

    // Score ring
    const cx = 250, cy = 360, r = 135, lw = 26;
    ctx.lineWidth = lw;
    ctx.strokeStyle = '#e5e9ee';
    ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.stroke();
    const sweep = Math.max(0, Math.min(100, d.score)) / 100 * Math.PI * 2;
    ctx.strokeStyle = d.scoreColor;
    ctx.lineCap = 'round';
    ctx.beginPath(); ctx.arc(cx, cy, r, -Math.PI / 2, -Math.PI / 2 + sweep); ctx.stroke();
    ctx.lineCap = 'butt';
    // Number
    ctx.fillStyle = '#1a1d21';
    ctx.textAlign = 'center';
    ctx.font = "700 92px system-ui, 'Segoe UI', Roboto, Arial, sans-serif";
    ctx.fillText(String(Math.round(d.score)), cx, cy + 20);
    ctx.fillStyle = '#5b6470';
    ctx.font = "600 26px system-ui, Arial, sans-serif";
    ctx.fillText('/ 100', cx, cy + 58);
    ctx.textAlign = 'left';

    // Right column
    const rx = 460;
    ctx.fillStyle = d.scoreColor;
    ctx.font = "700 46px system-ui, 'Segoe UI', Roboto, Arial, sans-serif";
    ctx.fillText(d.verdict, rx, 210);
    ctx.fillStyle = '#5b6470';
    ctx.font = "400 24px system-ui, Arial, sans-serif";
    ctx.fillText(`${d.signals} signal${d.signals === 1 ? '' : 's'} · ${d.language}`, rx, 248);

    // Category pills
    let px = rx, py = 285;
    ctx.font = "600 22px system-ui, Arial, sans-serif";
    for (const c of d.categories) {
        const label = `${c.label} ${c.count}`;
        const w = ctx.measureText(label).width + 34;
        roundRect(ctx, px, py, w, 40, 20);
        ctx.fillStyle = hexA(c.color, 0.14); ctx.fill();
        ctx.fillStyle = c.color; ctx.fillText(label, px + 17, py + 27);
        px += w + 12;
        if (px > W - 200) { px = rx; py += 52; }
    }

    // Rhythm sparkline
    const bx = rx, by = 380, bw = W - rx - 60, bh = 110;
    ctx.fillStyle = '#5b6470';
    ctx.font = "600 18px system-ui, Arial, sans-serif";
    ctx.fillText('SENTENCE RHYTHM', bx, by - 10);
    drawSparkline(ctx, d.lengths, bx, by, bw, bh);

    // Stats
    ctx.fillStyle = '#1a1d21';
    ctx.font = "400 22px system-ui, Arial, sans-serif";
    ctx.fillText(`${d.words} words · ${d.sentences} sentences · burstiness ${d.burstiness}`, bx, by + bh + 44);

    // Footer
    ctx.fillStyle = '#9aa4b0';
    ctx.font = "400 20px system-ui, Arial, sans-serif";
    ctx.textAlign = 'center';
    ctx.fillText('peopleworks.github.io/SignsofAI   ·   PeopleWorks — Pedro Hernández, Microsoft MVP for .NET', W / 2, H - 32);
    ctx.textAlign = 'left';
};

window.signsofai.downloadText = function (filename, text) {
    const blob = new Blob([text], { type: 'application/json' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = filename;
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
};

window.signsofai.downloadCanvas = function (canvas, filename) {
    canvas.toBlob(function (blob) {
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = filename || 'signsofai-score.png';
        document.body.appendChild(a); a.click(); a.remove();
        setTimeout(() => URL.revokeObjectURL(a.href), 1000);
    }, 'image/png');
};

function drawSparkline(ctx, lengths, x, y, w, h) {
    if (!lengths || lengths.length === 0) return;
    // downsample to <= 48 bars
    let vals = lengths.slice();
    if (vals.length > 48) {
        const bucket = Math.ceil(vals.length / 48), out = [];
        for (let i = 0; i < vals.length; i += bucket) {
            let s = 0, n = 0;
            for (let j = i; j < i + bucket && j < vals.length; j++) { s += vals[j]; n++; }
            out.push(s / n);
        }
        vals = out;
    }
    const max = Math.max(...vals, 1);
    const mean = vals.reduce((a, b) => a + b, 0) / vals.length;
    const slot = w / vals.length, bw = slot * 0.72;
    for (let i = 0; i < vals.length; i++) {
        const v = vals[i];
        const bh = Math.max(3, (v / max) * h);
        const dev = Math.min(1, Math.abs(v - mean) / Math.max(mean, 1) * 2.2);
        ctx.fillStyle = lerpHex([148, 163, 184], [22, 163, 74], dev);
        ctx.fillRect(x + i * slot + slot * 0.14, y + (h - bh), bw, bh);
    }
    // mean line
    ctx.strokeStyle = 'rgba(120,130,145,.5)';
    ctx.setLineDash([5, 4]); ctx.lineWidth = 1;
    const my = y + (h - (mean / max) * h);
    ctx.beginPath(); ctx.moveTo(x, my); ctx.lineTo(x + w, my); ctx.stroke();
    ctx.setLineDash([]);
}

function roundRect(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
}

function hexA(hex, a) {
    const [r, g, b] = hexToRgb(hex);
    return `rgba(${r},${g},${b},${a})`;
}
function lerpHex(a, b, t) {
    const r = Math.round(a[0] + (b[0] - a[0]) * t);
    const g = Math.round(a[1] + (b[1] - a[1]) * t);
    const bl = Math.round(a[2] + (b[2] - a[2]) * t);
    return `rgb(${r},${g},${bl})`;
}
function hexToRgb(hex) {
    hex = hex.replace('#', '');
    return [parseInt(hex.slice(0, 2), 16), parseInt(hex.slice(2, 4), 16), parseInt(hex.slice(4, 6), 16)];
}
