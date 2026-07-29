using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class RecipientSelectionForm : Form
{
    private readonly Guid _automationId;
    private readonly ChannelType _channelType;
    private readonly ContactDirectoryDefinition _directory;
    private readonly CheckedListBox _contacts = new();
    private readonly CheckedListBox _groups = new();
    private readonly TextBox _manual = new();
    private readonly Label _summary = new();

    internal IReadOnlyList<RecipientDefinition> Recipients { get; private set; } = [];

    internal RecipientSelectionForm(
        Guid automationId,
        ChannelType channelType,
        ContactDirectoryDefinition directory,
        IReadOnlyCollection<RecipientDefinition>? selectedRecipients = null)
    {
        _automationId = automationId;
        _channelType = channelType;
        _directory = VisualEditorSupport.Clone(directory);

        Text = $"Destinatários — {VisualEditorSupport.ChannelTypeText(channelType)}";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(940, 650);
        MinimumSize = new Size(780, 540);
        Font = new Font("Segoe UI", 9F);

        ConfigureControls();
        BuildLayout();
        LoadRecipients(selectedRecipients ?? []);
        UpdateSummary();
    }

    private void ConfigureControls()
    {
        foreach (var list in new[] { _contacts, _groups })
        {
            list.Dock = DockStyle.Fill;
            list.CheckOnClick = true;
            list.IntegralHeight = false;
            list.ItemCheck += (_, _) => BeginInvoke(new Action(UpdateSummary));
        }

        var contacts = _directory.Contacts
            .Where(x => x.CanBeUsedBy(_automationId) && HasAddress(x, _channelType))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ContactItem(x, _channelType))
            .ToArray();
        _contacts.Items.AddRange(contacts);

        var groups = _directory.Groups
            .Where(x => x.CanBeUsedBy(_automationId) && GroupHasAddress(x, _channelType))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new GroupItem(x))
            .ToArray();
        _groups.Items.AddRange(groups);

        _manual.Dock = DockStyle.Fill;
        _manual.Multiline = true;
        _manual.AcceptsReturn = true;
        _manual.ScrollBars = ScrollBars.Vertical;
        _manual.PlaceholderText = ChannelPlaceholder(_channelType);
        _manual.TextChanged += (_, _) => UpdateSummary();

        _summary.AutoSize = true;
        _summary.ForeColor = Color.FromArgb(45, 70, 95);
        _summary.Font = new Font(Font, FontStyle.Bold);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(880, 0),
            Text = "Selecione contatos ou grupos reutilizáveis do catálogo. Também é possível informar endereços manuais somente para este monitoramento.",
            ForeColor = Color.DimGray,
            Padding = new Padding(0, 0, 0, 10)
        }, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var contactsTab = new TabPage("Contatos") { Padding = new Padding(8) };
        contactsTab.Controls.Add(_contacts);
        var groupsTab = new TabPage("Grupos") { Padding = new Padding(8) };
        groupsTab.Controls.Add(_groups);
        var manualTab = new TabPage("Inserção manual") { Padding = new Padding(8) };
        manualTab.Controls.Add(_manual);
        tabs.TabPages.AddRange([contactsTab, groupsTab, manualTab]);
        root.Controls.Add(tabs, 0, 1);

        root.Controls.Add(_summary, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };
        var save = new Button { Text = "Aplicar destinatários", AutoSize = true, Height = 32 };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, Height = 32, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 3);

        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void LoadRecipients(IReadOnlyCollection<RecipientDefinition> recipients)
    {
        var contactIds = recipients
            .Where(x => x.Type == RecipientType.Contact && AppliesToChannel(x))
            .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToHashSet();
        for (var index = 0; index < _contacts.Items.Count; index++)
        {
            if (_contacts.Items[index] is ContactItem item && contactIds.Contains(item.Definition.Id))
            {
                _contacts.SetItemChecked(index, true);
            }
        }

        var groupIds = recipients
            .Where(x => x.Type == RecipientType.Group && AppliesToChannel(x))
            .Select(x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < _groups.Items.Count; index++)
        {
            if (_groups.Items[index] is GroupItem item && groupIds.Contains(item.Definition.Id))
            {
                _groups.SetItemChecked(index, true);
            }
        }

        _manual.Text = string.Join(Environment.NewLine, recipients
            .Where(x => x.Type == RecipientType.Fixed && AppliesToChannel(x))
            .SelectMany(x => SplitAddresses(x.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private void Save()
    {
        var recipients = new List<RecipientDefinition>();
        recipients.AddRange(_contacts.CheckedItems.Cast<ContactItem>().Select(x => new RecipientDefinition
        {
            Type = RecipientType.Contact,
            Value = x.Definition.Id.ToString("D"),
            DisplayName = x.Definition.Name,
            ChannelType = _channelType
        }));
        recipients.AddRange(_groups.CheckedItems.Cast<GroupItem>().Select(x => new RecipientDefinition
        {
            Type = RecipientType.Group,
            Value = x.Definition.Id,
            DisplayName = x.Definition.Name,
            ChannelType = _channelType
        }));

        var manualAddresses = SplitAddresses(_manual.Text).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (manualAddresses.Length > 0)
        {
            recipients.Add(new RecipientDefinition
            {
                Type = RecipientType.Fixed,
                Value = string.Join(";", manualAddresses),
                DisplayName = "Destinatário informado no monitoramento",
                ChannelType = _channelType
            });
        }

        Recipients = recipients;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateSummary()
    {
        var manualCount = SplitAddresses(_manual.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var total = _contacts.CheckedItems.Count + _groups.CheckedItems.Count + manualCount;
        _summary.Text = total == 0
            ? "Nenhum destinatário selecionado."
            : $"Seleção atual: {_contacts.CheckedItems.Count} contato(s), {_groups.CheckedItems.Count} grupo(s) e {manualCount} endereço(s) manual(is).";
    }

    private bool GroupHasAddress(ContactGroupDefinition group, ChannelType channelType)
    {
        if (group.Contacts.Any(x => x.CanBeUsedBy(_automationId) && HasAddress(x, channelType)))
        {
            return true;
        }

        return group.ContactIds
            .Select(id => _directory.Contacts.FirstOrDefault(x => x.Id == id))
            .Any(x => x is not null && x.CanBeUsedBy(_automationId) && HasAddress(x, channelType));
    }

    private bool AppliesToChannel(RecipientDefinition recipient) =>
        !recipient.ChannelType.HasValue || recipient.ChannelType == _channelType;

    private static bool HasAddress(ContactDefinition contact, ChannelType channelType) =>
        contact.Addresses.TryGetValue(channelType, out var addresses) && addresses.Any(x => !string.IsNullOrWhiteSpace(x));

    private static IEnumerable<string> SplitAddresses(string? value) =>
        (value ?? string.Empty)
        .Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !string.IsNullOrWhiteSpace(x));

    private static string ChannelPlaceholder(ChannelType channelType) => channelType switch
    {
        ChannelType.EvolutionApi => "Um número de WhatsApp por linha. Ex.: +5575999999999",
        ChannelType.Email => "Um endereço de e-mail por linha.",
        ChannelType.Telegram => "Um Chat ID do Telegram por linha.",
        _ => "Um destinatário por linha."
    };

    private sealed class ContactItem
    {
        internal ContactDefinition Definition { get; }
        private readonly ChannelType _channelType;
        internal ContactItem(ContactDefinition definition, ChannelType channelType)
        {
            Definition = definition;
            _channelType = channelType;
        }
        public override string ToString() => $"{Definition.Name} — {string.Join(", ", Definition.Addresses.GetValueOrDefault(_channelType, []))}";
    }

    private sealed class GroupItem
    {
        internal ContactGroupDefinition Definition { get; }
        internal GroupItem(ContactGroupDefinition definition) => Definition = definition;
        public override string ToString() => Definition.Name;
    }
}
