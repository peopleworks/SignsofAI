using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignsOfAI.Core.Reporting;

/// <summary>
/// Interface-language prose used by <see cref="EvidenceReport"/>. Findings themselves deliberately
/// do not live here: they come from the rule pack selected for the language of the analysed text.
/// </summary>
public static class ReportMessages
{
    public const string FallbackMarker = "fallback.marker";
    public const string FallbackSummary = "fallback.summary";
    public const string FallbackLanguage = "fallback.language";
    public const string NoRulePack = "analysis.no-rule-pack";
    public const string DefaultTitle = "default.title";
    public const string MetaDocument = "meta.document";
    public const string MetaGenerated = "meta.generated";
    public const string MetaFolder = "meta.folder";
    public const string SectionAnalysis = "section.analysis";
    public const string SectionCheckable = "section.checkable";
    public const string SectionCharacters = "section.characters";
    public const string SectionCitations = "section.citations";
    public const string SectionSignals = "section.signals";
    public const string SectionObservations = "section.observations";
    public const string SectionErrorRate = "section.error-rate";
    public const string SectionUnreadable = "section.unreadable";
    /// <summary>
    /// Above the measured boundary. One wording, not three.
    ///
    /// "Strong", "Moderate" and "Light" read as three measured degrees, and they never were: the
    /// corpus locates the boundary and says nothing whatever about 45 or 70, since no text known to
    /// be human came within twenty points of either. Separating degrees would need machine-written
    /// text, and Docs/CALIBRATION.md argues against ever collecting it. Printing the three words
    /// with a footnote admitting they are unmeasured does not fix that — the footnote is read once
    /// and the heading is read every time.
    /// </summary>
    public const string VerdictSigns = "verdict.signs";

    /// <summary>
    /// Below it. A statement about this tool, never about the writer.
    ///
    /// The report used to say "Reads mostly human" and the interface "Minimal signs of AI writing" —
    /// the same state, two claims, and the first one is not ours to make. A detector that detects
    /// nothing also returns a low score, and this project has deliberately never measured how much
    /// machine writing it catches, so a low score is evidence about the boundary and nothing else.
    /// </summary>
    public const string VerdictNone = "verdict.none";
    public const string AnalysisScoreWithVerdict = "analysis.score.with-verdict";
    public const string AnalysisScoreWithoutVerdict = "analysis.score.without-verdict";
    public const string AnalysisNoVerdict = "analysis.no-verdict";
    public const string AnalysisFactsCitationOne = "analysis.facts.citation.one";
    public const string AnalysisFactsCitationOther = "analysis.facts.citation.other";
    public const string AnalysisFactsArtifactOne = "analysis.facts.artifact.one";
    public const string AnalysisFactsArtifactOther = "analysis.facts.artifact.other";
    public const string AnalysisFactsBothOneOne = "analysis.facts.both.one-one";
    public const string AnalysisFactsBothOneOther = "analysis.facts.both.one-other";
    public const string AnalysisFactsBothOtherOne = "analysis.facts.both.other-one";
    public const string AnalysisFactsBothOtherOther = "analysis.facts.both.other-other";
    public const string AnalysisCountsOne = "analysis.counts.one";
    public const string AnalysisCountsOther = "analysis.counts.other";
    public const string AnalysisCountsWithObservationsOne = "analysis.counts-with-observations.one";
    public const string AnalysisCountsWithObservationsOther = "analysis.counts-with-observations.other";
    public const string AnalysisLanguageStats = "analysis.language-stats";
    public const string LanguageEnglish = "language.en";
    public const string LanguageSpanish = "language.es";
    public const string LanguageOther = "language.other";
    public const string CaveatUncalibrated = "caveat.uncalibrated";
    public const string CaveatAggregateNoThreshold = "caveat.aggregate-no-threshold";
    public const string CaveatLanguageUnmeasured = "caveat.language-unmeasured";
    public const string CaveatLanguageNoThreshold = "caveat.language-no-threshold";
    public const string CaveatLanguageMeasured = "caveat.language-measured";
    public const string CaveatAggregateMeasured = "caveat.aggregate-measured";
    public const string CheckableIntro = "checkable.intro";
    public const string CharactersExplanation = "characters.explanation";
    public const string CharactersTableHeader = "characters.table-header";
    public const string MoreRows = "common.more-rows";
    public const string CitationsIssuesNote = "citations.issues-note";
    public const string CitationsNoIssuesNote = "citations.no-issues-note";
    public const string SignalsNone = "signals.none";
    public const string ObservationsIntro = "observations.intro";
    public const string ObservationsRowOne = "observations.row.one";
    public const string ObservationsRowOther = "observations.row.other";
    public const string PrivacyDocument = "privacy.document";
    public const string FolderSummaryOne = "folder.summary.one";
    public const string FolderSummaryOther = "folder.summary.other";
    public const string FolderSummaryUnreadableOne = "folder.summary-unreadable.one";
    public const string FolderSummaryUnreadableOther = "folder.summary-unreadable.other";
    public const string FolderReadingOrder = "folder.reading-order";
    public const string FolderTableHeader = "folder.table-header";
    public const string FolderUnreadableRow = "folder.unreadable-row";
    public const string PrivacyFolder = "privacy.folder";
    public const string HowUncalibrated = "how.uncalibrated";
    public const string HowLanguageUnmeasured = "how.language-unmeasured";
    public const string HowLanguageNoThreshold = "how.language-no-threshold";
    public const string HowLanguageMeasured = "how.language-measured";
    public const string HowAggregateIntro = "how.aggregate-intro";
    public const string HowAggregateThreshold = "how.aggregate-threshold";
    public const string HowReadInterval = "how.read-interval";
    public const string HowNoisyIntro = "how.noisy-intro";
    public const string HowNoisyRule = "how.noisy-rule";
    public const string HowLimitation = "how.limitation";

