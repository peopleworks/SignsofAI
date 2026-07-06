namespace SignsOfAI.Core.Text;

/// <summary>
/// Lightweight EN/ES detector based on stop-word hit rate plus Spanish-specific
/// orthography (ñ, ¿, ¡, accented vowels). Good enough to pick a rule-pack; the UI
/// lets the user override with an explicit choice.
/// </summary>
public static class LanguageDetector
{
    public const string English = "en";
    public const string Spanish = "es";

    private static readonly HashSet<string> EnglishStop =
    [
        "the", "and", "of", "to", "in", "is", "that", "it", "for", "as", "with", "this", "are", "be", "on", "by", "an",
    ];

    private static readonly HashSet<string> SpanishStop =
    [
        "el", "la", "los", "las", "de", "que", "y", "en", "un", "una", "es", "por", "con", "para", "del", "se", "su", "al",
    ];

    /// <summary>Returns "en" or "es". Defaults to English for empty/ambiguous input.</summary>
    public static string Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return English;

        var tokens = text.ToLowerInvariant().Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        int en = 0, es = 0;
        foreach (var t in tokens)
        {
            var w = t.Trim('.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '¿', '¡');
            if (EnglishStop.Contains(w)) en++;
            if (SpanishStop.Contains(w)) es++;
        }

        // Spanish orthography is a strong tiebreaker.
        int spanishChars = text.Count(c => c is 'ñ' or 'Ñ' or '¿' or '¡' or 'á' or 'é' or 'í' or 'ó' or 'ú');
        es += spanishChars;

        return es > en ? Spanish : English;
    }
}
