using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class RuleEditorForm : Form
{
    private readonly TextBox _field = new();
    private readonly ComboBox _operator = new();
    private readonly TextBox _expectedValue = new();
    private readonly TextBox _expectedField = new();
    private readonly CheckBox _caseSensitive = new();
    private readonly TextBox _culture = new();

    internal RuleDefinition? Definition { get; private set; }

    internal RuleEditorForm(RuleDefinition definition, IReadOnlyCollection<string>? availableFields = null)
    {
        Text = "Condição";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(650, 380);
        MinimumSize = new Size(560, 340);

        _operator.DropDownStyle = ComboBoxStyle.DropDownList;
        _operator.DataSource = Enum.GetValues<RuleOperator>()
            .Select(x => new DisplayItem<RuleOperator>(x, VisualEditorSupport.RuleOperatorText(x)))
            .ToList();

        var fieldPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        _field.Width = 340;
        fieldPanel.Controls.Add(_field);
        if (availableFields is { Count: > 0 })
        {
            var select = new Button { Text = "Selecionar...", AutoSize = true };
            select.Click += (_, _) => SelectField(_field, availableFields);
            fieldPanel.Controls.Add(select);
        }

        var expectedFieldPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        _expectedField.Width = 340;
        expectedFieldPanel.Controls.Add(_expectedField);
        if (availableFields is { Count: > 0 })
        {
            var select = new Button { Text = "Selecionar...", AutoSize = true };
            select.Click += (_, _) => SelectField(_expectedField, availableFields);
            expectedFieldPanel.Controls.Add(select);
        }

        _caseSensitive.Text = "Diferenciar maiúsculas e minúsculas";
        _caseSensitive.AutoSize = true;
        _culture.Text = "pt-BR";

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, "Campo avaliado", fieldPanel);
        AddRow(table, "Operador", _operator);
        AddRow(table, "Valor esperado", _expectedValue);
        AddRow(table, "Comparar com campo", expectedFieldPanel);
        AddRow(table, "Cultura", _culture);
        AddRow(table, "Texto", _caseSensitive);

        var hint = new Label
        {
            Text = "Para listas, separe os valores por vírgula. Em 'Mudou de... para...', use origem|destino.",
            AutoSize = true,
            MaximumSize = new Size(580, 0),
            Padding = new Padding(0, 8, 0, 8)
        };
        table.Controls.Add(hint, 0, table.RowCount);
        table.SetColumnSpan(hint, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var save = new Button { Text = "Salvar condição", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        Controls.Add(table);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;

        _field.Text = definition.Field;
        SelectOperator(definition.Operator);
        _expectedValue.Text = definition.ExpectedValue ?? string.Empty;
        _expectedField.Text = definition.ExpectedField ?? string.Empty;
        _caseSensitive.Checked = definition.CaseSensitive;
        _culture.Text = definition.Culture ?? "pt-BR";
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(VisualEditorSupport.LabelFor(label), 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
    }

    private static void SelectField(TextBox target, IReadOnlyCollection<string> fields)
    {
        using var dialog = new Form
        {
            Text = "Selecionar campo",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(430, 500),
            MinimizeBox = false,
            MaximizeBox = false
        };
        var list = new ListBox { Dock = DockStyle.Fill };
        list.Items.AddRange(fields.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        var select = new Button { Text = "Selecionar", Dock = DockStyle.Bottom, Height = 38, DialogResult = DialogResult.OK };
        dialog.Controls.Add(list);
        dialog.Controls.Add(select);
        dialog.AcceptButton = select;
        list.DoubleClick += (_, _) => dialog.DialogResult = DialogResult.OK;
        if (dialog.ShowDialog() == DialogResult.OK && list.SelectedItem is string field)
        {
            target.Text = field;
        }
    }

    private void Save()
    {
        try
        {
            Definition = new RuleDefinition
            {
                Field = string.IsNullOrWhiteSpace(_field.Text)
                    ? throw new InvalidOperationException("Informe o campo avaliado.")
                    : _field.Text.Trim(),
                Operator = (_operator.SelectedItem as DisplayItem<RuleOperator>)?.Value ?? RuleOperator.Equal,
                ExpectedValue = string.IsNullOrWhiteSpace(_expectedValue.Text) ? null : _expectedValue.Text,
                ExpectedField = string.IsNullOrWhiteSpace(_expectedField.Text) ? null : _expectedField.Text.Trim(),
                CaseSensitive = _caseSensitive.Checked,
                Culture = string.IsNullOrWhiteSpace(_culture.Text) ? "pt-BR" : _culture.Text.Trim()
            };
            Definition.Validate();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Validação da condição");
        }
    }

    private void SelectOperator(RuleOperator value) =>
        VisualEditorSupport.SelectDisplayItem(_operator, value, RuleOperator.Equal);
}
