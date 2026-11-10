namespace EQVisualizer;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown fps = new() { Minimum = 15, Maximum = 120, Increment = 5, Dock = DockStyle.Fill };
    private readonly TrackBar strength = new() { Minimum = 0, Maximum = 300, TickFrequency = 25, Dock = DockStyle.Fill };
    private readonly ComboBox visualizer = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox key = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly CheckBox parallax = new() { Text = "Movimiento con el cursor", AutoSize = true };
    private readonly CheckBox control = new() { Text = "Ctrl", AutoSize = true };
    private readonly CheckBox alt = new() { Text = "Alt", AutoSize = true };
    private readonly CheckBox shift = new() { Text = "Shift", AutoSize = true };
    private readonly Settings settings;

    public SettingsForm(Settings settings)
    {
        this.settings = settings;
        Text = "Ajustes de EQ Visualizer";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        ClientSize = new Size(440, 330);
        Font = new Font("Segoe UI", 10);

        visualizer.Items.AddRange([VisualizerMode.Cover, VisualizerMode.Bottom, VisualizerMode.Both]);
        foreach (var value in new[] { Keys.V, Keys.M, Keys.P, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12 })
            key.Items.Add(value);
        fps.Value = Math.Clamp(settings.Fps, (int)fps.Minimum, (int)fps.Maximum);
        strength.Value = Math.Clamp((int)Math.Round(settings.Strength * 100), strength.Minimum, strength.Maximum);
        visualizer.SelectedItem = settings.VisualizerMode;
        key.SelectedItem = key.Items.Contains(settings.HotkeyKey) ? settings.HotkeyKey : Keys.V;
        parallax.Checked = settings.EnableParallax;
        control.Checked = settings.HotkeyControl; alt.Checked = settings.HotkeyAlt; shift.Checked = settings.HotkeyShift;

        var modifiers = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        modifiers.Controls.AddRange([control, alt, shift]);
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 7 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "Límite de FPS", fps);
        AddRow(table, 1, "Intensidad", strength);
        AddRow(table, 2, "Visualizador", visualizer);
        AddRow(table, 3, "Movimiento", parallax);
        AddRow(table, 4, "Tecla", key);
        AddRow(table, 5, "Modificadores", modifiers);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Guardar", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.AddRange([save, new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true }]);
        table.Controls.Add(buttons, 0, 6); table.SetColumnSpan(buttons, 2);
        Controls.Add(table); AcceptButton = save; CancelButton = (IButtonControl)buttons.Controls[1];
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        table.Controls.Add(control, 1, row);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            settings.Fps = (int)fps.Value;
            settings.Strength = strength.Value / 100f;
            settings.VisualizerMode = (VisualizerMode)(visualizer.SelectedItem ?? VisualizerMode.Both);
            settings.EnableParallax = parallax.Checked;
            settings.HotkeyKey = (Keys)(key.SelectedItem ?? Keys.V);
            settings.HotkeyControl = control.Checked; settings.HotkeyAlt = alt.Checked; settings.HotkeyShift = shift.Checked;
        }
        base.OnFormClosing(e);
    }
}
