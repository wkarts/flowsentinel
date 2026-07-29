namespace FlowSentinel.Desktop;

internal sealed class StatusDictionaryForm : Form
{
    private readonly DataGridView _grid = new();

    internal IReadOnlyDictionary<string, string> StatusLabels { get; private set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    internal StatusDictionaryForm(
        IReadOnlyDictionary<string, string> current,
        IEnumerable<string> discoveredStatuses)
    {
        Text = "Legenda administrativa de valores";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(720, 540);
        MinimumSize = new Size(620, 440);
        BuildLayout(current, discoveredStatuses);
    }

    private void BuildLayout(
        IReadOnlyDictionary<string, string> current,
        IEnumerable<string> discoveredStatuses)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(670, 0),
            Text = "Informe o significado administrativo dos códigos existentes na planilha. " +
                   "O FlowSentinel não presume o significado de X, SM, M ou outros códigos; a legenda fica vinculada à fonte monitorada."
        }, 0, 0);

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = true;
        _grid.AllowUserToDeleteRows = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Code",
            HeaderText = "Código na planilha",
            FillWeight = 45
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Meaning",
            HeaderText = "Significado / descrição administrativa",
            FillWeight = 155
        });

        var allCodes = discoveredStatuses
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Concat(current.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        foreach (var code in allCodes)
        {
            current.TryGetValue(code, out var meaning);
            _grid.Rows.Add(code, meaning ?? string.Empty);
        }
        layout.Controls.Add(_grid, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft
        };
        var save = new Button { Text = "Salvar legenda", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 2);

        AcceptButton = save;
        CancelButton = cancel;
        Controls.Add(layout);
    }

    private void Save()
    {
        try
        {
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var code = Convert.ToString(row.Cells["Code"].Value)?.Trim() ?? string.Empty;
                var meaning = Convert.ToString(row.Cells["Meaning"].Value)?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }
                if (!labels.TryAdd(code, meaning))
                {
                    throw new InvalidOperationException($"O código '{code}' foi informado mais de uma vez.");
                }
            }

            StatusLabels = labels;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Legenda de valores");
        }
    }
}
