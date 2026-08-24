using Microsoft.Extensions.DependencyInjection;
using SignsOfAI.Core;
using SignsOfAI.Core.Originality;
using SignsOfAI.Core.Rules;
using SignsOfAI.UI.Services;

namespace SignsOfAI.UI;

/// <summary>
/// The one place the interface's services are registered.
///
/// Both hosts call this — the web app in <c>Program.cs</c> and the desktop app when it builds its
/// WebView's service collection. Keeping it here rather than duplicating the list in each host is
/// what stops the two from drifting: a service added for one is automatically present in the other,
/// so a feature can never work in the browser and quietly throw on the desktop.
/// </summary>
public static class UiServices
{
    public static IServiceCollection AddSignsOfAiUi(this IServiceCollection services)
    {
        // The analysis engine is pure & stateless — one instance for the whole app, and it runs on
        // the user's machine in both hosts: nothing is uploaded anywhere.
        services.AddSingleton<AiWritingAnalyzer>();
        // Originality/copy checker — also pure & local; compares documents against each other.
        services.AddSingleton(sp => new OriginalityChecker());
        // Prebuilt BM25 index over the rule catalog for the /catalog page.
        services.AddSingleton(sp => new CatalogSearch(RuleCatalog.All()));

        // What this host can do. Browser defaults; a host that can do more replaces both of these
        // after calling this method — see SignsOfAI.Desktop.
        services.AddSingleton(HostCapabilities.Browser);

        // Reading a picked file. This is the browser's answer — Word and plain text, no dependency.
        // A host that can do better registers its own IDocumentReader *after* calling this method;
        // the last registration is the one resolved. SignsOfAI.Desktop does exactly that.
        services.AddScoped<IDocumentReader, BrowserDocumentReader>();

        // Scanning a folder. The browser is handed files, never a folder, so it has nothing to
        // offer and the interface hides the feature. The desktop replaces this.
        services.AddScoped<IFolderBatch, NoFolderBatch>();

        // Predictability from a model inside the app. Not something to hand a web visitor — an ONNX
        // runtime and a half-gigabyte of weights — so the browser keeps offering the optional server.
        services.AddScoped<ILocalPerplexity, NoLocalPerplexity>();

        // Telling the user a newer build exists. Nothing to tell in a browser tab, which always
        // serves whatever was last deployed; a host that gets downloaded replaces this.
        services.AddScoped<IUpdateCheck, NoUpdateCheck>();

        // Local persistence (localStorage in the browser, the WebView's own store on the desktop).
        services.AddScoped<BrowserStorage>();
        // Whether the user agreed to version checks, and when the last one was.
        services.AddScoped<UpdatePreference>();
        // User-defined catalogs (custom rule-packs), kept in that same local store.
        services.AddScoped<CatalogStore>();
        // Interface language (EN/ES/…), remembered locally.
        services.AddScoped<Loc>();

        // These four talk to endpoints of their own with absolute URLs, so each gets an unbound
        // HttpClient — no app base address. That is also why they need no host-specific handling:
        // on the desktop the very same calls go out natively, and without a CORS preflight.
        services.AddScoped(sp => new HumanizerService(new HttpClient()));
        services.AddScoped(sp => new PerplexityClient(new HttpClient()));
        services.AddScoped(sp => new EmbeddingClient(new HttpClient()));
        services.AddScoped(sp => new WebCheckClient(new HttpClient()));

        return services;
    }
}