    /// <summary>How many <c>{n}</c> placeholders each template takes.</summary>
    public static IReadOnlyDictionary<string, int> Arity { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [FallbackMarker] = 0,
        [FallbackSummary] = 1,
        [FallbackLanguage] = 1,
        [NoRulePack] = 1,          // {0} the language of the text
        [DefaultTitle] = 0,
        [MetaDocument] = 1,
        [MetaGenerated] = 2,
        [MetaFolder] = 1,
        [SectionAnalysis] = 0,
        [SectionCheckable] = 0,
        [SectionCharacters] = 0,
        [SectionCitations] = 0,
        [SectionSignals] = 0,
        [SectionObservations] = 0,
        [SectionErrorRate] = 0,
        [SectionUnreadable] = 0,
        [VerdictSigns] = 0,
        [VerdictNone] = 0,
        [AnalysisScoreWithVerdict] = 2,
        [AnalysisScoreWithoutVerdict] = 1,
        [AnalysisNoVerdict] = 0,
        [AnalysisFactsCitationOne] = 1,
        [AnalysisFactsCitationOther] = 1,
        [AnalysisFactsArtifactOne] = 1,
        [AnalysisFactsArtifactOther] = 1,
        [AnalysisFactsBothOneOne] = 2,
        [AnalysisFactsBothOneOther] = 2,
        [AnalysisFactsBothOtherOne] = 2,
        [AnalysisFactsBothOtherOther] = 2,
        [AnalysisCountsOne] = 1,
        [AnalysisCountsOther] = 1,
        [AnalysisCountsWithObservationsOne] = 2,
        [AnalysisCountsWithObservationsOther] = 2,
        [AnalysisLanguageStats] = 4,
        [LanguageEnglish] = 0,
        [LanguageSpanish] = 0,
        [LanguageOther] = 1,
        [CaveatUncalibrated] = 0,
        [CaveatAggregateNoThreshold] = 1,
        [CaveatLanguageUnmeasured] = 1,
        [CaveatLanguageNoThreshold] = 2,
        [CaveatLanguageMeasured] = 3,
        [CaveatAggregateMeasured] = 3,
        [CheckableIntro] = 0,
        [CharactersExplanation] = 0,
        [CharactersTableHeader] = 0,
        [MoreRows] = 1,
        [CitationsIssuesNote] = 0,
        [CitationsNoIssuesNote] = 0,
        [SignalsNone] = 0,
        [ObservationsIntro] = 0,
        [ObservationsRowOne] = 2,
        [ObservationsRowOther] = 2,
        [PrivacyDocument] = 0,
        [FolderSummaryOne] = 1,
        [FolderSummaryOther] = 1,
        [FolderSummaryUnreadableOne] = 2,
        [FolderSummaryUnreadableOther] = 2,
        [FolderReadingOrder] = 0,
        [FolderTableHeader] = 0,
        [FolderUnreadableRow] = 2,
        [PrivacyFolder] = 0,
        [HowUncalibrated] = 0,
        [HowLanguageUnmeasured] = 1,
        [HowLanguageNoThreshold] = 4,
        [HowLanguageMeasured] = 5,
        [HowAggregateIntro] = 3,
        [HowAggregateThreshold] = 6,
        [HowReadInterval] = 2,
        [HowNoisyIntro] = 0,
        [HowNoisyRule] = 2,
        [HowLimitation] = 0,
    };

