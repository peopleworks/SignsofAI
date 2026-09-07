using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SignsOfAI.UI;
using SignsOfAI.UI.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<SignsOfAI.Word.TaskPane>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same registration call as the other two hosts, so a service added for one is present here too.
builder.Services.AddSignsOfAiUi();

// Registered after AddSignsOfAiUi so it wins over the browser default. A task pane runs in an
// embedded browser: it is sandboxed exactly as a tab is, and it reaches no local service.
builder.Services.AddSingleton(HostCapabilities.WordTaskPane);

var host = builder.Build();
await host.Services.GetRequiredService<Loc>().EnsureInitializedAsync();
await host.RunAsync();
