using System.Text;
using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal enum ContactManagerStartAction
{
    None,
    NewContact,
    NewGroup,
    ShowGroups,
    ImportJson,
    ImportCsv,
    ExportJson,
    ExportCsv
}

internal sealed class ContactManagerForm : Form
{
    private readonly IContactDirectory _directory;
    private readonly IFlowStore _store;
    private readonly DataGridView _contactsGrid = new();
    private readonly DataGridView _groupsGrid = new();
    private readonly ToolStripStatusLabel _status = new("Pronto");
    private readonly Label _summary = new();
    private readonly TabControl _tabs = new();
    private readonly ContactManagerStartAction _startAction;

    private ContactDirectoryDefinition _snapshot = new();
    private IReadOnlyCollection<AutomationStoreItem> _automations = [];

    internal ContactManagerForm(
        IContactDirectory directory,
        IFlowStore store,
        ContactManagerStartAction startAction = ContactManagerStartAction.None)
    {
        _directory = directory;
        _store = store;
        _startAction = startAction;

        Text = "Contatos e grupos de notificação";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1080, 720);
        MinimumSize = new Size(900, 600);
        Font = new Font("Segoe UI", 9F);

        BuildLayout();
        Load += async (_, _) =>
        {
            await ReloadAsync();
            await ExecuteStartActionAsync();
        };
    }

    private async Task ExecuteStartActionAsync()
    {
        switch (_startAction)
        {
            case ContactManagerStartAction.NewContact:
                await AddContactAsync();
                break;
            case ContactManagerStartAction.NewGroup:
                _tabs.SelectedIndex = 1;
                await AddGroupAsync();
                break;
            case ContactManagerStartAction.ShowGroups:
                _tabs.SelectedIndex = 1;
                break;
            case ContactManagerStartAction.ImportJson:
                await ImportJsonAsync();
                break;
            case ContactManagerStartAction.ImportCsv:
                await ImportCsvAsync();
                break;
            case ContactManagerStartAction.ExportJson:
                await ExportJsonAsync();
                break;
            case ContactManagerStartAction.ExportCsv:
                await ExportCsvAsync();
                break;
        }
    }

    private void BuildLayout()
    {
        var toolbar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(8, 5, 8, 5),
            AutoSize = true
        };
        AddButton(toolbar, "Novo contato", async (_, _) => await AddContactAsync());
        AddButton(toolbar, "Novo grupo", async (_, _) => await AddGroupAsync());
        AddButton(toolbar, "Editar", async (_, _) => await EditSelectedAsync());
        AddButton(toolbar, "Excluir", async (_, _) => await DeleteSelectedAsync());
        toolbar.Items.Add(new ToolStripSeparator());

        var import = new ToolStripDropDownButton("Importar");
        AddMenuItem(import.DropDownItems, "Catálogo JSON...", async (_, _) => await ImportJsonAsync());
        AddMenuItem(import.DropDownItems, "Contatos CSV...", async (_, _) => await ImportCsvAsync());
        toolbar.Items.Add(import);

        var export = new ToolStripDropDownButton("Exportar");
        AddMenuItem(export.DropDownItems, "Catálogo JSON...", async (_, _) => await ExportJsonAsync());
        AddMenuItem(export.DropDownItems, "Contatos CSV...", async (_, _) => await ExportCsvAsync());
        toolbar.Items.Add(export);
        toolbar.Items.Add(new ToolStripSeparator());
        AddButton(toolbar, "Atualizar", async (_, _) => await ReloadAsync());

        _summary.AutoSize = true;
        _summary.Padding = new Padding(14, 10, 14, 8);
        _summary.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
        _summary.ForeColor = Color.FromArgb(35, 65, 95);

        ConfigureContactsGrid();
        ConfigureGroupsGrid();

        _tabs.Dock = DockStyle.Fill;
        var contactsTab = new TabPage("Contatos") { Padding = new Padding(8) };
        contactsTab.Controls.Add(_contactsGrid);
        var groupsTab = new TabPage("Grupos") { Padding = new Padding(8) };
        groupsTab.Controls.Add(_groupsGrid);
        _tabs.TabPages.Add(contactsTab);
        _tabs.TabPages.Add(groupsTab);

        _contactsGrid.CellDoubleClick += async (_, _) => await EditContactAsync();
        _groupsGrid.CellDoubleClick += async (_, _) => await EditGroupAsync();

        var status = new StatusStrip();
        status.Items.Add(_status);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(_summary, 0, 1);
        root.Controls.Add(_tabs, 0, 2);
        root.Controls.Add(status, 0, 3);
        Controls.Add(root);
    }

    private void ConfigureContactsGrid()
    {
        ConfigureGrid(_contactsGrid);
        _contactsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ContactRow.Name), HeaderText = "Nome", FillWeight = 130 });
        _contactsGrid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(ContactRow.Enabled), HeaderText = "Ativo", FillWeight = 38 });
        _contactsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ContactRow.WhatsApp), HeaderText = "WhatsApp", FillWeight = 130 });
        _contactsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ContactRow.Email), HeaderText = "E-mail", FillWeight = 150 });
        _contactsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ContactRow.Telegram), HeaderText = "Telegram", FillWeight = 95 });
        _contactsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ContactRow.Access), HeaderText = "Permissão", FillWeight = 120 });
    }

    private void ConfigureGroupsGrid()
    {
        ConfigureGrid(_groupsGrid);
        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(GroupRow.Name), HeaderText = "Grupo", FillWeight = 140 });
        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(GroupRow.Id), HeaderText = "Identificador", FillWeight = 95 });
        _groupsGrid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(GroupRow.Enabled), HeaderText = "Ativo", FillWeight = 38 });
        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(GroupRow.ContactCount), HeaderText = "Contatos", FillWeight = 55 });
        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(GroupRow.ContactNames), HeaderText = "Participantes", FillWeight = 190 });
        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(GroupRow.Access), HeaderText = "Permissão", FillWeight = 120 });
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoGenerateColumns = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor = SystemColors.Window;
        grid.BorderStyle = BorderStyle.None;
        grid.RowHeadersVisible = false;
        grid.RowTemplate.Height = 31;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 249, 252);
    }

    private async Task ReloadAsync()
    {
        await RunBusyAsync("Carregando catálogo...", async () =>
        {
            _snapshot = await _directory.GetSnapshotAsync(CancellationToken.None);
            _automations = await _store.GetAutomationsAsync(CancellationToken.None);
            BindData();
        });
    }

    private void BindData()
    {
        _contactsGrid.DataSource = _snapshot.Contacts
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ContactRow
            {
                Id = x.Id,
                Name = x.Name,
                Enabled = x.Enabled,
                WhatsApp = JoinAddresses(x, ChannelType.EvolutionApi),
                Email = JoinAddresses(x, ChannelType.Email),
                Telegram = JoinAddresses(x, ChannelType.Telegram),
                Access = AccessText(x.AccessScope, x.AllowedAutomationIds.Count)
            })
            .ToList();

        var contactsById = _snapshot.Contacts.ToDictionary(x => x.Id);
        _groupsGrid.DataSource = _snapshot.Groups
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new GroupRow
            {
                Id = x.Id,
                Name = x.Name,
                Enabled = x.Enabled,
                ContactCount = x.ContactIds.Count + x.Contacts.Count,
                ContactNames = string.Join(", ", x.ContactIds
                    .Select(id => contactsById.GetValueOrDefault(id)?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Concat(x.Contacts.Select(contact => contact.Name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)),
                Access = AccessText(x.AccessScope, x.AllowedAutomationIds.Count)
            })
            .ToList();

        _summary.Text = $"{_snapshot.Contacts.Count:N0} contato(s)    |    {_snapshot.Groups.Count:N0} grupo(s)    |    {_automations.Count:N0} automação(ões) disponível(is) para autorização";
    }

    private async Task AddContactAsync()
    {
        using var editor = new ContactEditorForm(new ContactDefinition { Enabled = true }, _automations);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Definition is null)
        {
            return;
        }

        _snapshot.Contacts.Add(editor.Definition);
        await SaveAndReloadAsync("Contato incluído.");
    }

    private async Task EditContactAsync()
    {
        var row = _contactsGrid.CurrentRow?.DataBoundItem as ContactRow;
        if (row is null)
        {
            return;
        }

        var current = _snapshot.Contacts.FirstOrDefault(x => x.Id == row.Id);
        if (current is null)
        {
            return;
        }

        using var editor = new ContactEditorForm(Clone(current), _automations);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Definition is null)
        {
            return;
        }

        var index = _snapshot.Contacts.FindIndex(x => x.Id == row.Id);
        _snapshot.Contacts[index] = editor.Definition;
        await SaveAndReloadAsync("Contato atualizado.");
    }

    private async Task AddGroupAsync()
    {
        using var editor = new DirectoryContactGroupEditorForm(
            new ContactGroupDefinition { Enabled = true },
            _snapshot.Contacts,
            _automations);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Definition is null)
        {
            return;
        }

        if (_snapshot.Groups.Any(x => string.Equals(x.Id, editor.Definition.Id, StringComparison.OrdinalIgnoreCase)))
        {
            VisualEditorSupport.ShowError(this, new InvalidOperationException("Já existe um grupo com esse identificador."), "Grupo de contatos");
            return;
        }

        _snapshot.Groups.Add(editor.Definition);
        await SaveAndReloadAsync("Grupo incluído.");
    }

    private async Task EditGroupAsync()
    {
        var row = _groupsGrid.CurrentRow?.DataBoundItem as GroupRow;
        if (row is null)
        {
            return;
        }

        var current = _snapshot.Groups.FirstOrDefault(x => string.Equals(x.Id, row.Id, StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            return;
        }

        using var editor = new DirectoryContactGroupEditorForm(Clone(current), _snapshot.Contacts, _automations);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Definition is null)
        {
            return;
        }

        if (_snapshot.Groups.Any(x => !ReferenceEquals(x, current) &&
                                      string.Equals(x.Id, editor.Definition.Id, StringComparison.OrdinalIgnoreCase)))
        {
            VisualEditorSupport.ShowError(this, new InvalidOperationException("Já existe outro grupo com esse identificador."), "Grupo de contatos");
            return;
        }

        var index = _snapshot.Groups.FindIndex(x => string.Equals(x.Id, row.Id, StringComparison.OrdinalIgnoreCase));
        _snapshot.Groups[index] = editor.Definition;
        await SaveAndReloadAsync("Grupo atualizado.");
    }

    private async Task EditSelectedAsync()
    {
        if (_tabs.SelectedIndex == 0)
        {
            await EditContactAsync();
        }
        else
        {
            await EditGroupAsync();
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_tabs.SelectedIndex == 0)
        {
            await DeleteContactAsync();
        }
        else
        {
            await DeleteGroupAsync();
        }
    }

    private async Task DeleteContactAsync()
    {
        var row = _contactsGrid.CurrentRow?.DataBoundItem as ContactRow;
        if (row is null)
        {
            return;
        }

        var groupNames = _snapshot.Groups
            .Where(x => x.ContactIds.Contains(row.Id))
            .Select(x => x.Name)
            .ToArray();
        var detail = groupNames.Length == 0
            ? string.Empty
            : $"\n\nO contato também será removido dos grupos: {string.Join(", ", groupNames)}.";

        if (MessageBox.Show(this, $"Excluir o contato '{row.Name}'?{detail}", "Confirmação",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        _snapshot.Contacts.RemoveAll(x => x.Id == row.Id);
        foreach (var group in _snapshot.Groups)
        {
            group.ContactIds.RemoveAll(x => x == row.Id);
        }
        _snapshot.Groups.RemoveAll(x => x.ContactIds.Count == 0 && x.Contacts.Count == 0);
        await SaveAndReloadAsync("Contato excluído.");
    }

    private async Task DeleteGroupAsync()
    {
        var row = _groupsGrid.CurrentRow?.DataBoundItem as GroupRow;
        if (row is null || MessageBox.Show(this, $"Excluir o grupo '{row.Name}'?", "Confirmação",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        _snapshot.Groups.RemoveAll(x => string.Equals(x.Id, row.Id, StringComparison.OrdinalIgnoreCase));
        await SaveAndReloadAsync("Grupo excluído.");
    }

    private async Task ImportJsonAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Catálogo de contatos (*.json)|*.json|Todos os arquivos (*.*)|*.*",
            Title = "Importar catálogo de contatos"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var imported = JsonSerializer.Deserialize<ContactDirectoryDefinition>(
                               await File.ReadAllTextAsync(dialog.FileName), FlowJson.Options)
                           ?? throw new InvalidOperationException("O arquivo não contém um catálogo de contatos válido.");
            imported.Validate();

            var choice = MessageBox.Show(this,
                "Selecione Sim para mesclar com o catálogo atual.\nSelecione Não para substituir completamente o catálogo.",
                "Importação de contatos", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (choice == DialogResult.Cancel)
            {
                return;
            }

            _snapshot = choice == DialogResult.Yes ? Merge(_snapshot, imported) : imported;
            await SaveAndReloadAsync("Catálogo importado.");
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Importação de contatos");
        }
    }

    private async Task ExportJsonAsync()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Catálogo de contatos (*.json)|*.json",
            FileName = $"FlowSentinel-contatos-{DateTime.Now:yyyyMMdd-HHmm}.json",
            Title = "Exportar catálogo de contatos"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(_snapshot, FlowJson.Options));
        _status.Text = "Catálogo exportado.";
    }

    private async Task ImportCsvAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Contatos CSV (*.csv)|*.csv|Todos os arquivos (*.*)|*.*",
            Title = "Importar contatos CSV"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(dialog.FileName, Encoding.UTF8);
            if (lines.Length == 0)
            {
                throw new InvalidOperationException("O arquivo CSV está vazio.");
            }

            var header = ParseCsvLine(lines[0]);
            var columns = header.Select((value, index) => new { value, index })
                .ToDictionary(x => x.value.Trim(), x => x.index, StringComparer.OrdinalIgnoreCase);
            if (!columns.ContainsKey("Nome"))
            {
                throw new InvalidOperationException("O CSV precisa possuir a coluna 'Nome'.");
            }

            var workingCopy = Clone(_snapshot);
            var imported = 0;
            for (var lineNumber = 1; lineNumber < lines.Length; lineNumber++)
            {
                if (string.IsNullOrWhiteSpace(lines[lineNumber]))
                {
                    continue;
                }
                var values = ParseCsvLine(lines[lineNumber]);
                var name = GetCsv(values, columns, "Nome").Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var contact = workingCopy.Contacts.FirstOrDefault(x =>
                    string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (contact is null)
                {
                    contact = new ContactDefinition { Name = name, Enabled = true };
                    workingCopy.Contacts.Add(contact);
                }
                else
                {
                    contact.Addresses.Clear();
                }

                AddCsvAddresses(contact, ChannelType.EvolutionApi, GetCsv(values, columns, "WhatsApp"));
                AddCsvAddresses(contact, ChannelType.Email, GetCsvAlias(values, columns, "Email", "E-mail"));
                AddCsvAddresses(contact, ChannelType.Telegram, GetCsv(values, columns, "Telegram"));
                contact.Notes = GetCsvAlias(values, columns, "Observacoes", "Observações");
                if (!contact.Addresses.Any())
                {
                    throw new InvalidOperationException($"A linha {lineNumber + 1} não possui nenhum endereço de notificação.");
                }
                imported++;
            }

            workingCopy.Validate();
            _snapshot = workingCopy;
            await SaveAndReloadAsync($"{imported:N0} contato(s) importado(s) do CSV.");
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Importação CSV");
        }
    }

    private async Task ExportCsvAsync()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Contatos CSV (*.csv)|*.csv",
            FileName = $"FlowSentinel-contatos-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            Title = "Exportar contatos CSV"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Nome;WhatsApp;Email;Telegram;Observacoes");
        foreach (var contact in _snapshot.Contacts.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(string.Join(";", new[]
            {
                Csv(contact.Name),
                Csv(JoinAddresses(contact, ChannelType.EvolutionApi)),
                Csv(JoinAddresses(contact, ChannelType.Email)),
                Csv(JoinAddresses(contact, ChannelType.Telegram)),
                Csv(contact.Notes)
            }));
        }
        await File.WriteAllTextAsync(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
        _status.Text = "Contatos exportados para CSV.";
    }

    private async Task SaveAndReloadAsync(string successMessage)
    {
        await RunBusyAsync("Salvando catálogo...", async () =>
        {
            await _directory.SaveAsync(_snapshot, CancellationToken.None);
            _snapshot = await _directory.GetSnapshotAsync(CancellationToken.None);
            BindData();
            _status.Text = successMessage;
        }, keepSuccessStatus: true);
    }

    private async Task RunBusyAsync(string message, Func<Task> operation, bool keepSuccessStatus = false)
    {
        try
        {
            UseWaitCursor = true;
            _status.Text = message;
            await operation();
            if (!keepSuccessStatus)
            {
                _status.Text = "Pronto";
            }
        }
        catch (Exception exception)
        {
            _status.Text = "Erro";
            VisualEditorSupport.ShowError(this, exception, "Catálogo de contatos");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static ContactDirectoryDefinition Merge(ContactDirectoryDefinition current, ContactDirectoryDefinition imported)
    {
        var result = Clone(current);
        var contactIdMap = new Dictionary<Guid, Guid>();

        foreach (var importedContact in imported.Contacts)
        {
            var existing = result.Contacts.FirstOrDefault(x => x.Id == importedContact.Id) ??
                           result.Contacts.FirstOrDefault(x =>
                               string.Equals(x.Name, importedContact.Name, StringComparison.OrdinalIgnoreCase));
            var mergedContact = Clone(importedContact);
            if (existing is null)
            {
                result.Contacts.Add(mergedContact);
                contactIdMap[importedContact.Id] = mergedContact.Id;
            }
            else
            {
                mergedContact.Id = existing.Id;
                var index = result.Contacts.IndexOf(existing);
                result.Contacts[index] = mergedContact;
                contactIdMap[importedContact.Id] = existing.Id;
            }
        }

        foreach (var importedGroup in imported.Groups)
        {
            var mergedGroup = Clone(importedGroup);
            mergedGroup.ContactIds = mergedGroup.ContactIds
                .Select(id => contactIdMap.GetValueOrDefault(id, id))
                .Distinct()
                .ToList();

            var existing = result.Groups.FindIndex(x =>
                string.Equals(x.Id, mergedGroup.Id, StringComparison.OrdinalIgnoreCase));
            if (existing < 0)
            {
                result.Groups.Add(mergedGroup);
            }
            else
            {
                result.Groups[existing] = mergedGroup;
            }
        }

        result.Validate();
        return result;
    }

    private static string AccessText(ContactAccessScope scope, int count) => scope == ContactAccessScope.AllAutomations
        ? "Todas as automações"
        : $"{count:N0} automação(ões) autorizada(s)";

    private static string JoinAddresses(ContactDefinition definition, ChannelType channelType) =>
        definition.Addresses.TryGetValue(channelType, out var values) ? string.Join("; ", values) : string.Empty;

    private static void AddCsvAddresses(ContactDefinition contact, ChannelType channelType, string value)
    {
        var values = value.Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (values.Count > 0)
        {
            contact.Addresses[channelType] = values;
        }
    }

    private static string GetCsv(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> columns, string column) =>
        columns.TryGetValue(column, out var index) && index < values.Count ? values[index] : string.Empty;

    private static string GetCsvAlias(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columns,
        params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var value = GetCsv(values, columns, alias);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return string.Empty;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ';' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }
        values.Add(current.ToString());
        return values;
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        return text.IndexOfAny([';', '"', '\r', '\n']) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, FlowJson.Options);
        return JsonSerializer.Deserialize<T>(json, FlowJson.Options)
               ?? throw new InvalidOperationException("Não foi possível copiar os dados do catálogo.");
    }

    private static void AddButton(ToolStrip toolbar, string text, EventHandler handler)
    {
        var button = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        button.Click += handler;
        toolbar.Items.Add(button);
    }

    private static void AddMenuItem(ToolStripItemCollection items, string text, EventHandler handler)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += handler;
        items.Add(item);
    }

    private sealed class ContactRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool Enabled { get; init; }
        public string WhatsApp { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Telegram { get; init; } = string.Empty;
        public string Access { get; init; } = string.Empty;
    }

    private sealed class GroupRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public bool Enabled { get; init; }
        public int ContactCount { get; init; }
        public string ContactNames { get; init; } = string.Empty;
        public string Access { get; init; } = string.Empty;
    }
}
