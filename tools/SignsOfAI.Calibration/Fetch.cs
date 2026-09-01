using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SignsOfAI.Calibration;

/// <summary>
/// Assembles a corpus of writing that is known to be human.
///
/// "Known" is doing real work here, and it rests on one fact rather than on a classifier: every text
/// collected was published before generative models could have written it. A paper with a 2019 DOI
/// and a Wikipedia revision stamped 2021 were not produced by something that did not exist. No
/// detector can offer a guarantee that strong about anything, which is precisely why the corpus has
/// to come from dates and not from judgement.
///
/// Nothing collected is redistributed. The texts land on the machine that fetched them and the
/// repository carries only the manifest, so the licence of each source stays the source's problem and
/// the claim stays checkable.
/// </summary>
public static partial class Fetch
{
    private const string UserAgent =
        "SignsOfAI-calibration/0.1 (open-source false-positive measurement; " +
        "https://github.com/peopleworks/SignsofAI)";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return http;
    }

    /// <summary>
    /// A courtesy pause between calls. These are free public APIs run by people who owe this project
    /// nothing, and a corpus assembled by hammering them is not one worth having.
    /// </summary>
    private static readonly TimeSpan Politeness = TimeSpan.FromMilliseconds(350);

    /// <summary>
    /// Fetches, waiting when asked to wait. A 429 is the server saying "slow down", and the correct
    /// response is to slow down rather than to retry immediately or to give up on the text.
    /// </summary>
    private static async Task<string> GetAsync(string url, int attempts = 4)
    {
        for (int attempt = 1; ; attempt++)
        {
            await Task.Delay(Politeness);
            var response = await Http.GetAsync(url);

            if (response.StatusCode is not (System.Net.HttpStatusCode.TooManyRequests
                                            or System.Net.HttpStatusCode.ServiceUnavailable))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }

            if (attempt >= attempts) response.EnsureSuccessStatusCode();

            // Honour Retry-After when the server sends one; otherwise back off geometrically.
            var wait = response.Headers.RetryAfter?.Delta
                       ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
            Console.Error.WriteLine($"  rate limited, waiting {wait.TotalSeconds:0}s…");
            await Task.Delay(wait);
        }
    }

    // ---- PLOS ------------------------------------------------------------------------------------

    /// <summary>
    /// Countries where the academic writing is overwhelmingly done by first-language English speakers.
    ///
    /// This is a **proxy and a crude one**. Nobody's first language is recorded in a DOI, and plenty of
    /// people at a London university learned English second. It errs deliberately in one direction: a
    /// paper with any anglophone affiliation at all counts as anglophone, which shrinks the
    /// second-language group and makes any gap this finds an understatement rather than an
    /// exaggeration. The manifest records the affiliation used, so every classification can be argued
    /// with individually.
    /// </summary>
    private static readonly string[] AnglophoneMarkers =
    [
        "United States", "USA", "U.S.A", "United Kingdom", "England", "Scotland", "Wales",
        "Northern Ireland", "Australia", "Canada", "New Zealand", "Ireland",
    ];

    /// <summary>
    /// Open-access research articles published well before generative models, from PLOS.
    ///
    /// Academic prose is not a student essay and the report says so. It is, however, the closest
    /// freely licensed writing to what a teacher actually reads, and it comes with author
    /// affiliations — which is the only handle available on the question that matters here.
    /// </summary>
    public static async Task<List<CorpusEntry>> PlosAsync(int count, string textsDir, int fromYear, int toYear)
    {
        Directory.CreateDirectory(textsDir);
        var entries = new List<CorpusEntry>();

        // Ask for more than needed: some articles fail to parse, and some come out too short once the
        // tables and figure captions are dropped.
        var query =
            $"https://api.plos.org/search?q=" +
            Uri.EscapeDataString($"publication_date:[{fromYear}-01-01T00:00:00Z TO {toYear}-12-31T23:59:59Z] AND doc_type:full AND article_type:\"Research Article\"") +
            "&fl=" + Uri.EscapeDataString("id,publication_date,author_affiliate,title_display") +
            $"&rows={count * 3}&wt=json";

        using var doc = JsonDocument.Parse(await GetAsync(query));
        var docs = doc.RootElement.GetProperty("response").GetProperty("docs");

        foreach (var article in docs.EnumerateArray())
        {
            if (entries.Count >= count) break;

            var doi = article.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(doi)) continue;

            var affiliations = article.TryGetProperty("author_affiliate", out var aff)
                ? aff.EnumerateArray().Select(a => a.GetString() ?? "").ToList()
                : [];
            if (affiliations.Count == 0) continue;

            bool anglophone = affiliations.Any(a =>
                AnglophoneMarkers.Any(m => a.Contains(m, StringComparison.OrdinalIgnoreCase)));

            string text;
            try
            {
                text = ExtractJats(await GetAsync(
                    $"https://journals.plos.org/plosone/article/file?id={Uri.EscapeDataString(doi)}&type=manuscript"));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  skipped {doi}: {ex.Message}");
                continue;
            }

            if (CountWords(text) < 1200) continue;

            var id = "plos-" + doi.Split('.')[^1];
            var file = id + ".txt";
            await File.WriteAllTextAsync(Path.Combine(textsDir, file), text);

            var year = article.TryGetProperty("publication_date", out var pd)
                ? int.Parse(pd.GetString()![..4]) : fromYear;

            entries.Add(new CorpusEntry
            {
                Id = id,
                Language = "en",
                Stratum = anglophone ? "en-anglophone-affiliation" : "en-other-affiliation",
                Year = year,
                License = "CC BY 4.0",
                Doi = doi,
                Url = $"https://doi.org/{doi}",
                File = file,
                Sha256 = CorpusManifest.HashText(text),
                Note = "Affiliation used for grouping: " + Shorten(affiliations[0]),
            });

            Console.WriteLine($"  {entries.Count,3}. {id}  {year}  {(anglophone ? "anglophone" : "other     ")}  {CountWords(text),6:N0} words");
        }

        return entries;
    }

    /// <summary>Body paragraphs only — no abstract, no tables, no figure captions, no references.</summary>
    private static string ExtractJats(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        var doc = XDocument.Load(reader);

        var body = doc.Descendants("body").FirstOrDefault()
                   ?? throw new InvalidOperationException("no <body>");

        // Tables and figures carry captions and numbers rather than prose, and would distort the
        // sentence statistics the analyzer is being measured on.
        foreach (var stripped in body.Descendants()
                     .Where(e => e.Name.LocalName is "table-wrap" or "fig" or "disp-formula" or "inline-formula")
                     .ToList())
        {
            stripped.Remove();
        }

        var paragraphs = body.Descendants("p")
            .Select(p => Whitespace().Replace(p.Value, " ").Trim())
            .Where(t => CountWords(t) >= 15);

        return string.Join("\n\n", paragraphs);
    }

    // ---- Wikipedia -------------------------------------------------------------------------------

    /// <summary>
    /// Article prose as it stood before generative models, taken from the revision history.
    ///
    /// This is the only route found that yields substantial Spanish prose that is provably pre-model,
    /// openly licensed and machine-fetchable — and Spanish is the half of this project that nobody
    /// else measures at all. Fetching both languages from the same source is deliberate: it separates
    /// a language effect from a register effect, which two different sources could not.
    /// </summary>
    public static async Task<List<CorpusEntry>> WikipediaAsync(
        string language, int count, string textsDir, string beforeIso = "2021-06-01T00:00:00Z")
    {
        Directory.CreateDirectory(textsDir);
        var entries = new List<CorpusEntry>();
        var api = $"https://{language}.wikipedia.org/w/api.php";
        var seen = new HashSet<string>(StringComparer.Ordinal);

        int emptyRounds = 0;
        while (entries.Count < count && emptyRounds < 20)
        {
            int before = entries.Count;
            var batch = await GetAsync(
                $"{api}?action=query&list=random&rnnamespace=0&rnlimit=20&format=json");
            using var randoms = JsonDocument.Parse(batch);

            foreach (var page in randoms.RootElement.GetProperty("query").GetProperty("random").EnumerateArray())
            {
                if (entries.Count >= count) break;

                var title = page.GetProperty("title").GetString()!;
                if (!seen.Add(title)) continue;

                try
                {
                    var revisionsJson = await GetAsync(
                        $"{api}?action=query&prop=revisions&titles={Uri.EscapeDataString(title)}" +
                        $"&rvprop=ids|timestamp&rvstart={Uri.EscapeDataString(beforeIso)}&rvdir=older" +
                        "&rvlimit=1&redirects=1&format=json");

                    using var revisions = JsonDocument.Parse(revisionsJson);
                    var pageNode = revisions.RootElement.GetProperty("query").GetProperty("pages")
                        .EnumerateObject().First().Value;
                    if (!pageNode.TryGetProperty("revisions", out var revs)) continue;

                    var revision = revs[0];
                    long revid = revision.GetProperty("revid").GetInt64();
                    var stamp = revision.GetProperty("timestamp").GetString()!;

                    var html = await GetAsync(
                        $"https://{language}.wikipedia.org/api/rest_v1/page/html/" +
                        $"{Uri.EscapeDataString(title.Replace(' ', '_'))}/{revid}");

                    var text = ExtractHtmlProse(html);
                    if (CountWords(text) < 700) continue;

                    var id = $"wp-{language}-{revid}";
                    var file = id + ".txt";
                    await File.WriteAllTextAsync(Path.Combine(textsDir, file), text);

                    entries.Add(new CorpusEntry
                    {
                        Id = id,
                        Language = language,
                        Stratum = $"{language}-wikipedia",
                        Year = int.Parse(stamp[..4]),
                        License = "CC BY-SA 3.0",
                        Url = $"https://{language}.wikipedia.org/w/index.php?oldid={revid}",
                        File = file,
                        Sha256 = CorpusManifest.HashText(text),
                        Note = $"Revision of \"{title}\" as of {stamp}, before generative models.",
                    });

                    Console.WriteLine($"  {entries.Count,3}. {id}  {stamp[..10]}  {CountWords(text),6:N0} words  {Shorten(title)}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  skipped {title}: {ex.Message}");
                }
            }

            // Random articles are mostly short stubs, so rounds that yield nothing are normal. Give
            // up eventually rather than loop forever against somebody else's server.
            emptyRounds = entries.Count == before ? emptyRounds + 1 : 0;
        }

        return entries;
    }

    /// <summary>Paragraph prose only, with the apparatus — tables, notes, references — removed.</summary>
    private static string ExtractHtmlProse(string html)
    {
        foreach (var tag in new[] { "table", "style", "script", "sup", "figure", "ol", "ul" })
            html = Regex.Replace(html, $"<{tag}\\b.*?</{tag}>", " ", RegexOptions.Singleline);

        var paragraphs = Regex.Matches(html, "<p[^>]*>(.*?)</p>", RegexOptions.Singleline)
            .Select(m => Whitespace().Replace(Regex.Replace(m.Groups[1].Value, "<[^>]+>", ""), " ").Trim())
            .Where(t => CountWords(t) >= 20);

        return string.Join("\n\n", paragraphs);
    }

    // ---- PELIC -----------------------------------------------------------------------------------

    /// <summary>
    /// Classroom essays by learners of English, from the University of Pittsburgh's PELIC corpus:
    /// 1,177 students of its Intensive English Program, first languages recorded, written between
    /// 2006 and 2012. The date is the whole basis for calling them human, exactly as with the
    /// articles — and a second-language essay from 2008 could not have been polished by a model that
    /// did not exist.
    ///
    /// This is the group the affiliation proxy was standing in for. Detectors flag 61% of essays by
    /// non-native English speakers (Liang et al., 2023); this project criticises that number and had
    /// never measured its own on that population, because the articles it calibrated on are written
    /// by people who write for a living. These are not. They are the writers the harm lands on, at
    /// the length the corpus already covers.
    ///
    /// <para><b>The selection is a rule, not a choice.</b> First submitted version only, writing
    /// classes only, at least 662 words so the group enters at the floor the corpus already has, and
    /// one text per student — the lowest answer id — so no prolific writer counts twice. Nothing is
    /// picked by score, level or first language; the same file yields the same texts on any machine,
    /// which makes this group reproducible in a way the article fetchers cannot be.</para>
    ///
    /// <para><b>Licence: CC BY-NC-ND 4.0.</b> Measuring against the texts and publishing the numbers
    /// is use; redistributing the texts would be a derivative. So, like every other source here, they
    /// stay on the machine that fetched them and only the manifest is committed.</para>
    /// </summary>
    public static async Task<List<CorpusEntry>> PelicAsync(string textsDir)
    {
        const string repo = "https://github.com/ELI-Data-Mining-Group/PELIC-dataset";
        const string csvUrl = "https://media.githubusercontent.com/media/ELI-Data-Mining-Group/PELIC-dataset/master/PELIC_compiled.csv";
        const int minimumWords = 662;

        Directory.CreateDirectory(textsDir);
        var sourceDir = Path.Combine(textsDir, "pelic-source");
        Directory.CreateDirectory(sourceDir);
        var csvPath = Path.Combine(sourceDir, "PELIC_compiled.csv");

        if (!File.Exists(csvPath))
        {
            Console.WriteLine("  downloading PELIC_compiled.csv (about 180 MB, once)…");
            await DownloadAsync(csvUrl, csvPath);
        }

        // Every candidate first: "one per student" has to be decided over the whole file in answer-id
        // order, not in whatever order the rows happen to arrive.
        var candidates = new List<(int AnswerId, string Student, string L1, string Semester, string Level, string Text)>();
        using (var reader = new StreamReader(csvPath, Encoding.UTF8))
        {
            var header = ReadCsvRecord(reader) ?? throw new InvalidDataException("PELIC_compiled.csv is empty.");
            int Column(string name) => Array.IndexOf(header, name) is var i and >= 0
                ? i : throw new InvalidDataException($"PELIC_compiled.csv has no '{name}' column.");
            int answerId = Column("answer_id"), student = Column("anon_id"), l1 = Column("L1"),
                semester = Column("semester"), level = Column("level_id"), classId = Column("class_id"),
                version = Column("version"), text = Column("text");

            while (ReadCsvRecord(reader) is { } row)
            {
                if (row.Length <= text) continue;
                if (row[version] != "1" || row[classId] != "w") continue;

                var body = row[text].Replace("\r\n", "\n").Trim();
                if (CountWords(body) < minimumWords) continue;

                candidates.Add((int.Parse(row[answerId]), row[student], row[l1], row[semester], row[level], body));
            }
        }

        var entries = new List<CorpusEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in candidates.OrderBy(c => c.AnswerId))
        {
            if (!seen.Add(c.Student)) continue;

            var id = $"pelic-{c.AnswerId:D6}";
            var file = id + ".txt";
            var content = c.Text + "\n";
            await File.WriteAllTextAsync(Path.Combine(textsDir, file), content);

            entries.Add(new CorpusEntry
            {
                Id = id,
                Language = "en",
                Stratum = "en-second-language-learner",
                Year = int.Parse(c.Semester[..4]),
                License = "CC BY-NC-ND 4.0",
                Url = repo,
                File = file,
                Sha256 = CorpusManifest.HashText(content),
                Note = $"PELIC answer_id {c.AnswerId}, student {c.Student}, L1 {c.L1}, level {c.Level}, {c.Semester}. " +
                       "Writing class, first submitted version, one text per student (lowest answer_id).",
            });
        }

        Console.WriteLine($"  {candidates.Count:N0} candidate texts of {minimumWords}+ words; {entries.Count} students, one text each");
        return entries;
    }

    /// <summary>Streams a large file to disk with progress; a 180 MB string is nobody's friend.</summary>
    private static async Task DownloadAsync(string url, string path)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync();
        await using (var target = File.Create(path + ".part"))
        {
            var buffer = new byte[1 << 16];
            long done = 0, reported = 0;
            int read;
            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read));
                done += read;
                if (done - reported >= 20L << 20)
                {
                    Console.WriteLine($"    {done >> 20} MB…");
                    reported = done;
                }
            }
        }

        File.Move(path + ".part", path, overwrite: true);
    }

    /// <summary>
    /// One RFC 4180 record. Quoted fields may hold newlines and doubled quotes — PELIC's essays hold
    /// both, so a line-based reader would shred them.
    /// </summary>
    private static string[]? ReadCsvRecord(TextReader reader)
    {
        if (reader.Peek() < 0) return null;

        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;

        while (true)
        {
            int c = reader.Read();
            if (c < 0)
            {
                fields.Add(field.ToString());
                return fields.ToArray();
            }

            char ch = (char)c;
            if (quoted)
            {
                if (ch != '"') field.Append(ch);
                else if (reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                else quoted = false;
                continue;
            }

            switch (ch)
            {
                case '"': quoted = true; break;
                case ',': fields.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    fields.Add(field.ToString());
                    return fields.ToArray();
                default: field.Append(ch); break;
            }
        }
    }

    // ---- shared ----------------------------------------------------------------------------------

    private static int CountWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string Shorten(string value) =>
        value.Length <= 70 ? value : value[..67] + "…";

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
