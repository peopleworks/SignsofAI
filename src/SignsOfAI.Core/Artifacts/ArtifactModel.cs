using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Artifacts;

/// <summary>
/// A family of character-level artifact — something present in the file that typing does not produce.
///
/// Deliberately *not* a <see cref="SignCategory"/>: these are not signs of AI writing and they never
/// touch the score. A score is a probability someone can argue with; a character at a known offset is
/// a fact they cannot. Keeping the two apart is the whole point.
/// </summary>
public enum ArtifactKind
{
    /// <summary>Zero-width and other characters that occupy no space when rendered.</summary>
    InvisibleCharacter,

    /// <summary>Bidirectional overrides, which can make displayed text differ from stored text.</summary>
    BidiControl,

    /// <summary>A letter from another script standing in for a Latin one (Cyrillic "а" for "a").</summary>
    LookalikeLetter,

    /// <summary>A space that is not U+0020 — non-breaking, narrow, en/em, ideographic.</summary>
    UnusualSpace,

    /// <summary>U+00AD, routine in text extracted from a PDF and rare anywhere else.</summary>
    SoftHyphen,

    /// <summary>A variation selector not attached to an emoji.</summary>
    VariationSelector,

    /// <summary>A private-use codepoint, which has no meaning outside the font that defined it.</summary>
    PrivateUse,

    /// <summary>A tag character (U+E0000–E007F) — an invisible channel for carrying hidden text.</summary>
    TagCharacter,
}

/// <summary>
/// How the artifacts are distributed, which is what separates "pasted from somewhere" from
/// "processed by something". A tool that rewrites a document leaves traces throughout it; a
/// copy-paste leaves them where the paste landed.
/// </summary>
public enum ArtifactPattern
{
    None,

    /// <summary>Present but clustered, or only of the kinds that ordinary copy-paste produces.</summary>
    Incidental,

    /// <summary>Enough of them, spread widely enough, that a tool is the ordinary explanation.</summary>
    Systematic,
}

/// <summary>One artifact at one position, with everything needed to point at it.</summary>
public sealed record ArtifactOccurrence
{
    public required ArtifactKind Kind { get; init; }

    /// <summary>Where it is, in the original text — not the normalized one.</summary>
    public required TextSpan Span { get; init; }

    /// <summary>1-based, so it matches what an editor shows.</summary>
    public required int Line { get; init; }

    /// <summary>1-based column, counted in characters.</summary>
    public required int Column { get; init; }

    /// <summary>The codepoint, formatted the way the Unicode standard writes it: "U+200B".</summary>
    public required string CodePoint { get; init; }

    /// <summary>The official Unicode name, so the reader can look it up rather than trust us.</summary>
    public required string CharacterName { get; init; }

    /// <summary>For a lookalike, the Latin letter it is standing in for.</summary>
    public string? LooksLike { get; init; }

    /// <summary>For a lookalike, the word it was found inside.</summary>
    public string? Word { get; init; }

    /// <summary>
    /// True when the character is one that typing simply does not produce, as opposed to one an
    /// ordinary paste from a web page or a PDF leaves behind. Only strong artifacts can make a
    /// document read as <see cref="ArtifactPattern.Systematic"/>.
    /// </summary>
    public required bool IsStrong { get; init; }

    public required string Message { get; init; }
}

/// <summary>The same character seen many times, rolled up so the report reads as a list of facts.</summary>
public sealed record ArtifactGroup
{
    public required ArtifactKind Kind { get; init; }
    public required string CodePoint { get; init; }
    public required string CharacterName { get; init; }
    public required int Count { get; init; }
    public string? LooksLike { get; init; }
    public required bool IsStrong { get; init; }
}

/// <summary>
/// What a character-level scan found. Independent of language, of the rule packs, and of the score.
/// </summary>
public sealed record ArtifactReport
{
    public required IReadOnlyList<ArtifactOccurrence> Occurrences { get; init; }

    public required IReadOnlyList<ArtifactGroup> Groups { get; init; }

    public required ArtifactPattern Pattern { get; init; }

    /// <summary>How many sections of the document contain at least one strong artifact.</summary>
    public required int SectionsAffected { get; init; }

    /// <summary>How many sections the document was divided into to measure spread.</summary>
    public required int SectionCount { get; init; }

    /// <summary>One sentence stating what was found and how it is distributed.</summary>
    public required string Summary { get; init; }

    /// <summary>
    /// What the reader should do with it — which is to ask where the file has been, never to
    /// conclude who wrote it. Always present when anything was found.
    /// </summary>
    public required string Advice { get; init; }

    public int Count => Occurrences.Count;

    public int StrongCount { get; init; }

    public bool Any => Occurrences.Count > 0;

    public static ArtifactReport Empty { get; } = new()
    {
        Occurrences = [],
        Groups = [],
        Pattern = ArtifactPattern.None,
        SectionsAffected = 0,
        SectionCount = 0,
        Summary = string.Empty,
        Advice = string.Empty,
        StrongCount = 0,
    };
}
