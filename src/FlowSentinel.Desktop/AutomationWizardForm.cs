using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class AutomationWizardForm : Form
{
    private readonly IFlowStore _store;
    private readonly ISourceDesignerService _sourceDesigner;
    private readonly ISecretProtector _secretProtector;
    private AutomationDefinition _working;

    private readonly TextBox _name = new();
    private readonly TextBox _description = new();
    private readonly CheckBox _enabled = new();
    private readonly NumericUpDown _intervalValue = new();
    private readonly ComboBox _intervalUnit = new();
    private readonly NumericUpDown _priority = new();
    private readonly ComboBox _missingRecord = new();
    private readonly CheckBox _resolvePersistence = new();

    private readonly DataGridView _sources = new();
    private readonly RuleSetEditorControl _entryRules = new();
    private readonly RuleSetEditorControl _persistenceRules = new();
    private readonly RuleSetEditorControl _completionRules = new();
    private readonly RuleSetEditorControl _suspensionRules = new();
    private readonly DataGridView _actions = new();
    private readonly DataGridView _groups = new();
    private readonly TextBox _json = new();
    private readonly TextBox _review = new();
    private readonly TabControl _tabs = new();

    internal AutomationDefinition? Definition { get; private set; }

    internal AutomationWizardForm(
        AutomationDefinition definition,
        IFlowStore store,
        ISourceDesignerService sourceDesigner,
        ISecretProtector secretProtector)
    {
        _working = VisualEditorSupport.Clone(definition);
        _store = store;
        _sourceDesigner = sourceDesigner;
        _secretProtector = secretProtector;

        Text = definition.Id == Guid.Empty || definition.Name == "Nova automação"
            ? "Nova automação — Assistente visual"
            : $"Editar automação — {definition.Name}";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1180, 800);
        MinimumSize = new Size(1020, 700);

        ConfigureControls();
        BuildLayout();
        LoadDefinition(_working);
    }

    private void ConfigureControls()
    {
        _description.Multiline = true;
        _description.Height = 80;
        _description.ScrollBars = ScrollBars.Vertical;
        _enabled.Text = "Automação ativa";
        _enabled.AutoSize = true;
        _intervalValue.Minimum = 1;
        _intervalValue.Maximum = 1000000;
        _intervalUnit.DropDownStyle = ComboBoxStyle.DropDownList;
        _intervalUnit.Items.AddRange(["Segundos", "Minutos", "Horas", "Dias"]);
        _priority.Minimum = 0;
        _priority.Maximum = 100000;
        _missingRecord.DropDownStyle = ComboBoxStyle.DropDownList;
        VisualEditorSupport.SetDisplayItems(_missingRecord, new[]
        {
            new DisplayItem<MissingRecordBehavior>(MissingRecordBehavior.Ignore, "Ignorar o desaparecimento"),
            new DisplayItem<MissingRecordBehavior>(MissingRecordBehavior.Resolve, "Concluir a ocorrência")
        });
        _resolvePersistence.Text = "Concluir quando os critérios de permanência deixarem de ser atendidos";
        _resolvePersistence.AutoSize = true;

        ConfigureSourcesGrid();
        ConfigureActionsGrid();
        ConfigureGroupsGrid();

        _json.Multiline = true;
        _json.AcceptsTab = true;
        _json.ScrollBars = ScrollBars.Both;
        _json.WordWrap = false;
        _json.Font = new Font("Consolas", 10);
        _json.Dock = DockStyle.Fill;

        _review.Multiline = true;
        _review.ReadOnly = true;
        _review.ScrollBars = ScrollBars.Both;
        _review.WordWrap = false;
        _review.Font = new Font("Consolas", 10);
        _review.Dock = DockStyle.Fill;
    }

    private void ConfigureSourcesGrid()
    {
        _sources.Dock = DockStyle.Fill;
        _sources.ReadOnly = true;
        _sources.AllowUserToAddRows = false;
        _sources.AllowUserToDeleteRows = false;
        _sources.MultiSelect = false;
        _sources.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _sources.AutoGenerateColumns = false;
        _sources.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _sources.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SourceRow.Name), HeaderText = "Nome", FillWeight = 150 });
        _sources.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SourceRow.Type), HeaderText = "Tipo", FillWeight = 90 });
        _sources.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SourceRow.Alias), HeaderText = "Alias", FillWeight = 90 });
        _sources.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SourceRow.Keys), HeaderText = "Campos-chave", FillWeight = 160 });
        _sources.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(SourceRow.Primary), HeaderText = "Principal", FillWeight = 45 });
        _sources.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(SourceRow.Enabled), HeaderText = "Ativa", FillWeight = 40 });
        _sources.CellDoubleClick += async (_, _) => await EditSourceAsync();
    }

    private void ConfigureActionsGrid()
    {
        _actions.Dock = DockStyle.Fill;
        _actions.ReadOnly = true;
        _actions.AllowUserToAddRows = false;
        _actions.AllowUserToDeleteRows = false;
        _actions.MultiSelect = false;
        _actions.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _actions.AutoGenerateColumns = false;
        _actions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _actions.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ActionRow.Name), HeaderText = "Ação", FillWeight = 160 });
        _actions.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ActionRow.Trigger), HeaderText = "Disparo", FillWeight = 90 });
        _actions.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ActionRow.Channels), HeaderText = "Canais", FillWeight = 180 });
        _actions.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ActionRow.Recipients), HeaderText = "Destinatários", FillWeight = 80 });
        _actions.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(ActionRow.Enabled), HeaderText = "Ativa", FillWeight = 40 });
        _actions.CellDoubleClick += async (_, _) => await EditActionAsync();
    }


    private void ConfigureGroupsGrid()
    {
        _groups.Dock = DockStyle.Fill;
        _groups.ReadOnly = true;
        _groups.AllowUserToAddRows = false;
        _groups.AllowUserToDeleteRows = false;
        _groups.MultiSelect = false;
        _groups.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _groups.AutoGenerateColumns = false;
        _groups.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _groups.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(GroupRow.Id), HeaderText = "Identificador", FillWeight = 100 });
        _groups.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(GroupRow.Name), HeaderText = "Grupo", FillWeight = 170 });
        _groups.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(GroupRow.Contacts), HeaderText = "Contatos", FillWeight = 60 });
        _groups.CellDoubleClick += (_, _) => EditGroup();
    }

    private void BuildLayout()
    {
        _tabs.Dock = DockStyle.Fill;
        _tabs.TabPages.Add(BuildGeneralTab());
        _tabs.TabPages.Add(BuildSourcesTab());
        _tabs.TabPages.Add(BuildRulesTab());
        _tabs.TabPages.Add(BuildActionsTab());
        _tabs.TabPages.Add(BuildGroupsTab());
        _tabs.TabPages.Add(BuildReviewTab());
        _tabs.TabPages.Add(BuildAdvancedTab());
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            if (_tabs.SelectedTab?.Text == "Revisão") UpdateReview();
            if (_tabs.SelectedTab?.Text == "Avançado") UpdateJson();
        };

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var navigation = new FlowLayoutPanel { AutoSize = true };
        var previous = new Button { Text = "← Voltar", AutoSize = true };
        var next = new Button { Text = "Avançar →", AutoSize = true };
        previous.Click += (_, _) => _tabs.SelectedIndex = Math.Max(0, _tabs.SelectedIndex - 1);
        next.Click += (_, _) => _tabs.SelectedIndex = Math.Min(_tabs.TabPages.Count - 1, _tabs.SelectedIndex + 1);
        navigation.Controls.Add(previous);
        navigation.Controls.Add(next);

        var actions = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Salvar automação", AutoSize = true };
        var draft = new Button { Text = "Salvar desativada", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save(forceDisabled: false);
        draft.Click += (_, _) => Save(forceDisabled: true);
        actions.Controls.Add(save);
        actions.Controls.Add(draft);
        actions.Controls.Add(cancel);
        bottom.Controls.Add(navigation, 0, 0);
        bottom.Controls.Add(actions, 1, 0);

        Controls.Add(_tabs);
        Controls.Add(bottom);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private TabPage BuildGeneralTab()
    {
        var tab = new TabPage("1. Geral");
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(18) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, "Nome da automação", _name);
        AddRow(table, "Descrição", _description);
        AddRow(table, "Situação", _enabled);
        AddRow(table, "Intervalo de verificação", PeriodPanel(_intervalValue, _intervalUnit));
        AddRow(table, "Prioridade", _priority);
        AddRow(table, "Quando o registro desaparecer", _missingRecord);
        AddRow(table, "Critério de permanência", _resolvePersistence);
        table.Controls.Add(new Label
        {
            Text = "O intervalo mínimo é de 5 segundos. Para uso normal, prefira minutos para manter baixo consumo de CPU e disco.",
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            Padding = new Padding(0, 12, 0, 0)
        }, 1, table.RowCount);
        tab.Controls.Add(table);
        return tab;
    }

    private TabPage BuildSourcesTab()
    {
        var tab = new TabPage("2. Fontes de dados");
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        AddButton(toolbar, "Adicionar fonte", async (_, _) => await AddSourceAsync());
        AddButton(toolbar, "Editar fonte", async (_, _) => await EditSourceAsync());
        AddButton(toolbar, "Duplicar", (_, _) => DuplicateSource());
        AddButton(toolbar, "Definir principal", (_, _) => SetPrimarySource());
        AddButton(toolbar, "Excluir", (_, _) => DeleteSource());
        AddButton(toolbar, "Testar selecionada", async (_, _) => await TestSelectedSourceAsync());
        var help = new Label
        {
            Text = "Cadastre planilhas Excel, CSV, TXT ou bancos de dados. Cada automação precisa ter exatamente uma fonte principal.",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10)
        };
        tab.Controls.Add(_sources);
        tab.Controls.Add(help);
        tab.Controls.Add(toolbar);
        return tab;
    }

    private TabPage BuildRulesTab()
    {
        var tab = new TabPage("3. Critérios");
        var inner = new TabControl { Dock = DockStyle.Fill };
        AddRulePage(inner, "Abertura", _entryRules);
        AddRulePage(inner, "Permanência", _persistenceRules);
        AddRulePage(inner, "Conclusão", _completionRules);
        AddRulePage(inner, "Suspensão", _suspensionRules);
        tab.Controls.Add(inner);
        return tab;
    }

    private static void AddRulePage(TabControl tabs, string title, RuleSetEditorControl control)
    {
        var page = new TabPage(title);
        page.Controls.Add(control);
        tabs.TabPages.Add(page);
    }

    private TabPage BuildActionsTab()
    {
        var tab = new TabPage("4. Ações e notificações");
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        AddButton(toolbar, "Adicionar ação", async (_, _) => await AddActionAsync());
        AddButton(toolbar, "Editar ação", async (_, _) => await EditActionAsync());
        AddButton(toolbar, "Duplicar", (_, _) => DuplicateAction());
        AddButton(toolbar, "Excluir", (_, _) => DeleteAction());
        var help = new Label
        {
            Text = "Uma ação pode usar vários canais e vários destinatários. Os canais precisam ser cadastrados previamente na tela principal.",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10)
        };
        tab.Controls.Add(_actions);
        tab.Controls.Add(help);
        tab.Controls.Add(toolbar);
        return tab;
    }


    private TabPage BuildGroupsTab()
    {
        var tab = new TabPage("5. Contatos e grupos");
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        AddButton(toolbar, "Adicionar grupo", (_, _) => AddGroup());
        AddButton(toolbar, "Editar grupo", (_, _) => EditGroup());
        AddButton(toolbar, "Excluir grupo", (_, _) => DeleteGroup());
        var help = new Label
        {
            Text = "Grupos podem conter vários contatos e endereços por canal. Nas ações, selecione destinatário do tipo Grupo e informe este identificador.",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10)
        };
        tab.Controls.Add(_groups);
        tab.Controls.Add(help);
        tab.Controls.Add(toolbar);
        return tab;
    }

    private TabPage BuildReviewTab()
    {
        var tab = new TabPage("Revisão");
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        var validate = new Button { Text = "Validar configuração", AutoSize = true };
        validate.Click += (_, _) => ValidateAndShow();
        toolbar.Controls.Add(validate);
        tab.Controls.Add(_review);
        tab.Controls.Add(toolbar);
        return tab;
    }

    private TabPage BuildAdvancedTab()
    {
        var tab = new TabPage("Avançado");
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        var refresh = new Button { Text = "Atualizar JSON pela interface", AutoSize = true };
        var apply = new Button { Text = "Aplicar JSON à interface", AutoSize = true };
        refresh.Click += (_, _) => UpdateJson();
        apply.Click += (_, _) => ApplyJson();
        toolbar.Controls.Add(refresh);
        toolbar.Controls.Add(apply);
        var help = new Label
        {
            Text = "Modo avançado. Use somente quando precisar de recursos ainda não expostos pela interface visual.",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10)
        };
        tab.Controls.Add(_json);
        tab.Controls.Add(help);
        tab.Controls.Add(toolbar);
        return tab;
    }

    private static FlowLayoutPanel PeriodPanel(Control value, Control unit)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        value.Width = 120;
        unit.Width = 140;
        panel.Controls.Add(value);
        panel.Controls.Add(unit);
        return panel;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(VisualEditorSupport.LabelFor(label), 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
    }

    private static void AddButton(Control parent, string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += handler;
        parent.Controls.Add(button);
    }

    private void LoadDefinition(AutomationDefinition definition)
    {
        _working = VisualEditorSupport.Clone(definition);
        _name.Text = definition.Name;
        _description.Text = definition.Description;
        _enabled.Checked = definition.Enabled;
        var interval = VisualEditorSupport.FromSeconds(definition.IntervalSeconds);
        _intervalValue.Value = Math.Clamp(interval.Value, _intervalValue.Minimum, _intervalValue.Maximum);
        _intervalUnit.SelectedItem = interval.Unit;
        _priority.Value = Math.Clamp(definition.Priority, 0, 100000);
        SelectDisplay(_missingRecord, definition.MissingRecordBehavior);
        _resolvePersistence.Checked = definition.ResolveWhenPersistenceFails;

        var fields = GetKnownFields();
        _entryRules.Configure("Criar uma ocorrência quando estes critérios forem atendidos.", definition.EntryRules, RuleSetType.Entry, fields);
        _persistenceRules.Configure("Continuar ativa enquanto estes critérios forem atendidos. Sem condições, a ocorrência permanece ativa.", definition.PersistenceRules, RuleSetType.Persistence, fields);
        _completionRules.Configure("Concluir quando estes critérios forem atendidos.", definition.CompletionRules, RuleSetType.Completion, fields);
        _suspensionRules.Configure("Suspender temporariamente quando estes critérios forem atendidos.", definition.SuspensionRules, RuleSetType.Suspension, fields);
        RefreshSourcesGrid();
        RefreshActionsGrid();
        RefreshGroupsGrid();
        UpdateReview();
        UpdateJson();
    }

    private async Task AddSourceAsync()
    {
        var source = new DataSourceDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Nova fonte",
            Alias = _working.Sources.Count == 0 ? "primary" : $"source{_working.Sources.Count + 1}",
            Type = SourceType.Excel,
            IsPrimary = _working.Sources.Count == 0,
            Enabled = true,
            KeyFields = []
        };
        using var editor = new SourceEditorForm(source, _sourceDesigner, _secretProtector);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Definition is not null)
        {
            if (editor.Definition.IsPrimary)
            {
                foreach (var item in _working.Sources) item.IsPrimary = false;
            }
            _working.Sources.Add(editor.Definition);
            RefreshSourcesAndFields();
        }
        await Task.CompletedTask;
    }

    private async Task EditSourceAsync()
    {
        var selected = SelectedSource();
        if (selected is null) return;
        var source = _working.Sources.FirstOrDefault(x => x.Id == selected.Id);
        if (source is null)
        {
            MessageBox.Show(this, "A fonte selecionada não está mais disponível. Atualize a lista e tente novamente.", "Fontes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshSourcesGrid();
            return;
        }
        using var editor = new SourceEditorForm(VisualEditorSupport.Clone(source), _sourceDesigner, _secretProtector);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Definition is not null)
        {
            if (editor.Definition.IsPrimary)
            {
                foreach (var item in _working.Sources.Where(x => x.Id != source.Id)) item.IsPrimary = false;
            }
            _working.Sources[_working.Sources.IndexOf(source)] = editor.Definition;
            RefreshSourcesAndFields();
        }
        await Task.CompletedTask;
    }

    private void DuplicateSource()
    {
        var selected = SelectedSource();
        if (selected is null) return;
        var source = _working.Sources.FirstOrDefault(x => x.Id == selected.Id);
        if (source is null)
        {
            RefreshSourcesGrid();
            return;
        }
        var clone = VisualEditorSupport.Clone(source);
        clone.Id = Guid.NewGuid();
        clone.Name += " - Cópia";
        clone.Alias += "Copy";
        clone.IsPrimary = false;
        _working.Sources.Add(clone);
        RefreshSourcesAndFields();
    }

    private void SetPrimarySource()
    {
        var selected = SelectedSource();
        if (selected is null) return;
        foreach (var source in _working.Sources) source.IsPrimary = source.Id == selected.Id;
        RefreshSourcesGrid();
    }

    private void DeleteSource()
    {
        var selected = SelectedSource();
        if (selected is null) return;
        if (MessageBox.Show(this, $"Excluir a fonte '{selected.Name}'?", "Confirmação", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var source = _working.Sources.FirstOrDefault(x => x.Id == selected.Id);
        if (source is null)
        {
            RefreshSourcesGrid();
            return;
        }
        _working.Sources.Remove(source);
        if (_working.Sources.Count > 0 && _working.Sources.All(x => !x.IsPrimary)) _working.Sources[0].IsPrimary = true;
        RefreshSourcesAndFields();
    }

    private async Task TestSelectedSourceAsync()
    {
        var selected = SelectedSource();
        if (selected is null) return;
        try
        {
            UseWaitCursor = true;
            var source = _working.Sources.FirstOrDefault(x => x.Id == selected.Id);
            if (source is null)
            {
                RefreshSourcesGrid();
                return;
            }
            var result = await _sourceDesigner.TestAsync(source, CancellationToken.None);
            MessageBox.Show(this, $"{result.Message}\nTempo: {result.Duration.TotalMilliseconds:N0} ms", "Teste da fonte", MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Teste da fonte");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task AddActionAsync()
    {
        var channels = await _store.GetChannelConfigurationsAsync(CancellationToken.None);
        using var editor = new ActionEditorForm(
            new ActionDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nova ação",
                Enabled = true,
                Trigger = ActionTrigger.OnOpen,
                MessageTemplate = "Ocorrência {{record.key}} detectada pela automação {{automation.name}}."
            },
            channels,
            GetKnownFields());
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Definition is not null)
        {
            _working.Actions.Add(editor.Definition);
            RefreshActionsGrid();
        }
    }

    private async Task EditActionAsync()
    {
        var selected = SelectedAction();
        if (selected is null) return;
        var action = _working.Actions.FirstOrDefault(x => x.Id == selected.Id);
        if (action is null)
        {
            RefreshActionsGrid();
            return;
        }
        var channels = await _store.GetChannelConfigurationsAsync(CancellationToken.None);
        using var editor = new ActionEditorForm(VisualEditorSupport.Clone(action), channels, GetKnownFields());
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Definition is not null)
        {
            _working.Actions[_working.Actions.IndexOf(action)] = editor.Definition;
            RefreshActionsGrid();
        }
    }

    private void DuplicateAction()
    {
        var selected = SelectedAction();
        if (selected is null) return;
        var action = _working.Actions.FirstOrDefault(x => x.Id == selected.Id);
        if (action is null)
        {
            RefreshActionsGrid();
            return;
        }
        var clone = VisualEditorSupport.Clone(action);
        clone.Id = Guid.NewGuid();
        clone.Name += " - Cópia";
        _working.Actions.Add(clone);
        RefreshActionsGrid();
    }

    private void DeleteAction()
    {
        var selected = SelectedAction();
        if (selected is null) return;
        if (MessageBox.Show(this, $"Excluir a ação '{selected.Name}'?", "Confirmação", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _working.Actions.RemoveAll(x => x.Id == selected.Id);
        RefreshActionsGrid();
    }


    private void AddGroup()
    {
        using var editor = new ContactGroupEditorForm(new ContactGroupDefinition
        {
            Id = $"group{_working.ContactGroups.Count + 1}",
            Name = "Novo grupo"
        });
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Definition is not null)
        {
            if (_working.ContactGroups.Any(x => string.Equals(x.Id, editor.Definition.Id, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Já existe um grupo com esse identificador.", "Grupos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _working.ContactGroups.Add(editor.Definition);
            RefreshGroupsGrid();
        }
    }

    private void EditGroup()
    {
        var selected = SelectedGroup();
        if (selected is null) return;
        var group = _working.ContactGroups.FirstOrDefault(x => string.Equals(x.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            RefreshGroupsGrid();
            return;
        }
        using var editor = new ContactGroupEditorForm(VisualEditorSupport.Clone(group));
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Definition is not null)
        {
            if (_working.ContactGroups.Any(x => !ReferenceEquals(x, group) && string.Equals(x.Id, editor.Definition.Id, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Já existe outro grupo com esse identificador.", "Grupos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _working.ContactGroups[_working.ContactGroups.IndexOf(group)] = editor.Definition;
            RefreshGroupsGrid();
        }
    }

    private void DeleteGroup()
    {
        var selected = SelectedGroup();
        if (selected is null) return;
        if (MessageBox.Show(this, $"Excluir o grupo '{selected.Name}'?", "Confirmação", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _working.ContactGroups.RemoveAll(x => string.Equals(x.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
        RefreshGroupsGrid();
    }

    private void Save(bool forceDisabled)
    {
        try
        {
            Definition = BuildDefinition();
            if (forceDisabled) Definition.Enabled = false;
            Definition.Validate();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Validação da automação");
        }
    }

    private AutomationDefinition BuildDefinition()
    {
        var interval = VisualEditorSupport.ToSeconds(_intervalValue.Value, Convert.ToString(_intervalUnit.SelectedItem) ?? "Segundos");
        var persistence = _persistenceRules.BuildDefinition();
        var completion = _completionRules.BuildDefinition();
        var suspension = _suspensionRules.BuildDefinition();

        return new AutomationDefinition
        {
            Id = _working.Id == Guid.Empty ? Guid.NewGuid() : _working.Id,
            Name = string.IsNullOrWhiteSpace(_name.Text) ? throw new InvalidOperationException("Informe o nome da automação.") : _name.Text.Trim(),
            Description = _description.Text.Trim(),
            Enabled = _enabled.Checked,
            IntervalSeconds = interval,
            Priority = (int)_priority.Value,
            MissingRecordBehavior = SelectedDisplayValue(_missingRecord, MissingRecordBehavior.Ignore),
            ResolveWhenPersistenceFails = _resolvePersistence.Checked,
            Sources = VisualEditorSupport.Clone(_working.Sources),
            EntryRules = _entryRules.BuildDefinition(),
            PersistenceRules = IsEmpty(persistence) ? null : persistence,
            CompletionRules = IsEmpty(completion) ? null : completion,
            SuspensionRules = IsEmpty(suspension) ? null : suspension,
            Actions = VisualEditorSupport.Clone(_working.Actions),
            ContactGroups = VisualEditorSupport.Clone(_working.ContactGroups)
        };
    }

    private static bool IsEmpty(RuleSetDefinition set) => set.Root.Rules.Count == 0 && set.Root.Groups.Count == 0 && !set.Root.Negate;

    private void ValidateAndShow()
    {
        try
        {
            var definition = BuildDefinition();
            definition.Validate();
            MessageBox.Show(this, "A configuração visual está válida e pronta para ser salva.", "Validação", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateReview();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Validação");
        }
    }

    private void UpdateReview()
    {
        try
        {
            var definition = BuildDefinition();
            var lines = new List<string>
            {
                $"AUTOMAÇÃO: {definition.Name}",
                $"Situação: {(definition.Enabled ? "Ativa" : "Desativada")}",
                $"Intervalo: {TimeSpan.FromSeconds(definition.IntervalSeconds)}",
                $"Fontes: {definition.Sources.Count}",
                $"Fonte principal: {definition.Sources.FirstOrDefault(x => x.IsPrimary)?.Name ?? "Não definida"}",
                $"Ações: {definition.Actions.Count}",
                $"Canais vinculados: {definition.Actions.SelectMany(x => x.Channels).Select(x => x.ChannelConfigurationId).Distinct().Count()}",
                $"Destinatários configurados: {definition.Actions.Sum(x => x.Recipients.Count)}",
                $"Grupos de contatos: {definition.ContactGroups.Count}",
                string.Empty,
                "FONTES"
            };
            lines.AddRange(definition.Sources.Select(x => $"- {x.Name}: {VisualEditorSupport.SourceTypeText(x.Type)} | chave: {string.Join(" + ", x.KeyFields)}{(x.IsPrimary ? " | PRINCIPAL" : string.Empty)}"));
            lines.Add(string.Empty);
            lines.Add("AÇÕES");
            lines.AddRange(definition.Actions.Select(x => $"- {x.Name}: {x.Trigger} | {x.Channels.Count} canal(is) | {x.Recipients.Count} destinatário(s)"));
            _review.Text = string.Join(Environment.NewLine, lines);
        }
        catch (Exception exception)
        {
            _review.Text = $"Configuração incompleta: {exception.Message}";
        }
    }

    private void UpdateJson()
    {
        try
        {
            _json.Text = JsonSerializer.Serialize(BuildDefinition(), FlowJson.Options);
        }
        catch (Exception exception)
        {
            _json.Text = $"Não foi possível gerar o JSON: {exception.Message}";
        }
    }

    private void ApplyJson()
    {
        try
        {
            var definition = JsonSerializer.Deserialize<AutomationDefinition>(_json.Text, FlowJson.Options)
                             ?? throw new InvalidOperationException("O JSON não contém uma automação válida.");
            definition.Validate();
            LoadDefinition(definition);
            MessageBox.Show(this, "JSON aplicado à interface visual.", "Modo avançado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "JSON da automação");
        }
    }

    private void RefreshSourcesAndFields()
    {
        RefreshSourcesGrid();
        var fields = GetKnownFields();
        _entryRules.SetAvailableFields(fields);
        _persistenceRules.SetAvailableFields(fields);
        _completionRules.SetAvailableFields(fields);
        _suspensionRules.SetAvailableFields(fields);
    }

    private void RefreshSourcesGrid()
    {
        _sources.DataSource = _working.Sources.Select(x => new SourceRow
        {
            Id = x.Id,
            Name = x.Name,
            Type = VisualEditorSupport.SourceTypeText(x.Type),
            Alias = x.Alias,
            Keys = string.Join(" + ", x.KeyFields),
            Primary = x.IsPrimary,
            Enabled = x.Enabled
        }).ToList();
    }

    private void RefreshActionsGrid()
    {
        _actions.DataSource = _working.Actions.Select(x => new ActionRow
        {
            Id = x.Id,
            Name = x.Name,
            Trigger = x.Trigger switch
            {
                ActionTrigger.OnOpen => "Na abertura",
                ActionTrigger.WhileActive => "Enquanto ativa",
                ActionTrigger.OnResolved => "Na conclusão",
                _ => x.Trigger.ToString()
            },
            Channels = string.Join(", ", x.Channels.Select(c => VisualEditorSupport.ChannelTypeText(c.ChannelType)).Distinct()),
            Recipients = x.Recipients.Count,
            Enabled = x.Enabled
        }).ToList();
    }


    private void RefreshGroupsGrid()
    {
        _groups.DataSource = _working.ContactGroups.Select(x => new GroupRow
        {
            Id = x.Id,
            Name = x.Name,
            Contacts = x.Contacts.Count
        }).ToList();
    }

    private IReadOnlyCollection<string> GetKnownFields()
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in _working.Sources)
        {
            foreach (var key in source.KeyFields) fields.Add(key);
            if (source.Configuration.ValueKind == JsonValueKind.Object &&
                source.Configuration.TryGetProperty("designerFields", out var designerFields) &&
                designerFields.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in designerFields.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        fields.Add(item.GetString()!);
                    }
                }
            }
        }
        foreach (var set in new[] { _working.EntryRules, _working.PersistenceRules, _working.CompletionRules, _working.SuspensionRules }.Where(x => x is not null))
        {
            CollectFields(set!.Root, fields);
        }
        foreach (var action in _working.Actions)
        {
            foreach (var recipient in action.Recipients.Where(x => x.Type == RecipientType.Field)) fields.Add(recipient.Value);
            if (action.Conditions is not null) CollectFields(action.Conditions.Root, fields);
        }
        return fields.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void CollectFields(RuleGroupDefinition group, ISet<string> fields)
    {
        foreach (var rule in group.Rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.Field)) fields.Add(rule.Field);
            if (!string.IsNullOrWhiteSpace(rule.ExpectedField)) fields.Add(rule.ExpectedField);
        }
        foreach (var child in group.Groups) CollectFields(child, fields);
    }

    private SourceRow? SelectedSource() => _sources.CurrentRow?.DataBoundItem as SourceRow;
    private ActionRow? SelectedAction() => _actions.CurrentRow?.DataBoundItem as ActionRow;
    private GroupRow? SelectedGroup() => _groups.CurrentRow?.DataBoundItem as GroupRow;

    private static void SelectDisplay<T>(ComboBox comboBox, T value) =>
        VisualEditorSupport.SelectDisplayItem(comboBox, value, value);

    private static T SelectedDisplayValue<T>(ComboBox comboBox, T fallback) =>
        comboBox.SelectedItem is DisplayItem<T> item ? item.Value : fallback;

    private sealed class SourceRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Alias { get; init; } = string.Empty;
        public string Keys { get; init; } = string.Empty;
        public bool Primary { get; init; }
        public bool Enabled { get; init; }
    }


    private sealed class GroupRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int Contacts { get; init; }
    }

    private sealed class ActionRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Trigger { get; init; } = string.Empty;
        public string Channels { get; init; } = string.Empty;
        public int Recipients { get; init; }
        public bool Enabled { get; init; }
    }
}
