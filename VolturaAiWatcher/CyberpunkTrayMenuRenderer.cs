namespace VolturaAiWatcher;

public sealed class CyberpunkTrayMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
{
    private static readonly System.Drawing.Color Background = System.Drawing.Color.FromArgb(8, 15, 11);
    private static readonly System.Drawing.Color Selection = System.Drawing.Color.FromArgb(20, 43, 27);
    private static readonly System.Drawing.Color Border = System.Drawing.Color.FromArgb(82, 196, 109);
    private static readonly System.Drawing.Color Accent = System.Drawing.Color.FromArgb(124, 255, 154);

    public CyberpunkTrayMenuRenderer() : base(new CyberpunkColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(System.Windows.Forms.ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(Background);
    }

    protected override void OnRenderToolStripBorder(System.Windows.Forms.ToolStripRenderEventArgs e)
    {
        using var pen = new System.Drawing.Pen(Border);
        var bounds = new System.Drawing.Rectangle(
            e.AffectedBounds.X,
            e.AffectedBounds.Y,
            System.Math.Max(0, e.AffectedBounds.Width - 1),
            System.Math.Max(0, e.AffectedBounds.Height - 1));
        e.Graphics.DrawRectangle(pen, bounds);
    }

    protected override void OnRenderMenuItemBackground(System.Windows.Forms.ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            using var brush = new System.Drawing.SolidBrush(Selection);
            e.Graphics.FillRectangle(brush, new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size));
        }
    }

    protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Accent : System.Drawing.Color.FromArgb(91, 123, 99);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(System.Windows.Forms.ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(52, 103, 65));
        e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
    }

    protected override void OnRenderItemCheck(System.Windows.Forms.ToolStripItemImageRenderEventArgs e)
    {
        using var pen = new System.Drawing.Pen(Accent, 1.7f);
        e.Graphics.DrawLines(
            pen,
            [
                new System.Drawing.Point(e.ImageRectangle.Left + 2, e.ImageRectangle.Top + 7),
                new System.Drawing.Point(e.ImageRectangle.Left + 6, e.ImageRectangle.Top + 11),
                new System.Drawing.Point(e.ImageRectangle.Left + 13, e.ImageRectangle.Top + 3)
            ]);
    }

    protected override void OnRenderArrow(System.Windows.Forms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item is null || e.Item.Enabled
            ? Accent
            : System.Drawing.Color.FromArgb(91, 123, 99);
        base.OnRenderArrow(e);
    }

    private sealed class CyberpunkColorTable : System.Windows.Forms.ProfessionalColorTable
    {
        public override System.Drawing.Color MenuBorder => Border;
        public override System.Drawing.Color MenuItemBorder => Border;
        public override System.Drawing.Color MenuItemSelected => Selection;
        public override System.Drawing.Color ToolStripDropDownBackground => Background;
        public override System.Drawing.Color ImageMarginGradientBegin => Background;
        public override System.Drawing.Color ImageMarginGradientMiddle => Background;
        public override System.Drawing.Color ImageMarginGradientEnd => Background;
        public override System.Drawing.Color SeparatorDark => System.Drawing.Color.FromArgb(52, 103, 65);
        public override System.Drawing.Color SeparatorLight => System.Drawing.Color.FromArgb(52, 103, 65);
    }
}
