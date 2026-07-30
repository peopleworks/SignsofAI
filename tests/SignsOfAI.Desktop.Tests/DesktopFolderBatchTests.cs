using System.IO;
using SignsOfAI.Desktop;

namespace SignsOfAI.Desktop.Tests;

/// <summary>
/// The folder scan, minus the dialog. What matters here is the promise the page relies on: only
/// readable files are listed, subfolders are obeyed, and a file that cannot be read comes back as a
/// row that says why — never as an exception that ends a scan of two hundred documents.
/// </summary>
public sealed class DesktopFolderBatchTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "soai-batch-" + Guid.NewGuid().ToString("N"));
    private readonly DesktopFolderBatch _batch = new();

    public DesktopFolderBatchTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "sub"));
        File.WriteAllText(Path.Combine(_dir, "a.txt"), "In today's digital age, we must delve into the rich tapestry of innovation.");
        File.WriteAllText(Path.Combine(_dir, "b.md"), "# Notes\n\nThe bus was late. I read two chapters standing up.");
        File.WriteAllText(Path.Combine(_dir, "empty.txt"), "");
        File.WriteAllBytes(Path.Combine(_dir, "picture.png"), [0x89, 0x50, 0x4E, 0x47]);
        File.WriteAllText(Path.Combine(_dir, "sub", "nested.md"), "It is worth noting that this serves as a testament.");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* a temp folder we could not clean is not a test failure */ }
    }

    [Fact]
    public void Is_available_on_the_desktop() => Assert.True(_batch.IsAvailable);

    [Fact]
    public async Task Lists_only_files_something_can_read()
    {
        var files = await _batch.ListAsync(_dir, recursive: false);
        var names = files.Select(f => f.Name).ToArray();

        Assert.Contains("a.txt", names);
        Assert.Contains("b.md", names);
        // A folder of submissions usually has images and archives in it too. Listing them as
        // "0 words" would bury the documents the user actually came for.
        Assert.DoesNotContain("picture.png", names);
    }

    [Fact]
    public async Task Honours_the_subfolder_choice()
    {
        var flat = await _batch.ListAsync(_dir, recursive: false);
        var deep = await _batch.ListAsync(_dir, recursive: true);

        Assert.DoesNotContain("nested.md", flat.Select(f => f.Name));
        Assert.Contains("nested.md", deep.Select(f => f.Name));
    }

    [Fact]
    public async Task Reads_a_document_and_reports_its_size()
    {
        var file = (await _batch.ListAsync(_dir, recursive: false)).Single(f => f.Name == "a.txt");
        var read = await _batch.ReadAsync(file);

        Assert.Null(read.Error);
        Assert.Contains("delve into", read.Text);
        Assert.True(file.Bytes > 0);
    }

    [Fact]
    public async Task An_empty_document_is_a_row_with_a_reason_not_an_exception()
    {
        var file = (await _batch.ListAsync(_dir, recursive: false)).Single(f => f.Name == "empty.txt");
        var read = await _batch.ReadAsync(file);

        Assert.Null(read.Text);
        Assert.False(string.IsNullOrWhiteSpace(read.Error));
    }

    [Fact]
    public async Task A_missing_folder_lists_nothing_rather_than_throwing()
    {
        var files = await _batch.ListAsync(Path.Combine(_dir, "does-not-exist"), recursive: false);
        Assert.Empty(files);
    }
}
