using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SignsOfAI.Core;
using SignsOfAI.Core.Rules;
using SignsOfAI.Web;
using SignsOfAI.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// The analysis engine is pure & stateless — one instance for the whole app, runs in-browser.
builder.Services.AddSingleton<AiWritingAnalyzer>();

// Browser-side helpers.
builder.Services.AddScoped<BrowserStorage>();
// Humanizer talks to the LLM provider directly, so it needs an unbound HttpClient (no app base).
builder.Services.AddScoped(sp => new HumanizerService(new HttpClient()));
// Prebuilt BM25 index over the rule catalog for the /catalog page.
builder.Services.AddSingleton(sp => new CatalogSearch(RuleCatalog.All()));
// User-defined catalogs (custom rule-packs) stored in the browser.
builder.Services.AddScoped<CatalogStore>();

await builder.Build().RunAsync();
