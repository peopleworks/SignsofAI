using System.Text;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Core.Artifacts;

/// <summary>
/// Finds characters in a document that writing it does not produce: zero-width characters, letters
/// borrowed from another script to impersonate Latin ones, direction controls, hidden tag characters.
///
/// This is the one check in the product that returns a fact instead of a judgement. Every other
/// analyzer answers "does this read like AI", which is a probability and is arguable by construction.
/// This one answers "is there a Cyrillic а at line 14, column 3", which either is or is not true, and
/// which anyone can verify in any editor without trusting us. That is why it does not feed the score.
///
/// It is also why the wording never says "cheating". Legitimate documents pick these up: text pasted
/// from a web page, extracted from a PDF, or written in more than one language. What the report
/// carries is the count, the positions, and how widely they are spread — the reader draws the
/// conclusion, and the right first move is always to ask the writer.
///
/// Stateless and language-independent: it works below the level of language, so it needs no rule pack
/// to find anything. The pack is consulted only for the words the reader sees.
/// </summary>
public static class ArtifactScanner
{
    /// <summary>Strong artifacts needed before spread is even considered.</summary>
    private const int SystematicMinimum = 4;

    private const int MaxSections = 10;
    private const int CharsPerSection = 150;

    private static readonly RulePack NeutralPack = new() { Language = "*" };

    /// <param name="text">The document, exactly as it arrived — never a cleaned copy.</param>
    /// <param name="pack">Supplies the wording; the built-in English text is used when null.</param>
    public static ArtifactReport Scan(string? text, RulePack? pack = null)
    {
        if (string.IsNullOrEmpty(text))
            return ArtifactReport.Empty;

        pack ??= NeutralPack;

        var scalars = Decode(text);
        var lineStarts = LineStarts(text);

        var occurrences = new List<ArtifactOccurrence>();
        ScanCharacters(scalars, text, lineStarts, pack, occurrences);
        ScanWords(scalars, text, lineStarts, pack, occurrences);

        if (occurrences.Count == 0)
            return ArtifactReport.Empty;

        occurrences.Sort((a, b) => a.Span.Start.CompareTo(b.Span.Start));

        int strong = occurrences.Count(o => o.IsStrong);
        int sectionCount = Math.Clamp(text.Length / CharsPerSection, 1, MaxSections);
        int sectionsAffected = CountSectionsAffected(occurrences, text.Length, sectionCount);

        // A tag character is the one kind with no innocent explanation in ordinary prose: it renders
        // as nothing and exists to carry text a reader cannot see. One is enough.
        bool hidden = occurrences.Any(o => o.Kind == ArtifactKind.TagCharacter);
        bool spread = strong >= SystematicMinimum && sectionsAffected * 2 >= sectionCount;

        var pattern = hidden || spread ? ArtifactPattern.Systematic : ArtifactPattern.Incidental;

        var summary = pattern == ArtifactPattern.Systematic
            ? pack.Text(PackMessages.ArtifactSummarySystematic, strong, sectionsAffected, sectionCount)
            : pack.Text(PackMessages.ArtifactSummaryIncidental, occurrences.Count);

        return new ArtifactReport
        {
            Occurrences = occurrences,
            Groups = Group(occurrences),
            Pattern = pattern,
            SectionsAffected = sectionsAffected,
            SectionCount = sectionCount,
            StrongCount = strong,
            Summary = summary,
            Advice = pack.Text(PackMessages.ArtifactAdvice),
        };
    }

    // ---- character-level pass -------------------------------------------------------------------

