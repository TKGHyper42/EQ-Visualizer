namespace EQVisualizer;

internal sealed class SettingsForm : Form
{
    private readonly Settings settings;
    private readonly Dictionary<string, NumericUpDown> numbers = [];
    private readonly CheckBox capture = new() { Text = "Analizar la salida de audio", AutoSize = true };
    private readonly CheckBox tunnel = new() { Text = "Túnel de portadas", AutoSize = true };
    private readonly CheckBox parallax = new() { Text = "Seguir suavemente el cursor", AutoSize = true };
    private readonly CheckBox ctrl = new() { Text = "Ctrl", AutoSize = true }, alt = new() { Text = "Alt", AutoSize = true }, shift = new() { Text = "Shift", AutoSize = true };
    private readonly ComboBox mode = Choices("Alrededor de la portada", "Franja inferior", "Ambos");
    private readonly ComboBox contrast = Choices("Adaptativo por iconos", "Oscurecer imagen completa", "Desactivado");
    private readonly ComboBox toggleKey = KeysBox(), effectsKey = KeysBox();

    public SettingsForm(Settings settings)
    {
        this.settings = settings;
        Text = "EQ Visualizer · Ajustes"; StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 510); MinimumSize = new Size(580, 510); ShowInTaskbar = false;
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(22, 24, 33); ForeColor = Color.FromArgb(234, 237, 245);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var appearance = Page(tabs, "Portada");
        Row(appearance, "Visualizador", mode); mode.SelectedIndex = (int)settings.VisualizerMode;
        Number(appearance, "Tamaño de portada (%)", "size", settings.CoverSize * 100, 15, 65);
        Number(appearance, "Posición horizontal (%)", "x", settings.PositionX * 100, 10, 90);
        Number(appearance, "Posición vertical (%)", "y", settings.PositionY * 100, 10, 90);
        Number(appearance, "Margen sobre barra (%)", "margin", settings.BottomMargin * 100, 0, 20, 1);
        Row(appearance, "Legibilidad de iconos", contrast); contrast.SelectedIndex = (int)settings.ContrastMode;
        Hint(appearance, "El modo adaptativo sigue las posiciones que expone el Explorador. Si no están disponibles, Estado lo indica; puedes elegir oscurecer la imagen completa.");

        var audio = Page(tabs, "Audio");
        capture.Checked = settings.CaptureAudio; Row(audio, "Captura", capture);
        Number(audio, "Sensibilidad", "sensitivity", settings.Sensitivity, .1m, 5, .1m);
        Number(audio, "Suavizado (segundos)", "smoothing", settings.Smoothing, .02m, 1, .02m);
        Number(audio, "Intensidad (%)", "strength", settings.Strength * 100, 0, 300);
        Hint(audio, "Elige el reproductor y la salida desde el menú de la bandeja. La sesión proporciona título y portada; la captura analiza toda la mezcla de la salida elegida, incluidos sonidos de otras apps. El aislamiento por proceso aún no está implementado.");
        Hint(audio, "El audio solo se analiza en memoria: no se reproduce, graba ni transmite. Sin historial musical, telemetría ni cuentas de Spotify.");

        var motion = Page(tabs, "Movimiento");
        tunnel.Checked = settings.EnableTunnel; parallax.Checked = settings.EnableParallax;
        Row(motion, "Profundidad", tunnel); Row(motion, "Paralaje", parallax);
        Number(motion, "Intensidad de paralaje (%)", "parallax", settings.ParallaxStrength * 100, 0, 300);
        Hint(motion, "Puedes desactivar cada efecto y conservar la portada. La animación se pausa al detener la música, bloquear la sesión o cubrir el monitor con una ventana maximizada o fullscreen.");

        var performance = Page(tabs, "Rendimiento");
        Number(performance, "Límite de FPS", "fps", settings.Fps, 30, 120, 5);
        Number(performance, "Escala interna (%)", "scale", settings.RenderScale * 100, 50, 100, 5);
        Number(performance, "Capas de profundidad", "layers", settings.QualityLayers, 3, 12);
        Hint(performance, "45 FPS es el objetivo inicial. Baja la escala o las capas para reducir carga de GPU. La resolución parte de cada monitor. El rendimiento depende del equipo.");
        Hint(performance, "Direct3D 11 es el motor principal. Si no está disponible se usa compatibilidad GDI+. Estado muestra el motor activo. OpenGL y Vulkan están pendientes.");

        var hotkeys = Page(tabs, "Atajos");
        toggleKey.SelectedItem = settings.HotkeyKey; effectsKey.SelectedItem = settings.EffectsHotkeyKey;
        Row(hotkeys, "Activar / desactivar", toggleKey); Row(hotkeys, "Movimiento on / off", effectsKey);
        ctrl.Checked = settings.HotkeyControl; alt.Checked = settings.HotkeyAlt; shift.Checked = settings.HotkeyShift;
        var modifiers = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; modifiers.Controls.AddRange([ctrl, alt, shift]);
        Row(hotkeys, "Modificadores", modifiers);
        Hint(hotkeys, "Por defecto: Ctrl + Alt + V activa el wallpaper; Ctrl + Alt + E alterna túnel y paralaje. Salir desde la bandeja revela el fondo original.");
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(12), FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Guardar", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.AddRange([save, cancel]); Controls.Add(tabs); Controls.Add(buttons); AcceptButton = save; CancelButton = cancel;
    }
    private static ComboBox Choices(params object[] items)
    {
        var box = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; box.Items.AddRange(items); return box;
    }
    private static ComboBox KeysBox()
    {
        var box = Choices(); foreach (var key in Enum.GetValues<Keys>().Distinct().Where(k => k >= Keys.A && k <= Keys.Z || k >= Keys.F1 && k <= Keys.F12)) box.Items.Add(key);
        return box;
    }
    private TableLayoutPanel Page(TabControl tabs, string title)
    {
        var page = new TabPage(title) { BackColor = BackColor, ForeColor = ForeColor, AutoScroll = true };
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(18) };
        table.ColumnStyles.Add(new(SizeType.Percent, 52)); table.ColumnStyles.Add(new(SizeType.Percent, 48)); page.Controls.Add(table); tabs.TabPages.Add(page); return table;
    }
    private static void Row(TableLayoutPanel table, string label, Control control)
    {
        int row = table.RowCount++; table.RowStyles.Add(new(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 10, 10) }, 0, row);
        control.Margin = new Padding(3, 7, 3, 7); table.Controls.Add(control, 1, row);
    }
    private void Number(TableLayoutPanel table, string label, string name, float value, decimal min, decimal max, decimal step = 1)
    {
        var box = new NumericUpDown { Minimum = min, Maximum = max, Increment = step, DecimalPlaces = step < 1 ? 2 : 0, Dock = DockStyle.Fill };
        box.Value = Math.Clamp((decimal)value, min, max); numbers[name] = box; Row(table, label, box);
    }
    private static void Hint(TableLayoutPanel table, string text)
    {
        int row = table.RowCount++; var label = new Label { Text = text, AutoSize = true, MaximumSize = new Size(510, 0), Margin = new Padding(0, 18, 0, 4) };
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

