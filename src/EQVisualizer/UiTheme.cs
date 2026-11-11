using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace EQVisualizer;

internal static class Ui
{
    private static readonly ConditionalWeakTable<Control, ColorRoles> colorRoles = new();

    public static bool Animate => SystemInformation.IsMenuAnimationEnabled && !SystemInformation.HighContrast;
    public static Icon CreateIcon()
    {
        using var stream = typeof(Ui).Assembly.GetManifestResourceStream("EQVisualizer.Assets.AppIcon.ico") ?? throw new InvalidOperationException("Icono integrado no disponible.");
        using var icon = new Icon(stream); return (Icon)icon.Clone();
    }
    public static Color Mix(Color a, Color b, float p) => Color.FromArgb((int)(a.R + (b.R - a.R) * p), (int)(a.G + (b.G - a.G) * p), (int)(a.B + (b.B - a.B) * p));
    public static Color Background { get; private set; }
    public static Color Sidebar { get; private set; }
    public static Color Surface { get; private set; }
    public static Color Elevated { get; private set; }
    public static Color Line { get; private set; }
    public static Color Text { get; private set; }
    public static Color Muted { get; private set; }
    public static Color Accent { get; private set; }
    public static Color AccentText => InterfacePalettes.TextOnAccent(Accent);
    static Ui() => ApplyTheme(InterfaceTheme.Carbon, 0);
    public static void ApplyTheme(InterfaceTheme theme, int customAccent)
    {
        var palette = InterfacePalettes.For(theme);
        Background = palette.Background; Sidebar = palette.Sidebar; Surface = palette.Surface;
        Elevated = palette.Elevated; Line = palette.Line; Text = palette.Text; Muted = palette.Muted;
        Accent = customAccent == 0 ? palette.Accent : InterfacePalettes.EnsureAccent(Color.FromArgb(customAccent), palette.Surface);
    }

    // A supplied selector replaces its previous role. Omitted selectors keep their existing roles.
    public static T BindColors<T>(T control, Func<Color>? foreground = null, Func<Color>? background = null) where T : Control
    {
        if (foreground is null && background is null) return control;

        var roles = colorRoles.GetValue(control, static _ => new ColorRoles());
        roles.Foreground = foreground ?? roles.Foreground;
        roles.Background = background ?? roles.Background;
        roles.Apply(control);
        return control;
    }

    // Bind controls initialized with static theme colors before changing the palette.
    public static void TrackCurrentThemeColors(Control root)
    {
        if (root.IsDisposed || root.Disposing) return;
        Func<Color>? fg = root.ForeColor == Text ? static () => Text
            : root.ForeColor == Muted ? static () => Muted
            : root.ForeColor == Accent ? static () => Accent
            : root.ForeColor == Line ? static () => Line : null;
        Func<Color>? bg = root.BackColor == Background ? static () => Background
            : root.BackColor == Sidebar ? static () => Sidebar
            : root.BackColor == Surface ? static () => Surface
            : root.BackColor == Elevated ? static () => Elevated : null;
        if (fg is not null || bg is not null) BindColors(root, fg, bg);
        foreach (Control child in root.Controls) TrackCurrentThemeColors(child);
    }

    public static void RefreshColors(Control root)
    {
        if (root.IsDisposed || root.Disposing) return;

        if (colorRoles.TryGetValue(root, out var roles))
            roles.Apply(root);

        if (root.HasChildren)
        {
            foreach (Control child in root.Controls)
                RefreshColors(child);
        }
        root.Invalidate();
    }

    private sealed class ColorRoles
    {
        public Func<Color>? Foreground { get; set; }
        public Func<Color>? Background { get; set; }

        public void Apply(Control control)
        {
            if (Foreground is { } foreground) control.ForeColor = foreground();
            if (Background is { } background) control.BackColor = background();
        }
    }

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
    private readonly UiButton? maximize;

