// Starting the app, out loud when it fails.
//
// The runtime arrives as ~54 separate files, each one pinned by a SHA-256 in the boot manifest.
// GitHub Pages answers one of them with a 503 now and then. The browser hashes whatever body did
// arrive — an error page, not the assembly — the integrity check fails, and Blazor gives up for
// good. What the visitor saw was the loading circle, forever, with no message: the failure happens
// before Blazor has an error UI to show. That is how this reached us, as "the app is down", when
// every file on the server was intact.
//
// So: retry the download before believing it. The panel that speaks when the retry does not help
// lives inline in index.html, because it also has to survive this file never arriving.
(function () {
    const ATTEMPTS = 3;
    const BACKOFF_MS = [400, 1200];

    // A fetch has no timeout of its own. A server that accepts the connection and then never
    // answers would otherwise consume no attempt, reach no panel, and hand back the same eternal
    // spinner this file exists to kill — the failure mode measured and missed on the first pass.
    const ATTEMPT_TIMEOUT_MS = 25000;

    // The .NET runtime's own scripts are ES modules: the loader asserts they come back as a URL, so
    // a Response would break the import outright. Those keep the default loader — and therefore get
    // no retry, which is one of the holes the inline watchdog covers.
    const NOT_OURS = 'dotnetjs';

    const boot = window.signsofaiBoot;

    async function fetchWithRetry(url, integrity) {
        // Local to this download, never shared. A module-level "last failure" would let a file that
        // failed once and then recovered be named as the cause of somebody else's error later.
        let failure = null;

        for (let attempt = 0; attempt < ATTEMPTS; attempt++) {
            if (attempt > 0) {
                await new Promise(done => setTimeout(done, BACKOFF_MS[attempt - 1] ?? 1200));
            }

            const abort = new AbortController();
            const timer = setTimeout(() => abort.abort(), ATTEMPT_TIMEOUT_MS);
            try {
                const response = await fetch(url, {
                    // Handing fetch the manifest's own hash keeps the guarantee we would otherwise
                    // be dropping. Returning a Response takes the check away from Blazor entirely:
                    // the runtime short-circuits on a returned promise before it sets integrity of
                    // its own, so this line is not hygiene, it is the whole verification.
                    integrity: integrity || undefined,
                    // A first try may legitimately come from the browser cache. A retry may not —
                    // if what we hold is a truncated or stale body, asking for it again is useless.
                    cache: attempt === 0 ? 'default' : 'reload',
                    signal: abort.signal,
                });
                if (response.ok) {
                    if (attempt > 0) {
                        // The only trace a rescued startup leaves. Without it, a recovered blip is
                        // indistinguishable from a clean load, and the next person to ask "was the
                        // site flaky?" has nothing to look at.
                        console.info(`[signsofai] ${url} recovered after ${attempt + 1} attempts`);
                    }
                    boot.progress();
                    return response;
                }
                failure = { url, reason: `HTTP ${response.status}` };
            } catch (error) {
                // An integrity mismatch rejects here too, which is the case we are actually chasing:
                // a 503 body hashes to something else. So does an abort. Both are worth retrying.
                failure = { url, reason: String(error && error.message || error) };
            } finally {
                clearTimeout(timer);
            }
        }

        // Blazor does not reject Blazor.start() when a boot file will not come — the failure
        // surfaces as an unhandled rejection inside mono_download_assets, which is why the spinner
        // used to spin for good. We are the ones who know this download is out of tries, so the
        // explanation is asked for from here rather than from a .catch that never runs.
        boot.fail(`${failure.reason}\n${failure.url}`);
        throw new Error(`${failure.reason} for ${failure.url}`);
    }

    // A 503 on the Blazor script leaves this file running with nothing to call. Saying so now beats
    // waiting for the watchdog: we already know it is never going to start.
    if (typeof Blazor === 'undefined') {
        boot.fail('Blazor.start is unavailable — _framework/blazor.webassembly.js did not load.');
        return;
    }

    Blazor.start({
        loadBootResource(type, name, defaultUri, integrity) {
            return type === NOT_OURS ? undefined : fetchWithRetry(defaultUri, integrity);
        },
    }).then(
        () => boot.ok(),
        // Reached by startup failures that are not downloads at all. Those carry their own error,
        // and it is the one to show — the retry's own failures have already spoken for themselves.
        error => boot.fail(String(error && error.message || error))
    );
})();
