using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SignsOfAI.UI;
using SignsOfAI.UI.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The interface and its services live in SignsOfAI.UI, shared with the desktop app. Registering
// them from that one place is what keeps the two hosts from drifting apart.
builder.Services.AddSignsOfAiUi();

var host = builder.Build();

// Resolve the stored/browser language before the first render, so a Spanish visitor never sees a
// flash of English chrome. In WASM the root provider is the one scope components resolve from, so
// this is the very same Loc instance the components inject.
await host.Services.GetRequiredService<Loc>().EnsureInitializedAsync();

await host.RunAsync();
