// Behaviour tests for src/SignsOfAI.Web/wwwroot/js/boot.js.
//
// These replace an earlier set that asserted on the file's *text*. A reviewer showed that every one
// of those passed against code that was broken on purpose — `void undefined;` satisfied the test
// guarding the ES-module exclusion while breaking every module load. A test you can satisfy without
// doing the thing is worse than no test: it reports safety it never checked.
//
// So this runs the real file. The script is an IIFE that calls Blazor.start() on load, so each test
// builds a fresh sandbox, hands it a fake Blazor that captures the loadBootResource callback, and
// then drives that callback directly.
//
//     node --test tests/boot/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const BOOT_JS = fileURLToPath(
    new URL('../../src/SignsOfAI.Web/wwwroot/js/boot.js', import.meta.url));
const SOURCE = readFileSync(BOOT_JS, 'utf8');

// The one delay the file uses for its per-attempt abort. Recognising it by value lets the clock run
// the backoffs instantly while still letting a test choose when the timeout itself fires.
const ABORT_DELAY = 25000;

/**
 * Loads boot.js into a fresh sandbox.
 *
 * @param {object} options
 * @param {Function} options.fetch          stands in for the browser's fetch
 * @param {number}   [options.abortAfterMs] real delay to use for the 25 s abort timer
 * @param {boolean}  [options.withBlazor]   false to simulate the Blazor script never arriving
 * @param {Function} [options.onStart]      what the fake Blazor.start() resolves or rejects with
 */
function load({ fetch, abortAfterMs = 500, withBlazor = true, onStart }) {
    const calls = [];
    const boot = {
        started: false, shown: false, detail: undefined, progressed: 0,
        progress() { boot.progressed++; },
        ok() { boot.started = true; },
        fail(detail) {
            if (boot.started || boot.shown) return;
            boot.shown = true;
            boot.detail = detail ?? null;
        },
    };

    let loadBootResource;
    const sandbox = {
        window: { signsofaiBoot: boot },
        AbortController,
        console: { info: (...args) => calls.push(args.join(' ')) },
        setTimeout: (fn, delay) => setTimeout(fn, delay === ABORT_DELAY ? abortAfterMs : 1),
        clearTimeout,
        fetch: (url, init) => fetch(url, init),
    };
    if (withBlazor) {
        sandbox.Blazor = {
            start(options) {
                loadBootResource = options.loadBootResource;
                return onStart ? onStart() : new Promise(() => {});
            },
        };
    }

    vm.createContext(sandbox);
    vm.runInContext(SOURCE, sandbox);
    return { boot, logged: calls, load: (...args) => loadBootResource(...args) };
}

const ok = () => ({ ok: true, status: 200 });
const unavailable = () => ({ ok: false, status: 503 });

test('a transient 503 is retried and the asset still loads', async () => {
    let attempts = 0;
    const app = load({ fetch: async () => (++attempts < 3 ? unavailable() : ok()) });

    const response = await app.load('assembly', 'A.wasm', '/A.wasm', 'sha256-x');

    assert.equal(response.ok, true);
    assert.equal(attempts, 3, 'should have used all three attempts');
    assert.equal(app.boot.shown, false, 'a recovered download must not raise the panel');
    assert.equal(app.boot.progressed, 1, 'a completed download must report progress');
});

test('a recovered download says so, so a rescued startup leaves a trace', async () => {
    let attempts = 0;
    const app = load({ fetch: async () => (++attempts < 2 ? unavailable() : ok()) });

    await app.load('assembly', 'A.wasm', '/A.wasm', 'sha256-x');

    assert.match(app.logged.join('\n'), /recovered after 2 attempts/);
});

test('a download that never succeeds gives up and names the file it could not get', async () => {
    let attempts = 0;
    const app = load({ fetch: async () => { attempts++; return unavailable(); } });

    await assert.rejects(() => app.load('assembly', 'A.wasm', '/A.wasm', 'sha256-x'));

    assert.equal(attempts, 3, 'should stop after three attempts, not retry for ever');
    assert.equal(app.boot.shown, true, 'the panel must be raised from the retry loop');
    assert.match(app.boot.detail, /HTTP 503/);
    assert.match(app.boot.detail, /\/A\.wasm/);
});

