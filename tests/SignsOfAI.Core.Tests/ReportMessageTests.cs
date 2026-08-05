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
