namespace VolturaAiWatcher;

public sealed class CompactMessagePreview : System.Windows.Controls.TextBlock
{
    public static readonly System.Windows.DependencyProperty MessageTextProperty =
        System.Windows.DependencyProperty.Register(
            nameof(MessageText),
            typeof(string),
            typeof(CompactMessagePreview),
            new System.Windows.PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly System.Windows.DependencyProperty WorkingDirectoryProperty =
        System.Windows.DependencyProperty.Register(
            nameof(WorkingDirectory),
            typeof(string),
            typeof(CompactMessagePreview),
            new System.Windows.PropertyMetadata(null, OnContentChanged));

    public string MessageText
    {
        get => (string)GetValue(MessageTextProperty);
        set => SetValue(MessageTextProperty, value);
    }

    public string? WorkingDirectory
    {
        get => (string?)GetValue(WorkingDirectoryProperty);
        set => SetValue(WorkingDirectoryProperty, value);
    }

    private static void OnContentChanged(
        System.Windows.DependencyObject dependencyObject,
        System.Windows.DependencyPropertyChangedEventArgs args)
    {
        ((CompactMessagePreview)dependencyObject).RebuildInlines();
    }

    private void RebuildInlines()
    {
        Inlines.Clear();
        var blocks = MessageDocumentParser.Parse(MessageText ?? string.Empty, WorkingDirectory);
        for (var index = 0; index < blocks.Count; index++)
        {
            if (index > 0)
            {
                Inlines.Add(new System.Windows.Documents.LineBreak());
            }

            AddBlock(blocks[index]);
        }
    }

    private void AddBlock(MessageDocumentBlock block)
    {
        if (block.FilePath is not null && block.LinkText is not null)
        {
            var fileName = new System.Windows.Documents.Run(block.LinkText)
            {
                Foreground = CreateBrush("#7CFFCA"),
                FontSize = 11.5,
                FontWeight = System.Windows.FontWeights.SemiBold
            };
            Inlines.Add(fileName);
            return;
        }

        var inlineContent = MessageInlineParser.Parse(block.Text, WorkingDirectory);
        var runs = new System.Collections.Generic.List<System.Windows.Documents.Run>();
        foreach (var inline in inlineContent)
        {
            var run = new System.Windows.Documents.Run(inline.Text);
            if (inline.Kind is
                MessageInlineKind.InlineCode or
                MessageInlineKind.WebLink or
                MessageInlineKind.FileLink)
            {
                run.Foreground = CreateBrush("#7CFFCA");
                run.FontWeight = System.Windows.FontWeights.SemiBold;
            }

            runs.Add(run);
        }

        foreach (var run in runs)
        {
            switch (block.Kind)
            {
                case MessageDocumentBlockKind.Header1:
                    run.FontSize = 12.5;
                    run.FontWeight = System.Windows.FontWeights.Bold;
                    run.Foreground = CreateBrush("#7CFF9A");
                    break;
                case MessageDocumentBlockKind.Header2:
                    run.FontSize = 11.5;
                    run.FontWeight = System.Windows.FontWeights.SemiBold;
                    run.Foreground = CreateBrush("#A8FFB9");
                    break;
            }

            Inlines.Add(run);
        }
    }

    private static System.Windows.Media.Brush CreateBrush(string color) =>
        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color)!;
}
