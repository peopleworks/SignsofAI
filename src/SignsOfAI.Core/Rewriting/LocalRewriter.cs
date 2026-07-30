using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Core.Rewriting;

/// <summary>How much of the rule-pack the rewriter is allowed to touch.</summary>
public enum RewriteStrength
{
    /// <summary>Only the strongest tells. Changes little, and what it changes is hard to argue with.</summary>
    Light,

    /// <summary>The strong and moderate tells — the default.</summary>
    Standard,

    /// <summary>Everything mechanical, down to deleting empty intensifiers.</summary>
    Thorough,
}

/// <summary>
/// One proposed change to the text: what it covers, and what could go there instead.
/// Spans are positions in the <em>original</em> text, so a plan stays valid while the user
/// accepts and rejects edits in any order.
/// </summary>
public sealed record RewriteEdit
{
    public required string RuleId { get; init; }

    /// <summary>Where in the original text this applies.</summary>
    public required TextSpan Span { get; init; }

    /// <summary>The exact text being replaced.</summary>
    public required string Original { get; init; }

    /// <summary>
    /// Candidate replacements, best first, already case-matched to <see cref="Original"/>.
    /// Empty when <see cref="IsDeletion"/> is true.
    /// </summary>
    public required IReadOnlyList<string> Options { get; init; }

    /// <summary>The fix is to remove the word, not swap it.</summary>
    public required bool IsDeletion { get; init; }

    /// <summary>
    /// Whether this edit is safe to apply without the writer choosing.
    ///
    /// False when the matched word is an inflected form of the rule's canonical term — a rule listing
    /// "delve, delves, delving" carries one replacement list, and substituting "examine" into "delving"
    /// would produce broken grammar. Those edits are still offered, with their options, but a tool that
    /// silently mangles tenses has no business advising anyone on writing.
    /// </summary>
    public required bool AutoApply { get; init; }

    public required Severity Severity { get; init; }

    public required double Weight { get; init; }

    /// <summary>The default replacement, or the empty string for a deletion.</summary>
    public string Preferred => Options.Count > 0 ? Options[0] : string.Empty;
}

