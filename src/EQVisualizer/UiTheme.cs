using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
namespace EQVisualizer;

internal static class Ui
{
    public static bool Animate => SystemInformation.IsMenuAnimationEnabled && !SystemInformation.HighContrast;
    public static Icon CreateIcon()
    {
        using var stream = typeof(Ui).Assembly.GetManifestResourceStream("EQVisualizer.Assets.AppIcon.ico") ?? throw new InvalidOperationException("Icono integrado no disponible.");
        using var icon = new Icon(stream); return (Icon)icon.Clone();
    }
    public static Color Mix(Color a, Color b, float p) => Color.FromArgb((int)(a.R + (b.R - a.R) * p), (int)(a.G + (b.G - a.G) * p), (int)(a.B + (b.B - a.B) * p));
    public static Color Accent { get; set; } = Color.FromArgb(110, 231, 207);
    public static readonly Color Background = Color.FromArgb(12, 16, 23), Sidebar = Color.FromArgb(16, 21, 30),
        Surface = Color.FromArgb(22, 29, 40), Elevated = Color.FromArgb(31, 41, 56), Line = Color.FromArgb(43, 56, 73),
        Text = Color.FromArgb(238, 244, 250), Muted = Color.FromArgb(145, 160, 180);
    public static Font Font(float size = 10, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);
    public static GraphicsPath Round(RectangleF r, float radius = 12)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height)); var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
    public static void Mark(Graphics g, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(Elevated); using var shape = Round(bounds, 10); g.FillPath(bg, shape);
        using var pen = new Pen(Accent, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float[] heights = [.35f, .7f, 1, .55f, .8f];
        for (int i = 0; i < 5; i++) { float x = bounds.X + bounds.Width * (.22f + i * .14f); float h = bounds.Height * .48f * heights[i]; g.DrawLine(pen, x, bounds.Y + bounds.Height / 2 - h / 2, x, bounds.Y + bounds.Height / 2 + h / 2); }
    }
}

internal class ModernForm : Form
{
    protected ModernForm(string caption)
    {
        Text = caption; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
        Icon = Ui.CreateIcon();
        BackColor = Ui.Background; ForeColor = Ui.Text; Font = Ui.Font(); AutoScaleMode = AutoScaleMode.Dpi; ShowInTaskbar = false;
        Padding = new Padding(1); DoubleBuffered = true;
        var chrome = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Ui.Sidebar, Padding = new Padding(16, 0, 10, 0) };
        var label = new Label { Text = caption, ForeColor = Ui.Muted, Font = Ui.Font(9), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        var close = new UiButton { Text = "×", Dock = DockStyle.Right, Width = 36, Height = 32, AccessibleName = "Cerrar", Quiet = true };
        close.Click += (_, _) => Close();
        void Drag(object? sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } }
        chrome.MouseDown += Drag; label.MouseDown += Drag; chrome.Controls.Add(label); chrome.Controls.Add(close); Controls.Add(chrome);
        Chrome = chrome;
    }
    protected Panel Chrome { get; }
    protected double WindowOpacity { get; set; } = 1;
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using var pen = new Pen(Ui.Line); e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1); }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e); var area = Screen.FromControl(this).WorkingArea;
        if (Width > area.Width || Height > area.Height) { MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height)); Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height)); Location = new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2); }
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) { int corner = 2; DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int)); }
        if (!Ui.Animate) { Opacity = WindowOpacity; return; }
        Opacity = Math.Max(.8, WindowOpacity - .14);
        var fade = new UiTween(() => Opacity = Math.Max(.8, WindowOpacity - .14) + (WindowOpacity - Math.Max(.8, WindowOpacity - .14)) * entrance!.Value);
        entrance = fade; fade.Move(1, true);
    }
    private UiTween? entrance;
    protected override void Dispose(bool disposing) { if (disposing) { entrance?.Dispose(); Icon?.Dispose(); } base.Dispose(disposing); }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != 0x84 || (int)m.Result != 1 || MinimumSize == MaximumSize) return;
        var p = PointToClient(new Point(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16))));
        int edge = Math.Max(5, DeviceDpi / 24); bool left = p.X < edge, right = p.X >= Width - edge, top = p.Y < edge, bottom = p.Y >= Height - edge;
        m.Result = (IntPtr)(top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 1);
    }
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attribute, ref int value, int size);
}

