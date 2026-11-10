namespace EQVisualizer;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown fps = new() { Minimum = 15, Maximum = 120, Increment = 5, Dock = DockStyle.Fill };
    private readonly TrackBar strength = new() { Minimum = 0, Maximum = 300, TickFrequency = 25, Dock = DockStyle.Fill };
    private readonly ComboBox key = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
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
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 245);
        Font = new Font("Segoe UI", 10);

        foreach (var value in new[] { Keys.V, Keys.M, Keys.P, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12 })
            key.Items.Add(value);
        fps.Value = Math.Clamp(settings.Fps, (int)fps.Minimum, (int)fps.Maximum);
        strength.Value = Math.Clamp((int)Math.Round(settings.Strength * 100), strength.Minimum, strength.Maximum);
        key.SelectedItem = key.Items.Contains(settings.HotkeyKey) ? settings.HotkeyKey : Keys.V;
        control.Checked = settings.HotkeyControl;
        alt.Checked = settings.HotkeyAlt;
        shift.Checked = settings.HotkeyShift;

        var modifiers = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        modifiers.Controls.AddRange([control, alt, shift]);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 5
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.Controls.Add(new Label { Text = "Límite de FPS", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        table.Controls.Add(fps, 1, 0);
        table.Controls.Add(new Label { Text = "Intensidad", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        table.Controls.Add(strength, 1, 1);
        table.Controls.Add(new Label { Text = "Tecla", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        table.Controls.Add(key, 1, 2);
        table.Controls.Add(new Label { Text = "Modificadores", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        table.Controls.Add(modifiers, 1, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var apply = new Button { Text = "Guardar", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.AddRange([apply, cancel]);
        table.Controls.Add(buttons, 0, 4);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = apply;
        CancelButton = cancel;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            settings.Fps = (int)fps.Value;
            settings.Strength = strength.Value / 100f;
            settings.HotkeyKey = (Keys)(key.SelectedItem ?? Keys.V);
            settings.HotkeyControl = control.Checked;
            settings.HotkeyAlt = alt.Checked;
            settings.HotkeyShift = shift.Checked;
        }
        base.OnFormClosing(e);
    }
}