/// <summary>
/// Rewrites AI tells out of text using nothing but the rule-pack — no model, no network, no API key.
/// It is deterministic and instant, which is what makes a live side-by-side view possible: every
/// keystroke can re-plan and re-apply.
///
/// It only handles what substitution can honestly fix: the <em>lexical</em> rules (overused words).
/// Rhetorical and syntactic tells — negative parallelisms, copula avoidance, robotic rhythm — are
/// structural rewrites that need real language ability, so they are left for the optional LLM pass
/// and merely reported here.
/// </summary>
public static class LocalRewriter
{
    /// <summary>
    /// Works out the non-overlapping set of changes available for <paramref name="findings"/>.
    /// Pure: call it as often as you like.
    /// </summary>
    public static IReadOnlyList<RewriteEdit> Plan(
        string text,
        IReadOnlyList<Finding> findings,
        RulePack pack,
        RewriteStrength strength = RewriteStrength.Standard)
    {
        if (string.IsNullOrEmpty(text) || findings.Count == 0) return [];

        var rules = new Dictionary<string, LexicalRule>(StringComparer.Ordinal);
        foreach (var rule in pack.Lexical ?? []) rules[rule.Id] = rule;

        var minimum = Floor(strength);
        var edits = new List<RewriteEdit>();
        var cursor = 0;

        // Spans of the rhetorical / syntactic constructions in this text. A word sitting inside one is
        // not free-standing: it is holding that construction up.
        var constructions = findings
            .Where(f => f.Category is SignCategory.Rhetorical or SignCategory.Syntactic && f.Span.Length > 0)
            .Select(f => f.Span)
            .ToList();

        foreach (var finding in findings.OrderBy(f => f.Span.Start).ThenByDescending(f => f.Weight))
        {
            if (finding.Span.Length == 0 || finding.Span.End > text.Length) continue; // document-level
            if (finding.Span.Start < cursor) continue;                                // overlaps a kept edit
            if (finding.Severity < minimum) continue;
            if (!rules.TryGetValue(finding.RuleId, out var rule)) continue;            // not mechanically fixable

            var original = finding.Span.Slice(text);

            if (rule.Delete)
            {
                // "It's not just a tool, it's a solution" — "just" is an empty intensifier in general,
                // but not here: the negative parallelism around it depends on the word, and deleting it
                // flips the sentence into "it's not a tool". Whenever a deletion falls inside a flagged
                // construction the premise "this word carries no meaning" is simply false, so the
                // construction is left for a real rewrite. Substitutions are unaffected — swapping a
                // synonym preserves the sense, while removing a word can invert it.
                if (constructions.Any(c => finding.Span.Start >= c.Start && finding.Span.End <= c.End))
                    continue;

                // Deleting a word is immune to inflection, so it is always safe to apply.
                edits.Add(new RewriteEdit
                {
                    RuleId = rule.Id,
                    Span = finding.Span,
                    Original = original,
                    Options = [],
                    IsDeletion = true,
                    AutoApply = true,
                    Severity = finding.Severity,
                    Weight = finding.Weight,
                });
                cursor = finding.Span.End;
                continue;
            }

            var options = rule.RewriteOptions();
            if (options.Count == 0) continue;

            // Spanish articles carry gender, so "el panorama" → "el situación" is wrong. The gender to
            // match is taken from the article already in the sentence rather than guessed from the
            // word — "panorama" and "problema" end in -a and are masculine, so an ending-based guess
            // fails on exactly the words that matter. Only alternatives that agree survive; if none
            // do, the swap would need the article rebuilt and is left alone.
            if (pack.Language == "es" && ArticleGender(text, finding.Span.Start) is { } gender)
            {
                options = [.. options.Where(o => ApparentGender(o) == gender)];
                if (options.Count == 0) continue;
            }

            // "delve into", "a testament to", "embark on" — the word governs the particle after it, and
            // a one-word swap gets the pairing wrong ("examine into", "proof to"). Offering alternatives
            // doesn't rescue it either: choosing "look into" would produce "look into into". So the
            // rewriter declines the edit; the finding still tells the writer what to consider.
            if (IsFollowedByGovernedParticle(text, finding.Span.End, pack.Language)) continue;

            // "a plethora of options" → "a many of options". In the "a ___ of" frame the word is doing
            // quantifier duty, and swapping it needs the determiner restructured too, which is beyond a
            // substitution. "the rich tapestry of" is untouched by this: it isn't an indefinite article.
            if (IsQuantifierFrame(text, finding.Span, pack.Language)) continue;

            var canonical = rule.Terms.Length > 0 ? rule.Terms[0] : original;

            edits.Add(new RewriteEdit
            {
                RuleId = rule.Id,
                Span = finding.Span,
                Original = original,
                Options = [.. options.Select(option => MatchCase(original, option))],
                IsDeletion = false,
                AutoApply = string.Equals(original, canonical, StringComparison.OrdinalIgnoreCase),
                Severity = finding.Severity,
                Weight = finding.Weight,
            });
            cursor = finding.Span.End;
        }

        return edits;
    }

