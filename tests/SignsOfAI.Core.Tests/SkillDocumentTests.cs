using System;
using System.IO;
using System.Linq;
using SignsOfAI.Core.Calibration;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The skill document is instructions an assistant follows without checking them, in a session this
/// repository will never see. That makes every number in it a claim we cannot correct later, and a
/// stale one is worse than none: it would be quoted at a student with our name on it.
///
/// So the numbers <c>SKILL.md</c> prints are checked against the calibration the build actually
/// ships. Re-running <c>tools/SignsOfAI.Calibration</c> and forgetting the skill fails here.
/// </summary>
public class SkillDocumentTests
{
    private static readonly string Skill = File.ReadAllText(Find("SKILL.md"));

    private static string Find(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, name)))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, name);
    }

    [Fact]
    public void It_quotes_the_boundary_this_build_actually_uses()
    {
        var threshold = PublishedCalibration.Current?.RecommendedThreshold;
        Assert.NotNull(threshold);

        Assert.Contains($"{threshold!.Value:0}/100", Skill);
    }

    [Fact]
    public void It_quotes_the_corpus_and_the_interval_this_build_actually_ships()
    {
        var published = PublishedCalibration.Current;
        Assert.NotNull(published);

        Assert.Contains($"{published!.FlaggedAtThreshold} of {published.Texts}", Skill);
        Assert.Contains($"{published.RateHigh * 100:0.0}%", Skill);
    }

    /// <summary>
    /// The rate is only quotable for languages the corpus contains, and the skill has to name them
    /// rather than let an assistant assume its own language is covered.
    /// </summary>
    [Fact]
    public void It_names_the_languages_that_have_a_measured_rate()
    {
        var languages = PublishedCalibration.Current?.Languages ?? [];
        Assert.NotEmpty(languages);

        foreach (var name in languages.Select(l => l.Language switch
        {
            "en" => "English",
            "es" => "Spanish",
            var other => other,
        }))
        {
            Assert.Contains(name, Skill);
        }
    }

    /// <summary>
    /// The same decision <c>LocaleFileTests</c> guards for the interface, guarded here for the
    /// instructions: a low score is a fact about this tool, never a claim that a person wrote
    /// something. An assistant reading a skill that says otherwise would repeat it verbatim.
    /// </summary>
    [Theory]
    [InlineData("cannot determine who wrote")]        // the frontmatter, which every installer shows
    [InlineData("not evidence a human wrote it")]     // finding nothing is not a finding
    [InlineData("fact about the tool")]               // the wording #32 settled for the report
    [InlineData("no result meaning")]                 // the writer baseline has no "someone else" answer
    public void It_refuses_to_claim_authorship(string required) =>
        Assert.Contains(required, Skill, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void It_carries_the_frontmatter_an_installer_reads()
    {
        Assert.StartsWith("---", Skill);

        var frontmatter = Skill.Split("---", 3)[1];
        Assert.Contains("name: signs-of-ai", frontmatter);
        Assert.Contains("description:", frontmatter);
    }
}
