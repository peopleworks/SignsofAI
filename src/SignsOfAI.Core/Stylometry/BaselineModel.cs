namespace SignsOfAI.Core.Stylometry;

/// <summary>One piece of writing offered for comparison — a past assignment, or the new submission.</summary>
public sealed record AuthorSample(string Id, string Title, string Text);

/// <summary>
/// Where the questioned text sits relative to the writer's own range.
///
/// There is deliberately no value meaning "someone else wrote this". The measurement cannot support
/// that claim and neither can anyone reading it: style moves with the assignment, the genre, the
/// deadline, the amount of care taken, and with a person simply getting better. What the scale says
/// is whether this piece looks like the others *by the same measure that the others look like each
/// other*, and the strongest honest reading of the far end is "worth asking about".
/// </summary>
public enum BaselinePlacement
{
    /// <summary>Not enough writing to say anything. The only safe answer when samples are thin.</summary>
    Undetermined,

    /// <summary>Inside the range this writer's own pieces already cover between themselves.</summary>
    WithinRange,

    /// <summary>Just past that range — closer to the edge than to anything unusual.</summary>
    AtTheEdge,

    /// <summary>Beyond the range the samples cover. A reason to ask, never a conclusion.</summary>
    BeyondRange,
}

/// <summary>
/// One function word pulling the comparison, with the rates behind it so a reader can check rather
/// than believe. Function words are the ones stylometry uses precisely because they do not follow the
/// topic: "the", "however", "de", "aunque" appear regardless of what the essay is about.
/// </summary>
public sealed record StyleDriver
{
    public required string Word { get; init; }

    /// <summary>How far from the writer's usual rate, in their own standard deviations.</summary>
    public required double ZScore { get; init; }

    /// <summary>Times per 1,000 words in the questioned text.</summary>
    public required double QuestionedRate { get; init; }

    /// <summary>Times per 1,000 words, averaged across the writer's own samples.</summary>
    public required double BaselineRate { get; init; }

    /// <summary>The lowest rate any of the writer's own pieces shows for this word.</summary>
    public required double BaselineLowest { get; init; }

    /// <summary>The highest rate any of the writer's own pieces shows for this word.</summary>
    public required double BaselineHighest { get; init; }

    /// <summary>True when the questioned text uses it more than the writer usually does.</summary>
    public bool UsedMore => QuestionedRate > BaselineRate;

    /// <summary>
    /// True when this word is used at a rate the writer has never used it at, in any of their pieces.
    /// Needs no statistics to check: two numbers and a range, countable by hand.
    /// </summary>
    public bool OutsideOwnRange => QuestionedRate < BaselineLowest || QuestionedRate > BaselineHighest;
}

/// <summary>
/// The comparison of one text against a writer's own earlier work.
///
/// This is the answer to the strongest objection to the whole category of AI detectors: they ask
/// "does this look like a machine", a question whose answer punishes anyone whose ordinary register
/// is formal — which is most people writing in a second language. This asks "does this look like the
/// person who wrote the other things", where a formal writer's baseline is already formal.
/// </summary>
public sealed record BaselineReport
{
    public required BaselinePlacement Placement { get; init; }

    /// <summary>
    /// How far the questioned text sits from the writer's centre, in the writer's own units. Only
    /// meaningful next to <see cref="WithinAuthorMax"/> — on its own it is a number with no scale.
    /// </summary>
    public required double Distance { get; init; }

    /// <summary>The furthest any one of the writer's own samples sits from their centre.</summary>
    public required double WithinAuthorMax { get; init; }

    public required double WithinAuthorMedian { get; init; }

    /// <summary>Every one of the writer's own samples, measured the same way as the questioned text.</summary>
    public required IReadOnlyList<double> WithinAuthorDistances { get; init; }

    /// <summary>The function words pulling hardest, most extreme first.</summary>
    public required IReadOnlyList<StyleDriver> Drivers { get; init; }

    public required int BaselineWordCount { get; init; }
    public required int QuestionedWordCount { get; init; }
    public required int SampleCount { get; init; }

    /// <summary>How many function words survived as usable features.</summary>
    public required int FeatureCount { get; init; }

    /// <summary>
    /// How many of those words the questioned text uses at a rate this writer has never used them at.
    ///
    /// This exists because of a known weakness in the aggregate. Delta is a mean across every feature,
    /// so a handful of words used at wildly different rates gets diluted by dozens that match: a text
    /// using "of" at seven times the writer's rate can still average out near their range. The count
    /// does not average anything, and a reader can verify any one of them by counting words.
    /// </summary>
    public required int WordsOutsideOwnRange { get; init; }

    /// <summary>
    /// True when the writer's own samples disagree with each other a lot, which makes any comparison
    /// weak. Usually it means the samples are of different kinds — or that one of them is not by the
    /// same person, which is worth knowing before anybody draws a conclusion from the rest.
    /// </summary>
    public required bool BaselineIsBroad { get; init; }

    /// <summary>Why the comparison could not be made, when it could not. Null otherwise.</summary>
    public string? Unavailable { get; init; }

    public required string Summary { get; init; }

    /// <summary>What this does and does not license. Always present.</summary>
    public required string Advice { get; init; }

    public bool HasResult => Placement != BaselinePlacement.Undetermined;

    public static BaselineReport NotAvailable(string reason, string advice) => new()
    {
        Placement = BaselinePlacement.Undetermined,
        Distance = 0,
        WithinAuthorMax = 0,
        WithinAuthorMedian = 0,
        WithinAuthorDistances = [],
        Drivers = [],
        BaselineWordCount = 0,
        QuestionedWordCount = 0,
        SampleCount = 0,
        FeatureCount = 0,
        WordsOutsideOwnRange = 0,
        BaselineIsBroad = false,
        Unavailable = reason,
        Summary = reason,
        Advice = advice,
    };
}