    /// <summary>
    /// Applies a plan to the text.
    /// </summary>
    /// <param name="chosen">
    /// Span start → the replacement to use, overriding <see cref="RewriteEdit.Preferred"/>. This is how
    /// the UI records "I picked the third alternative".
    /// </param>
    /// <param name="rejected">
    /// Span starts to leave untouched. Also how an edit needing a decision stays out until the writer
    /// makes one.
    /// </param>
    /// <param name="language">
    /// Used only for English "a"/"an" agreement, which depends on the alternative the writer picked and
    /// so cannot be decided when the plan is built.
    /// </param>
    public static string Apply(
        string text,
        IReadOnlyList<RewriteEdit> edits,
        IReadOnlyDictionary<int, string>? chosen = null,
        IReadOnlySet<int>? rejected = null,
        string language = "en")
    {
        if (string.IsNullOrEmpty(text) || edits.Count == 0) return text ?? string.Empty;

        var result = new System.Text.StringBuilder(text.Length);
        var copied = 0;            // how much of the original has been written out
        var capitalizePending = false;

        // Capitalization is deferred rather than applied to the character right after a deletion:
        // that character may itself be the start of the next edit, and consuming it here would drop
        // that edit on the floor. Instead the flag travels until an actual letter gets written.
        void Emit(string chunk)
        {
            if (!capitalizePending || chunk.Length == 0)
            {
                result.Append(chunk);
                return;
            }
            for (var i = 0; i < chunk.Length; i++)
            {
                if (!char.IsLetter(chunk[i])) continue;
                result.Append(chunk, 0, i);
                result.Append(char.ToUpperInvariant(chunk[i]));
                result.Append(chunk, i + 1, chunk.Length - i - 1);
                capitalizePending = false;
                return;
            }
            result.Append(chunk); // nothing but punctuation/space so far — keep waiting for a letter
        }

        foreach (var edit in edits.OrderBy(e => e.Span.Start))
        {
            if (rejected is not null && rejected.Contains(edit.Span.Start)) continue;
            if (edit.Span.Start < copied) continue; // an earlier deletion already swallowed this

            var replacement = edit.IsDeletion
                ? string.Empty
                : chosen is not null && chosen.TryGetValue(edit.Span.Start, out var pick) && pick.Length > 0
                    ? pick
                    : edit.Preferred;

            if (!edit.IsDeletion && replacement.Length == 0) continue;

            var (from, to, capitalizeNext) = edit.IsDeletion
                ? DeletionRange(text, edit.Span, copied)
                : (edit.Span.Start, edit.Span.End, false);

            var gap = text[copied..from];
            if (!edit.IsDeletion && language != "es")
                gap = AgreeArticle(gap, replacement);

            Emit(gap);
            Emit(replacement);
            copied = to;

            if (capitalizeNext) capitalizePending = true;
        }

        Emit(text[copied..]);
        return result.ToString();
    }

    // Particles a preceding word governs, so swapping that word changes which particle is correct.
    // The genitive ("of" / "de") is deliberately absent: it survives a noun swap unharmed —
    // "rich tapestry of" → "rich mix of" reads fine, and excluding it would suppress good edits.
    private static readonly HashSet<string> EnglishParticles = new(StringComparer.OrdinalIgnoreCase)
    {
        "into", "to", "on", "onto", "upon", "in", "with", "for", "from", "at", "about",
        "through", "over", "toward", "towards", "against", "as",
    };