test('the manifest hash is handed to fetch on every attempt', async () => {
    // Load-bearing, and silent if it breaks. Returning a Response short-circuits the runtime before
    // it sets integrity of its own, so dropping this would fail no test and quietly stop verifying
    // every assembly the app loads.
    const seen = [];
    const app = load({ fetch: async (url, init) => { seen.push(init.integrity); return unavailable(); } });

    await assert.rejects(() => app.load('assembly', 'A.wasm', '/A.wasm', 'sha256-abc'));

    assert.deepEqual(seen, ['sha256-abc', 'sha256-abc', 'sha256-abc']);
});

test('an asset with no hash in the manifest is not sent an empty integrity', async () => {
    // The runtime normalises a missing hash to "", and fetch would treat "" as "verify nothing"
    // rather than "no opinion". Passing undefined keeps parity with the default loader.
    let seen;
    const app = load({ fetch: async (url, init) => { seen = init; return ok(); } });

    await app.load('assembly', 'A.wasm', '/A.wasm', '');

    assert.equal(seen.integrity, undefined);
});

test('the first attempt may use the cache; the retries may not', async () => {
    const seen = [];
    const app = load({ fetch: async (url, init) => { seen.push(init.cache); return unavailable(); } });

    await assert.rejects(() => app.load('assembly', 'A.wasm', '/A.wasm', 'sha256-x'));

    assert.deepEqual(seen, ['default', 'reload', 'reload']);
});

test('the runtime ES modules are left to the default loader and never fetched here', async () => {
    // Answering these with a Response breaks the import outright — a fix for rare 503s turned into
    // a permanent failure. The old test for this passed against `void undefined;`.
    let fetched = 0;
    const app = load({ fetch: async () => { fetched++; return ok(); } });

    const result = await app.load('dotnetjs', 'dotnet.js', '/dotnet.js', 'sha256-x');

    assert.equal(result, undefined, 'must hand dotnetjs back to the default loader');
    assert.equal(fetched, 0, 'must not fetch a dotnetjs module itself');
});

test('a connection that hangs is abandoned rather than waited on for ever', async () => {
    // The failure the first version missed: fetch has no timeout, so a server that accepts and then
    // never answers consumed no attempt, reached no panel, and left the eternal spinner in place.
    let attempts = 0;
    const app = load({
        abortAfterMs: 5,
        fetch: (url, init) => new Promise((resolve, reject) => {
            attempts++;
            init.signal.addEventListener('abort', () => reject(new Error('aborted')));
        }),
    });

    await assert.rejects(() => app.load('assembly', 'A.wasm', '/A.wasm', 'sha256-x'));

    assert.equal(attempts, 3);
    assert.equal(app.boot.shown, true, 'a hung connection must still reach the panel');
});

test('a file that failed once and recovered is not blamed for a later startup error', async () => {
    // The misattribution a reviewer found by executing it: failure state kept per module rather
    // than per download let a recovered file be named as the cause of somebody else's error.
    let attempts = 0;
    let capture;
    const app = load({
        fetch: async () => (++attempts === 1 ? unavailable() : ok()),
        onStart: () => new Promise((resolve, reject) => { capture = reject; }),
    });

    await app.load('assembly', 'recovered.wasm', '/recovered.wasm', 'sha256-x');
    capture(new Error('WASM instantiation failed'));
    await new Promise(done => setTimeout(done, 5));

    assert.equal(app.boot.shown, true);
    assert.match(app.boot.detail, /WASM instantiation failed/);
    assert.doesNotMatch(app.boot.detail, /recovered\.wasm/,
        'the panel must report the error that raised it, not a download that succeeded');
});

test('a successful startup marks the app as started', async () => {
    const app = load({ fetch: async () => ok(), onStart: () => Promise.resolve() });

    await new Promise(done => setTimeout(done, 5));

    assert.equal(app.boot.started, true);
    assert.equal(app.boot.shown, false);
});

test('a missing Blazor script is reported at once rather than waited out', async () => {
    // A 503 on blazor.webassembly.js leaves this file running with nothing to call. Before the
    // guard it threw a synchronous ReferenceError that no catch saw.
    const app = load({ fetch: async () => ok(), withBlazor: false });

    assert.equal(app.boot.shown, true);
    assert.match(app.boot.detail, /blazor\.webassembly\.js/);
});
