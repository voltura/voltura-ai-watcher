namespace VolturaAiWatcher;

internal enum MessageDocumentBlockKind
{
    Paragraph,
    Header1,
    Header2
}

internal sealed record MessageDocumentBlock(
    MessageDocumentBlockKind Kind,
    string Text,
    string? LinkText = null,
    string? FilePath = null);

internal enum MessageInlineKind
{
    Text,
    InlineCode,
    WebLink,
    FileLink
}

internal sealed record MessageInline(
    MessageInlineKind Kind,
    string Text,
    string? Target = null);

internal static class MessageInlineParser
{
    private static readonly System.Text.RegularExpressions.Regex InlinePattern = new(
        @"(?<markdown>\[(?<label>[^\]\r\n]+)\]\((?<target><[^>\r\n]+>|[^)\r\n]+)\))|(?<code>`(?<codeValue>[^`\r\n]+)`)|(?<url>https?://[^\s<>()`]+)",
        System.Text.RegularExpressions.RegexOptions.Compiled |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant |
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static System.Collections.Generic.IReadOnlyList<MessageInline> Parse(
        string text,
        string? workingDirectory)
    {
        var inlines = new System.Collections.Generic.List<MessageInline>();
        var position = 0;
        foreach (System.Text.RegularExpressions.Match match in InlinePattern.Matches(text))
        {
            if (match.Index > position)
            {
                inlines.Add(new MessageInline(
                    MessageInlineKind.Text,
                    text[position..match.Index]));
            }

            if (match.Groups["markdown"].Success)
            {
                AddMarkdownLink(inlines, match, workingDirectory);
            }
            else if (match.Groups["code"].Success)
            {
                AddInlineCode(inlines, match.Groups["codeValue"].Value, workingDirectory);
            }
            else
            {
                AddBareWebLink(inlines, match.Groups["url"].Value);
            }

            position = match.Index + match.Length;
        }

        if (position < text.Length)
        {
            inlines.Add(new MessageInline(MessageInlineKind.Text, text[position..]));
        }

        return inlines;
    }

    private static void AddMarkdownLink(
        System.Collections.Generic.ICollection<MessageInline> inlines,
        System.Text.RegularExpressions.Match match,
        string? workingDirectory)
    {
        var label = match.Groups["label"].Value;
        var target = match.Groups["target"].Value.Trim().Trim('<', '>');
        if (TryCreateWebTarget(target, out var webTarget))
        {
            inlines.Add(new MessageInline(MessageInlineKind.WebLink, label, webTarget));
            return;
        }

        var filePath = ReferencedFileResolver.ResolveExistingFile(target, workingDirectory);
        if (filePath is not null)
        {
            inlines.Add(new MessageInline(MessageInlineKind.FileLink, label, filePath));
            return;
        }

        inlines.Add(new MessageInline(MessageInlineKind.Text, match.Value));
    }

    private static void AddInlineCode(
        System.Collections.Generic.ICollection<MessageInline> inlines,
        string value,
        string? workingDirectory)
    {
        if (TryCreateWebTarget(value, out var webTarget))
        {
            inlines.Add(new MessageInline(MessageInlineKind.WebLink, value, webTarget));
            return;
        }

        var filePath = ReferencedFileResolver.ResolveExistingFile(value, workingDirectory);
        inlines.Add(filePath is null
            ? new MessageInline(MessageInlineKind.InlineCode, value)
            : new MessageInline(MessageInlineKind.FileLink, value, filePath));
    }

    private static void AddBareWebLink(
        System.Collections.Generic.ICollection<MessageInline> inlines,
        string value)
    {
        var target = value.TrimEnd('.', ',', ';', '!', '?');
        if (TryCreateWebTarget(target, out var webTarget))
        {
            inlines.Add(new MessageInline(MessageInlineKind.WebLink, target, webTarget));
            if (target.Length < value.Length)
            {
                inlines.Add(new MessageInline(MessageInlineKind.Text, value[target.Length..]));
            }

            return;
        }

        inlines.Add(new MessageInline(MessageInlineKind.Text, value));
    }

    private static bool TryCreateWebTarget(
        string value,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? target)
    {
        target = null;
        if (!System.Uri.TryCreate(value, System.UriKind.Absolute, out var uri) ||
            (uri.Scheme != System.Uri.UriSchemeHttp &&
             uri.Scheme != System.Uri.UriSchemeHttps))
        {
            return false;
        }

        target = uri.AbsoluteUri;
        return true;
    }
}

internal static class MessageDocumentParser
{
    private static readonly System.Text.RegularExpressions.Regex HeaderPattern = new(
        @"^(?<markers>#{1,2})\s+(?<text>.+?)\s*$",
        System.Text.RegularExpressions.RegexOptions.Compiled |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static System.Collections.Generic.IReadOnlyList<MessageDocumentBlock> Parse(
        string text,
        string? workingDirectory)
    {
        var normalized = RemoveOuterMarkdownQuote(text)
            .Replace("\r\n", "\n", System.StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var blocks = new System.Collections.Generic.List<MessageDocumentBlock>();
        var paragraphLines = new System.Collections.Generic.List<string>();

        void FlushParagraph()
        {
            if (paragraphLines.Count == 0)
            {
                return;
            }

            blocks.Add(new MessageDocumentBlock(
                MessageDocumentBlockKind.Paragraph,
                string.Join("\n", paragraphLines)));
            paragraphLines.Clear();
        }

        foreach (var line in lines)
        {
            var header = HeaderPattern.Match(line);
            if (header.Success)
            {
                FlushParagraph();
                var kind = header.Groups["markers"].Value.Length == 1
                    ? MessageDocumentBlockKind.Header1
                    : MessageDocumentBlockKind.Header2;
                var headerText = header.Groups["text"].Value;
                blocks.Add(CreateHeaderBlock(kind, headerText, workingDirectory));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                continue;
            }

            paragraphLines.Add(line);
        }

        FlushParagraph();
        return blocks;
    }

    private static string RemoveOuterMarkdownQuote(string text)
    {
        var trimmed = text.Trim();
        return trimmed.StartsWith("\"#", System.StringComparison.Ordinal) &&
               trimmed.EndsWith('"') &&
               trimmed.Length > 2
            ? trimmed[1..^1]
            : text;
    }

    private static MessageDocumentBlock CreateHeaderBlock(
        MessageDocumentBlockKind kind,
        string headerText,
        string? workingDirectory)
    {
        if (kind != MessageDocumentBlockKind.Header2)
        {
            return new MessageDocumentBlock(kind, headerText);
        }

        var separatorIndex = headerText.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= headerText.Length - 1)
        {
            return new MessageDocumentBlock(kind, headerText);
        }

        var label = headerText[..separatorIndex].Trim();
        var reference = headerText[(separatorIndex + 1)..].Trim();
        var filePath = ReferencedFileResolver.ResolveExistingFile(reference, workingDirectory);
        return filePath is null
            ? new MessageDocumentBlock(kind, headerText)
            : new MessageDocumentBlock(kind, headerText, label, filePath);
    }
}

internal static class MessageDocumentBuilder
{
    public static System.Windows.Documents.FlowDocument Build(
        string text,
        string? workingDirectory)
    {
        var document = new System.Windows.Documents.FlowDocument
        {
            PagePadding = new System.Windows.Thickness(0),
            FontFamily = new System.Windows.Media.FontFamily("Bahnschrift SemiCondensed"),
            FontSize = 13,
            Foreground = CreateBrush("#D2F4DA"),
            LineHeight = 19
        };

        foreach (var block in MessageDocumentParser.Parse(text, workingDirectory))
        {
            document.Blocks.Add(BuildParagraph(block, workingDirectory));
        }

        return document;
    }