    private static readonly HashSet<string> SpanishParticles = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "a", "al", "con", "para", "por", "sobre", "hacia", "desde", "hasta", "ante", "como",
    };

    /// <summary>
    /// Whether the match sits in an "a ___ of" frame, where the word is acting as a quantifier and a
    /// swap would need the determiner rebuilt.
    /// </summary>
    private static bool IsQuantifierFrame(string text, TextSpan span, string language)
    {
        var genitive = language == "es" ? "de" : "of";
        if (!IsNextWord(text, span.End, genitive)) return false;

        var articles = language == "es"
            ? new[] { "un", "una" }
            : ["a", "an"];
        return articles.Any(article => IsPreviousWord(text, span.Start, article));
    }

    private static bool IsNextWord(string text, int at, string word)
    {
        var i = at;
        while (i < text.Length && text[i] == ' ') i++;
        if (i == at || i + word.Length > text.Length) return false;
        if (!text.AsSpan(i, word.Length).Equals(word, StringComparison.OrdinalIgnoreCase)) return false;
        var after = i + word.Length;
        return after == text.Length || !char.IsLetter(text[after]);
    }

    private static bool IsPreviousWord(string text, int at, string word)
    {
        var i = at;
        while (i > 0 && text[i - 1] == ' ') i--;
        if (i == at || i - word.Length < 0) return false;
        if (!text.AsSpan(i - word.Length, word.Length).Equals(word, StringComparison.OrdinalIgnoreCase)) return false;
        var before = i - word.Length - 1;
        return before < 0 || !char.IsLetter(text[before]);
    }

    /// <summary>
    /// The gender the sentence already commits to, read off the article in front of the word, or null
    /// when there is no article to agree with. Ground truth rather than a guess — which is the point,
    /// since the word itself may be one of the -a masculines.
    /// </summary>
    private static char? ArticleGender(string text, int at)
    {
        if (IsPreviousWord(text, at, "el") || IsPreviousWord(text, at, "los")
            || IsPreviousWord(text, at, "un") || IsPreviousWord(text, at, "unos")
            || IsPreviousWord(text, at, "del") || IsPreviousWord(text, at, "al")) return 'm';

        if (IsPreviousWord(text, at, "la") || IsPreviousWord(text, at, "las")
            || IsPreviousWord(text, at, "una") || IsPreviousWord(text, at, "unas")) return 'f';

        return null;
    }

    /// <summary>
    /// Gender guessed from a replacement's ending: the -ción/-sión/-dad/-tad/-tud/-umbre families are
    /// reliably feminine, a final -a usually is, everything else reads masculine. Only ever applied to
    /// the *replacement*, so a wrong guess costs a declined edit rather than producing broken agreement.
    /// </summary>
    private static char ApparentGender(string word)
    {
        var w = word.Trim().ToLowerInvariant();
        if (w.Length == 0) return 'm';

        // Judge on the last word, so "lo más avanzado" is read as "avanzado".
        var space = w.LastIndexOf(' ');
        if (space >= 0) w = w[(space + 1)..];

        if (w.EndsWith("ción") || w.EndsWith("sión") || w.EndsWith("ión")
            || w.EndsWith("dad") || w.EndsWith("tad") || w.EndsWith("tud") || w.EndsWith("umbre"))
            return 'f';

        return w.EndsWith('a') ? 'f' : 'm';
    }

    /// <summary>Whether the word right after <paramref name="at"/> is a particle governed by what precedes it.</summary>
    private static bool IsFollowedByGovernedParticle(string text, int at, string language)
    {
        var i = at;
        while (i < text.Length && text[i] == ' ') i++;
        if (i >= text.Length || i == at) return false; // must be separated by a space

        var start = i;
        while (i < text.Length && char.IsLetter(text[i])) i++;
        if (i == start) return false;

        var word = text[start..i];
        var particles = language == "es" ? SpanishParticles : EnglishParticles;
        return particles.Contains(word);
    }

    /// <summary>Severity at or above which a strength setting acts.</summary>
    private static Severity Floor(RewriteStrength strength) => strength switch
    {
        RewriteStrength.Light => Severity.High,
        RewriteStrength.Standard => Severity.Medium,
        _ => Severity.Info,
    };

    /// <summary>
    /// Widens a deletion to take the punctuation and spacing that only existed to hold the word,
    /// so removing it leaves clean prose instead of a double space or an orphaned comma.
    /// </summary>
    private static (int From, int To, bool CapitalizeNext) DeletionRange(string text, TextSpan span, int floor)
    {
        int from = span.Start, to = span.End;

        var sentenceInitial = IsSentenceInitial(text, from, floor);
        var precededBySpace = from - 1 >= floor && char.IsWhiteSpace(text[from - 1]);

        // ", actually," — the commas were bracketing this word, so both go with it.
        if (from - 2 >= floor && text[from - 2] == ',' && text[from - 1] == ' '
            && to < text.Length && text[to] == ',')
        {
            return (from - 2, to + 1, false);
        }

        // "Moreover, x" / "actually, x" — a trailing comma belonged to the word.
        if (to < text.Length && text[to] == ',') to++;

        // "…late, truly." — the word runs into punctuation, so the space in front of it is the one
        // that has to go. Taking the trailing side instead would leave " .".
        if (to < text.Length && text[to] is '.' or '!' or '?' or ';' or ':' or ')' && precededBySpace)
        {
            while (from - 1 >= floor && text[from - 1] == ' ') from--;
            // The word was the whole tail of a clause, so the comma introducing it goes too:
            // "late, truly." → "late." rather than "late,.".
            if (from - 1 >= floor && text[from - 1] == ',') from--;
            return (from, to, false);
        }

        if (sentenceInitial || precededBySpace)
        {
            // Leave exactly one separator: either the space already before the word, or none at all
            // when the word opened the sentence.
            while (to < text.Length && text[to] == ' ') to++;
        }

        return (from, to, sentenceInitial);
    }

    /// <summary>
    /// Whether a capital letter belongs at this position. Only true after a real sentence end —
    /// a semicolon or colon continues the sentence, so recapitalizing there would be wrong.
    /// </summary>
    private static bool IsSentenceInitial(string text, int index, int floor)
    {
        for (var i = index - 1; i >= floor; i--)
        {
            if (char.IsWhiteSpace(text[i])) continue;
            return text[i] is '.' or '!' or '?' or '\n';
        }
        return index <= floor; // nothing but whitespace before it
    }

    /// <summary>
    /// Fixes the indefinite article when a swap changes the initial sound: "a crucial step" becomes
    /// "an essential step", not "a essential step". Only touches an article immediately before the
    /// replacement, so nothing else in the sentence can be affected.
    /// </summary>
    private static string AgreeArticle(string gap, string replacement)
    {
        if (replacement.Length == 0) return gap;

        // The gap must end with the article plus its separating whitespace.
        var end = gap.Length;
        while (end > 0 && char.IsWhiteSpace(gap[end - 1])) end--;
        if (end == gap.Length) return gap; // no whitespace: not "a "/"an " right before the word

        var start = end;
        while (start > 0 && char.IsLetter(gap[start - 1])) start--;

        var article = gap[start..end];
        if (!article.Equals("a", StringComparison.OrdinalIgnoreCase)
            && !article.Equals("an", StringComparison.OrdinalIgnoreCase)) return gap;
        if (start > 0 && char.IsLetter(gap[start - 1])) return gap; // part of a longer word

        var wanted = StartsWithVowelSound(replacement) ? "an" : "a";
        if (article.Equals(wanted, StringComparison.OrdinalIgnoreCase)) return gap;

        if (char.IsUpper(article[0])) wanted = char.ToUpperInvariant(wanted[0]) + wanted[1..];
        return gap[..start] + wanted + gap[end..];
    }

    /// <summary>
    /// A spelling-based approximation, which is what the article rule mostly follows. It gets the
    /// well-known exceptions ("hour", "university") wrong in one direction or the other, but every
    /// replacement in the built-in packs is an ordinary word where the letter is the right signal.
    /// </summary>
    private static bool StartsWithVowelSound(string word) =>
        word.Length > 0 && word[0] is 'a' or 'e' or 'i' or 'o' or 'u' or 'A' or 'E' or 'I' or 'O' or 'U';

    /// <summary>
    /// Carries the original's capitalization onto the replacement, so "Delve" becomes "Examine"
    /// rather than "examine" mid-sentence.
    /// </summary>
    private static string MatchCase(string original, string replacement)
    {
        if (original.Length == 0 || replacement.Length == 0) return replacement;

        var letters = original.Where(char.IsLetter).ToList();
        if (letters.Count > 1 && letters.All(char.IsUpper))
            return replacement.ToUpperInvariant();

        if (char.IsUpper(original[0]))
            return char.ToUpperInvariant(replacement[0]) + replacement[1..];

        return replacement;
    }
}
