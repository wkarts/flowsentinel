using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class RuleGroupEditorForm : Form
{
    private readonly ComboBox _operator = new();
    private readonly CheckBox _negate = new();

    internal LogicalOperator SelectedOperator { get; private set; }
    internal bool Negate { get; private set; }

    internal RuleGroupEditorForm(LogicalOperator logicalOperator, bool negate)
    {
        Text = "Grupo de condições";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(470, 250);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _operator.DropDownStyle = ComboBoxStyle.DropDownList;
        _operator.Items.AddRange([
            new DisplayItem<LogicalOperator>(LogicalOperator.And, "TODAS as condições (E)"),
            new DisplayItem<LogicalOperator>(LogicalOperator.Or, "QUALQUER condição (OU)")
        ]);
        VisualEditorSupport.SelectDisplayItem(_operator, logicalOperator, LogicalOperator.And);
        _negate.Text = "Negar o resultado deste grupo";
        _negate.Checked = negate;
        _negate.AutoSize = true;

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.Controls.Add(VisualEditorSupport.LabelFor("Combinação"), 0, 0);
        _operator.Dock = DockStyle.Fill;
        table.Controls.Add(_operator, 1, 0);
        table.Controls.Add(VisualEditorSupport.LabelFor("Resultado"), 0, 1);
        table.Controls.Add(_negate, 1, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var save = new Button { Text = "Salvar", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            SelectedOperator = (_operator.SelectedItem as DisplayItem<LogicalOperator>)?.Value ?? LogicalOperator.And;
            Negate = _negate.Checked;
            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        Controls.Add(table);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
