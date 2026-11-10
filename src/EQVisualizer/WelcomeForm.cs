namespace EQVisualizer;

internal sealed class WelcomeForm : Form
{
    private readonly Settings settings;
    private readonly RadioButton cover = new() { Text = "Alrededor de la portada", AutoSize = true, Checked = true };
    private readonly RadioButton bottom = new() { Text = "Franja inferior", AutoSize = true };
    private readonly RadioButton both = new() { Text = "Ambos efectos", AutoSize = true };

    public WelcomeForm(Settings settings)
    {
        this.settings = settings;
        Text = "Bienvenido a EQ Visualizer";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        ClientSize = new Size(430, 245);
        Font = new Font("Segoe UI", 10);

        var title = new Label { Text = "Elige cómo quieres ver tu música", Font = new Font("Segoe UI Semibold", 14), AutoSize = true };
        var detail = new Label { Text = "Puedes cambiarlo cuando quieras desde Ajustes.", AutoSize = true };
        var start = new Button { Text = "Empezar", DialogResult = DialogResult.OK, AutoSize = true };
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(24), WrapContents = false };
        layout.Controls.AddRange([title, detail, new Label { Height = 12 }, cover, bottom, both, new Label { Height = 12 }, start]);
        Controls.Add(layout); AcceptButton = start;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
            settings.VisualizerMode = both.Checked ? VisualizerMode.Both : bottom.Checked ? VisualizerMode.Bottom : VisualizerMode.Cover;
        base.OnFormClosing(e);
    }
}
