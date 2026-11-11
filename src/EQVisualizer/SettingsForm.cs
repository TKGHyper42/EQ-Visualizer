namespace EQVisualizer;

internal sealed class SettingsForm : ModernForm
{
    private readonly Settings settings;
    private readonly Dictionary<string, UiSlider> sliders = [];
    private readonly CheckBox capture = new UiToggle() { Text = "Analizar la salida de audio", AutoSize = false };
    private readonly CheckBox tunnel = new UiToggle() { Text = "Túnel en perspectiva", AutoSize = false };
    private readonly CheckBox parallax = new UiToggle() { Text = "Seguir suavemente el cursor", AutoSize = false };
    private readonly CheckBox trackInfo = new UiToggle() { Text = "Título y artista", AutoSize = false };
    private readonly CheckBox softwareRenderer = new UiToggle() { Text = "Usar GDI+ (compatibilidad)", AutoSize = false };
    private readonly CheckBox ctrl = new UiChip() { Text = "Ctrl", AutoSize = false }, alt = new UiChip() { Text = "Alt", AutoSize = false }, shift = new UiChip() { Text = "Shift", AutoSize = false };
    private readonly UiSelect mode = Choices(VisualizerModes.All.Select(m => (object)m.Label()).ToArray());
    private readonly UiSelect contrast = Choices("Adaptativo por iconos", "Oscurecer imagen completa", "Desactivado");
    private readonly UiSelect idleMode = Choices("Ocultar wallpaper", "Reloj", "Imagen personalizada", "Imagen y reloj");
    private readonly UiSelect clockStyle = Choices("Digital redondeado", "Analógico circular");
    private readonly UiSelect interfaceTheme = Choices("Carbón · oscuro", "Grafito · oscuro", "Ciruela · oscuro", "Bosque · oscuro",
        "Perla · claro", "Arena · claro", "Bruma · claro", "Rosa · claro", "Aurora · oscuro");
    private readonly TextBox idlePath = new() { BorderStyle = BorderStyle.None, BackColor = Ui.Elevated, ForeColor = Ui.Text, ReadOnly = true };
    private readonly CheckBox previewIdle = new UiToggle { Text = "Ver sin música", AutoSize = false, Width = 170 };
    private readonly UiSelect toggleKey = KeysBox(), effectsKey = KeysBox();
    private readonly SettingsPreview preview;
    private readonly ThemeSample themeSample = new();
    private Color selectedAccent;
    private bool customAccent;
    private readonly UiButton accentButton = new() { Text = "Elegir color…", Width = 175 };