internal sealed class UiButton : Button
{
    private bool hover;
    private bool selected;
    private readonly UiTween animation;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Quiet { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected { get => selected; set { selected = value; animation.Move(selected || hover ? 1 : 0, Visible); Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Navigation { get; set; }
    public UiButton() { animation = new UiTween(Invalidate); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Height = 40; Width = 120; Cursor = Cursors.Hand; Font = Ui.Font(10, FontStyle.Bold); SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; animation.Move(1, Visible); Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; animation.Move(selected ? 1 : 0, Visible); Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Parent?.BackColor ?? Ui.Background);
        var rect = new RectangleF(1, 1, Width - 2, Height - 2); using var path = Ui.Round(rect, 8);
        Color resting = Primary ? Ui.Accent : Quiet ? Parent?.BackColor ?? Ui.Sidebar : Ui.Surface;
        Color target = Primary ? Ui.Mix(Ui.Accent, Color.White, .25f) : Selected ? Ui.Mix(Ui.Surface, Ui.Accent, .16f) : Ui.Elevated;
        Color bg = Ui.Mix(resting, target, animation.Value);
        using (var fill = new SolidBrush(bg)) g.FillPath(fill, path);
        if (Focused) { using var focus = new Pen(Ui.Accent, 1); g.DrawPath(focus, path); }
        var color = Primary ? Ui.Background : Selected ? Ui.Accent : Ui.Text;
        var bounds = new Rectangle(Navigation ? 18 : 4, 0, Width - (Navigation ? 30 : 8), Height);
        TextRenderer.DrawText(g, Text, Font, bounds, color, (Navigation ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
}

internal sealed class UiToggle : CheckBox
{
    private readonly UiTween animation;
    public UiToggle() { animation = new UiTween(Invalidate); AutoSize = false; Height = 34; Width = 250; Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(BackColor);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width - 56, Height), Ui.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        var track = new RectangleF(Width - 46, (Height - 24) / 2, 44, 24); using var shape = Ui.Round(track, 12);
        using (var fill = new SolidBrush(Ui.Mix(Ui.Line, Ui.Accent, animation.Value))) g.FillPath(fill, shape);
        using (var knob = new SolidBrush(Ui.Mix(Ui.Text, Ui.Background, animation.Value))) g.FillEllipse(knob, track.X + 3 + 20 * animation.Value, track.Y + 3, 18, 18);
        if (Focused) { using var pen = new Pen(Ui.Accent); g.DrawPath(pen, shape); }
    }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); animation?.Move(Checked ? 1 : 0, Visible); Invalidate(); }
    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
}

// Timers run only during a short UI transition, never as a permanent render loop.
internal sealed class UiTween : IDisposable
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Action changed;
    private readonly Stopwatch clock = new();
    private float start, target;
    public float Value { get; private set; }
    public UiTween(Action changed)
    {
        this.changed = changed;
        timer.Tick += (_, _) =>
        {
            float t = Math.Clamp((float)clock.Elapsed.TotalMilliseconds / 160, 0, 1);
            Value = start + (target - start) * (1 - MathF.Pow(1 - t, 3)); changed();
            if (t >= 1) timer.Stop();
        };
    }
    public void Move(float value, bool animate)
    {
        timer.Stop(); start = Value; target = value;
        if (!animate || !Ui.Animate) { Value = value; changed(); return; }
        clock.Restart(); timer.Start();
    }
    public void Reset(float value) { timer.Stop(); Value = start = target = value; changed(); }
    public void Dispose() => timer.Dispose();
}

internal sealed class UiChip : CheckBox
{
    public UiChip() { Appearance = Appearance.Button; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; AutoSize = false; Width = 66; Height = 32; TextAlign = ContentAlignment.MiddleCenter; Cursor = Cursors.Hand; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Parent?.BackColor ?? Ui.Surface);
        using var path = Ui.Round(new RectangleF(1, 1, Width - 2, Height - 2), 6); using var brush = new SolidBrush(Checked ? Ui.Mix(Ui.Elevated, Ui.Accent, .18f) : Ui.Elevated); g.FillPath(brush, path);
        if (Focused) { using var pen = new Pen(Ui.Accent); g.DrawPath(pen, path); }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Checked ? Ui.Accent : Ui.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
}

internal sealed class UiSelect : ComboBox
{
    public UiSelect()
    {
        DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat; BackColor = Ui.Elevated; ForeColor = Ui.Text;
        DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 28; Font = Ui.Font(); Cursor = Cursors.Hand;
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        using var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Ui.Mix(Ui.Elevated, Ui.Accent, .19f) : Ui.Elevated); e.Graphics.FillRectangle(brush, e.Bounds);
        string text = e.Index < 0 ? Text : Items[e.Index]?.ToString() ?? "";
        TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height), Ui.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != 0xF || !IsHandleCreated) return;
        using var g = Graphics.FromHwnd(Handle); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var fill = new SolidBrush(Ui.Elevated); g.FillRectangle(fill, Width - 29, 0, 29, Height);
        using var edge = new Pen(Ui.Elevated, 3); g.DrawRectangle(edge, 1, 1, Width - 2, Height - 2);
        using var chevron = new Pen(Ui.Muted, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLines(chevron, [new PointF(Width - 19, Height / 2f - 2), new PointF(Width - 14, Height / 2f + 3), new PointF(Width - 9, Height / 2f - 2)]);
    }
}

