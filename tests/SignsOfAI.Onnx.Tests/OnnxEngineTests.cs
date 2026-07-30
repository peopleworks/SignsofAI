using Microsoft.Extensions.Logging.Abstractions;
using SignsOfAI.Onnx.Config;
using SignsOfAI.Onnx.Engine;
using SignsOfAI.Onnx.Scoring;

namespace SignsOfAI.Onnx.Tests;

public sealed class OnnxEngineTests
{
    [Fact]
    public void DefaultOptions_AreValidAndConstructibleInCode()
    {
        var perplexity = PerplexityOptions.Defaults();
        var embeddings = EmbeddingOptions.Defaults();

        Assert.Empty(perplexity.Validate());
        Assert.Empty(embeddings.Validate());
        Assert.Equal("qwen2.5-0.5b-instruct-int8", perplexity.DefaultModel);
        Assert.Equal("embeddinggemma-300m-int8", embeddings.DefaultModel);
    }

    [Fact]
    public void InvalidOptions_ReportErrorsWithoutLoadingModels()
    {
        var perplexity = new PerplexityOptions();
        var embeddings = new EmbeddingOptions();

        Assert.Contains(perplexity.Validate(), error => error.Contains("at least one", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(embeddings.Validate(), error => error.Contains("at least one", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MissingModel_IsUnavailableAndDoesNotThrowDuringConstructionOrProbe()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"signsofai-missing-{Guid.NewGuid():N}");
        var profile = ValidPerplexityProfile("missing-model");

        using var engine = new OnnxModelEngine(profile, basePath, NullLogger.Instance);

        Assert.Equal(Path.GetFullPath(profile.ModelDir, basePath), engine.ModelDirectory);
        Assert.False(engine.IsAvailable);
        Assert.False(engine.FilesReady);
        Assert.False(await engine.ProbeAsync(CancellationToken.None));
        Assert.False(engine.IsLoaded);
    }

    [Fact]
    public async Task MissingEmbeddingModel_IsUnavailableAndDoesNotThrowDuringConstructionOrProbe()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"signsofai-missing-{Guid.NewGuid():N}");
        var profile = new EmbeddingProfile
        {
            Id = "missing-embedding",
            ModelDir = "models/test",
            ModelFile = "model.onnx",
            TokenizerFile = "tokenizer.json",
        };

        using var engine = new OnnxEmbeddingEngine(profile, basePath, NullLogger.Instance);

        Assert.Equal(Path.GetFullPath(profile.ModelDir, basePath), engine.ModelDirectory);
        Assert.False(engine.IsAvailable);
        Assert.False(engine.FilesReady);
        Assert.False(await engine.ProbeAsync(CancellationToken.None));
        Assert.False(engine.IsLoaded);
    }

    [Fact]
    public void PerplexityScorer_PreservesCalibratedPureMath()
    {
        var profile = ValidPerplexityProfile("math-model");
        profile.Baselines["en"] = new LangBaseline { Center = 4.35, Spread = 0.75, Steepness = 1.3 };
        var raw = new PerplexityRaw(Math.Exp(4.35), -4.35, 12, 34);

        var score = PerplexityScorer.Score(raw, "en", profile);

        Assert.Equal(Math.Round(Math.Exp(4.35), 2), score.Ppl);
        Assert.Equal(-4.35, score.AvgLogProb);
        Assert.Equal(12, score.TokenCount);
        Assert.Equal(0.5, score.Predictability);
        Assert.Equal("typical", score.Band);
        Assert.Equal("math-model", score.Model);
        Assert.Equal("en", score.Lang);
        Assert.Equal(34, score.ElapsedMs);
    }

    [Fact]
    public async Task QwenModel_ScoresText_WhenWeightsAreInstalled()
    {
        var repoRoot = FindRepoRoot();
        var profile = PerplexityOptions.Defaults().Models[0];
        var modelPath = Path.Combine(repoRoot, profile.ModelDir, profile.ModelFile);
        if (!File.Exists(modelPath))
            return;

        using var engine = new OnnxModelEngine(profile, repoRoot, NullLogger.Instance);
        if (!await engine.ProbeAsync(CancellationToken.None))
            return;

        var raw = await engine.ScoreAsync(
            "A short sentence validates local in-process inference.",
            CancellationToken.None);

        Assert.True(double.IsFinite(raw.Perplexity));
        Assert.True(raw.Perplexity > 0);
        Assert.True(double.IsFinite(raw.MeanLogProb));
        Assert.True(raw.ScoredTokens > 0);
    }

    [Fact]
    public async Task EmbeddingGemma_ProducesNormalizedVector_WhenWeightsAreInstalled()
    {
        var repoRoot = FindRepoRoot();
        var profile = EmbeddingOptions.Defaults().Models[0];
        var modelPath = Path.Combine(repoRoot, profile.ModelDir, profile.ModelFile);
        if (!File.Exists(modelPath))
            return;

        using var engine = new OnnxEmbeddingEngine(profile, repoRoot, NullLogger.Instance);
        if (!await engine.ProbeAsync(CancellationToken.None))
            return;

        var vectors = await engine.EmbedAsync(
            ["A short sentence validates local embedding inference."],
            64,
            CancellationToken.None);

        var vector = Assert.Single(vectors);
        Assert.Equal(64, vector.Length);
        Assert.All(vector, value => Assert.True(float.IsFinite(value)));
        var norm = Math.Sqrt(vector.Sum(value => value * (double)value));
        Assert.InRange(norm, 0.999, 1.001);
    }

    private static ModelProfile ValidPerplexityProfile(string id) => new()
    {
        Id = id,
        ModelDir = "models/test",
        ModelFile = "model.onnx",
        TokenizerFile = "tokenizer.json",
        NumLayers = 1,
        NumKvHeads = 1,
        HeadDim = 1,
        Vocab = 2,
        Baselines =
        {
            ["en"] = new LangBaseline { Center = 4.2 },
        },
    };

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "SignsOfAI.Onnx", "SignsOfAI.Onnx.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the SignsOfAI repository root.");
    }
}
