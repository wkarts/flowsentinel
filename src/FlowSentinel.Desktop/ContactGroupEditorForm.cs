using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class ContactGroupEditorForm : Form
{
    private readonly TextBox _id = new();
    private readonly TextBox _name = new();
    private readonly DataGridView _contacts = new();

    internal ContactGroupDefinition? Definition { get; private set; }

    internal ContactGroupEditorForm(ContactGroupDefinition definition)
    {
        Text = "Grupo de contatos";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(880, 580);
        MinimumSize = new Size(730, 470);

        _contacts.Dock = DockStyle.Fill;
        _contacts.AllowUserToAddRows = true;
        _contacts.AllowUserToDeleteRows = true;
        _contacts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _contacts.Columns.Add("ContactName", "Nome");
        _contacts.Columns.Add("WhatsApp", "WhatsApp");
        _contacts.Columns.Add("Email", "E-mail");
        _contacts.Columns.Add("Telegram", "Telegram Chat ID");
        _contacts.Columns[0].FillWeight = 130;

        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(10) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(header, "Identificador", _id);
        AddRow(header, "Nome", _name);
        header.Controls.Add(new Label
        {
            Text = "Separe vários endereços do mesmo canal por ponto e vírgula.",
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        }, 1, header.RowCount);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var save = new Button { Text = "Salvar grupo", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        Controls.Add(_contacts);
        Controls.Add(header);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;

        _id.Text = definition.Id;
        _name.Text = definition.Name;
        foreach (var contact in definition.Contacts)
        {
            _contacts.Rows.Add(
                contact.Name,
                Join(contact, ChannelType.EvolutionApi),
                Join(contact, ChannelType.Email),
                Join(contact, ChannelType.Telegram));
        }
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.Controls.Add(VisualEditorSupport.LabelFor(label), 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
    }

    private static string Join(ContactDefinition contact, ChannelType type) =>
        contact.Addresses.TryGetValue(type, out var values) ? string.Join("; ", values) : string.Empty;

    private void Save()
    {
        try
        {
            var id = _id.Text.Trim();
            var name = _name.Text.Trim();
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Informe o identificador do grupo.");
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Informe o nome do grupo.");

            var contacts = new List<ContactDefinition>();
            foreach (DataGridViewRow row in _contacts.Rows)
            {
                if (row.IsNewRow) continue;
                var contactName = Convert.ToString(row.Cells[0].Value)?.Trim();
                if (string.IsNullOrWhiteSpace(contactName)) continue;
                var contact = new ContactDefinition { Name = contactName };
                AddAddresses(contact, ChannelType.EvolutionApi, Convert.ToString(row.Cells[1].Value));
                AddAddresses(contact, ChannelType.Email, Convert.ToString(row.Cells[2].Value));
                AddAddresses(contact, ChannelType.Telegram, Convert.ToString(row.Cells[3].Value));
                contacts.Add(contact);
            }

            Definition = new ContactGroupDefinition { Id = id, Name = name, Contacts = contacts };
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Validação do grupo");
        }
    }

    private static void AddAddresses(ContactDefinition contact, ChannelType type, string? raw)
    {
        var values = (raw ?? string.Empty)
            .Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (values.Count > 0) contact.Addresses[type] = values;
    }
}
