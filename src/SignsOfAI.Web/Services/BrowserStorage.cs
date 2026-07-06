using Microsoft.JSInterop;

namespace SignsOfAI.Web.Services;

/// <summary>Thin wrapper over the browser's localStorage. Used to persist the user's own API key
/// locally — it is never sent anywhere except directly to the Anthropic API from the browser.</summary>
public sealed class BrowserStorage(IJSRuntime js)
{
    public async ValueTask<string?> GetAsync(string key) =>
        await js.InvokeAsync<string?>("localStorage.getItem", key);

    public async ValueTask SetAsync(string key, string value) =>
        await js.InvokeVoidAsync("localStorage.setItem", key, value);

    public async ValueTask RemoveAsync(string key) =>
        await js.InvokeVoidAsync("localStorage.removeItem", key);
}