    private static System.Windows.Documents.Paragraph BuildParagraph(
        MessageDocumentBlock block,
        string? workingDirectory)
    {
        var paragraph = new System.Windows.Documents.Paragraph
        {
            Margin = block.Kind switch
            {
                MessageDocumentBlockKind.Header1 => new System.Windows.Thickness(0, 2, 0, 12),
                MessageDocumentBlockKind.Header2 => new System.Windows.Thickness(0, 8, 0, 6),
                _ => new System.Windows.Thickness(0, 0, 0, 10)
            },
            FontSize = block.Kind switch
            {
                MessageDocumentBlockKind.Header1 => 20,
                MessageDocumentBlockKind.Header2 => 16,
                _ => 13
            },
            FontWeight = block.Kind == MessageDocumentBlockKind.Paragraph
                ? System.Windows.FontWeights.Normal
                : System.Windows.FontWeights.Bold,
            Foreground = block.Kind switch
            {
                MessageDocumentBlockKind.Header1 => CreateBrush("#7CFF9A"),
                MessageDocumentBlockKind.Header2 => CreateBrush("#A8FFB9"),
                _ => CreateBrush("#D2F4DA")
            }
        };

        if (block.FilePath is not null && block.LinkText is not null)
        {
            var link = new System.Windows.Documents.Hyperlink(
                new System.Windows.Documents.Run(block.LinkText))
            {
                Foreground = CreateBrush("#7CFFCA"),
                TextDecorations = System.Windows.TextDecorations.Underline,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = ReferencedFileToolTipFormatter.FormatDirectOpen(block.FilePath),
                Tag = block.FilePath
            };
            link.Click += (_, _) => ReferencedFileActions.OpenPath(block.FilePath);
            paragraph.Inlines.Add(link);
        }
        else
        {
            AddTextWithLineBreaks(paragraph, block.Text, workingDirectory);
        }

        return paragraph;
    }

