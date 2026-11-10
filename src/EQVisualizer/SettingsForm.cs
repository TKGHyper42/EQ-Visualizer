namespace EQVisualizer;

internal sealed class SettingsForm : ModernForm
{
    private readonly Settings settings;
    private readonly Dictionary<string, UiNumber> numbers = [];
    private readonly CheckBox capture = new UiToggle() { Text = "Analizar la salida de audio", AutoSize = false };
    private readonly CheckBox tunnel = new UiToggle() { Text = "Túnel de portadas", AutoSize = false };
    private readonly CheckBox parallax = new UiToggle() { Text = "Seguir suavemente el cursor", AutoSize = false };
    private readonly CheckBox ctrl = new UiChip() { Text = "Ctrl", AutoSize = false }, alt = new UiChip() { Text = "Alt", AutoSize = false }, shift = new UiChip() { Text = "Shift", AutoSize = false };
    private readonly UiSelect mode = Choices("Alrededor de la portada", "Franja inferior", "Ambos");
    private readonly UiSelect contrast = Choices("Adaptativo por iconos", "Oscurecer imagen completa", "Desactivado");
    private readonly UiSelect toggleKey = KeysBox(), effectsKey = KeysBox();

    public SettingsForm(Settings settings) : base("EQ Visualizer / Ajustes")
    {
        this.settings = settings;
        ClientSize = new Size(940, 660); MinimumSize = new Size(820, 600);
        var body = new Panel { Dock = DockStyle.Fill };
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 210, BackColor = Ui.Sidebar, Padding = new Padding(18, 26, 18, 20) };
        var brand = new Panel { Dock = DockStyle.Top, Height = 84 };
        brand.Paint += (_, e) => Ui.Mark(e.Graphics, new Rectangle(0, 0, 40, 40));
        brand.Controls.Add(new Label { Text = "EQ Visualizer", Font = Ui.Font(14, FontStyle.Bold), ForeColor = Ui.Text, Location = new Point(50, 2), Size = new Size(145, 28) });
        brand.Controls.Add(new Label { Text = "TU MÚSICA, A TU MANERA", Font = Ui.Font(7.5f), ForeColor = Ui.Muted, Location = new Point(0, 54), Size = new Size(180, 22) });
        navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var note = new Label { Text = "HECHO PARA TU ESCRITORIO\n\nMúsica. Portadas. Movimiento.", Font = Ui.Font(8.5f), ForeColor = Ui.Muted, Dock = DockStyle.Bottom, Height = 66 };
        sidebar.Controls.Add(navigation); sidebar.Controls.Add(note); sidebar.Controls.Add(brand);
        contentHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 24, 28, 12), BackColor = Ui.Background };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 74, Padding = new Padding(28, 14, 28, 16), BackColor = Ui.Background };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 270, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = new UiButton { Text = "Guardar cambios", DialogResult = DialogResult.OK, Primary = true, Width = 148, Margin = new Padding(8, 0, 0, 0) };
        var cancel = new UiButton { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 100, Quiet = true, Margin = new Padding(0) };
        buttons.Controls.AddRange([save, cancel]);
        footer.Controls.Add(new Label { Text = "Los cambios se aplican al guardar", ForeColor = Ui.Muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Ui.Font(8.5f) });
        footer.Controls.Add(buttons);
        body.Controls.Add(contentHost); body.Controls.Add(footer); body.Controls.Add(sidebar); Controls.Add(body); Chrome.SendToBack();
        pageMotion = new UiTween(() =>
        {
            if (slidingPage is null || slidingPage.IsDisposed) return;
            int offset = (int)(14 * (1 - pageMotion!.Value));
            slidingPage.SetBounds(slidingBounds.X + offset, slidingBounds.Y, slidingBounds.Width, slidingBounds.Height);
            if (pageMotion.Value >= 1) slidingPage.Dock = DockStyle.Fill;
        });
        AcceptButton = save; CancelButton = cancel;
        var appearance = Page("Portada", "El centro de tu escritorio.");
        Row(appearance, "Visualizador", mode); mode.SelectedIndex = (int)settings.VisualizerMode;
        Number(appearance, "Tamaño de portada (%)", "size", settings.CoverSize * 100, 15, 65);
        Number(appearance, "Posición horizontal (%)", "x", settings.PositionX * 100, 10, 90);
        Number(appearance, "Posición vertical (%)", "y", settings.PositionY * 100, 10, 90);
        Number(appearance, "Margen sobre barra (%)", "margin", settings.BottomMargin * 100, 0, 20, 1);
        Row(appearance, "Legibilidad de iconos", contrast); contrast.SelectedIndex = (int)settings.ContrastMode;
        Hint(appearance, "El modo adaptativo sigue las posiciones que expone el Explorador. Si no están disponibles, Estado lo indica; puedes elegir oscurecer la imagen completa.");

        var audio = Page("Audio", "Dale forma a lo que escuchas.");
        capture.Checked = settings.CaptureAudio; Row(audio, "Captura", capture);
        Number(audio, "Sensibilidad", "sensitivity", settings.Sensitivity, .1m, 5, .1m);
        Number(audio, "Suavizado (segundos)", "smoothing", settings.Smoothing, .02m, 1, .02m);
        Number(audio, "Intensidad (%)", "strength", settings.Strength * 100, 0, 300);
        Hint(audio, "Elige el reproductor y la salida desde el menú de la bandeja. La sesión proporciona título y portada; la captura analiza toda la mezcla de la salida elegida, incluidos sonidos de otras apps. El aislamiento por proceso aún no está implementado.");
        Hint(audio, "El audio solo se analiza en memoria: no se reproduce, graba ni transmite. Sin historial musical, telemetría ni cuentas de Spotify.");

        var motion = Page("Movimiento", "Un poco de profundidad. A tu ritmo.");
        tunnel.Checked = settings.EnableTunnel; parallax.Checked = settings.EnableParallax;
        Row(motion, "Profundidad", tunnel); Row(motion, "Paralaje", parallax);
        Number(motion, "Intensidad de paralaje (%)", "parallax", settings.ParallaxStrength * 100, 0, 300);
        Hint(motion, "Puedes desactivar cada efecto y conservar la portada. La animación se pausa al detener la música, bloquear la sesión o cubrir el monitor con una ventana maximizada o fullscreen.");

        var performance = Page("Rendimiento", "Encuentra el equilibrio para tu equipo.");
        Number(performance, "Límite de FPS", "fps", settings.Fps, 30, 120, 5);
        Number(performance, "Escala interna (%)", "scale", settings.RenderScale * 100, 50, 100, 5);
        Number(performance, "Capas de profundidad", "layers", settings.QualityLayers, 3, 12);
        Hint(performance, "45 FPS es el objetivo inicial. Baja la escala o las capas para reducir carga de GPU. La resolución parte de cada monitor. El rendimiento depende del equipo.");
        Hint(performance, "Direct3D 11 es el motor principal. Si no está disponible se usa compatibilidad GDI+. Estado muestra el motor activo. OpenGL y Vulkan están pendientes.");

        var hotkeys = Page("Atajos", "Tus efectos, a una tecla de distancia.");
        toggleKey.SelectedItem = settings.HotkeyKey; effectsKey.SelectedItem = settings.EffectsHotkeyKey;
        Row(hotkeys, "Activar / desactivar", toggleKey); Row(hotkeys, "Movimiento on / off", effectsKey);
        ctrl.Checked = settings.HotkeyControl; alt.Checked = settings.HotkeyAlt; shift.Checked = settings.HotkeyShift;
        var modifiers = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = false }; modifiers.Controls.AddRange([ctrl, alt, shift]);
        Row(hotkeys, "Modificadores", modifiers);
        Hint(hotkeys, "Por defecto: Ctrl + Alt + V activa el wallpaper; Ctrl + Alt + E alterna túnel y paralaje. Salir desde la bandeja revela el fondo original.");
        SelectSection(0);
    }
    private readonly List<Panel> pages = [];
    private readonly List<UiButton> navigationButtons = [];
    private readonly Panel contentHost;
    private readonly FlowLayoutPanel navigation;
    private readonly UiTween pageMotion;
    private Panel? slidingPage;
    private Rectangle slidingBounds;
    public int SectionCount => pages.Count;
    public void SelectSection(int index)
    {
        if (slidingPage is not null) slidingPage.Dock = DockStyle.Fill;
        for (int i = 0; i < pages.Count; i++) { pages[i].Visible = i == index; navigationButtons[i].Selected = i == index; navigationButtons[i].Invalidate(); }
        if (index >= 0 && index < pages.Count)
        {
            slidingPage = pages[index]; slidingPage.BringToFront(); contentHost.PerformLayout(); slidingBounds = slidingPage.Bounds;
            slidingPage.Dock = DockStyle.None; pageMotion.Reset(0); pageMotion.Move(1, Visible);
        }
    }
    protected override void Dispose(bool disposing) { if (disposing) pageMotion?.Dispose(); base.Dispose(disposing); }
    private static UiSelect Choices(params object[] items)
    {
        var box = new UiSelect { Dock = DockStyle.Fill }; box.Items.AddRange(items); return box;
    }
    private static UiSelect KeysBox()
    {
        var box = Choices(); foreach (var key in Enum.GetValues<Keys>().Distinct().Where(k => k >= Keys.A && k <= Keys.Z || k >= Keys.F1 && k <= Keys.F12)) box.Items.Add(key);
        return box;
    }
    private TableLayoutPanel Page(string title, string description)
    {
        int index = pages.Count;
        var page = new Panel { Dock = DockStyle.Fill, Visible = false, AutoScroll = true, BackColor = Ui.Background };
        var heading = new Panel { Dock = DockStyle.Top, Height = 86 };
        heading.Controls.Add(new Label { Text = title, Font = Ui.Font(24, FontStyle.Bold), ForeColor = Ui.Text, AutoSize = true, Location = new Point(0, 0) });
        heading.Controls.Add(new Label { Text = description, Font = Ui.Font(10), ForeColor = Ui.Muted, AutoSize = true, Location = new Point(2, 49) });
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(6, 0, 6, 4), BackColor = Ui.Surface };
        table.ColumnStyles.Add(new(SizeType.Percent, 52)); table.ColumnStyles.Add(new(SizeType.Percent, 48));
        var card = new UiCard { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(14) };
        card.Controls.Add(table); page.Controls.Add(card); page.Controls.Add(heading); contentHost.Controls.Add(page); pages.Add(page);
        var button = new UiButton { Text = $"{index + 1:00}   {title}", Navigation = true, Quiet = true, Width = 174, Height = 46, Margin = new Padding(0, 0, 0, 8) };
        button.Click += (_, _) => SelectSection(index); navigation.Controls.Add(button); navigationButtons.Add(button);
        return table;
    }
    private static void Row(TableLayoutPanel table, string label, Control control)
    {
        int row = table.RowCount++; table.RowStyles.Add(new(SizeType.Absolute, 52));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Ui.Text, Margin = new Padding(0, 8, 14, 8) }, 0, row);
        control.Margin = new Padding(3, 8, 3, 8); control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.AccessibleName = label; table.Controls.Add(control, 1, row);
    }
    private void Number(TableLayoutPanel table, string label, string name, float value, decimal min, decimal max, decimal step = 1)
    {
        var box = new UiNumber { Minimum = min, Maximum = max, Increment = step, DecimalPlaces = step < 1 ? 2 : 0, Dock = DockStyle.Fill };
        box.Value = Math.Clamp((decimal)value, min, max); numbers[name] = box; Row(table, label, box);
    }
    private static void Hint(TableLayoutPanel table, string text)
    {
        int row = table.RowCount++; table.RowStyles.Add(new(SizeType.AutoSize));
        var label = new Label { Text = text, AutoSize = true, MaximumSize = new Size(550, 0), ForeColor = Ui.Muted, Font = Ui.Font(9), Margin = new Padding(0, 18, 0, 4) };
        table.Controls.Add(label, 0, row); table.SetColumnSpan(label, 2);
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            if (toggleKey.SelectedItem is not Keys || effectsKey.SelectedItem is not Keys ||
                Equals(toggleKey.SelectedItem, effectsKey.SelectedItem) || !ctrl.Checked && !alt.Checked && !shift.Checked)
            { MessageBox.Show(this, "Elige dos teclas distintas y al menos un modificador.", "Atajos"); e.Cancel = true; return; }
            float N(string name) => (float)numbers[name].Value;
            settings.VisualizerMode = (VisualizerMode)mode.SelectedIndex; settings.ContrastMode = (ContrastMode)contrast.SelectedIndex;
            settings.CoverSize = N("size") / 100; settings.PositionX = N("x") / 100; settings.PositionY = N("y") / 100; settings.BottomMargin = N("margin") / 100;
            settings.CaptureAudio = capture.Checked; settings.Sensitivity = N("sensitivity"); settings.Smoothing = N("smoothing"); settings.Strength = N("strength") / 100;
            settings.EnableTunnel = tunnel.Checked; settings.EnableParallax = parallax.Checked; settings.ParallaxStrength = N("parallax") / 100;
            settings.Fps = (int)N("fps"); settings.RenderScale = N("scale") / 100; settings.QualityLayers = (int)N("layers");
            settings.HotkeyKey = (Keys)toggleKey.SelectedItem; settings.EffectsHotkeyKey = (Keys)effectsKey.SelectedItem;
            settings.HotkeyControl = ctrl.Checked; settings.HotkeyAlt = alt.Checked; settings.HotkeyShift = shift.Checked;
        }
        base.OnFormClosing(e);
    }
}

