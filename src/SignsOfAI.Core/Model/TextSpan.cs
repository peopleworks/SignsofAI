namespace SignsOfAI.Core.Model;

/// <summary>A character range in the analyzed text, used to anchor a finding for highlighting.</summary>
public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;

    public string Slice(string source) => source.Substring(Start, Length);
}
