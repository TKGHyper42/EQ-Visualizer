using System.Drawing.Drawing2D;

namespace EQVisualizer;

internal sealed class ThemeSample : Control
{
    private InterfacePalette palette = InterfacePalettes.For(InterfaceTheme.Carbon);
    private Color accent = InterfacePalettes.For(InterfaceTheme.Carbon).Accent;
    public ThemeSample()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        AccessibleName = "Muestra de los colores del tema seleccionado";
    }
    public void SetPalette(InterfaceTheme theme, Color selectedAccent)
    {
        palette = InterfacePalettes.For(theme);
        accent = InterfacePalettes.EnsureAccent(selectedAccent, palette.Surface);
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Ui.Surface);
        if (Width < 80 || Height < 60) return;
        using (var frame = Ui.Round(new RectangleF(1, 1, Width - 2, Height - 2), 12))
        using (var fill = new SolidBrush(palette.Background))
        using (var line = new Pen(palette.Line)) { g.FillPath(fill, frame); g.DrawPath(line, frame); }
        using (var side = new SolidBrush(palette.Sidebar)) g.FillRectangle(side, 10, 12, 40, Height - 24);
        using (var surface = new SolidBrush(palette.Surface))
        using (var card = Ui.Round(new RectangleF(60, 12, Width - 72, Height - 24), 8)) g.FillPath(surface, card);
        TextRenderer.DrawText(g, "Tu música, tu estilo", Ui.Font(10, FontStyle.Bold),
            new Rectangle(74, 22, Math.Max(10, Width - 230), 24), palette.Text,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, "Portada y visualizador", Ui.Font(8.5f),
            new Rectangle(74, 47, Math.Max(10, Width - 230), 22), palette.Muted,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        using (var button = Ui.Round(new RectangleF(Width - 142, 24, 116, 37), 8))
        using (var brush = new SolidBrush(accent)) g.FillPath(brush, button);
        TextRenderer.DrawText(g, "Aplicar", Ui.Font(9, FontStyle.Bold),
            new Rectangle(Width - 142, 24, 116, 37), InterfacePalettes.TextOnAccent(accent),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
