using SignsOfAI.Core;
using SignsOfAI.Core.Reporting;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The project tells contributors that adding a language is adding a file. These are the tests that
/// make that true rather than aspirational — and that stop a language without a pack being reported
/// as though it had one.
/// </summary>
public class RulePackLoaderTests
{
    [Fact]
    public void Every_pack_file_in_the_project_is_discoverable_without_a_build_edit()
    {
        // The packs were listed one by one in the .csproj, so a translator had to edit the build to
        // be heard at all. A wildcard replaced that; this test is what keeps it a wildcard.
        Assert.Contains("en", RulePackLoader.Languages);
        Assert.Contains("es", RulePackLoader.Languages);

        var onDisk = Directory
            .GetFiles(ProjectPacksDirectory(), "rules.*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f)!.Split('.')[1])
            .Order(StringComparer.Ordinal);

        Assert.Equal(onDisk, RulePackLoader.Languages.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_language_with_no_pack_says_which_pack_it_actually_used()
    {
        // Silently loading English while the result claimed the requested language turned "nothing
        // fired" into a finding, when nothing had been looked for.
        //
        // "zz" throughout, never a real code: this suite must keep passing on the day somebody
        // contributes the language it uses as its example, and picking "fr" would make a welcome
        // pull request look like a regression.
        var (_, language) = RulePackLoader.Resolve("zz");

        Assert.Equal("en", language);
        Assert.False(RulePackLoader.Available("zz"));
    }

    [Fact]
    public void A_language_with_a_pack_reports_itself()
    {
        Assert.Equal("es", RulePackLoader.Resolve("es").Language);
        Assert.True(RulePackLoader.Available("es"));
    }

    [Fact]
    public void The_result_keeps_the_two_languages_apart()
    {
        var result = new AiWritingAnalyzer().Analyze("Le texte est court mais suffisant.", "zz");

        // The text is in the language asked for. The rules that read it were not.
        Assert.Equal("zz", result.Language);
        Assert.Equal("en", result.RulePackLanguage);
    }

    [Fact]
    public void The_report_refuses_to_present_an_English_reading_as_a_result_in_that_language()
    {
        var result = new AiWritingAnalyzer().Analyze(
            "La rédaction académique exige de la précision et une structure claire.", "zz");

        var report = EvidenceReport.ToMarkdown(result);

        Assert.Contains("no rule pack for", report);
        Assert.Contains("nothing was looked for", report);
    }

    [Fact]
    public void A_language_that_has_a_pack_carries_no_such_warning()
    {
        var result = new AiWritingAnalyzer().Analyze(
            "La redacción académica exige precisión y una estructura clara.", "es");

        Assert.DoesNotContain("no rule pack for", EvidenceReport.ToMarkdown(result));
    }

    /// <summary>The packs as they sit in the repository, not as the build happened to embed them.</summary>
    private static string ProjectPacksDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SignsOfAI.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "SignsOfAI.Core", "Rules", "Packs");
    }
}