    private static void AddTextWithLineBreaks(
        System.Windows.Documents.Paragraph paragraph,
        string text,
        string? workingDirectory)
    {
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                paragraph.Inlines.Add(new System.Windows.Documents.LineBreak());
            }

            AddInlines(paragraph, lines[index], workingDirectory);
        }
    }

    private static void AddInlines(
        System.Windows.Documents.Paragraph paragraph,
        string text,
        string? workingDirectory)
    {
        foreach (var inline in MessageInlineParser.Parse(text, workingDirectory))
        {
            switch (inline.Kind)
            {
                case MessageInlineKind.InlineCode:
                    paragraph.Inlines.Add(new System.Windows.Documents.Run(inline.Text)
                    {
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        Foreground = CreateBrush("#B8EFC3")
                    });
                    break;
                case MessageInlineKind.WebLink:
                case MessageInlineKind.FileLink:
                    paragraph.Inlines.Add(CreateHyperlink(inline));
                    break;
                default:
                    paragraph.Inlines.Add(new System.Windows.Documents.Run(inline.Text));
                    break;
            }
        }
    }

    private static System.Windows.Documents.Hyperlink CreateHyperlink(MessageInline inline)
    {
        var link = new System.Windows.Documents.Hyperlink(
            new System.Windows.Documents.Run(inline.Text))
        {
            Foreground = CreateBrush("#7CFFCA"),
            TextDecorations = System.Windows.TextDecorations.Underline,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = inline.Kind == MessageInlineKind.FileLink
                ? ReferencedFileToolTipFormatter.FormatDirectOpen(inline.Target!)
                : inline.Target
        };
        link.Click += (_, args) =>
        {
            args.Handled = true;
            if (inline.Kind == MessageInlineKind.FileLink)
            {
                ReferencedFileActions.OpenPath(inline.Target);
            }
            else
            {
                ReferencedFileActions.OpenWebLink(inline.Target);
            }
        };
        return link;
    }

    private static System.Windows.Media.Brush CreateBrush(string color) =>
        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color)!;
}
