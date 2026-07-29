namespace VolturaAiWatcher.Tests;

public sealed class MessageDocumentParserTests
{
    [Fact]
    public void ParsesMarkdownHeadersAndExistingFileReference()
    {
        var fixture = CreateFixture();
        try
        {
            var slashPath = fixture.FilePath.Replace('\\', '/');
            var message =
                $"""
                # Files mentioned by the user:

                ## screenshot.png: {slashPath}

                ## My request for Codex:
                Fix the layout.
                """;

            var blocks = MessageDocumentParser.Parse(message, fixture.Directory);

            Assert.Collection(
                blocks,
                block =>
                {
                    Assert.Equal(MessageDocumentBlockKind.Header1, block.Kind);
                    Assert.Equal("Files mentioned by the user:", block.Text);
                    Assert.Null(block.FilePath);
                },
                block =>
                {
                    Assert.Equal(MessageDocumentBlockKind.Header2, block.Kind);
                    Assert.Equal("screenshot.png", block.LinkText);
                    Assert.Equal(fixture.FilePath, block.FilePath, ignoreCase: true);
                },
                block =>
                {
                    Assert.Equal(MessageDocumentBlockKind.Header2, block.Kind);
                    Assert.Equal("My request for Codex:", block.Text);
                    Assert.Null(block.FilePath);
                },
                block =>
                {
                    Assert.Equal(MessageDocumentBlockKind.Paragraph, block.Kind);
                    Assert.Equal("Fix the layout.", block.Text);
                });
        }
        finally
        {
            System.IO.Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    [Fact]
    public void MissingFileRemainsANonInteractiveSubheader()
    {
        var blocks = MessageDocumentParser.Parse(
            "## missing.png: C:/path/that/does/not/exist.png",
            workingDirectory: null);

        var block = Assert.Single(blocks);
        Assert.Equal(MessageDocumentBlockKind.Header2, block.Kind);
        Assert.Null(block.LinkText);
        Assert.Null(block.FilePath);
    }

    [Fact]
    public void PreservesBodyLineBreaksWithinParagraph()
    {
        var blocks = MessageDocumentParser.Parse("First line\nSecond line", workingDirectory: null);

        var block = Assert.Single(blocks);
        Assert.Equal(MessageDocumentBlockKind.Paragraph, block.Kind);
        Assert.Equal("First line\nSecond line", block.Text);
    }

    [Fact]
    public void RemovesOuterQuoteAroundMarkdownAttachmentMessage()
    {
        var blocks = MessageDocumentParser.Parse(
            "\"# Files mentioned by the user:\n\n## My request for Codex:\nFix it.\"",
            workingDirectory: null);

        Assert.Equal(MessageDocumentBlockKind.Header1, blocks[0].Kind);
        Assert.Equal("Files mentioned by the user:", blocks[0].Text);
        Assert.Equal(MessageDocumentBlockKind.Header2, blocks[1].Kind);
        Assert.Equal("Fix it.", blocks[2].Text);
    }

    [Fact]
    public void ParsesActualCodexProductLinkMessageShape()
    {
        var fixture = CreateFixture();
        try
        {
            var fileTarget = fixture.FilePath.Replace('\\', '/');
            var message =
                $"""
                Updated the product link to `https://voltura.github.io/voltura-ai-watcher/` in:

                - [MainWindow.xaml.cs]({fileTarget}:801)
                - Added the required release-note entry.

                Verification: build succeeded.
                """;

            var blocks = MessageDocumentParser.Parse(message, fixture.Directory);
            var firstParagraph = MessageInlineParser.Parse(blocks[0].Text, fixture.Directory);
            var fileParagraph = MessageInlineParser.Parse(blocks[1].Text, fixture.Directory);

            var webLink = Assert.Single(
                firstParagraph,
                inline => inline.Kind == MessageInlineKind.WebLink);
            Assert.Equal(
                "https://voltura.github.io/voltura-ai-watcher/",
                webLink.Text);
            Assert.Equal(
                "https://voltura.github.io/voltura-ai-watcher/",
                webLink.Target);

            var fileLink = Assert.Single(
                fileParagraph,
                inline => inline.Kind == MessageInlineKind.FileLink);
            Assert.Equal("MainWindow.xaml.cs", fileLink.Text);
            Assert.Equal(fixture.FilePath, fileLink.Target, ignoreCase: true);
            Assert.DoesNotContain(fileTarget, fileLink.Text);
            Assert.DoesNotContain(":801", fileLink.Text);
        }
        finally
        {
            System.IO.Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    [Fact]
    public void ParsesBareWebAddressWithoutTrailingPunctuation()
    {
        var inlines = MessageInlineParser.Parse(
            "Open http://example.com/docs.",
            workingDirectory: null);

        Assert.Collection(
            inlines,
            inline => Assert.Equal("Open ", inline.Text),
            inline =>
            {
                Assert.Equal(MessageInlineKind.WebLink, inline.Kind);
                Assert.Equal("http://example.com/docs", inline.Text);
            },
            inline => Assert.Equal(".", inline.Text));
    }

    [Theory]
    [InlineData(1155, true)]
    [InlineData(2, false)]
    [InlineData(5, false)]
    public void OpenWithFallbackOnlyHandlesMissingAssociation(int errorCode, bool expected)
    {
        Assert.Equal(expected, ReferencedFileActions.RequiresOpenWith(errorCode));
    }

    private static (string Directory, string FilePath) CreateFixture()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "VolturaAiWatcherTests",
            System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var filePath = System.IO.Path.Combine(directory, "screenshot.png");
        System.IO.File.WriteAllText(filePath, "test");
        return (directory, filePath);
    }
}