internal sealed class UiNumber : UserControl
{
    private readonly TextBox input = new() { BorderStyle = BorderStyle.None, BackColor = Ui.Elevated, ForeColor = Ui.Text, TextAlign = HorizontalAlignment.Center };
    private decimal value;
    private string lastFormatted = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal Minimum { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal Maximum { get; set; } = 100;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal Increment { get; set; } = 1;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int DecimalPlaces { get; set; }
    public event EventHandler? ValueChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal Value { get { Commit(); return value; } set { var previous = this.value; this.value = Math.Clamp(value, Minimum, Maximum); RefreshText(); if (previous != this.value) ValueChanged?.Invoke(this, EventArgs.Empty); } }
    public UiNumber()
    {
        Height = 36; MinimumSize = new Size(120, 36); BackColor = Ui.Elevated; Font = Ui.Font(); TabStop = false;
        var minus = new UiButton { Text = "−", Dock = DockStyle.Left, Width = 36, AccessibleName = "Reducir" };
        var plus = new UiButton { Text = "+", Dock = DockStyle.Right, Width = 36, AccessibleName = "Aumentar" };
        minus.Click += (_, _) => Value = Value - Increment; plus.Click += (_, _) => Value = Value + Increment;
        input.Validated += (_, _) => Commit();
        input.KeyDown += (_, e) => { if (e.KeyCode == Keys.Up) { Value = Value + Increment; e.Handled = true; } if (e.KeyCode == Keys.Down) { Value = Value - Increment; e.Handled = true; } };
        Controls.Add(input); Controls.Add(minus); Controls.Add(plus); RefreshText();
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); input.SetBounds(40, (Height - input.PreferredHeight) / 2, Math.Max(20, Width - 80), input.PreferredHeight); }
    private void Commit() { var previous = value; if (input.Text != lastFormatted && decimal.TryParse(input.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed)) value = Math.Clamp(parsed, Minimum, Maximum); RefreshText(); if (previous != value) ValueChanged?.Invoke(this, EventArgs.Empty); }
    private void RefreshText() { lastFormatted = value.ToString("F" + DecimalPlaces, CultureInfo.CurrentCulture); input.Text = lastFormatted; }
}

internal sealed class UiCard : Panel
{
    public UiCard() { BackColor = Ui.Surface; Padding = new Padding(18); SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Ui.Background); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Ui.Round(new RectangleF(0, 0, Width - 1, Height - 1), 14); using var fill = new SolidBrush(Ui.Surface); e.Graphics.FillPath(fill, path);
        using var border = new Pen(Ui.Line); e.Graphics.DrawPath(border, path);
    }
}

internal sealed class UiMenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Ui.Surface;
    public override Color ImageMarginGradientBegin => Ui.Surface;
    public override Color ImageMarginGradientMiddle => Ui.Surface;
    public override Color ImageMarginGradientEnd => Ui.Surface;
    public override Color MenuItemSelected => Ui.Elevated;
    public override Color MenuItemSelectedGradientBegin => Ui.Elevated;
    public override Color MenuItemSelectedGradientEnd => Ui.Elevated;
    public override Color MenuItemBorder => Ui.Line;
    public override Color MenuBorder => Ui.Line;
    public override Color SeparatorDark => Ui.Line;
}
internal sealed class UiMenuRenderer : ToolStripProfessionalRenderer
{
    public UiMenuRenderer() : base(new UiMenuColors()) { RoundedEdges = true; }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Ui.Muted; base.OnRenderArrow(e); }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? Ui.Text : Ui.Muted; base.OnRenderItemText(e); }
}
