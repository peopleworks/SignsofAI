using System.Net.Http;
using System.Text.Json;
using SignsOfAI.UI.Services;

namespace SignsOfAI.Desktop;

/// <summary>
/// Asks GitHub which desktop build is the newest, so somebody running an old one finds out.
///
/// It exists because there is no auto-update and there deliberately will not be: the Windows build is
/// unsigned, and a program that downloads and runs code on its own is the behaviour this project
/// tells teachers to be suspicious of. This reports a number and a link. The person decides.
///
/// <b>What leaves the machine.</b> One GET to the public releases endpoint, with no cookie, no
/// account, no identifier and nothing about the document being analysed — GitHub sees an address and
/// a user agent, exactly as it would if the person opened the releases page in a browser. It does not
/// happen until the user has been asked and said yes, and then at most once a day.
///
/// <b>Why not <c>/releases/latest</c>.</b> That resolves to whichever release is newest overall, and
/// this repository publishes two tag lines on purpose — about half the time the newest release is a
/// NuGet one with no desktop build attached. So it lists and filters, and the filtering lives in
/// <see cref="DesktopRelease.Newest"/> where it can be tested without a network.
/// </summary>
public sealed class GitHubUpdateCheck : IUpdateCheck
{
    private const string ReleasesApi =
        "https://api.github.com/repos/peopleworks/SignsofAI/releases?per_page=30";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        // Short on purpose. A version check that hangs is worse than one that fails: the answer is
        // discarded either way, and the only difference is how long a background task sits there.
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "SignsOfAI-desktop (version check; https://github.com/peopleworks/SignsofAI)");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    public bool IsAvailable => true;

    public async Task<UpdateStatus> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await Http.GetAsync(ReleasesApi, ct);

            // 403 is the unauthenticated rate limit, and on a school network it is the expected
            // answer rather than an exceptional one: sixty requests an hour are shared by every
            // machine behind the same address. Nothing to report and nothing to say about it.
            if (!response.IsSuccessStatusCode) return UpdateStatus.Nothing;

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (json.RootElement.ValueKind is not JsonValueKind.Array) return UpdateStatus.Nothing;

            var tags = json.RootElement.EnumerateArray()
                .Where(r => !r.TryGetProperty("draft", out var draft) || !draft.GetBoolean())
                .Where(r => !r.TryGetProperty("prerelease", out var pre) || !pre.GetBoolean())
                .Select(r => r.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null)
                .Where(t => t is not null)
                .Select(t => t!);

            if (DesktopRelease.Newest(tags) is not { } latest) return UpdateStatus.Nothing;

            return new UpdateStatus(
                latest,
                DesktopRelease.ReleasePageFor(latest),
                DesktopRelease.IsNewerThan(latest, DesktopVersion.Running()));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException
                                       or JsonException or InvalidOperationException
                                       or UriFormatException)
        {
            // Offline, a proxy that returns an HTML login page, a malformed body. None of these is
            // the user's problem and none is worth a message on a page about somebody's essay.
            return UpdateStatus.Nothing;
        }
    }
}
