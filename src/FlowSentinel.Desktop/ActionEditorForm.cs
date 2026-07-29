using System.Globalization;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class ActionEditorForm : Form
{
    private readonly Guid _id;
    private readonly Guid _automationId;
    private readonly ContactDirectoryDefinition _contactDirectory;
    private readonly TextBox _name = new();
    private readonly CheckBox _enabled = new();
    private readonly ComboBox _trigger = new();
    private readonly CheckBox _evaluateWhileActiveOnOpen = new();
    private readonly NumericUpDown _delayValue = new();
    private readonly ComboBox _delayUnit = new();
    private readonly CheckBox _repeatEnabled = new();
    private readonly NumericUpDown _repeatValue = new();
    private readonly ComboBox _repeatUnit = new();
    private readonly NumericUpDown _maxExecutions = new();
    private readonly CheckBox _resetOnReentry = new();
    private readonly CheckBox _cancelPendingWhenConditionFails = new();
    private readonly CheckBox _scheduleEnabled = new();
    private readonly DateTimePicker _scheduleStart = new();
    private readonly DateTimePicker _scheduleEnd = new();
    private readonly CheckedListBox _scheduleDays = new();
    private readonly ComboBox _channelStrategy = new();
    private readonly ComboBox _successPolicy = new();
    private readonly CheckedListBox _channels = new();
    private readonly DataGridView _channelPolicies = new();
    private readonly DataGridView _recipients = new();
    private readonly TextBox _subject = new();
    private readonly TextBox _message = new();
    private readonly ListBox _fields = new();
    private readonly RuleSetEditorControl _conditions = new();
    private readonly RuleSetEditorControl _persistenceConditions = new();
    private readonly RuleSetEditorControl _completionConditions = new();
    private readonly IReadOnlyCollection<ChannelConfiguration> _availableChannels;
    private readonly IReadOnlyDictionary<Guid, ActionChannelDefinition> _originalChannelDefinitions;

    internal ActionDefinition? Definition { get; private set; }

    internal ActionEditorForm(
        ActionDefinition definition,
        IReadOnlyCollection<ChannelConfiguration> availableChannels,
        IReadOnlyCollection<string> availableFields,
        Guid automationId,
        ContactDirectoryDefinition contactDirectory)
    {
        _id = definition.Id;
        _automationId = automationId;
        _contactDirectory = VisualEditorSupport.Clone(contactDirectory);
        _originalChannelDefinitions = definition.Channels
            .GroupBy(x => x.ChannelConfigurationId)
            .ToDictionary(x => x.Key, x => VisualEditorSupport.Clone(x.First()));
        _availableChannels = availableChannels
            .Where(x => x.Enabled || _originalChannelDefinitions.ContainsKey(x.Id))
            .ToArray();

        Text = "Ação e notificações";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1080, 760);
        MinimumSize = new Size(930, 650);

        ConfigureControls(availableFields);
        BuildLayout();
        LoadDefinition(definition, availableFields);
    }

    private void ConfigureControls(IReadOnlyCollection<string> availableFields)
    {
        _enabled.Text = "Ação habilitada";
        _enabled.AutoSize = true;

        _trigger.DropDownStyle = ComboBoxStyle.DropDownList;
        VisualEditorSupport.SetDisplayItems(_trigger, new[]
        {
            new DisplayItem<ActionTrigger>(ActionTrigger.OnOpen, "Ao abrir a ocorrência"),
            new DisplayItem<ActionTrigger>(ActionTrigger.WhileActive, "Enquanto permanecer ativa"),
            new DisplayItem<ActionTrigger>(ActionTrigger.OnResolved, "Ao concluir a ocorrência")
        });
        _trigger.SelectedIndexChanged += (_, _) => UpdateTriggerState();

        _evaluateWhileActiveOnOpen.Text = "Avaliar a condição 'Enquanto ativa' já na abertura da ocorrência";
        _evaluateWhileActiveOnOpen.AutoSize = true;

        ConfigurePeriod(_delayValue, _delayUnit);
        ConfigurePeriod(_repeatValue, _repeatUnit);
        _repeatEnabled.Text = "Repetir enquanto a política permitir";
        _repeatEnabled.AutoSize = true;
        _repeatEnabled.CheckedChanged += (_, _) => UpdateRepeatState();
        _resetOnReentry.Text = "Reiniciar a contagem quando a condição voltar a ocorrer";
        _resetOnReentry.AutoSize = true;
        _cancelPendingWhenConditionFails.Text = "Cancelar entregas pendentes quando a condição for encerrada";
        _cancelPendingWhenConditionFails.AutoSize = true;
        ConfigureScheduleControls();
        _maxExecutions.Minimum = 0;
        _maxExecutions.Maximum = 100000;
        _maxExecutions.Value = 1;

        _channelStrategy.DropDownStyle = ComboBoxStyle.DropDownList;
        VisualEditorSupport.SetDisplayItems(_channelStrategy, new[]
        {
            new DisplayItem<ChannelExecutionStrategy>(ChannelExecutionStrategy.All, "Enviar por todos os canais"),
            new DisplayItem<ChannelExecutionStrategy>(ChannelExecutionStrategy.AtLeastOne, "Ao menos um canal precisa enviar"),
            new DisplayItem<ChannelExecutionStrategy>(ChannelExecutionStrategy.FirstSuccessful, "Parar no primeiro envio bem-sucedido")
        });
        _successPolicy.DropDownStyle = ComboBoxStyle.DropDownList;
        VisualEditorSupport.SetDisplayItems(_successPolicy, new[]
        {
            new DisplayItem<ActionSuccessPolicy>(ActionSuccessPolicy.AllDeliveries, "Todos os destinatários devem receber"),
            new DisplayItem<ActionSuccessPolicy>(ActionSuccessPolicy.AtLeastOneDelivery, "Ao menos um destinatário deve receber")
        });

        _channels.CheckOnClick = true;
        _channels.Dock = DockStyle.Fill;
        foreach (var channel in _availableChannels.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            _channels.Items.Add(new ChannelListItem(channel));
        }
        ConfigureChannelPolicies();

        _recipients.AllowUserToAddRows = true;
        _recipients.AllowUserToDeleteRows = true;
        _recipients.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _recipients.Dock = DockStyle.Fill;
        var typeColumn = new DataGridViewComboBoxColumn
        {
            Name = "RecipientType",
            HeaderText = "Origem",
            DataSource = Enum.GetValues<RecipientType>().Select(x => new DisplayItem<RecipientType>(x, VisualEditorSupport.RecipientTypeText(x))).ToList(),
            DisplayMember = nameof(DisplayItem<RecipientType>.Text),
            ValueMember = nameof(DisplayItem<RecipientType>.Value),
            FillWeight = 90
        };
        var channelColumn = new DataGridViewComboBoxColumn
        {
            Name = "ChannelType",
            HeaderText = "Canal específico",
            DataSource = Enum.GetValues<ChannelType>().Select(x => new DisplayItem<ChannelType>(x, VisualEditorSupport.ChannelTypeText(x))).ToList(),
            DisplayMember = nameof(DisplayItem<ChannelType>.Text),
            ValueMember = nameof(DisplayItem<ChannelType>.Value),
            FillWeight = 100
        };
        _recipients.Columns.Add(typeColumn);
        _recipients.Columns.Add(channelColumn);
        _recipients.Columns.Add(new DataGridViewTextBoxColumn { Name = "RecipientValue", HeaderText = "Valor, campo ou grupo", FillWeight = 180 });
        _recipients.Columns.Add(new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "Nome amigável", FillWeight = 120 });
        _recipients.DataError += (_, _) => { };

        _subject.Dock = DockStyle.Fill;
        _message.Multiline = true;
        _message.AcceptsTab = true;
        _message.ScrollBars = ScrollBars.Both;
        _message.Dock = DockStyle.Fill;
        _message.Font = new Font("Segoe UI", 10);
        _fields.Items.AddRange(availableFields.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        _fields.Items.AddRange(["automation.name", "record.key", "now", "today"]);
        _fields.DoubleClick += (_, _) => InsertSelectedField();

        _conditions.Configure(
            "Condições que iniciam esta ação. Para lembretes de pendência, informe aqui o valor que abre a pendência.",
            definition: null,
            RuleSetType.ActionCondition,
            availableFields);
        _persistenceConditions.Configure(
            "Opcional: condições que mantêm o ciclo ativo depois de iniciado. Sem regras, a condição inicial será reavaliada.",
            definition: null,
            RuleSetType.ActionPersistence,
            availableFields);
        _completionConditions.Configure(
            "Opcional: condições que encerram o ciclo, cancelam novas repetições e podem cancelar entregas ainda pendentes.",
            definition: null,
            RuleSetType.ActionCompletion,
            availableFields);
    }

    private void ConfigureScheduleControls()
    {
        _scheduleEnabled.Text = "Restringir envios a dias e horários";
        _scheduleEnabled.AutoSize = true;
        _scheduleEnabled.CheckedChanged += (_, _) => UpdateScheduleState();
        foreach (var picker in new[] { _scheduleStart, _scheduleEnd })
        {
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = "HH:mm";
            picker.ShowUpDown = true;
            picker.Width = 110;
        }
        _scheduleStart.Value = DateTime.Today.AddHours(8);
        _scheduleEnd.Value = DateTime.Today.AddHours(18);
        _scheduleDays.CheckOnClick = true;
        _scheduleDays.Height = 115;
        _scheduleDays.Items.AddRange(new object[]
        {
            new DisplayItem<DayOfWeek>(DayOfWeek.Monday, "Segunda-feira"),
            new DisplayItem<DayOfWeek>(DayOfWeek.Tuesday, "Terça-feira"),
            new DisplayItem<DayOfWeek>(DayOfWeek.Wednesday, "Quarta-feira"),
            new DisplayItem<DayOfWeek>(DayOfWeek.Thursday, "Quinta-feira"),
            new DisplayItem<DayOfWeek>(DayOfWeek.Friday, "Sexta-feira"),
            new DisplayItem<DayOfWeek>(DayOfWeek.Saturday, "Sábado"),
            new DisplayItem<DayOfWeek>(DayOfWeek.Sunday, "Domingo")
        });
        for (var index = 0; index < 5; index++)
        {
            _scheduleDays.SetItemChecked(index, true);
        }
        UpdateScheduleState();
    }

    private void ConfigureChannelPolicies()
    {
        _channelPolicies.Dock = DockStyle.Fill;
        _channelPolicies.AllowUserToAddRows = false;
        _channelPolicies.AllowUserToDeleteRows = false;
        _channelPolicies.RowHeadersVisible = false;
        _channelPolicies.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _channelPolicies.Columns.Add(new DataGridViewTextBoxColumn { Name = "ChannelName", HeaderText = "Canal", ReadOnly = true, FillWeight = 105 });
        _channelPolicies.Columns.Add(new DataGridViewTextBoxColumn { Name = "ChannelType", HeaderText = "Tipo", ReadOnly = true, FillWeight = 80 });
        _channelPolicies.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "GroupingMode",
            HeaderText = "Forma de envio",
            FillWeight = 105,
            DataSource = new[]
            {
                new DisplayItem<NotificationGroupingMode>(NotificationGroupingMode.Individual, "Individual"),
                new DisplayItem<NotificationGroupingMode>(NotificationGroupingMode.ByEntity, "Agrupar por registro"),
                new DisplayItem<NotificationGroupingMode>(NotificationGroupingMode.SingleMessage, "Resumo único")
            },
            DisplayMember = nameof(DisplayItem<NotificationGroupingMode>.Text),
            ValueMember = nameof(DisplayItem<NotificationGroupingMode>.Value)
        });
        _channelPolicies.Columns.Add(new DataGridViewTextBoxColumn { Name = "GroupField", HeaderText = "Campo de agrupamento", FillWeight = 105 });
        _channelPolicies.Columns.Add(new DataGridViewTextBoxColumn { Name = "GroupingWindow", HeaderText = "Janela (s)", FillWeight = 55 });
        _channelPolicies.DataError += (_, _) => { };

        foreach (var channel in _availableChannels.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var local = channel.Type == ChannelType.LocalWindows;
            var rowIndex = _channelPolicies.Rows.Add(
                channel.Name,
                VisualEditorSupport.ChannelTypeText(channel.Type),
                NotificationGroupingMode.Individual,
                "EntityKey",
                local ? 0 : 8);
            var row = _channelPolicies.Rows[rowIndex];
            row.Tag = channel;
            if (local)
            {
                row.Cells["GroupingMode"].ReadOnly = true;
                row.Cells["GroupField"].ReadOnly = true;
                row.Cells["GroupingWindow"].ReadOnly = true;
            }
        }
    }

    private void OpenCatalogMenu(Control anchor)
    {
        var channels = _channels.CheckedItems.Cast<ChannelListItem>()
            .Select(x => x.Configuration)
            .Where(x => x.Type != ChannelType.LocalWindows)
            .ToArray();
        if (channels.Length == 0)
        {
            MessageBox.Show(this, "Marque primeiro ao menos um canal externo na aba 'Canais e agrupamento'.",
                "Destinatários", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (channels.Length == 1)
        {
            SelectRecipientsFromCatalog(channels[0]);
            return;
        }

        var menu = new ContextMenuStrip();
        foreach (var channel in channels)
        {
            var item = new ToolStripMenuItem($"{channel.Name} — {VisualEditorSupport.ChannelTypeText(channel.Type)}");
            item.Click += (_, _) => SelectRecipientsFromCatalog(channel);
            menu.Items.Add(item);
        }
        menu.Show(anchor, new Point(0, anchor.Height));
    }

    private void SelectRecipientsFromCatalog(ChannelConfiguration channel)
    {
        var current = ReadRecipientRows()
            .Where(x => x.ChannelType == channel.Type)
            .Where(x => x.Type is RecipientType.Fixed or RecipientType.Contact or RecipientType.Group)
            .ToArray();
        using var selector = new RecipientSelectionForm(_automationId, channel.Type, _contactDirectory, current);
        if (selector.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        for (var index = _recipients.Rows.Count - 1; index >= 0; index--)
        {
            var row = _recipients.Rows[index];
            if (row.IsNewRow)
            {
                continue;
            }
            var type = row.Cells["RecipientType"].Value is RecipientType recipientType ? recipientType : RecipientType.Fixed;
            var specificChannel = row.Cells["ChannelType"].Value is ChannelType channelType ? channelType : (ChannelType?)null;
            if (specificChannel == channel.Type && type is RecipientType.Fixed or RecipientType.Contact or RecipientType.Group)
            {
                _recipients.Rows.RemoveAt(index);
            }
        }

        foreach (var recipient in selector.Recipients)
        {
            AddRecipientRow(recipient);
        }
    }

    private List<RecipientDefinition> ReadRecipientRows()
    {
        var recipients = new List<RecipientDefinition>();
        foreach (DataGridViewRow row in _recipients.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }
            var value = Convert.ToString(row.Cells["RecipientValue"].Value)?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }
            recipients.Add(new RecipientDefinition
            {
                Type = row.Cells["RecipientType"].Value is RecipientType recipientType ? recipientType : RecipientType.Fixed,
                ChannelType = row.Cells["ChannelType"].Value is ChannelType channelType ? channelType : null,
                Value = value,
                DisplayName = Convert.ToString(row.Cells["DisplayName"].Value)?.Trim() ?? string.Empty
            });
        }
        return recipients;
    }

    private void AddRecipientRow(RecipientDefinition recipient)
    {
        var rowIndex = _recipients.Rows.Add();
        var row = _recipients.Rows[rowIndex];
        row.Cells["RecipientType"].Value = recipient.Type;
        row.Cells["ChannelType"].Value = recipient.ChannelType;
        row.Cells["RecipientValue"].Value = recipient.Value;
        row.Cells["DisplayName"].Value = recipient.DisplayName;
    }

    private static void ConfigurePeriod(NumericUpDown value, ComboBox unit)
    {
        value.Minimum = 0;
        value.Maximum = 1000000;
        unit.DropDownStyle = ComboBoxStyle.DropDownList;
        unit.Items.AddRange(["Segundos", "Minutos", "Horas", "Dias"]);
        unit.SelectedItem = "Minutos";
    }

    private void BuildLayout()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildScheduleTab());
        tabs.TabPages.Add(BuildChannelsTab());
        tabs.TabPages.Add(BuildRecipientsTab());
        tabs.TabPages.Add(BuildTemplateTab());
        var conditionsTab = new TabPage("Início da ação");
        conditionsTab.Controls.Add(_conditions);
        tabs.TabPages.Add(conditionsTab);
        var persistenceTab = new TabPage("Enquanto ativa");
        persistenceTab.Controls.Add(_persistenceConditions);
        tabs.TabPages.Add(persistenceTab);
        var completionTab = new TabPage("Encerramento");
        completionTab.Controls.Add(_completionConditions);
        tabs.TabPages.Add(completionTab);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var save = new Button { Text = "Salvar ação", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        Controls.Add(tabs);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private TabPage BuildGeneralTab()
    {
        var tab = new TabPage("Geral e repetição");
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(14) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, "Nome da ação", _name);
        AddRow(table, "Situação", _enabled);
        AddRow(table, "Momento do disparo", _trigger);
        AddRow(table, "Primeira avaliação", _evaluateWhileActiveOnOpen);
        AddRow(table, "Atraso do primeiro envio", PeriodPanel(_delayValue, _delayUnit));
        AddRow(table, "Repetição", _repeatEnabled);
        AddRow(table, "Repetir a cada", PeriodPanel(_repeatValue, _repeatUnit));
        AddRow(table, "Máximo de execuções", _maxExecutions);
        AddRow(table, "Novo ciclo da condição", _resetOnReentry);
        AddRow(table, "Ao encerrar a condição", _cancelPendingWhenConditionFails);
        AddRow(table, "Estratégia dos canais", _channelStrategy);
        AddRow(table, "Sucesso da ação", _successPolicy);
        table.Controls.Add(new Label
        {
            Text = "Use máximo 0 para repetição sem limite. A política de permanência da ocorrência continua sendo respeitada.",
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Padding = new Padding(0, 8, 0, 0)
        }, 1, table.RowCount);
        tab.Controls.Add(table);
        return tab;
    }

    private TabPage BuildScheduleTab()
    {
        var tab = new TabPage("Dias e horários");
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(14) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, "Controle de horário", _scheduleEnabled);
        AddRow(table, "Horário inicial", _scheduleStart);
        AddRow(table, "Horário final", _scheduleEnd);
        AddRow(table, "Dias permitidos", _scheduleDays);
        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            ForeColor = Color.DimGray,
            Text = "O ciclo continua sendo verificado fora da janela, mas as mensagens só são agendadas nos dias e horários permitidos. Horários invertidos representam uma janela que atravessa a meia-noite."
        };
        AddRow(table, "Comportamento", note);
        tab.Controls.Add(table);
        return tab;
    }

    private TabPage BuildChannelsTab()
    {
        var tab = new TabPage("Canais e agrupamento");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.Controls.Add(new Label
        {
            Text = _availableChannels.Count == 0
                ? "Nenhum canal foi cadastrado. Feche esta tela e cadastre um canal nas configurações."
                : "Marque um ou vários canais. A política de agrupamento pode ser diferente em cada canal.",
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        }, 0, 0);
        root.Controls.Add(_channels, 0, 1);
        root.Controls.Add(new Label
        {
            Text = "Política de entrega por canal",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Padding = new Padding(0, 10, 0, 6)
        }, 0, 2);
        root.Controls.Add(_channelPolicies, 0, 3);
        tab.Controls.Add(root);
        return tab;
    }

    private TabPage BuildRecipientsTab()
    {
        var tab = new TabPage("Destinatários");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(new Label
        {
            Text = "Use contatos e grupos do catálogo, valores manuais ou campos da fonte. O catálogo evita recadastrar destinatários em cada monitoramento.",
            AutoSize = true,
            MaximumSize = new Size(960, 0),
            Padding = new Padding(0, 0, 0, 8)
        }, 0, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        var catalog = new Button { Text = "Selecionar do catálogo...", AutoSize = true, Height = 31 };
        catalog.Click += (_, _) => OpenCatalogMenu(catalog);
        actions.Controls.Add(catalog);
        actions.Controls.Add(new Label
        {
            Text = "Também é possível editar manualmente a grade abaixo.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(12, 7, 3, 3)
        });
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(_recipients, 0, 2);
        tab.Controls.Add(root);
        return tab;
    }

    private TabPage BuildTemplateTab()
    {
        var tab = new TabPage("Mensagem");
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 760 };
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(10) };
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editor.Controls.Add(new Label { Text = "Assunto", AutoSize = true }, 0, 0);
        editor.Controls.Add(_subject, 0, 1);
        editor.Controls.Add(new Label { Text = "Mensagem", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 0, 2);
        editor.Controls.Add(_message, 0, 3);
        split.Panel1.Controls.Add(editor);

        var fieldsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var fieldsLabel = new Label { Text = "Campos disponíveis\nDuplo clique para inserir", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        _fields.Dock = DockStyle.Fill;
        fieldsPanel.Controls.Add(_fields);
        fieldsPanel.Controls.Add(fieldsLabel);
        split.Panel2.Controls.Add(fieldsPanel);
        tab.Controls.Add(split);
        return tab;
    }

    private static FlowLayoutPanel PeriodPanel(Control value, Control unit)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        value.Width = 120;
        unit.Width = 130;
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

    private void LoadDefinition(ActionDefinition definition, IReadOnlyCollection<string> availableFields)
    {
        _name.Text = definition.Name;
        _enabled.Checked = definition.Enabled;
        SelectDisplay(_trigger, definition.Trigger);
        _evaluateWhileActiveOnOpen.Checked = definition.EvaluateWhileActiveOnOpen;
        SetPeriod(definition.DelaySeconds, _delayValue, _delayUnit);
        _repeatEnabled.Checked = definition.Repeat.Enabled;
        SetPeriod(definition.Repeat.IntervalSeconds, _repeatValue, _repeatUnit);
        _maxExecutions.Value = Math.Clamp(definition.Repeat.MaxExecutions, 0, 100000);
        _resetOnReentry.Checked = definition.Repeat.ResetOnConditionReentry;
        _cancelPendingWhenConditionFails.Checked = definition.CancelPendingWhenConditionFails;
        LoadSchedule(definition.Schedule ?? new ActionScheduleDefinition());
        SelectDisplay(_channelStrategy, definition.ChannelStrategy);
        SelectDisplay(_successPolicy, definition.SuccessPolicy);
        _subject.Text = definition.SubjectTemplate;
        _message.Text = definition.MessageTemplate;

        for (var index = 0; index < _channels.Items.Count; index++)
        {
            if (_channels.Items[index] is ChannelListItem item && definition.Channels.Any(x => x.ChannelConfigurationId == item.Configuration.Id))
            {
                _channels.SetItemChecked(index, true);
            }
        }
        foreach (DataGridViewRow row in _channelPolicies.Rows)
        {
            if (row.Tag is not ChannelConfiguration channel)
            {
                continue;
            }
            var saved = definition.Channels.FirstOrDefault(x => x.ChannelConfigurationId == channel.Id);
            row.Cells["GroupingMode"].Value = channel.Type == ChannelType.LocalWindows
                ? NotificationGroupingMode.Individual
                : saved?.GroupingMode ?? NotificationGroupingMode.Individual;
            row.Cells["GroupField"].Value = saved?.GroupField ?? "EntityKey";
            row.Cells["GroupingWindow"].Value = saved?.GroupingWindowSeconds ??
                                                   (channel.Type == ChannelType.LocalWindows ? 0 : 8);
        }

        foreach (var recipient in definition.Recipients)
        {
            AddRecipientRow(recipient);
        }

        _conditions.Configure(
            "Condições que iniciam esta ação. Para lembretes de pendência, informe aqui o valor que abre a pendência.",
            definition.Conditions,
            RuleSetType.ActionCondition,
            availableFields);
        _persistenceConditions.Configure(
            "Opcional: condições que mantêm o ciclo ativo depois de iniciado. Sem regras, a condição inicial será reavaliada.",
            definition.PersistenceConditions,
            RuleSetType.ActionPersistence,
            availableFields);
        _completionConditions.Configure(
            "Opcional: condições que encerram o ciclo, cancelam novas repetições e podem cancelar entregas ainda pendentes.",
            definition.CompletionConditions,
            RuleSetType.ActionCompletion,
            availableFields);
        UpdateRepeatState();
        UpdateScheduleState();
        UpdateTriggerState();
    }

    private void Save()
    {
        try
        {
            var channels = new List<ActionChannelDefinition>();
            var order = 0;
            foreach (var checkedItem in _channels.CheckedItems.Cast<ChannelListItem>())
            {
                _originalChannelDefinitions.TryGetValue(checkedItem.Configuration.Id, out var originalChannel);
                var policyRow = _channelPolicies.Rows.Cast<DataGridViewRow>()
                    .FirstOrDefault(x => x.Tag is ChannelConfiguration policyChannel && policyChannel.Id == checkedItem.Configuration.Id);
                var groupingMode = checkedItem.Configuration.Type == ChannelType.LocalWindows
                    ? NotificationGroupingMode.Individual
                    : policyRow?.Cells["GroupingMode"].Value is NotificationGroupingMode mode
                        ? mode
                        : originalChannel?.GroupingMode ?? NotificationGroupingMode.Individual;
                var groupField = Convert.ToString(policyRow?.Cells["GroupField"].Value)?.Trim();
                var groupingWindow = int.TryParse(Convert.ToString(policyRow?.Cells["GroupingWindow"].Value), out var parsedWindow)
                    ? Math.Clamp(parsedWindow, 0, 300)
                    : originalChannel?.GroupingWindowSeconds ?? 8;
                channels.Add(new ActionChannelDefinition
                {
                    ChannelConfigurationId = checkedItem.Configuration.Id,
                    ChannelType = checkedItem.Configuration.Type,
                    Order = order++,
                    Required = SelectedValue(_channelStrategy, ChannelExecutionStrategy.All) == ChannelExecutionStrategy.All,
                    GroupingMode = groupingMode,
                    GroupField = string.IsNullOrWhiteSpace(groupField) ? "EntityKey" : groupField,
                    GroupingWindowSeconds = groupingMode == NotificationGroupingMode.Individual ? 0 : groupingWindow
                });
            }

            var recipients = ReadRecipientRows();

            var conditions = _conditions.BuildDefinition();
            var persistenceConditions = _persistenceConditions.BuildDefinition();
            var completionConditions = _completionConditions.BuildDefinition();
            Definition = new ActionDefinition
            {
                Id = _id == Guid.Empty ? Guid.NewGuid() : _id,
                Name = string.IsNullOrWhiteSpace(_name.Text) ? throw new InvalidOperationException("Informe o nome da ação.") : _name.Text.Trim(),
                Enabled = _enabled.Checked,
                Trigger = SelectedValue(_trigger, ActionTrigger.OnOpen),
                EvaluateWhileActiveOnOpen = _evaluateWhileActiveOnOpen.Checked,
                DelaySeconds = VisualEditorSupport.ToSeconds(_delayValue.Value, Convert.ToString(_delayUnit.SelectedItem) ?? "Segundos"),
                Repeat = new RepeatPolicyDefinition
                {
                    Enabled = _repeatEnabled.Checked,
                    IntervalSeconds = Math.Max(1, VisualEditorSupport.ToSeconds(_repeatValue.Value, Convert.ToString(_repeatUnit.SelectedItem) ?? "Segundos")),
                    MaxExecutions = (int)_maxExecutions.Value,
                    ResetOnConditionReentry = _resetOnReentry.Checked
                },
                Schedule = BuildSchedule(),
                ChannelStrategy = SelectedValue(_channelStrategy, ChannelExecutionStrategy.All),
                SuccessPolicy = SelectedValue(_successPolicy, ActionSuccessPolicy.AllDeliveries),
                Conditions = IsEmpty(conditions) ? null : conditions,
                PersistenceConditions = IsEmpty(persistenceConditions) ? null : persistenceConditions,
                CompletionConditions = IsEmpty(completionConditions) ? null : completionConditions,
                CancelPendingWhenConditionFails = _cancelPendingWhenConditionFails.Checked,
                SubjectTemplate = _subject.Text,
                MessageTemplate = _message.Text,
                Channels = channels,
                Recipients = recipients
            };
            Definition.Validate();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Validação da ação");
        }
    }

    private void InsertSelectedField()
    {
        if (_fields.SelectedItem is not string field) return;
        var token = "{{" + field + "}}";
        var target = _message.Focused ? _message : _subject;
        var position = target.SelectionStart;
        target.Text = target.Text.Insert(position, token);
        target.SelectionStart = position + token.Length;
        target.Focus();
    }

    private void UpdateTriggerState()
    {
        _evaluateWhileActiveOnOpen.Enabled = SelectedValue(_trigger, ActionTrigger.OnOpen) == ActionTrigger.WhileActive;
    }

    private void UpdateRepeatState()
    {
        _repeatValue.Enabled = _repeatUnit.Enabled = _maxExecutions.Enabled = _repeatEnabled.Checked;
        _resetOnReentry.Enabled = _repeatEnabled.Checked;
    }

    private void UpdateScheduleState()
    {
        _scheduleStart.Enabled = _scheduleEnd.Enabled = _scheduleDays.Enabled = _scheduleEnabled.Checked;
    }

    private void LoadSchedule(ActionScheduleDefinition schedule)
    {
        _scheduleEnabled.Checked = schedule.Enabled;
        SetTime(_scheduleStart, schedule.StartTime, new TimeOnly(8, 0));
        SetTime(_scheduleEnd, schedule.EndTime, new TimeOnly(18, 0));
        for (var index = 0; index < _scheduleDays.Items.Count; index++)
        {
            var selected = _scheduleDays.Items[index] is DisplayItem<DayOfWeek> item &&
                           (schedule.DaysOfWeek?.Count > 0 ? schedule.DaysOfWeek.Contains(item.Value) : index < 5);
            _scheduleDays.SetItemChecked(index, selected);
        }
    }

    private ActionScheduleDefinition BuildSchedule() => new()
    {
        Enabled = _scheduleEnabled.Checked,
        StartTime = _scheduleStart.Value.ToString("HH:mm", CultureInfo.InvariantCulture),
        EndTime = _scheduleEnd.Value.ToString("HH:mm", CultureInfo.InvariantCulture),
        DaysOfWeek = _scheduleDays.CheckedItems
            .Cast<DisplayItem<DayOfWeek>>()
            .Select(x => x.Value)
            .ToList()
    };

    private static void SetTime(DateTimePicker picker, string? value, TimeOnly fallback)
    {
        var time = TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : fallback;
        picker.Value = DateTime.Today.Add(time.ToTimeSpan());
    }

    private static bool IsEmpty(RuleSetDefinition definition) =>
        definition.Root.Rules.Count == 0 && definition.Root.Groups.Count == 0;

    private static void SetPeriod(int seconds, NumericUpDown value, ComboBox unit)
    {
        var converted = VisualEditorSupport.FromSeconds(Math.Max(0, seconds));
        value.Value = Math.Clamp(converted.Value, value.Minimum, value.Maximum);
        unit.SelectedItem = converted.Unit;
    }

    private static void SelectDisplay<T>(ComboBox comboBox, T value) =>
        VisualEditorSupport.SelectDisplayItem(comboBox, value, value);

    private static T SelectedValue<T>(ComboBox comboBox, T fallback) =>
        comboBox.SelectedItem is DisplayItem<T> item ? item.Value : fallback;

    private sealed class ChannelListItem
    {
        internal ChannelListItem(ChannelConfiguration configuration) => Configuration = configuration;
        internal ChannelConfiguration Configuration { get; }
        public override string ToString() => $"{Configuration.Name} — {VisualEditorSupport.ChannelTypeText(Configuration.Type)}{(Configuration.Enabled ? string.Empty : " (desativado)")}";
    }
}