    protected ModernForm(string caption, bool windowControls = false)
    {
        Text = caption; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
        Icon = Ui.CreateIcon();
        Ui.BindColors(this, static () => Ui.Text, static () => Ui.Background);
        Font = Ui.Font(); AutoScaleMode = AutoScaleMode.Dpi; ShowInTaskbar = false;
        Padding = new Padding(1); DoubleBuffered = true;
        var chrome = Ui.BindColors(new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(16, 0, 10, 0) },
            background: static () => Ui.Sidebar);
        var label = Ui.BindColors(new Label { Text = caption, Font = Ui.Font(9), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft },
            foreground: static () => Ui.Muted);
        var close = CaptionButton("×", "Cerrar");
        close.Click += (_, _) => Close();
        void Drag(object? sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } }
        chrome.MouseDown += Drag;
        label.MouseDown += Drag;
        chrome.Controls.Add(label);
        if (windowControls)
        {
            var minimize = CaptionButton("−", "Minimizar");
            minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
            maximize = CaptionButton("□", "Maximizar");
            maximize.Click += (_, _) => ToggleMaximized();
            chrome.Controls.Add(minimize);
            chrome.Controls.Add(maximize);
        }
        chrome.Controls.Add(close);
        Controls.Add(chrome);
        Chrome = chrome;
    }
    private static UiButton CaptionButton(string symbol, string action) =>
        new() { Text = symbol, Dock = DockStyle.Right, Width = 36, Height = 32, AccessibleName = action, Quiet = true };

    private void ToggleMaximized()
    {
        if (WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Normal;
            return;
        }
        MaximizedBounds = Screen.FromControl(this).WorkingArea;
        WindowState = FormWindowState.Maximized;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (maximize is null) return;
        bool isMaximized = WindowState == FormWindowState.Maximized;
        maximize.Text = isMaximized ? "❐" : "□";
        maximize.AccessibleName = isMaximized ? "Restaurar" : "Maximizar";
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
        if (m.Msg != 0x84 || (int)m.Result != 1 || MinimumSize == MaximumSize || WindowState == FormWindowState.Maximized) return;
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
        Color target = Primary ? Ui.Mix(Ui.Accent, InterfacePalettes.Contrast(Color.White, Ui.Background) > InterfacePalettes.Contrast(Color.Black, Ui.Background) ? Color.White : Color.Black, .1f)
            : Selected ? Ui.Mix(Ui.Surface, Ui.Accent, .16f) : Ui.Elevated;
        Color bg = Ui.Mix(resting, target, animation.Value);
        using (var fill = new SolidBrush(bg)) g.FillPath(fill, path);
        if (Focused) { using var focus = new Pen(Ui.Accent, 1); g.DrawPath(focus, path); }
        var color = Primary ? Ui.AccentText : Selected ? Ui.Accent : Ui.Text;
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
        DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat;
        Ui.BindColors(this, static () => Ui.Text, static () => Ui.Elevated);
        DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 28; Font = Ui.Font(); Cursor = Cursors.Hand;
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        using var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Ui.Mix(BackColor, Ui.Accent, .19f) : BackColor); e.Graphics.FillRectangle(brush, e.Bounds);
        string text = e.Index < 0 ? Text : Items[e.Index]?.ToString() ?? "";
        TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != 0xF || !IsHandleCreated) return;
        using var g = Graphics.FromHwnd(Handle); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var fill = new SolidBrush(BackColor); g.FillRectangle(fill, Width - 29, 0, 29, Height);
        using var edge = new Pen(BackColor, 3); g.DrawRectangle(edge, 1, 1, Width - 2, Height - 2);
        using var chevron = new Pen(Ui.Muted, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLines(chevron, [new PointF(Width - 19, Height / 2f - 2), new PointF(Width - 14, Height / 2f + 3), new PointF(Width - 9, Height / 2f - 2)]);
    }
}

