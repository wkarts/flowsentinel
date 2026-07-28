using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class ActionEditorForm : Form
{
    private readonly Guid _id;
    private readonly TextBox _name = new();
    private readonly CheckBox _enabled = new();
    private readonly ComboBox _trigger = new();
    private readonly NumericUpDown _delayValue = new();
    private readonly ComboBox _delayUnit = new();
    private readonly CheckBox _repeatEnabled = new();
    private readonly NumericUpDown _repeatValue = new();
    private readonly ComboBox _repeatUnit = new();
    private readonly NumericUpDown _maxExecutions = new();
    private readonly ComboBox _channelStrategy = new();
    private readonly ComboBox _successPolicy = new();
    private readonly CheckedListBox _channels = new();
    private readonly DataGridView _recipients = new();
    private readonly TextBox _subject = new();
    private readonly TextBox _message = new();
    private readonly ListBox _fields = new();
    private readonly RuleSetEditorControl _conditions = new();
    private readonly IReadOnlyCollection<ChannelConfiguration> _availableChannels;

    internal ActionDefinition? Definition { get; private set; }

    internal ActionEditorForm(
        ActionDefinition definition,
        IReadOnlyCollection<ChannelConfiguration> availableChannels,
        IReadOnlyCollection<string> availableFields)
    {
        _id = definition.Id;
        _availableChannels = availableChannels;

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
        _trigger.DataSource = new[]
        {
            new DisplayItem<ActionTrigger>(ActionTrigger.OnOpen, "Ao abrir a ocorrência"),
            new DisplayItem<ActionTrigger>(ActionTrigger.WhileActive, "Enquanto permanecer ativa"),
            new DisplayItem<ActionTrigger>(ActionTrigger.OnResolved, "Ao concluir a ocorrência")
        };

        ConfigurePeriod(_delayValue, _delayUnit);
        ConfigurePeriod(_repeatValue, _repeatUnit);
        _repeatEnabled.Text = "Repetir enquanto a política permitir";
        _repeatEnabled.AutoSize = true;
        _repeatEnabled.CheckedChanged += (_, _) => UpdateRepeatState();
        _maxExecutions.Minimum = 0;
        _maxExecutions.Maximum = 100000;
        _maxExecutions.Value = 1;

        _channelStrategy.DropDownStyle = ComboBoxStyle.DropDownList;
        _channelStrategy.DataSource = new[]
        {
            new DisplayItem<ChannelExecutionStrategy>(ChannelExecutionStrategy.All, "Enviar por todos os canais"),
            new DisplayItem<ChannelExecutionStrategy>(ChannelExecutionStrategy.AtLeastOne, "Ao menos um canal precisa enviar"),
            new DisplayItem<ChannelExecutionStrategy>(ChannelExecutionStrategy.FirstSuccessful, "Parar no primeiro envio bem-sucedido")
        };
        _successPolicy.DropDownStyle = ComboBoxStyle.DropDownList;
        _successPolicy.DataSource = new[]
        {
            new DisplayItem<ActionSuccessPolicy>(ActionSuccessPolicy.AllDeliveries, "Todos os destinatários devem receber"),
            new DisplayItem<ActionSuccessPolicy>(ActionSuccessPolicy.AtLeastOneDelivery, "Ao menos um destinatário deve receber")
        };

        _channels.CheckOnClick = true;
        _channels.Dock = DockStyle.Fill;
        foreach (var channel in _availableChannels.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            _channels.Items.Add(new ChannelListItem(channel));
        }

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
            "Opcional: esta ação será executada apenas quando estas condições forem atendidas.",
            definition: null,
            RuleSetType.ActionCondition,
            availableFields);
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
        tabs.TabPages.Add(BuildChannelsTab());
        tabs.TabPages.Add(BuildRecipientsTab());
        tabs.TabPages.Add(BuildTemplateTab());
        var conditionsTab = new TabPage("Condições da ação");
        conditionsTab.Controls.Add(_conditions);
        tabs.TabPages.Add(conditionsTab);

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
        AddRow(table, "Atraso do primeiro envio", PeriodPanel(_delayValue, _delayUnit));
        AddRow(table, "Repetição", _repeatEnabled);
        AddRow(table, "Repetir a cada", PeriodPanel(_repeatValue, _repeatUnit));
        AddRow(table, "Máximo de execuções", _maxExecutions);
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

    private TabPage BuildChannelsTab()
    {
        var tab = new TabPage("Canais");
        var label = new Label
        {
            Text = _availableChannels.Count == 0
                ? "Nenhum canal foi cadastrado. Feche esta tela, acesse Canais e cadastre ao menos um canal."
                : "Marque um ou vários canais para esta ação.",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10)
        };
        tab.Controls.Add(_channels);
        tab.Controls.Add(label);
        return tab;
    }

    private TabPage BuildRecipientsTab()
    {
        var tab = new TabPage("Destinatários");
        var help = new Label
        {
            Text = "Endereço fixo: número, e-mail ou Chat ID. Campo da fonte: nome da coluna, por exemplo Telefone. Grupo: ID do grupo de contatos.",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10)
        };
        tab.Controls.Add(_recipients);
        tab.Controls.Add(help);
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
        SetPeriod(definition.DelaySeconds, _delayValue, _delayUnit);
        _repeatEnabled.Checked = definition.Repeat.Enabled;
        SetPeriod(definition.Repeat.IntervalSeconds, _repeatValue, _repeatUnit);
        _maxExecutions.Value = Math.Clamp(definition.Repeat.MaxExecutions, 0, 100000);
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

        foreach (var recipient in definition.Recipients)
        {
            var rowIndex = _recipients.Rows.Add();
            var row = _recipients.Rows[rowIndex];
            row.Cells["RecipientType"].Value = recipient.Type;
            row.Cells["ChannelType"].Value = recipient.ChannelType;
            row.Cells["RecipientValue"].Value = recipient.Value;
            row.Cells["DisplayName"].Value = recipient.DisplayName;
        }

        _conditions.Configure(
            "Opcional: esta ação será executada apenas quando estas condições forem atendidas.",
            definition.Conditions,
            RuleSetType.ActionCondition,
            availableFields);
        UpdateRepeatState();
    }

    private void Save()
    {
        try
        {
            var channels = new List<ActionChannelDefinition>();
            var order = 0;
            foreach (var checkedItem in _channels.CheckedItems.Cast<ChannelListItem>())
            {
                channels.Add(new ActionChannelDefinition
                {
                    ChannelConfigurationId = checkedItem.Configuration.Id,
                    ChannelType = checkedItem.Configuration.Type,
                    Order = order++,
                    Required = SelectedValue(_channelStrategy, ChannelExecutionStrategy.All) == ChannelExecutionStrategy.All
                });
            }

            var recipients = new List<RecipientDefinition>();
            foreach (DataGridViewRow row in _recipients.Rows)
            {
                if (row.IsNewRow) continue;
                var value = Convert.ToString(row.Cells["RecipientValue"].Value)?.Trim();
                if (string.IsNullOrWhiteSpace(value)) continue;
                var type = row.Cells["RecipientType"].Value is RecipientType recipientType ? recipientType : RecipientType.Fixed;
                ChannelType? channel = row.Cells["ChannelType"].Value is ChannelType channelType ? channelType : null;
                recipients.Add(new RecipientDefinition
                {
                    Type = type,
                    ChannelType = channel,
                    Value = value,
                    DisplayName = Convert.ToString(row.Cells["DisplayName"].Value)?.Trim() ?? string.Empty
                });
            }

            var conditions = _conditions.BuildDefinition();
            Definition = new ActionDefinition
            {
                Id = _id == Guid.Empty ? Guid.NewGuid() : _id,
                Name = string.IsNullOrWhiteSpace(_name.Text) ? throw new InvalidOperationException("Informe o nome da ação.") : _name.Text.Trim(),
                Enabled = _enabled.Checked,
                Trigger = SelectedValue(_trigger, ActionTrigger.OnOpen),
                DelaySeconds = VisualEditorSupport.ToSeconds(_delayValue.Value, Convert.ToString(_delayUnit.SelectedItem) ?? "Segundos"),
                Repeat = new RepeatPolicyDefinition
                {
                    Enabled = _repeatEnabled.Checked,
                    IntervalSeconds = Math.Max(1, VisualEditorSupport.ToSeconds(_repeatValue.Value, Convert.ToString(_repeatUnit.SelectedItem) ?? "Segundos")),
                    MaxExecutions = (int)_maxExecutions.Value
                },
                ChannelStrategy = SelectedValue(_channelStrategy, ChannelExecutionStrategy.All),
                SuccessPolicy = SelectedValue(_successPolicy, ActionSuccessPolicy.AllDeliveries),
                Conditions = conditions.Root.Rules.Count == 0 && conditions.Root.Groups.Count == 0 ? null : conditions,
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

    private void UpdateRepeatState()
    {
        _repeatValue.Enabled = _repeatUnit.Enabled = _maxExecutions.Enabled = _repeatEnabled.Checked;
    }

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
