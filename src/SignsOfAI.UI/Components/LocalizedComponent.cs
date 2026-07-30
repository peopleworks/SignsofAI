using Microsoft.AspNetCore.Components;
using SignsOfAI.UI.Services;

namespace SignsOfAI.UI.Components;

/// <summary>
/// Base for any component whose copy comes from <see cref="Loc"/>. It injects the service as
/// <c>L</c> and re-renders when the language flips, so a page only has to write <c>@L["key"]</c>
/// and the EN↔ES switch is instant with no page reload.
/// </summary>
public abstract class LocalizedComponent : ComponentBase, IDisposable
{
    [Inject] protected Loc L { get; set; } = default!;

    protected override void OnInitialized() => L.Changed += OnLanguageChanged;

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose() => L.Changed -= OnLanguageChanged;
}
