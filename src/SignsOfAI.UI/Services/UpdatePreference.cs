namespace SignsOfAI.UI.Services;

/// <summary>
/// What the user has decided about version checks, and how often one is due.
///
/// Kept out of the component and out of the host so there is one answer to "may we check?" rather
/// than one per surface — the same reason <see cref="Model.VerdictBands"/> exists, applied to a much
/// smaller question. It stores three things and nothing else: whether they said yes, the day of the
/// last check, and the version they have already been told about.
///
/// Deliberately no identifier of any kind. There is nothing here that could become one later.
/// </summary>
public sealed class UpdatePreference(BrowserStorage storage)
{
    private const string ConsentKey = "signsofai.updates.consent";
    private const string LastCheckKey = "signsofai.updates.lastcheck";
    private const string DismissedKey = "signsofai.updates.dismissed";

    /// <summary>
    /// True if they agreed, false if they declined, <b>null if they were never asked</b>.
    ///
    /// The three states have to stay distinct: "not yet asked" is what makes the app show the
    /// question, and collapsing it into "no" would mean the question never appears and nobody ever
    /// hears about a fix.
    /// </summary>
    public async ValueTask<bool?> ConsentAsync() =>
        await storage.GetAsync(ConsentKey) switch
        {
            "yes" => true,
            "no" => false,
            _ => null,
        };

    public async ValueTask SetConsentAsync(bool agreed)
    {
        await storage.SetAsync(ConsentKey, agreed ? "yes" : "no");

        // Declining clears the schedule too, so turning it back on later checks immediately rather
        // than waiting out a day that was counted while the feature was off.
        if (!agreed) await storage.RemoveAsync(LastCheckKey);
    }

    /// <summary>
    /// Whether a check is due. At most one a day, and the reason is somebody else's server: the
    /// GitHub API allows 60 unauthenticated requests an hour per address, and a school with forty
    /// machines behind one NAT would exhaust that between first and second period.
    /// </summary>
    public async ValueTask<bool> DueAsync(DateOnly today) =>
        await storage.GetAsync(LastCheckKey) is not { } last
        || !DateOnly.TryParse(last, out var when)
        || when < today;

    public ValueTask MarkCheckedAsync(DateOnly today) =>
        storage.SetAsync(LastCheckKey, today.ToString("yyyy-MM-dd"));

    /// <summary>The version they have already been told about and closed. Told once, not every launch.</summary>
    public ValueTask<string?> DismissedAsync() => storage.GetAsync(DismissedKey);

    public ValueTask DismissAsync(string version) => storage.SetAsync(DismissedKey, version);
}
