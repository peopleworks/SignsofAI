using SignsOfAI.Core.Originality;
using Xunit;

namespace SignsOfAI.Core.Tests;

public class DistinctivePhraseExtractorTests
{
    [Fact]
    public void Empty_text_returns_nothing()
    {
        Assert.Empty(DistinctivePhraseExtractor.Extract(""));
        Assert.Empty(DistinctivePhraseExtractor.Extract("   "));
    }

    [Fact]
    public void Prefers_distinctive_sentences_over_generic_filler()
    {
        var text = "It is what it is and we go on. " +
                   "The Treaty of Westphalia in 1648 established the modern principle of state sovereignty across Europe.";
        var phrases = DistinctivePhraseExtractor.Extract(text, maxPhrases: 1);

        Assert.Single(phrases);
        Assert.Contains("Westphalia", phrases[0].Phrase);
    }

    [Fact]
    public void Phrase_maps_back_to_the_original_text_verbatim()
    {
        var text = "Mitochondrial biogenesis accelerates dramatically during sustained endurance training in elite athletes.";
        var phrases = DistinctivePhraseExtractor.Extract(text, maxPhrases: 1);

        Assert.Single(phrases);
        Assert.Equal(phrases[0].Phrase, phrases[0].Span.Slice(text));
    }

    [Fact]
    public void Caps_phrase_length_to_max_words()
    {
        var text = "Photosynthesis fundamentally transforms atmospheric carbon dioxide into glucose through a remarkable " +
                   "sequence of light-dependent and light-independent biochemical reactions inside chloroplast membranes worldwide.";
        var phrases = DistinctivePhraseExtractor.Extract(text, maxPhrases: 1, maxWords: 6);

        Assert.Single(phrases);
        int wordCount = phrases[0].Phrase.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.True(wordCount <= 6, $"expected <= 6 words, got {wordCount}");
    }

    [Fact]
    public void Respects_max_phrases()
    {
        var text = "Constantinople fell to the Ottomans in 1453 after a lengthy siege. " +
                   "Gutenberg introduced movable-type printing to Europe around 1440 in Mainz. " +
                   "Magellan's expedition first circumnavigated the globe between 1519 and 1522. " +
                   "Copernicus proposed heliocentrism in his 1543 treatise on celestial spheres.";
        var phrases = DistinctivePhraseExtractor.Extract(text, maxPhrases: 2);
        Assert.Equal(2, phrases.Count);
    }
}
