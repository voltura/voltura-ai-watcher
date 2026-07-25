namespace VolturaAiWatcher.Tests;

public sealed class ReferencedFileResolverTests
{
    [Fact]
    public void ResolvesMarkdownAbsolutePathWithLineNumber()
    {
        var fixture = CreateFixture();
        try
        {
            var message = $"Updated [watcher]({fixture.FilePath}:42).";

            var result = ReferencedFileResolver.ResolveFirstExistingFile(message, fixture.Directory);

            Assert.Equal(fixture.FilePath, result, ignoreCase: true);
        }
        finally
        {
            System.IO.Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    [Fact]
    public void ResolvesRelativeInlineCodeAgainstWorkingDirectory()
    {
        var fixture = CreateFixture();
        try
        {
            var message = "Changed `src/watcher file.cs`.";
            var nestedDirectory = System.IO.Path.Combine(fixture.Directory, "src");
            System.IO.Directory.CreateDirectory(nestedDirectory);
            var nestedFile = System.IO.Path.Combine(nestedDirectory, "watcher file.cs");
            System.IO.File.WriteAllText(nestedFile, "test");

            var result = ReferencedFileResolver.ResolveFirstExistingFile(message, fixture.Directory);

            Assert.Equal(nestedFile, result, ignoreCase: true);
        }
        finally
        {
            System.IO.Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    [Fact]
    public void IgnoresWebLinksAndMissingFiles()
    {
        var fixture = CreateFixture();
        try
        {
            var message = "See [docs](https://example.com/file.cs) and `missing.cs`.";

            var result = ReferencedFileResolver.ResolveFirstExistingFile(message, fixture.Directory);

            Assert.Null(result);
        }
        finally
        {
            System.IO.Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(@"C:\source\file.cs", @"C:\source\file.cs")]
    [InlineData(@"C:\source folder\file.cs", @"""C:\source folder\file.cs""")]
    public void ClipboardPathQuotesOnlyPathsWithSpaces(string path, string expected)
    {
        Assert.Equal(expected, ReferencedFileActions.FormatClipboardPath(path));
    }

    private static (string Directory, string FilePath) CreateFixture()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "VolturaAiWatcherTests",
            System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var filePath = System.IO.Path.Combine(directory, "watcher.cs");
        System.IO.File.WriteAllText(filePath, "test");
        return (directory, filePath);
    }
}