internal sealed class UiSlider : UserControl
{
    private readonly TextBox input = Ui.BindColors(new TextBox { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Center },
        static () => Ui.Text, static () => Ui.Elevated);
    private readonly TrackBar track;
    private readonly decimal minimum, maximum, scale;
    private readonly int decimalPlaces;
    private decimal value;
    private string lastFormatted = "";
    private bool updatingTrack;
    public event EventHandler? ValueChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal Value
    {
        get { Commit(); return value; }
        set
        {
            decimal previous = this.value;
            this.value = Math.Clamp(value, minimum, maximum);
            updatingTrack = true;
            track.Value = (int)Math.Round(this.value * scale);
            updatingTrack = false;
            RefreshText();
            if (previous != this.value) ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public UiSlider(string label, decimal minimum, decimal maximum, decimal increment, decimal initialValue)
    {
        this.minimum = minimum;
        this.maximum = maximum;
        decimalPlaces = increment < 1 ? 2 : 0;
        scale = decimalPlaces == 0 ? 1 : 100;
        int smallChange = (int)(increment * scale);
        track = Ui.BindColors(new TrackBar
        {
            AccessibleName = label,
            AutoSize = false,
            TickStyle = TickStyle.None,
            Minimum = (int)(minimum * scale),
            Maximum = (int)(maximum * scale),
            SmallChange = smallChange,
            LargeChange = smallChange * 5,
            TabIndex = 0
        }, background: static () => Ui.Surface);
        track.ValueChanged += (_, _) =>
        {
            if (!updatingTrack) Value = track.Value / scale;
        };
        input.AccessibleName = $"Valor de {label}";
        input.TabIndex = 1;
        input.Validated += (_, _) => Commit();
        input.KeyDown += (_, e) =>
        {
            if (e.KeyCode is not (Keys.Up or Keys.Down)) return;
            Value += e.KeyCode == Keys.Up ? increment : -increment;
            e.SuppressKeyPress = true;
        };
        Height = 36;
        MinimumSize = new Size(120, 36);
        Ui.BindColors(this, background: static () => Ui.Surface);
        Font = Ui.Font();
        TabStop = false;
        Controls.AddRange([track, input]);
        Value = initialValue;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (track is null) return;
        int inputWidth = LogicalToDeviceUnits(56);
        int gap = LogicalToDeviceUnits(8);
        int inputHeight = input.PreferredHeight;
        track.SetBounds(0, 0, Math.Max(1, Width - inputWidth - gap), Height);
        input.SetBounds(Width - inputWidth, (Height - inputHeight) / 2, inputWidth, inputHeight);
    }

    private void Commit()
    {
        if (input.Text == lastFormatted) return;
        if (decimal.TryParse(input.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
            Value = parsed;
        else
            RefreshText();
    }

    private void RefreshText()
    {
        lastFormatted = value.ToString("F" + decimalPlaces, CultureInfo.CurrentCulture);
        input.Text = lastFormatted;
    }
}

internal sealed class UiCard : Panel
{
    public UiCard()
    {
        Ui.BindColors(this, background: static () => Ui.Surface);
        Padding = new Padding(18);
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Ui.Background); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Ui.Round(new RectangleF(0, 0, Width - 1, Height - 1), 14); using var fill = new SolidBrush(BackColor); e.Graphics.FillPath(fill, path);
        using var border = new Pen(Ui.Line); e.Graphics.DrawPath(border, path);
    }
}

internal sealed class UiMenuRenderer : ToolStripRenderer
{
    public static void ApplyTo(ToolStrip root) =>
        (root.Renderer as UiMenuRenderer ?? new UiMenuRenderer()).ApplyToStrip(root);

    private void ApplyToStrip(ToolStrip strip)
    {
        strip.BackColor = Ui.Surface;
        strip.ForeColor = Ui.Text;
        if (!ReferenceEquals(strip.Renderer, this))
            strip.Renderer = this;

        strip.ItemAdded -= MenuItemAdded;
        strip.ItemAdded += MenuItemAdded;
        if (strip is ToolStripDropDown dropDown)
        {
            dropDown.Opening -= MenuOpening;
            dropDown.Opening += MenuOpening;
        }
        foreach (ToolStripItem item in strip.Items)
            ApplyToItem(item);

        strip.Invalidate();
    }

    private void ApplyToItem(ToolStripItem item)
    {
        item.BackColor = Ui.Surface;
        item.ForeColor = Ui.Text;
        if (item is ToolStripDropDownItem branch)
        {
            branch.DropDownOpening -= SubmenuOpening;
            branch.DropDownOpening += SubmenuOpening;
            if (branch.HasDropDownItems)
                ApplyToStrip(branch.DropDown);
        }
    }

    private static void MenuItemAdded(object? sender, ToolStripItemEventArgs e)
    {
        if (sender is ToolStrip { Renderer: UiMenuRenderer renderer } && e.Item is { } item)
            renderer.ApplyToItem(item);
    }

    private static void MenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is ToolStrip { Renderer: UiMenuRenderer renderer } strip)
            renderer.ApplyToStrip(strip);
    }

    private static void SubmenuOpening(object? sender, EventArgs e)
    {
        // A branch may create and populate its drop-down for the first time during opening.
        if (sender is ToolStripDropDownItem { Owner.Renderer: UiMenuRenderer renderer } branch)
            renderer.ApplyToStrip(branch.DropDown);
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Ui.Surface);
        e.Graphics.FillRectangle(brush, e.ToolStrip.ClientRectangle);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var bounds = e.ToolStrip.ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var pen = new Pen(Ui.Line);
        e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Enabled || (!e.Item.Selected && !e.Item.Pressed)) return;

        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        bounds.Inflate(-(e.ToolStrip?.LogicalToDeviceUnits(2) ?? 2), 0);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var brush = new SolidBrush(Ui.Elevated);
        using var pen = new Pen(Ui.Line);
        e.Graphics.FillRectangle(brush, bounds);
        e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int inset = e.ToolStrip?.LogicalToDeviceUnits(4) ?? 4;
        using var pen = new Pen(Ui.Line);
        if (e.Vertical)
            e.Graphics.DrawLine(pen, e.Item.Width / 2, inset, e.Item.Width / 2, e.Item.Height - inset);
        else
            e.Graphics.DrawLine(pen, inset, e.Item.Height / 2, e.Item.Width - inset, e.Item.Height / 2);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == false ? Ui.Muted : Ui.Text;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Ui.Text : Ui.Muted;
        if (e.TextDirection == ToolStripTextDirection.Horizontal)
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, e.TextColor, e.TextFormat);
        else
            base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Item is not ToolStripMenuItem item) return;

        var bounds = e.ImageRectangle;
        float size = Math.Min(bounds.Width, bounds.Height);
        if (size <= 0) return;
        float x = bounds.Left + bounds.Width / 2f;
        float y = bounds.Top + bounds.Height / 2f;
        using var pen = new Pen(item.Enabled ? Ui.Text : Ui.Muted, Math.Max(1, size / 8))
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
        };
        if (item.CheckState == CheckState.Indeterminate)
            e.Graphics.DrawLine(pen, x - size * .25f, y, x + size * .25f, y);
        else
            e.Graphics.DrawLines(pen, [new PointF(x - size * .28f, y), new PointF(x - size * .08f, y + size * .2f),
                new PointF(x + size * .28f, y - size * .22f)]);
    }
}
