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
    public void DescribesWhyAnApprovalTranscriptFileWasSelected()
    {
        var fixture = CreateFixture();
        try
        {
            var secondFile = System.IO.Path.Combine(fixture.Directory, "second.cs");
            System.IO.File.WriteAllText(secondFile, "test");
            var message =
                $"""
                Approval review context.
                >>> TRANSCRIPT DELTA START
                [1] tool apply_patch call: {fixture.FilePath}
                [2] tool apply_patch result: {secondFile}
                >>> TRANSCRIPT DELTA END
                >>> APPROVAL REQUEST START
                Planned action.
                >>> APPROVAL REQUEST END
                """;

            var result = ReferencedFileResolver.ResolveFirstExistingFileReference(
                message,
                fixture.Directory);

            Assert.NotNull(result);
            Assert.Equal(fixture.FilePath, result.Path, ignoreCase: true);
            Assert.Equal(ReferencedFileSourceKind.AbsolutePath, result.SourceKind);
            Assert.Equal(ReferencedFileMessageSection.ApprovalTranscript, result.MessageSection);
            Assert.Equal(1, result.SelectionIndex);
            Assert.Equal(2, result.AvailableFileCount);
            Assert.Equal(
                $"""
                Open watcher.cs
                {fixture.FilePath}
                Source: approval transcript · absolute path
                Why: first available file reference (1 of 2)
                """,
                ReferencedFileToolTipFormatter.FormatAutomaticOpen(result));
        }
        finally
        {
            System.IO.Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    [Fact]
    public void DescribesDirectMessageFileLinksWithoutAutomaticSelectionReason()
    {
        var fixture = CreateFixture();
        try
        {
            Assert.Equal(
                $"Open watcher.cs\n{fixture.FilePath}\nSource: direct link in message",
                ReferencedFileToolTipFormatter.FormatDirectOpen(fixture.FilePath));
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

    [Fact]
    public void ResolvesOneExplicitExistingFileReference()
    {
        var fixture = CreateFixture();
        try
        {
            var result = ReferencedFileResolver.ResolveExistingFile(
                fixture.FilePath.Replace('\\', '/'),
                fixture.Directory);

            Assert.Equal(fixture.FilePath, result, ignoreCase: true);
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
