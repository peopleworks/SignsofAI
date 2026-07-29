using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SignsOfAI.Core;
using SignsOfAI.Core.Originality;
using SignsOfAI.Core.Rules;
using SignsOfAI.Web;
using SignsOfAI.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// The analysis engine is pure & stateless — one instance for the whole app, runs in-browser.
builder.Services.AddSingleton<AiWritingAnalyzer>();
// Originality/copy checker — also pure & client-side; compares documents against each other.
builder.Services.AddSingleton(sp => new OriginalityChecker());

// Browser-side helpers.
builder.Services.AddScoped<BrowserStorage>();
// Humanizer talks to the LLM provider directly, so it needs an unbound HttpClient (no app base).
builder.Services.AddScoped(sp => new HumanizerService(new HttpClient()));
// Perplexity client talks to our (optional) server-side endpoint — unbound HttpClient (its own base URL).
builder.Services.AddScoped(sp => new PerplexityClient(new HttpClient()));
// Embedding client for the optional server-side paraphrase check — also an unbound HttpClient.
builder.Services.AddScoped(sp => new EmbeddingClient(new HttpClient()));
// Web-check client for the optional server-side automatic web search — also an unbound HttpClient.
builder.Services.AddScoped(sp => new WebCheckClient(new HttpClient()));
// Prebuilt BM25 index over the rule catalog for the /catalog page.
builder.Services.AddSingleton(sp => new CatalogSearch(RuleCatalog.All()));
// User-defined catalogs (custom rule-packs) stored in the browser.
builder.Services.AddScoped<CatalogStore>();
// Interface language (EN/ES), remembered in localStorage.
builder.Services.AddScoped<Loc>();

var host = builder.Build();

// Resolve the stored/browser language before the first render, so a Spanish visitor never sees a
// flash of English chrome. In WASM the root provider is the one scope components resolve from, so
// this is the very same Loc instance the components inject.
await host.Services.GetRequiredService<Loc>().InitAsync();

await host.RunAsync();