    /// <summary>
    /// English last-resort wording. These values are the source of truth for translation hashes and
    /// preserve the report's previous English text.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Defaults { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [FallbackMarker] = "This block has not been translated yet; it is shown in English.",
        [FallbackSummary] = "This report contains {0} block(s) not yet translated. Each is marked and shown in English.",
        [FallbackLanguage] = "This report is not available in {0}, so the whole of it is shown in English. " +
                             "Nothing has been withheld or shortened, but a reader who cannot read English " +
                             "cannot read the part that limits the score, and that part is the point of the page.",
        [NoRulePack] = "> **There is no rule pack for {0} yet, so this text was examined with the English one.** " +
                       "Treat the score as saying nothing at all: the tells this tool knows are English " +
                       "ones, and few of them can fire on writing in another language — so a low number " +
                       "here means nothing was looked for, not that nothing was found. Rule packs are " +
                       "JSON files anyone can contribute.",
        [DefaultTitle] = "Writing analysis report",
        [MetaDocument] = "**Document:** {0}",
        [MetaGenerated] = "**Generated:** {0} · **Engine:** SignsOfAI {1}",
        [MetaFolder] = "**Folder:** {0}",
        [SectionAnalysis] = "What the analysis says",
        [SectionCheckable] = "Checkable facts",
        [SectionCharacters] = "Characters found in the file",
        [SectionCitations] = "What the document says about its own sources",
        [SectionSignals] = "Signals counted",
        [SectionObservations] = "Found, but at a rate people write at",
        [SectionErrorRate] = "How often this is wrong",
        [SectionUnreadable] = "Could not be read",
        [VerdictSigns] = "Signs of AI writing",
        [VerdictNone] = "No signs above the measured boundary",
        [AnalysisScoreWithVerdict] = "**{0}/100 — {1}**",
        [AnalysisScoreWithoutVerdict] = "**{0}/100**",
        [AnalysisNoVerdict] = "*Below the threshold this build can support, so no verdict is given. A low score is not evidence that a person wrote this.*",
        [AnalysisFactsCitationOne] = "**Checkable facts found: {0} source contradiction. These did not move the score.**",
        [AnalysisFactsCitationOther] = "**Checkable facts found: {0} source contradictions. These did not move the score.**",
        [AnalysisFactsArtifactOne] = "**Checkable facts found: {0} unusual character. These did not move the score.**",
        [AnalysisFactsArtifactOther] = "**Checkable facts found: {0} unusual characters. These did not move the score.**",
        [AnalysisFactsBothOneOne] = "**Checkable facts found: {0} source contradiction, {1} unusual character. These did not move the score.**",
        [AnalysisFactsBothOneOther] = "**Checkable facts found: {0} source contradiction, {1} unusual characters. These did not move the score.**",
        [AnalysisFactsBothOtherOne] = "**Checkable facts found: {0} source contradictions, {1} unusual character. These did not move the score.**",
        [AnalysisFactsBothOtherOther] = "**Checkable facts found: {0} source contradictions, {1} unusual characters. These did not move the score.**",
        [AnalysisCountsOne] = "- {0} signal counted",
        [AnalysisCountsOther] = "- {0} signals counted",
        [AnalysisCountsWithObservationsOne] = "- {0} signal counted, plus {1} found at a rate people write at, which count for nothing",
        [AnalysisCountsWithObservationsOther] = "- {0} signals counted, plus {1} found at a rate people write at, which count for nothing",
        [AnalysisLanguageStats] = "- Analysed as {0} · {1} words · {2} sentences · sentence-length variability {3}",
        [LanguageEnglish] = "English",
        [LanguageSpanish] = "Spanish",
        [LanguageOther] = "language code {0}",
        [CaveatUncalibrated] = "> **This build has not been calibrated.** No false-positive rate has been measured for it, so the score above should not be used to support a decision about a person.",
        [CaveatAggregateNoThreshold] = "> **No threshold is supported yet.** This build was measured against {0} texts, too few to bound its false-positive rate, so no score on this page should be used to support a decision about a person.",
        [CaveatLanguageUnmeasured] = "> **This build has never been measured for {0}.** It has no false-positive rate or supported threshold for writing in this language, and the aggregate result from other languages is not a substitute. No score on this page should be used to support a decision about a person.",
        [CaveatLanguageNoThreshold] = "> **No threshold is supported for this language yet.** The corpus holds {0} texts in it — too few to bound how often this build is wrong about writing in it, so no score on this page should be used to support a decision about a person. The best bound these texts support is {1}, and the overall figure is not a substitute for it.",
        [CaveatLanguageMeasured] = "> **A score is not proof.** On {0} texts in this language, published before generative models existed, this build's false-positive rate at a threshold of {1}/100 was under {2} — the upper end of a 95% interval, not a guarantee, and measured on published articles rather than student work. Below that threshold, treat the score as saying nothing.",
        [CaveatAggregateMeasured] = "> **A score is not proof.** On {0} texts published before generative models existed, this build's false-positive rate at a threshold of {1}/100 was under {2} — the upper end of a 95% interval, not a guarantee, and measured on published articles rather than student work. Below that threshold, treat the score as saying nothing.",
        [CheckableIntro] = "These are not judgements about the writing and they did not move the score. Each is either present in the file or it is not.",
        [CharactersExplanation] = "Several of these have ordinary explanations — word processors insert soft hyphens and unusual spaces on their own, and any copy-paste can carry them. Invisible characters and letters borrowed from another alphabet are harder to arrive at by accident, though pasting text can do it. This table says what is in the file, not how it got there.",
        [CharactersTableHeader] = "| Character | Codepoint | Line | Column |",
        [MoreRows] = "… and {0} more.",
        [CitationsIssuesNote] = "> None of this needed the internet: the document disagrees with itself. It is a question to ask, not a conclusion — the answer is usually one sentence.",
        [CitationsNoIssuesNote] = "> Nothing here is a finding. It describes what could and could not be checked.",
        [SignalsNone] = "None.",
        [ObservationsIntro] = "Measured against writing published before generative models existed. Shown because they are real, and counted for nothing because they are ordinary.",
        [ObservationsRowOne] = "- {0} — {1} occurrence",
        [ObservationsRowOther] = "- {0} — {1} occurrences",
        [PrivacyDocument] = "*This report was produced on the device that ran the analysis and contains material from the document it describes. It is yours to keep or to send; nothing here was uploaded anywhere.*",
        [FolderSummaryOne] = "{0} file scanned.",
        [FolderSummaryOther] = "{0} files scanned.",
        [FolderSummaryUnreadableOne] = "{0} file scanned, {1} unreadable.",
        [FolderSummaryUnreadableOther] = "{0} files scanned, {1} unreadable.",
        [FolderReadingOrder] = "> **This is a reading order, not a ranking.** A higher score means look sooner, and nothing more. Nothing on this page establishes that anyone did anything.",
        [FolderTableHeader] = "| File | Score | Signals | Words |",
        [FolderUnreadableRow] = "- {0} — {1}",
        [PrivacyFolder] = "*Produced on the device that scanned the folder. It names your students' files, so treat it as you would the coursework itself; nothing here was uploaded anywhere.*",
        [HowUncalibrated] = "This build ships no calibration, so nothing is known about how often it is wrong. That is itself the most important thing on this page.",
        [HowLanguageUnmeasured] = "This build has never been measured on writing in {0}. No language-specific false-positive rate or threshold exists, and the aggregate result from other languages is not a substitute.",
        [HowLanguageNoThreshold] = "Measured against **{0} texts in this language**, published before generative models existed, on {2} with engine {3}. That sample is too small to support a threshold; the best upper bound it supports is **{1}**, and the overall figure is not a substitute.",
        [HowLanguageMeasured] = "Measured against **{0} texts in this language**, published before generative models existed, on {3} with engine {4}. At **{1}/100**, the upper end of the measured 95% false-positive interval was **{2}** — an interval, not a guarantee.",
        [HowAggregateIntro] = "Measured against **{0} texts published before generative models existed**, so their authorship rests on their dates rather than on anybody's judgement. Measured on {1} with engine {2}.",
        [HowAggregateThreshold] = "At **{0}/100**, {1} of those {2} were flagged — an observed {3}, with a 95% interval of {4} – {5}.",
        [HowReadInterval] = "Read the interval, not the observed rate. {0} out of {1} is not a false-positive rate you can round down.",
        [HowNoisyIntro] = "The rules seen most often on that human writing, worst first — if the evidence above leans on one of these, weigh it accordingly:",
        [HowNoisyRule] = "- `{0}` — {1} of human texts",
        [HowLimitation] = "What this does **not** tell you: how much machine-written text it catches. That is the other half of the picture and it is deliberately not measured here, because any collection of machine-written text samples whichever models were convenient that month. A tool that flags nothing has a perfect false-positive rate.",
    };

    /// <summary>
    /// A report language is accepted only when the prose that limits the score, the fallback notices,
    /// and the section map are present and current. Everything else may be translated incrementally.
    /// </summary>
    public static IReadOnlySet<string> MandatoryCore { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        FallbackMarker, FallbackSummary, FallbackLanguage, DefaultTitle,
        SectionAnalysis, SectionCheckable, SectionCharacters, SectionCitations, SectionSignals,
        SectionObservations, SectionErrorRate, SectionUnreadable,
        VerdictSigns, VerdictNone,
        AnalysisNoVerdict,
        LanguageEnglish, LanguageSpanish, LanguageOther,
        CaveatUncalibrated, CaveatAggregateNoThreshold, CaveatLanguageUnmeasured,
        CaveatLanguageNoThreshold, CaveatLanguageMeasured, CaveatAggregateMeasured,
        HowUncalibrated, HowLanguageUnmeasured, HowLanguageNoThreshold, HowLanguageMeasured,
        HowAggregateIntro, HowAggregateThreshold, HowReadInterval, HowNoisyIntro, HowNoisyRule,
        HowLimitation,
    };

    private static readonly ConcurrentDictionary<string, ReportResource?> Resources =
        new(StringComparer.OrdinalIgnoreCase);

    internal static ReportText For(string? language)
    {
        var requested = Normalize(language);
        if (requested == "en") return new ReportText("en", null);

        var resource = Resources.GetOrAdd(requested, Load);
        if (resource is null || resource.Translators.Count == 0
            || !MandatoryCore.All(key => Valid(resource, key)))
            // A language whose core is missing or stale does not get to half-speak: the report is
            // written in English and says on its face that it is. Refusing to render instead would
            // lose the evidence entirely, which is the outcome this feature exists to prevent —
            // a teacher holding two hundred essays and a button that does nothing is worse off
            // than one holding a report they must read in English.
            return new ReportText("en", null) { UnavailableLanguage = requested };

        return new ReportText(requested, resource);
    }

    /// <summary>The SHA-256 pin a translation records for the English source it represents.</summary>
    public static string SourceHash(string english) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(english))).ToLowerInvariant();

    private static ReportResource? Load(string language)
    {
        try
        {
            var name = $"SignsOfAI.Core.Reporting.report.{language}.json";
            using var stream = typeof(ReportMessages).Assembly.GetManifestResourceStream(name);
            return stream is null
                ? null
                : JsonSerializer.Deserialize(stream, ReportResourceJsonContext.Default.ReportResource);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    private static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "en";
        var primary = language.Trim().Split('-', '_')[0].ToLowerInvariant();
        return primary.Length is > 0 and <= 12 && primary.All(char.IsAsciiLetter) ? primary : "en";
    }

    internal static bool Valid(ReportResource resource, string key)
    {
        if (!Defaults.TryGetValue(key, out var source)
            || !Arity.TryGetValue(key, out var arity)
            || !resource.Messages.TryGetValue(key, out var entry)
            || string.IsNullOrWhiteSpace(entry.Text)
            || PlaceholderArity(entry.Text) != arity
            || !CanFormat(entry.Text, arity))
            return false;

        return string.Equals(resource.Language, "en", StringComparison.OrdinalIgnoreCase)
            ? string.Equals(entry.Text, source, StringComparison.Ordinal)
            : string.Equals(entry.SourceHash, SourceHash(source), StringComparison.OrdinalIgnoreCase);
    }

    internal static int PlaceholderArity(string template)
    {
        var found = new HashSet<int>();
        for (var i = 0; i < template.Length - 2; i++)
        {
            if (template[i] != '{' || template[i + 1] == '{' || !char.IsAsciiDigit(template[i + 1]))
                continue;

            var end = i + 1;
            var value = 0;
            while (end < template.Length && char.IsAsciiDigit(template[end]))
            {
                if (value > 1000) return int.MaxValue;
                value = value * 10 + template[end] - '0';
                end++;
            }

            if (end < template.Length && (template[end] == '}' || template[end] == ':' || template[end] == ','))
                found.Add(value);
        }

        return found.Count == 0 ? 0 : found.Max() + 1;
    }

    private static bool CanFormat(string template, int arity)
    {
        try
        {
            _ = string.Format(CultureInfo.InvariantCulture, template,
                Enumerable.Repeat<object?>("", arity).ToArray());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

internal sealed class ReportText(string language, ReportResource? resource)
{
    public string Language { get; } = language;
    public int FallbackBlocks { get; private set; }

    /// <summary>
    /// The language that was asked for and could not be honoured, or null when it could. The report
    /// prints this rather than quietly serving English, because a page that looks complete while
    /// withholding what limits it is the failure this project criticises in everyone else.
    /// </summary>
    public string? UnavailableLanguage { get; init; }
    public string FallbackMarker => Raw(ReportMessages.FallbackMarker).Text;
    public string FallbackSummary => string.Format(
        CultureInfo.InvariantCulture, Raw(ReportMessages.FallbackSummary).Text, FallbackBlocks);

    public LocalizedReportText Get(string key, params object?[] args)
    {
        var value = Raw(key);
        if (value.FellBack) FallbackBlocks++;

        try
        {
            return value with { Text = string.Format(CultureInfo.InvariantCulture, value.Text, args) };
        }
        catch (FormatException)
        {
            // Validated resources should never reach this path. A readable English block is safer
            // than losing the report if an unexpected runtime value exposes a formatting edge case.
            FallbackBlocks += value.FellBack ? 0 : 1;
            return new LocalizedReportText(string.Format(
                CultureInfo.InvariantCulture, ReportMessages.Defaults[key], args), true);
        }
    }

    private LocalizedReportText Raw(string key)
    {
        if (resource is not null && ReportMessages.Valid(resource, key))
            return new LocalizedReportText(resource.Messages[key].Text, false);

        return new LocalizedReportText(ReportMessages.Defaults[key], resource is not null);
    }
}

internal sealed record LocalizedReportText(string Text, bool FellBack);

public sealed record ReportResource
{
    public required string Language { get; init; }
    public IReadOnlyList<string> Translators { get; init; } = [];
    public Dictionary<string, ReportResourceEntry> Messages { get; init; } = new(StringComparer.Ordinal);
}

public sealed record ReportResourceEntry
{
    public required string Text { get; init; }
    public string? SourceHash { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
                             PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ReportResource))]
internal partial class ReportResourceJsonContext : JsonSerializerContext;