    public SettingsForm(Settings settings, Image? artwork = null) : base("EQ Visualizer / Ajustes")
    {
        this.settings = settings;
        ShowInTaskbar = true;
        customAccent = settings.InterfaceAccent != 0;
        selectedAccent = customAccent ? InterfacePalettes.EnsureAccent(Color.FromArgb(settings.InterfaceAccent), InterfacePalettes.For(settings.InterfaceTheme).Surface)
            : InterfacePalettes.For(settings.InterfaceTheme).Accent;
        preview = new SettingsPreview(artwork);
        WindowOpacity = settings.InterfaceOpacity / 100d;
        ClientSize = new Size(940, 720); MinimumSize = new Size(820, 650);
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
        pageHost = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Background };
        var previewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
        previewLayout.ColumnStyles.Add(new(SizeType.Percent, 100));
        previewLayout.RowStyles.Add(new(SizeType.Absolute, 174));
        previewLayout.RowStyles.Add(new(SizeType.Absolute, 38));
        previewLayout.RowStyles.Add(new(SizeType.Percent, 100));
        previewLayout.Controls.Add(pageHost, 0, 2);
        contentHost.Controls.Add(previewLayout);
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
        Row(appearance, "Visualizador", mode); mode.SelectedIndex = Math.Clamp((int)settings.VisualizerMode, 0, mode.Items.Count - 1);
        trackInfo.Checked = settings.ShowTrackInfo; Row(appearance, "Información de la canción", trackInfo);
        Slider(appearance, "Tamaño de portada (%)", "size", settings.CoverSize * 100, 15, 65);
        Slider(appearance, "Posición horizontal (%)", "x", settings.PositionX * 100, 10, 90);
        Slider(appearance, "Posición vertical (%)", "y", settings.PositionY * 100, 10, 90);
        Slider(appearance, "Margen sobre barra (%)", "margin", settings.BottomMargin * 100, 0, 20, 1);
        Row(appearance, "Legibilidad de iconos", contrast); contrast.SelectedIndex = Math.Clamp((int)settings.ContrastMode, 0, contrast.Items.Count - 1);
        Row(appearance, "Cuando no hay sesión", idleMode); idleMode.SelectedIndex = Math.Clamp((int)settings.NoSessionMode, 0, idleMode.Items.Count - 1);
        Row(appearance, "Estilo de reloj", clockStyle); clockStyle.SelectedIndex = Math.Clamp((int)settings.ClockStyle, 0, clockStyle.Items.Count - 1);
        var idleFile = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Elevated };
        var browse = new UiButton { Text = "Elegir…", Dock = DockStyle.Right, Width = 94 };
        idlePath.Text = settings.NoSessionImagePath ?? "";
        idlePath.Dock = DockStyle.Fill;
        browse.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Title = "Elige una imagen de tu equipo", Filter = "Imágenes compatibles|*.png;*.jpg;*.jpeg;*.bmp|Todos los archivos|*.*", CheckFileExists = true };
            if (picker.ShowDialog(this) == DialogResult.OK) idlePath.Text = picker.FileName;
        };
        idleFile.Controls.Add(idlePath); idleFile.Controls.Add(browse);
        Row(appearance, "Tu imagen", idleFile);
        Hint(appearance, "El modo adaptativo sigue las posiciones que expone el Explorador. Si no están disponibles, Estado lo indica; puedes elegir oscurecer la imagen completa.");

        var interfacePage = Page("Interfaz", "Colores que se sienten tuyos.");
        interfaceTheme.SelectedIndex = Math.Clamp((int)settings.InterfaceTheme, 0, interfaceTheme.Items.Count - 1);
        Row(interfacePage, "Tema", interfaceTheme);
        Row(interfacePage, "Color de acento", accentButton);
        accentButton.Click += (_, _) =>
        {
            using var picker = new ColorDialog { Color = selectedAccent, FullOpen = true, AllowFullOpen = true };
            if (picker.ShowDialog(this) == DialogResult.OK)
            {
                selectedAccent = InterfacePalettes.EnsureAccent(picker.Color,
                    InterfacePalettes.For((InterfaceTheme)interfaceTheme.SelectedIndex).Surface);
                customAccent = true; UpdatePreview();
            }
        };
        Slider(interfacePage, "Opacidad de Ajustes (%)", "opacity", settings.InterfaceOpacity, 80, 100, 5);
        var sampleRow = interfacePage.RowCount++; interfacePage.RowStyles.Add(new(SizeType.Absolute, 108));
        themeSample.Dock = DockStyle.Fill; themeSample.Margin = new Padding(0, 6, 0, 6);
        interfacePage.Controls.Add(themeSample, 0, sampleRow); interfacePage.SetColumnSpan(themeSample, 2);
        Hint(interfacePage, "Todos los temas tienen texto y botones de alto contraste. Al elegir otro tema se recupera su acento original; también puedes escoger uno propio.");
        Hint(interfacePage, "La opacidad afecta solo a la ventana de Ajustes. 100 % es completamente opaca.");

        var audio = Page("Audio", "Dale forma a lo que escuchas.");
        capture.Checked = settings.CaptureAudio; Row(audio, "Captura", capture);
        Slider(audio, "Sensibilidad", "sensitivity", settings.Sensitivity, .1m, 5, .1m);
        Slider(audio, "Suavizado (segundos)", "smoothing", settings.Smoothing, .02m, 1, .02m);
        Slider(audio, "Intensidad (%)", "strength", settings.Strength * 100, 0, 300);
        Slider(audio, "Altura del círculo (%)", "circleHeight", settings.CircleBarHeight * 100, 50, 200, 5);
        Slider(audio, "Altura de la franja (%)", "bottomHeight", settings.BottomBarHeight * 100, 50, 200, 5);
        Slider(audio, "Grosor de barras (%)", "thickness", settings.BarThickness * 100, 50, 300, 10);
        Hint(audio, "En la bandeja, Reproductor elige metadatos y Capturar sonido de elige mezcla o proceso aislado. El aislamiento requiere Windows build 20348+ y elegir el proceso de audio correcto; en Windows 10 habitual queda disponible la mezcla completa.");
        Hint(audio, "El audio solo se analiza en memoria: no se reproduce, graba ni transmite. Sin historial musical, telemetría ni cuentas de Spotify.");

        var motion = Page("Movimiento", "Un poco de profundidad. A tu ritmo.");
        tunnel.Checked = settings.EnableTunnel; parallax.Checked = settings.EnableParallax;
        Row(motion, "Efecto opcional", tunnel); Row(motion, "Paralaje", parallax);
        Slider(motion, "Intensidad de paralaje (%)", "parallax", settings.ParallaxStrength * 100, 0, 300);
        Slider(motion, "Desenfoque del fondo (%)", "blur", settings.BackgroundBlur, 0, 100, 5);
        Hint(motion, "Puedes desactivar cada efecto y conservar la portada. La animación se pausa al detener la música, bloquear la sesión o cubrir el monitor con una ventana maximizada o fullscreen.");

        var performance = Page("Rendimiento", "Encuentra el equilibrio para tu equipo.");
        softwareRenderer.Checked = settings.UseGdiRenderer;
        Row(performance, "Motor gráfico", softwareRenderer);
        Slider(performance, "Límite de FPS", "fps", settings.Fps, 30, 120, 5);
        Slider(performance, "Escala interna (%)", "scale", settings.RenderScale * 100, 50, 100, 5);
        Slider(performance, "Capas de profundidad", "layers", settings.QualityLayers, 3, 12);
        Hint(performance, "45 FPS es el objetivo inicial. Baja la escala o las capas para reducir carga de GPU. La resolución parte de cada monitor. El rendimiento depende del equipo.");
        Hint(performance, "Usa GDI+ si RivaTuner detecta el visualizador de Direct3D. El modo de compatibilidad puede aumentar el uso de CPU. Estado muestra el motor activo.");

        var hotkeys = Page("Atajos", "Tus efectos, a una tecla de distancia.");
        toggleKey.SelectedItem = settings.HotkeyKey; effectsKey.SelectedItem = settings.EffectsHotkeyKey;
        Row(hotkeys, "Activar / desactivar", toggleKey); Row(hotkeys, "Movimiento on / off", effectsKey);
        ctrl.Checked = settings.HotkeyControl; alt.Checked = settings.HotkeyAlt; shift.Checked = settings.HotkeyShift;
        var modifiers = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = false }; modifiers.Controls.AddRange([ctrl, alt, shift]);
        Row(hotkeys, "Modificadores", modifiers);
        Hint(hotkeys, "Por defecto: Ctrl + Alt + V activa el wallpaper; Ctrl + Alt + E alterna túnel y paralaje. Salir desde la bandeja revela el fondo original.");
        var previewBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Ui.Background };
        previewIdle.Dock = DockStyle.Right; previewBar.Controls.Add(previewIdle);
        previewIdle.Checked = settings.NoSessionMode != IdleMode.Off && artwork is null;
        previewIdle.CheckedChanged += (_, _) => UpdatePreview();
        idleMode.SelectedIndexChanged += (_, _) => UpdatePreview();
        clockStyle.SelectedIndexChanged += (_, _) => UpdatePreview();
        idlePath.TextChanged += (_, _) => UpdatePreview();
        interfaceTheme.SelectedIndexChanged += (_, _) =>
        {
            selectedAccent = InterfacePalettes.For((InterfaceTheme)interfaceTheme.SelectedIndex).Accent;
            customAccent = false; UpdatePreview();
        };
        preview.Dock = DockStyle.Fill;
        previewLayout.Controls.Add(preview, 0, 0);
        previewLayout.Controls.Add(previewBar, 0, 1);
        foreach (var slider in sliders.Values) slider.ValueChanged += (_, _) => UpdatePreview();
        foreach (var box in new CheckBox[] { capture, tunnel, parallax, trackInfo }) box.CheckedChanged += (_, _) => UpdatePreview();
        mode.SelectedIndexChanged += (_, _) => UpdatePreview();
        contrast.SelectedIndexChanged += (_, _) => UpdatePreview();
        UpdatePreview();
        SelectSection(0);
    }
    private readonly List<Panel> pages = [];
    private readonly List<UiButton> navigationButtons = [];
    private readonly Panel contentHost;
    private readonly Panel pageHost;
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
            slidingPage = pages[index]; slidingPage.BringToFront(); pageHost.PerformLayout(); slidingBounds = slidingPage.Bounds;
            slidingPage.Dock = DockStyle.None; pageMotion.Reset(0); pageMotion.Move(1, Visible);
        }
    }
    protected override void Dispose(bool disposing) { if (disposing) pageMotion?.Dispose(); base.Dispose(disposing); }
    private void UpdatePreview()
    {
        if (!sliders.ContainsKey("layers")) return;
        float N(string name) => (float)sliders[name].Value;
        themeSample.SetPalette((InterfaceTheme)Math.Max(0, interfaceTheme.SelectedIndex), selectedAccent);
        preview.SetOptions(new PreviewOptions((VisualizerMode)Math.Max(0, mode.SelectedIndex),
            N("size") / 100, N("x") / 100, N("y") / 100, N("margin") / 100,
            N("strength") / 100, N("sensitivity"), N("thickness") / 100, N("circleHeight") / 100, N("bottomHeight") / 100,
            (int)N("layers"), tunnel.Checked, trackInfo.Checked, N("blur"), selectedAccent,
            (ContrastMode)Math.Max(0, contrast.SelectedIndex), (IdleMode)Math.Max(0, idleMode.SelectedIndex),
            previewIdle.Checked, idlePath.Text, (InterfaceTheme)Math.Max(0, interfaceTheme.SelectedIndex),
            (IdleClockStyle)Math.Max(0, clockStyle.SelectedIndex)));
        WindowOpacity = N("opacity") / 100d;
        if (IsHandleCreated) Opacity = WindowOpacity;
    }
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
        card.Controls.Add(table); page.Controls.Add(card); page.Controls.Add(heading); pageHost.Controls.Add(page); pages.Add(page);
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
    private void Slider(TableLayoutPanel table, string label, string name, float value, decimal min, decimal max, decimal step = 1)
    {
        var slider = new UiSlider(label, min, max, step, (decimal)value) { Dock = DockStyle.Fill };
        sliders[name] = slider;
        Row(table, label, slider);
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
            if (((IdleMode)idleMode.SelectedIndex).HasImage() && !SettingsPreview.CanLoadImage(idlePath.Text))
            { MessageBox.Show(this, "Selecciona una imagen PNG, JPG, JPEG o BMP válida de hasta 20 MB y 8192 × 8192 píxeles.", "Imagen sin música"); e.Cancel = true; return; }
            if (toggleKey.SelectedItem is not Keys || effectsKey.SelectedItem is not Keys ||
                Equals(toggleKey.SelectedItem, effectsKey.SelectedItem) || !ctrl.Checked && !alt.Checked && !shift.Checked)
            { MessageBox.Show(this, "Elige dos teclas distintas y al menos un modificador.", "Atajos"); e.Cancel = true; return; }
            float N(string name) => (float)sliders[name].Value;
            settings.VisualizerMode = (VisualizerMode)mode.SelectedIndex; settings.ContrastMode = (ContrastMode)contrast.SelectedIndex;
            settings.ShowTrackInfo = trackInfo.Checked; settings.BarThickness = N("thickness") / 100;
            settings.CircleBarHeight = N("circleHeight") / 100;
            settings.BottomBarHeight = N("bottomHeight") / 100;
            settings.BackgroundBlur = N("blur");
            settings.InterfaceTheme = (InterfaceTheme)interfaceTheme.SelectedIndex;
            settings.InterfaceAccent = customAccent ? selectedAccent.ToArgb() : 0;
            settings.InterfaceOpacity = (int)N("opacity");
            settings.NoSessionMode = (IdleMode)idleMode.SelectedIndex;
            settings.ClockStyle = (IdleClockStyle)clockStyle.SelectedIndex;
            settings.NoSessionImagePath = string.IsNullOrWhiteSpace(idlePath.Text) ? null : idlePath.Text;
            settings.CoverSize = N("size") / 100; settings.PositionX = N("x") / 100; settings.PositionY = N("y") / 100; settings.BottomMargin = N("margin") / 100;
            settings.CaptureAudio = capture.Checked; settings.Sensitivity = N("sensitivity"); settings.Smoothing = N("smoothing"); settings.Strength = N("strength") / 100;
            settings.EnableTunnel = tunnel.Checked; settings.EnableParallax = parallax.Checked; settings.ParallaxStrength = N("parallax") / 100;
            settings.Fps = (int)N("fps"); settings.RenderScale = N("scale") / 100; settings.QualityLayers = (int)N("layers");
            settings.UseGdiRenderer = softwareRenderer.Checked;
            settings.HotkeyKey = (Keys)toggleKey.SelectedItem; settings.EffectsHotkeyKey = (Keys)effectsKey.SelectedItem;
            settings.HotkeyControl = ctrl.Checked; settings.HotkeyAlt = alt.Checked; settings.HotkeyShift = shift.Checked;
        }
        base.OnFormClosing(e);
    }
}
