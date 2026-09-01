// Starting the app, out loud when it fails.
//
// The runtime arrives as ~50 separate files, each one pinned by a SHA-256 in the boot manifest.
// GitHub Pages answers one of them with a 503 now and then. The browser hashes whatever body did
// arrive — an error page, not the assembly — the integrity check fails, and Blazor gives up for
// good. What the visitor sees is the loading circle, forever, with no message: the failure happens
// before Blazor has an error UI to show. That is how this reached us, as "the app is down", when
// every file on the server was intact.
//
// So: retry the download before believing it, and if it still will not come, say so.
(function () {
    const ATTEMPTS = 3;
    const BACKOFF_MS = [400, 1200];

    // The .NET runtime's own scripts are ES modules — they have to be handed back as a URL for the
    // import to work, so a Response would break them. Those keep the default loader.
    const NOT_OURS = 'dotnetjs';

    let lastFailure = null;

    async function fetchWithRetry(url, integrity) {
        for (let attempt = 0; attempt < ATTEMPTS; attempt++) {
            if (attempt > 0) {
                await new Promise(done => setTimeout(done, BACKOFF_MS[attempt - 1] ?? 1200));
            }
            try {
                const response = await fetch(url, {
                    // Handing fetch the manifest's own hash keeps the guarantee we would otherwise
                    // be dropping: returning a Response from loadBootResource takes the integrity
                    // check away from Blazor, so the browser has to do it here instead.
                    integrity: integrity || undefined,
                    // A first try may legitimately come from the browser cache. A retry may not —
                    // if what we hold is a truncated or stale body, asking for it again is useless.
                    cache: attempt === 0 ? 'default' : 'reload',
                });
                if (response.ok) return response;
                lastFailure = { url, reason: `HTTP ${response.status}` };
            } catch (error) {
                // An integrity mismatch rejects here too, which is the case we are actually chasing:
                // a 503 body hashes to something else, and retrying is exactly the right answer.
                lastFailure = { url, reason: String(error && error.message || error) };
            }
        }
        // Blazor does not reject Blazor.start() when a boot file will not come — the failure
        // surfaces as an unhandled rejection deep in mono_download_assets, which is why the
        // spinner used to spin for good. We are the ones who know the download is out of tries,
        // so the explanation is written from here rather than from a .catch that never runs.
        reportFailure();
        throw new Error(`${lastFailure.reason} for ${lastFailure.url}`);
    }

    // The app has not started, so Blazor cannot render this and its own error UI is not wired yet.
    // It also styles itself: app.css is a separate request, and a page that is explaining a failed
    // download should not depend on one more download having worked. The custom properties are used
    // where they exist and fall back to their light-theme values where they do not.
    let reported = false;

    function reportFailure(error) {
        // Several assets can fail in the same run; the first explanation is the one that stands.
        if (reported) return;
        reported = true;

        const spanish = (navigator.language || '').toLowerCase().startsWith('es');
        const copy = spanish ? {
            title: 'No se pudo terminar de cargar',
            body: 'Un archivo de la aplicación no llegó. Casi siempre es un fallo pasajero del ' +
                  'servidor, no un problema de tu texto ni de tu navegador. Nada de lo que ' +
                  'escribiste salió de tu equipo — la aplicación ni siquiera llegó a arrancar.',
            retry: 'Reintentar',
            details: 'Detalle técnico',
        } : {
            title: "Couldn't finish loading",
            body: 'One of the application files did not arrive. This is almost always a passing ' +
                  'server fault, not a problem with your text or your browser. Nothing you typed ' +
                  'left your machine — the application never got as far as starting.',
            retry: 'Try again',
            details: 'Technical detail',
        };

        const app = document.getElementById('app');
        if (!app) return;

        app.innerHTML = '';
        const panel = document.createElement('div');
        panel.setAttribute('role', 'alert');
        panel.style.cssText =
            'max-width:34rem;margin:12vh auto;padding:1.5rem;border-radius:14px;' +
            'border:1px solid var(--border,#e2e6ea);background:var(--surface,#fff);' +
            'color:var(--text,#1a1d21);font:1rem/1.5 system-ui,-apple-system,Segoe UI,sans-serif;';

        const title = document.createElement('h1');
        title.textContent = copy.title;
        title.style.cssText = 'margin:0 0 .6rem;font-size:1.25rem;';

        const body = document.createElement('p');
        body.textContent = copy.body;
        body.style.cssText = 'margin:0 0 1.2rem;color:var(--text-muted,#5b6470);';

        const retry = document.createElement('button');
        retry.type = 'button';
        retry.textContent = copy.retry;
        retry.style.cssText =
            'padding:.55rem 1.1rem;border:0;border-radius:8px;cursor:pointer;font:inherit;' +
            'background:var(--brand,#2563eb);color:var(--brand-ink,#fff);';
        retry.addEventListener('click', () => window.location.reload());

        const details = document.createElement('details');
        details.style.cssText = 'margin-top:1.2rem;font-size:.82rem;color:var(--text-muted,#5b6470);';
        const summary = document.createElement('summary');
        summary.textContent = copy.details;
        summary.style.cssText = 'cursor:pointer;';
        const pre = document.createElement('pre');
        // textContent, not innerHTML: the URL comes back from the network and is never markup here.
        pre.textContent = lastFailure
            ? `${lastFailure.reason}\n${lastFailure.url}`
            : String(error && error.message || error);
        pre.style.cssText = 'white-space:pre-wrap;word-break:break-all;margin:.5rem 0 0;';
        details.append(summary, pre);

        panel.append(title, body, retry, details);
        app.append(panel);
    }

    Blazor.start({
        loadBootResource(type, name, defaultUri, integrity) {
            return type === NOT_OURS ? undefined : fetchWithRetry(defaultUri, integrity);
        },
    }).catch(reportFailure);
})();
