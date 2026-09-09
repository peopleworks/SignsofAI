using System.Text.Json;
using SignsOfAI.Core.Reporting;

namespace SignsOfAI.Core.Tests;

public class ReportMessageTests
{
    [Fact]
    public void English_resource_is_complete_and_byte_identical_to_compiled_defaults()
    {
        var resource = Load("en");

        Assert.Equal(ReportMessages.Defaults.Count, resource.Messages.Count);
        foreach (var (key, source) in ReportMessages.Defaults)
        {
            var entry = Assert.Contains(key, resource.Messages);
            Assert.Equal(source, entry.Text);
            Assert.Equal(ReportMessages.Arity[key], PlaceholderArity(entry.Text));
            AssertFormats(entry.Text, ReportMessages.Arity[key]);
        }
    }

    [Fact]
    public void Spanish_resource_has_a_current_mandatory_core()
    {
        var resource = Load("es");

        Assert.NotEmpty(resource.Translators);
        foreach (var key in ReportMessages.MandatoryCore)
        {
            var entry = Assert.Contains(key, resource.Messages);
            Assert.False(string.IsNullOrWhiteSpace(entry.Text));
            Assert.Equal(ReportMessages.Arity[key], PlaceholderArity(entry.Text));
            AssertFormats(entry.Text, ReportMessages.Arity[key]);
            Assert.Equal(ReportMessages.SourceHash(ReportMessages.Defaults[key]), entry.SourceHash);
        }
    }

    [Fact]
    public void Every_translated_string_is_pinned_to_its_current_English_source()
    {
        var resource = Load("es");

        foreach (var (key, entry) in resource.Messages)
        {
            var source = Assert.Contains(key, ReportMessages.Defaults);
            Assert.Equal(ReportMessages.Arity[key], PlaceholderArity(entry.Text));
            AssertFormats(entry.Text, ReportMessages.Arity[key]);
            Assert.Equal(ReportMessages.SourceHash(source), entry.SourceHash);
        }
    }

    [Fact]
    public void Spanish_says_every_block_the_report_can_print()
    {
        // The core being current is not the same as the translation being complete. It carried 39 of
        // 76 blocks, so `--report` on a Spanish document produced a report that opened in Spanish and
        // then said "Este bloque aún no está traducido" six times (#77). A half-translated report is
        // not a Spanish report, and nothing failed while it was one.
        var resource = Load("es");

        var missing = ReportMessages.Defaults.Keys
            .Where(key => !resource.Messages.ContainsKey(key))
            .Order()
            .ToList();

        Assert.True(missing.Count == 0,
            $"report.es.json is missing {missing.Count} of {ReportMessages.Defaults.Count} blocks, so a "
            + $"Spanish report prints them in English behind a fallback marker: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_default_declares_its_template_arity()
    {
        Assert.Equal(ReportMessages.Defaults.Keys.Order(), ReportMessages.Arity.Keys.Order());
        foreach (var (key, text) in ReportMessages.Defaults)
            Assert.Equal(ReportMessages.Arity[key], PlaceholderArity(text));
    }

    private static ReportResource Load(string language)
    {
        var name = $"SignsOfAI.Core.Reporting.report.{language}.json";
        using var stream = typeof(ReportMessages).Assembly.GetManifestResourceStream(name);
        Assert.NotNull(stream);
        return JsonSerializer.Deserialize<ReportResource>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static int PlaceholderArity(string template)
    {
        var found = new HashSet<int>();
        for (var i = 0; i < template.Length - 2; i++)
        {
            if (template[i] != '{' || !char.IsAsciiDigit(template[i + 1])) continue;
            var end = i + 1;
            var value = 0;
            while (end < template.Length && char.IsAsciiDigit(template[end]))
            {
                value = value * 10 + template[end] - '0';
                end++;
            }
            if (end < template.Length && (template[end] == '}' || template[end] == ':' || template[end] == ','))
                found.Add(value);
        }
        return found.Count == 0 ? 0 : found.Max() + 1;
    }

    private static void AssertFormats(string template, int arity)
    {
        var args = Enumerable.Repeat<object?>("", arity).ToArray();
        var error = Record.Exception(() => string.Format(template, args));
        Assert.Null(error);
    }
}