    private static void ScanCharacters(
        IReadOnlyList<Scalar> scalars, string text, int[] lineStarts,
        RulePack pack, List<ArtifactOccurrence> into)
    {
        for (int i = 0; i < scalars.Count; i++)
        {
            var s = scalars[i];
            int cp = s.CodePoint;

            ArtifactKind kind;
            bool strong;
            string messageKey;

            if (Characters.IsZeroWidth(cp))
            {
                if (IsLegitimateJoin(scalars, i)) continue;
                (kind, strong, messageKey) = (ArtifactKind.InvisibleCharacter, true, PackMessages.ArtifactInvisible);
            }
            else if (Characters.IsBidiControl(cp))
            {
                (kind, strong, messageKey) = (ArtifactKind.BidiControl, true, PackMessages.ArtifactBidi);
            }
            else if (Characters.IsTagCharacter(cp))
            {
                (kind, strong, messageKey) = (ArtifactKind.TagCharacter, true, PackMessages.ArtifactTag);
            }
            else if (Characters.IsVariationSelector(cp))
            {
                if (FollowsEmoji(scalars, i)) continue;
                (kind, strong, messageKey) = (ArtifactKind.VariationSelector, true, PackMessages.ArtifactVariationSelector);
            }
            else if (Characters.IsPrivateUse(cp))
            {
                (kind, strong, messageKey) = (ArtifactKind.PrivateUse, true, PackMessages.ArtifactPrivateUse);
            }
            else if (cp == Characters.SoftHyphen)
            {
                (kind, strong, messageKey) = (ArtifactKind.SoftHyphen, false, PackMessages.ArtifactSoftHyphen);
            }
            else if (Characters.IsUnusualSpace(cp))
            {
                (kind, strong, messageKey) = (ArtifactKind.UnusualSpace, false, PackMessages.ArtifactSpace);
            }
            else
            {
                continue;
            }

            var code = Characters.Format(cp);
            var name = Characters.Name(cp);
            var (line, column) = Position(lineStarts, s.Index);

            into.Add(new ArtifactOccurrence
            {
                Kind = kind,
                Span = new TextSpan(s.Index, s.Length),
                Line = line,
                Column = column,
                CodePoint = code,
                CharacterName = name,
                IsStrong = strong,
                Message = pack.Text(messageKey, code, name),
            });
        }
    }

    /// <summary>
    /// A zero-width joiner or non-joiner is ordinary orthography in Arabic, Hebrew and the Indic
    /// scripts, and it is how emoji sequences are built. Neither is an artifact.
    /// </summary>
    private static bool IsLegitimateJoin(IReadOnlyList<Scalar> scalars, int i)
    {
        int cp = scalars[i].CodePoint;
        if (cp is not (0x200C or 0x200D))
            return false;

        int before = i > 0 ? scalars[i - 1].CodePoint : -1;
        int after = i + 1 < scalars.Count ? scalars[i + 1].CodePoint : -1;

        if (before >= 0 && Characters.NeedsJoinControls(before)) return true;
        if (after >= 0 && Characters.NeedsJoinControls(after)) return true;

        return cp == 0x200D
               && before >= 0 && after >= 0
               && Characters.IsEmojiLike(before) && Characters.IsEmojiLike(after);
    }

    private static bool FollowsEmoji(IReadOnlyList<Scalar> scalars, int i) =>
        i > 0 && Characters.IsEmojiLike(scalars[i - 1].CodePoint);

    // ---- word-level pass ------------------------------------------------------------------------

    /// <summary>
    /// Looks for letters standing in for Latin ones, one word at a time.
    ///
    /// The decision is made per run of letters rather than per character, because the character alone
    /// cannot tell the two cases apart. In "аnalysis" a Cyrillic а sits among Latin letters and is an
    /// impostor. In "α-helix" the Greek letter is the whole first run and is exactly what the writer
    /// meant. So a lookalike only counts when Latin letters are the majority of the run it sits in —
    /// which is the shape of a substitution and not the shape of a real word from another script.
    /// </summary>
    private static void ScanWords(
        IReadOnlyList<Scalar> scalars, string text, int[] lineStarts,
        RulePack pack, List<ArtifactOccurrence> into)
    {
        int i = 0;
        while (i < scalars.Count)
        {
            if (!Rune.IsLetter(scalars[i].Rune)) { i++; continue; }

            int start = i;
            while (i < scalars.Count && Rune.IsLetter(scalars[i].Rune)) i++;

            EvaluateRun(scalars, start, i, text, lineStarts, pack, into);
        }
    }

