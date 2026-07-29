using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class ContactEditorForm : Form
{
    private readonly Guid _id;
    private readonly IReadOnlyCollection<AutomationStoreItem> _automations;
    private readonly TextBox _name = new();
    private readonly CheckBox _enabled = new();
    private readonly TextBox _whatsApp = new();
    private readonly TextBox _email = new();
    private readonly TextBox _telegram = new();
    private readonly TextBox _notes = new();
    private readonly ComboBox _accessScope = new();
    private readonly CheckedListBox _allowedAutomations = new();

    internal ContactDefinition? Definition { get; private set; }

    internal ContactEditorForm(ContactDefinition definition, IReadOnlyCollection<AutomationStoreItem> automations)
    {
        _id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id;
        _automations = automations;

        Text = "Contato";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 650);
        MinimumSize = new Size(680, 560);
        Font = new Font("Segoe UI", 9F);

        ConfigureControls();
        BuildLayout();
        LoadDefinition(definition);
    }

    private void ConfigureControls()
    {
        _enabled.Text = "Contato habilitado";
        _enabled.AutoSize = true;

        foreach (var field in new[] { _whatsApp, _email, _telegram })
        {
            field.Multiline = true;
            field.ScrollBars = ScrollBars.Vertical;
            field.Height = 54;
        }
        _whatsApp.PlaceholderText = "+5575999999999; +5575888888888";
        _email.PlaceholderText = "financeiro@empresa.com.br; gestor@empresa.com.br";
        _telegram.PlaceholderText = "Chat ID ou identificador do Telegram";

        _notes.Multiline = true;
        _notes.ScrollBars = ScrollBars.Vertical;
        _notes.Height = 70;

        _accessScope.DropDownStyle = ComboBoxStyle.DropDownList;
        VisualEditorSupport.SetDisplayItems(_accessScope, new[]
        {
            new DisplayItem<ContactAccessScope>(ContactAccessScope.AllAutomations, "Disponível para todas as automações"),
            new DisplayItem<ContactAccessScope>(ContactAccessScope.SelectedAutomations, "Disponível somente para automações selecionadas")
        });
        _accessScope.SelectedIndexChanged += (_, _) => UpdateScopeState();

        _allowedAutomations.Dock = DockStyle.Fill;
        _allowedAutomations.CheckOnClick = true;
        _allowedAutomations.IntegralHeight = false;
        foreach (var automation in _automations.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            _allowedAutomations.Items.Add(new AutomationItem(automation));
        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(fields, "Nome", _name);
        AddField(fields, "Situação", _enabled);
        AddField(fields, "WhatsApp", _whatsApp);
        AddField(fields, "E-mail", _email);
        AddField(fields, "Telegram", _telegram);
        AddField(fields, "Observações", _notes);
        AddField(fields, "Permissão de uso", _accessScope);
        root.Controls.Add(fields, 0, 0);

        var accessGroup = new GroupBox
        {
            Text = "Automações autorizadas",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        accessGroup.Controls.Add(_allowedAutomations);
        root.Controls.Add(accessGroup, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };
        var save = new Button { Text = "Salvar contato", AutoSize = true, Height = 32 };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, Height = 32, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 2);

        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void LoadDefinition(ContactDefinition definition)
    {
        _name.Text = definition.Name;
        _enabled.Checked = definition.Enabled;
        _whatsApp.Text = Join(definition, ChannelType.EvolutionApi);
        _email.Text = Join(definition, ChannelType.Email);
        _telegram.Text = Join(definition, ChannelType.Telegram);
        _notes.Text = definition.Notes;
        VisualEditorSupport.SelectDisplayItem(_accessScope, definition.AccessScope, ContactAccessScope.AllAutomations);

        var allowed = definition.AllowedAutomationIds.ToHashSet();
        for (var index = 0; index < _allowedAutomations.Items.Count; index++)
        {
            if (_allowedAutomations.Items[index] is AutomationItem item && allowed.Contains(item.Definition.Id))
            {
                _allowedAutomations.SetItemChecked(index, true);
            }
        }
        UpdateScopeState();
    }

    private void Save()
    {
        try
        {
            var definition = new ContactDefinition
            {
                Id = _id,
                Name = _name.Text.Trim(),
                Enabled = _enabled.Checked,
                Notes = _notes.Text.Trim(),
                AccessScope = VisualEditorSupport.SelectedValue(_accessScope, ContactAccessScope.AllAutomations),
                AllowedAutomationIds = _allowedAutomations.CheckedItems.Cast<AutomationItem>().Select(x => x.Definition.Id).ToList()
            };
            AddAddresses(definition, ChannelType.EvolutionApi, _whatsApp.Text);
            AddAddresses(definition, ChannelType.Email, _email.Text);
            AddAddresses(definition, ChannelType.Telegram, _telegram.Text);
            definition.Validate();

            Definition = definition;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Validação do contato");
        }
    }

    private void UpdateScopeState()
    {
        var selected = VisualEditorSupport.SelectedValue(_accessScope, ContactAccessScope.AllAutomations);
        _allowedAutomations.Enabled = selected == ContactAccessScope.SelectedAutomations;
    }

    private static void AddField(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(VisualEditorSupport.LabelFor(label), 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
    }

    private static string Join(ContactDefinition definition, ChannelType channelType) =>
        definition.Addresses.TryGetValue(channelType, out var values)
            ? string.Join("; ", values)
            : string.Empty;

    private static void AddAddresses(ContactDefinition definition, ChannelType channelType, string raw)
    {
        var values = Split(raw).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (values.Count > 0)
        {
            definition.Addresses[channelType] = values;
        }
    }

    private static IEnumerable<string> Split(string? value) =>
        (value ?? string.Empty).Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed class AutomationItem
    {
        internal AutomationStoreItem Definition { get; }
        internal AutomationItem(AutomationStoreItem definition) => Definition = definition;
        public override string ToString() => Definition.Name;
    }
}
