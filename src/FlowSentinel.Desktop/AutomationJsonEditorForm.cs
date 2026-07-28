using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class AutomationJsonEditorForm : Form
{
    private readonly TextBox _editor = new();
    private readonly Label _validation = new();
    private readonly ISecretProtector? _secretProtector;

    public AutomationDefinition? Definition { get; private set; }

    public AutomationJsonEditorForm(
        AutomationDefinition definition,
        ISecretProtector? secretProtector = null)
    {
        _secretProtector = secretProtector;
        Text = "Editor da automação";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1000, 760);
        MinimumSize = new Size(760, 520);

        _editor.Multiline = true;
        _editor.AcceptsTab = true;
        _editor.ScrollBars = ScrollBars.Both;
        _editor.WordWrap = false;
        _editor.Font = new Font("Consolas", 10);
        _editor.Dock = DockStyle.Fill;
        _editor.Text = JsonSerializer.Serialize(definition, FlowJson.Options);

        _validation.Dock = DockStyle.Bottom;
        _validation.Height = 30;
        _validation.Padding = new Padding(8, 6, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var save = new Button { Text = "Validar e salvar", AutoSize = true };
        var format = new Button { Text = "Formatar JSON", AutoSize = true };
        var protectUser = new Button { Text = "Proteger para usuário", AutoSize = true, Enabled = secretProtector is not null };
        var protectMachine = new Button { Text = "Proteger para máquina", AutoSize = true, Enabled = secretProtector is not null };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        format.Click += (_, _) => FormatJson();
        protectUser.Click += (_, _) => ProtectSelection(SecretProtectionScope.CurrentUser);
        protectMachine.Click += (_, _) => ProtectSelection(SecretProtectionScope.LocalMachine);
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(protectMachine);
        buttons.Controls.Add(protectUser);
        buttons.Controls.Add(format);

        Controls.Add(_editor);
        Controls.Add(_validation);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void Save()
    {
        try
        {
            var definition = JsonSerializer.Deserialize<AutomationDefinition>(_editor.Text, FlowJson.Options)
                             ?? throw new InvalidOperationException("O JSON não representa uma automação.");
            definition.Validate();
            Definition = definition;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            SetValidation(exception.Message, Color.DarkRed);
            MessageBox.Show(this, exception.Message, "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void FormatJson()
    {
        try
        {
            using var document = JsonDocument.Parse(_editor.Text);
            _editor.Text = JsonSerializer.Serialize(document.RootElement, FlowJson.Options);
            SetValidation("JSON válido.", Color.DarkGreen);
        }
        catch (Exception exception)
        {
            SetValidation(exception.Message, Color.DarkRed);
        }
    }

    private void ProtectSelection(SecretProtectionScope scope)
    {
        if (_secretProtector is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_editor.SelectedText))
        {
            MessageBox.Show(
                this,
                "Selecione somente o conteúdo do segredo dentro das aspas.",
                "DPAPI",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            _editor.SelectedText = _secretProtector.Protect(_editor.SelectedText, scope);
            SetValidation(
                scope == SecretProtectionScope.LocalMachine
                    ? "Seleção protegida para esta máquina."
                    : "Seleção protegida para o usuário atual.",
                Color.DarkGreen);
        }
        catch (Exception exception)
        {
            SetValidation(exception.Message, Color.DarkRed);
        }
    }

    private void SetValidation(string message, Color color)
    {
        _validation.Text = message;
        _validation.ForeColor = color;
    }
}