    private static void EvaluateRun(
        IReadOnlyList<Scalar> scalars, int start, int end, string text, int[] lineStarts,
        RulePack pack, List<ArtifactOccurrence> into)
    {
        int latin = 0, other = 0;
        List<(Scalar Scalar, char Latin)>? suspects = null;

        for (int i = start; i < end; i++)
        {
            int cp = scalars[i].CodePoint;
            var looksLike = Characters.LooksLike(cp);

            if (looksLike is { } letter)
                (suspects ??= []).Add((scalars[i], letter));
            else if (Characters.IsLatinLetter(cp))
                latin++;
            else
                other++;
        }

        if (suspects is null || latin == 0 || latin < suspects.Count + other)
            return;

        int from = scalars[start].Index;
        int to = scalars[end - 1].Index + scalars[end - 1].Length;
        var word = text[from..to];

        foreach (var (scalar, letter) in suspects)
        {
            var code = Characters.Format(scalar.CodePoint);
            var name = Characters.Name(scalar.CodePoint);
            var (line, column) = Position(lineStarts, scalar.Index);

            into.Add(new ArtifactOccurrence
            {
                Kind = ArtifactKind.LookalikeLetter,
                Span = new TextSpan(scalar.Index, scalar.Length),
                Line = line,
                Column = column,
                CodePoint = code,
                CharacterName = name,
                LooksLike = letter.ToString(),
                Word = word,
                IsStrong = true,
                Message = pack.Text(PackMessages.ArtifactLookalike, code, name, letter, word),
            });
        }
    }

    // ---- rollups --------------------------------------------------------------------------------

    private static List<ArtifactGroup> Group(IEnumerable<ArtifactOccurrence> occurrences) =>
        [.. occurrences
            .GroupBy(o => (o.Kind, o.CodePoint, o.LooksLike))
            .Select(g => new ArtifactGroup
            {
                Kind = g.Key.Kind,
                CodePoint = g.Key.CodePoint,
                CharacterName = g.First().CharacterName,
                LooksLike = g.Key.LooksLike,
                Count = g.Count(),
                IsStrong = g.First().IsStrong,
            })
            .OrderByDescending(g => g.IsStrong)
            .ThenByDescending(g => g.Count)
            .ThenBy(g => g.CodePoint, StringComparer.Ordinal)];

    /// <summary>
    /// How many equal slices of the document contain a strong artifact. This is the measurement that
    /// separates a paste from a pass: pasted text carries its artifacts in one place, whereas a tool
    /// that rewrote the document leaves them everywhere it touched.
    /// </summary>
    private static int CountSectionsAffected(
        IEnumerable<ArtifactOccurrence> occurrences, int length, int sectionCount)
    {
        if (sectionCount <= 1) return occurrences.Any(o => o.IsStrong) ? 1 : 0;

        var seen = new bool[sectionCount];
        foreach (var o in occurrences)
        {
            if (!o.IsStrong) continue;
            int section = Math.Clamp(o.Span.Start * sectionCount / Math.Max(1, length), 0, sectionCount - 1);
            seen[section] = true;
        }
        return seen.Count(x => x);
    }

    // ---- text plumbing --------------------------------------------------------------------------

    /// <summary>A single Unicode scalar with the offset and width it occupies in the UTF-16 string.</summary>
    internal readonly record struct Scalar(Rune Rune, int Index, int Length)
    {
        public int CodePoint => Rune.Value;
    }

    internal static List<Scalar> Decode(string text)
    {
        var scalars = new List<Scalar>(text.Length);
        int pos = 0;
        while (pos < text.Length)
        {
            // An unpaired surrogate decodes to the replacement rune over one char, which keeps
            // offsets exact; malformed input must not shift every position after it.
            Rune.DecodeFromUtf16(text.AsSpan(pos), out var rune, out int consumed);
            scalars.Add(new Scalar(rune, pos, consumed));
            pos += consumed;
        }
        return scalars;
    }

    private static int[] LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (int i = 0; i < text.Length; i++)
            if (text[i] == '\n') starts.Add(i + 1);
        return [.. starts];
    }

    /// <summary>1-based line and column, so the numbers match what an editor shows.</summary>
    private static (int Line, int Column) Position(int[] lineStarts, int index)
    {
        int lo = 0, hi = lineStarts.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (lineStarts[mid] <= index) lo = mid; else hi = mid - 1;
        }
        return (lo + 1, index - lineStarts[lo] + 1);
    }
}
