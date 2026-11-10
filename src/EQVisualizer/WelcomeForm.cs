namespace EQVisualizer;

internal sealed class WelcomeForm : ModernForm
{
    private readonly Settings settings;
    private readonly RadioButton cover = new WelcomeChoice("Solo círculo", "El ritmo alrededor de la portada.", 0) { Checked = true };
    private readonly RadioButton bottom = new WelcomeChoice("Solo franja inferior", "El ritmo sobre la barra de tareas.", 1);
    private readonly RadioButton both = new WelcomeChoice("Círculo y franja", "Los dos visualizadores a la vez.", 2);

    public WelcomeForm(Settings settings) : base("EQ Visualizer / Bienvenida")
    {
        this.settings = settings; ClientSize = new Size(800, 530); MinimumSize = MaximumSize = Size;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(34, 24, 34, 26), ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new(SizeType.Absolute, 36)); layout.RowStyles.Add(new(SizeType.Absolute, 65));
        layout.RowStyles.Add(new(SizeType.Absolute, 48)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 64));
        layout.Controls.Add(new Label { Text = "BIENVENIDO A EQ VISUALIZER", Font = Ui.Font(9, FontStyle.Bold), ForeColor = Ui.Accent, AutoSize = true }, 0, 0);
        layout.Controls.Add(new Label { Text = "Tu música merece su espacio.", Font = Ui.Font(26, FontStyle.Bold), ForeColor = Ui.Text, AutoSize = true }, 0, 1);
        layout.Controls.Add(new Label { Text = "Elige cómo acompañará cada canción a tu escritorio.", Font = Ui.Font(11), ForeColor = Ui.Muted, AutoSize = true }, 0, 2);
        var choices = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        choices.Controls.AddRange([cover, bottom, both]); layout.Controls.Add(choices, 0, 3);
        var footer = new Panel { Dock = DockStyle.Fill };
        var start = new UiButton { Text = "Empezar  →", Primary = true, Dock = DockStyle.Right, Width = 155, DialogResult = DialogResult.OK };
        footer.Controls.Add(new Label { Text = "Puedes cambiarlo cuando quieras en Ajustes.", ForeColor = Ui.Muted, Font = Ui.Font(9), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });
        footer.Controls.Add(start); layout.Controls.Add(footer, 0, 4);
        Controls.Add(layout); Chrome.SendToBack(); AcceptButton = start;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
            settings.VisualizerMode = both.Checked ? VisualizerMode.Both : bottom.Checked ? VisualizerMode.Bottom : VisualizerMode.Circle;
        base.OnFormClosing(e);
    }
}


internal sealed class WelcomeChoice(string title, string description, int mode) : RadioButton
{
    protected override Size DefaultSize => new(232, 178);
    protected override void OnCreateControl() { base.OnCreateControl(); Text = title; AutoSize = false; Size = DefaultSize; Margin = new Padding(0, 0, 12, 0); Cursor = Cursors.Hand; AccessibleName = title; }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.Clear(Parent?.BackColor ?? Ui.Background);
        using var path = Ui.Round(new RectangleF(1, 1, Width - 2, Height - 2), 14); using var bg = new SolidBrush(Checked ? Color.FromArgb(22, 45, 48) : Ui.Surface); g.FillPath(bg, path);
        using var border = new Pen(Checked || Focused ? Ui.Accent : Ui.Line, Checked ? 1.5f : 1); g.DrawPath(border, path);
        using var ink = new SolidBrush(Ui.Accent); g.FillRectangle(ink, Width / 2 - 20, 25, 40, 40);
        using var pen = new Pen(Color.FromArgb(150, Ui.Accent), 1.6f);
        if (mode is 0 or 2) g.DrawEllipse(pen, Width / 2 - 31, 14, 62, 62);
        if (mode is 1 or 2) for (int i = 0; i < 15; i++) { float h = 5 + MathF.Sin(i * .6f) * 4; g.DrawLine(pen, Width / 2 - 45 + i * 6, 90, Width / 2 - 45 + i * 6, 90 - h); }
        using var titleFont = Ui.Font(10, FontStyle.Bold); using var descriptionFont = Ui.Font(8.5f);
        TextRenderer.DrawText(g, title, titleFont, new Rectangle(14, 108, Width - 28, 24), Ui.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, description, descriptionFont, new Rectangle(12, 140, Width - 24, 24), Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
    }
}

