namespace SignsOfAI.Perplexity.Api.Config;

/// <summary>
/// Config for the <b>optional</b> automatic web spot-check (Phase D+). It is <b>off unless configured</b>:
/// the on-device, one-click-search experience is the default. When an operator supplies a search provider
/// and key (here or via the <c>BRAVE_API_KEY</c> environment variable — the key never touches the browser),
/// the server can automatically report web pages that contain a passage verbatim. Useful for live demos /
/// presentations; kept behind config so the hosted default stays dependency-free.
/// </summary>
public sealed class WebSearchOptions
{
    /// <summary>Master switch. Even when true, the feature only activates if a key resolves.</summary>
    public bool Enabled { get; init; }

    /// <summary>Search provider. Currently "brave" (Brave Search API); provider-abstracted for others.</summary>
    public string Provider { get; init; } = "brave";

    /// <summary>API key. Prefer the environment variable over committing it to appsettings.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Cap on how many distinctive phrases we search per document (quota control for live demos).</summary>
    public int MaxPhrasesPerDoc { get; init; } = 8;

    /// <summary>Cap on results returned per phrase.</summary>
    public int MaxResultsPerPhrase { get; init; } = 5;

    /// <summary>The key from config, falling back to the BRAVE_API_KEY environment variable.</summary>
    public string? ResolveKey() =>
        !string.IsNullOrWhiteSpace(ApiKey) ? ApiKey : Environment.GetEnvironmentVariable("BRAVE_API_KEY");

    /// <summary>The feature is genuinely usable (enabled AND a key is present).</summary>
    public bool IsActive => Enabled && !string.IsNullOrWhiteSpace(ResolveKey());
}
