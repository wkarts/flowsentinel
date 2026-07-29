using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class DirectoryContactGroupEditorForm : Form
{
    private readonly IReadOnlyCollection<ContactDefinition> _contacts;
    private readonly IReadOnlyCollection<AutomationStoreItem> _automations;
    private readonly TextBox _id = new();
    private readonly TextBox _name = new();
    private readonly CheckBox _enabled = new();
    private readonly ComboBox _accessScope = new();
    private readonly CheckedListBox _allowedAutomations = new();
    private readonly CheckedListBox _selectedContacts = new();

    internal ContactGroupDefinition? Definition { get; private set; }

    internal DirectoryContactGroupEditorForm(
        ContactGroupDefinition definition,
        IReadOnlyCollection<ContactDefinition> contacts,
        IReadOnlyCollection<AutomationStoreItem> automations)
    {
        _contacts = contacts;
        _automations = automations;

        Text = "Grupo de contatos";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 670);
        MinimumSize = new Size(760, 560);
        Font = new Font("Segoe UI", 9F);

        ConfigureControls();
        BuildLayout();
        LoadDefinition(definition);
    }

    private void ConfigureControls()
    {
        _id.PlaceholderText = "Ex.: financeiro, gestores, suporte";
        _enabled.Text = "Grupo habilitado";
        _enabled.AutoSize = true;

        _accessScope.DropDownStyle = ComboBoxStyle.DropDownList;
        VisualEditorSupport.SetDisplayItems(_accessScope, new[]
        {
            new DisplayItem<ContactAccessScope>(ContactAccessScope.AllAutomations, "Disponível para todas as automações"),
            new DisplayItem<ContactAccessScope>(ContactAccessScope.SelectedAutomations, "Disponível somente para automações selecionadas")
        });
        _accessScope.SelectedIndexChanged += (_, _) => UpdateScopeState();

        foreach (var list in new[] { _allowedAutomations, _selectedContacts })
        {
            list.Dock = DockStyle.Fill;
            list.CheckOnClick = true;
            list.IntegralHeight = false;
        }

        foreach (var automation in _automations.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            _allowedAutomations.Items.Add(new AutomationItem(automation));
        }
        foreach (var contact in _contacts.Where(x => x.Enabled).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            _selectedContacts.Items.Add(new ContactItem(contact));
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
        AddField(fields, "Identificador", _id);
        AddField(fields, "Nome", _name);
        AddField(fields, "Situação", _enabled);
        AddField(fields, "Permissão de uso", _accessScope);
        root.Controls.Add(fields, 0, 0);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 430 };
        var contactsGroup = new GroupBox { Text = "Contatos do grupo", Dock = DockStyle.Fill, Padding = new Padding(10) };
        contactsGroup.Controls.Add(_selectedContacts);
        var accessGroup = new GroupBox { Text = "Automações autorizadas", Dock = DockStyle.Fill, Padding = new Padding(10) };
        accessGroup.Controls.Add(_allowedAutomations);
        split.Panel1.Controls.Add(contactsGroup);
        split.Panel2.Controls.Add(accessGroup);
        root.Controls.Add(split, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };
        var save = new Button { Text = "Salvar grupo", AutoSize = true, Height = 32 };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, Height = 32, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 2);

        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void LoadDefinition(ContactGroupDefinition definition)
    {
        _id.Text = definition.Id;
        _name.Text = definition.Name;
        _enabled.Checked = definition.Enabled;
        VisualEditorSupport.SelectDisplayItem(_accessScope, definition.AccessScope, ContactAccessScope.AllAutomations);

        var selectedContacts = definition.ContactIds.ToHashSet();
        for (var index = 0; index < _selectedContacts.Items.Count; index++)
        {
            if (_selectedContacts.Items[index] is ContactItem item && selectedContacts.Contains(item.Definition.Id))
            {
                _selectedContacts.SetItemChecked(index, true);
            }
        }

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
            var definition = new ContactGroupDefinition
            {
                Id = _id.Text.Trim(),
                Name = _name.Text.Trim(),
                Enabled = _enabled.Checked,
                AccessScope = VisualEditorSupport.SelectedValue(_accessScope, ContactAccessScope.AllAutomations),
                AllowedAutomationIds = _allowedAutomations.CheckedItems.Cast<AutomationItem>().Select(x => x.Definition.Id).ToList(),
                ContactIds = _selectedContacts.CheckedItems.Cast<ContactItem>().Select(x => x.Definition.Id).ToList()
            };
            definition.Validate();
            Definition = definition;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Validação do grupo");
        }
    }

    private void UpdateScopeState()
    {
        _allowedAutomations.Enabled = VisualEditorSupport.SelectedValue(_accessScope, ContactAccessScope.AllAutomations) ==
                                      ContactAccessScope.SelectedAutomations;
    }

    private static void AddField(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(VisualEditorSupport.LabelFor(label), 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
    }

    private sealed class AutomationItem
    {
        internal AutomationStoreItem Definition { get; }
        internal AutomationItem(AutomationStoreItem definition) => Definition = definition;
        public override string ToString() => Definition.Name;
    }

    private sealed class ContactItem
    {
        internal ContactDefinition Definition { get; }
        internal ContactItem(ContactDefinition definition) => Definition = definition;
        public override string ToString() => Definition.Name;
    }
}
