using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignsOfAI.Calibration;

/// <summary>
/// One text of known human authorship, described well enough that somebody else can obtain the same
/// thing and check it.
/// </summary>
public sealed class CorpusEntry
{
    public required string Id { get; set; }

    /// <summary>"en" or "es".</summary>
    public required string Language { get; set; }

    /// <summary>
    /// The group this counts toward. The axis worth reporting on is whether the writer was working in
    /// a second language, because that is where this category of tool does its damage.
    /// </summary>
    public required string Stratum { get; set; }

    /// <summary>
    /// Year of publication, and the entire basis for calling the text human. A paper with a 2019 DOI
    /// was not written by a model that did not exist, which is a stronger guarantee than any
    /// classifier can offer about anything.
    /// </summary>
    public required int Year { get; set; }

    public required string License { get; set; }

    /// <summary>Where it came from, so the claim can be traced rather than trusted.</summary>
    public string? Url { get; set; }

    public string? Doi { get; set; }

    /// <summary>Filename inside the texts directory.</summary>
    public required string File { get; set; }

    /// <summary>
    /// SHA-256 of the extracted text, recorded when the corpus is assembled.
    ///
    /// This proves a run was made against the file the manifest names. It does *not* prove two people
    /// extracting the same article independently produced identical text — they will not, because PDF
    /// and HTML extraction differ. Cross-machine reproducibility needs the extracted texts shared, not
    /// just the manifest, and the report says which hashes it saw.
    /// </summary>
    public string? Sha256 { get; set; }

    /// <summary>Why this text is in the group it is in — the reasoning, so it can be argued with.</summary>
    public string? Note { get; set; }
}

/// <summary>
/// The corpus as a data file rather than a folder of assumptions.
///
/// The texts themselves are deliberately not in the repository: licences differ per source, the bulk
/// would dwarf the code, and neither problem is solved by ignoring it. What lives here is the index —
/// what each text is, where it came from, why it counts as human, and its hash — which is the part
/// that has to be reviewable. It is JSON so that adding twenty Spanish articles is a pull request
/// anybody can read, exactly like the rule packs.
/// </summary>
public sealed class CorpusManifest
{
    public required string Id { get; set; }

    public string? Description { get; set; }

    /// <summary>The false-positive rate the project intends to be able to promise.</summary>
    public double TargetFalsePositiveRate { get; set; } = 0.05;

    public List<CorpusEntry> Texts { get; set; } = [];

    public static CorpusManifest Load(string path) =>
        JsonSerializer.Deserialize(File.ReadAllText(path), CorpusJson.Default.CorpusManifest)
        ?? throw new InvalidOperationException($"'{path}' deserialized to null.");

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(this, CorpusJson.Default.CorpusManifest) + "\n");

    /// <summary>
    /// A fingerprint of what was measured: every entry's identity and content hash, in a fixed order.
    /// Reported alongside the numbers, so a result and the corpus it came from cannot drift apart
    /// unnoticed — the same discipline the released binaries get.
    /// </summary>
    public string Fingerprint()
    {
        var canonical = string.Join('\n', Texts
            .OrderBy(t => t.Id, StringComparer.Ordinal)
            .Select(t => $"{t.Id}\t{t.Language}\t{t.Stratum}\t{t.Year}\t{t.Sha256}"));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..16];
    }

    public static string HashText(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(CorpusManifest))]
public partial class CorpusJson : JsonSerializerContext;
