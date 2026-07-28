using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class RuleSetEditorControl : UserControl
{
    private readonly TreeView _tree = new();
    private readonly Label _description = new();
    private RuleSetType _type;
    private IReadOnlyCollection<string> _availableFields = [];

    internal RuleSetEditorControl()
    {
        Dock = DockStyle.Fill;
        BuildLayout();
        LoadDefinition(RuleSetDefinition.AlwaysTrue(RuleSetType.Entry), []);
    }

    internal void Configure(string description, RuleSetDefinition? definition, RuleSetType type, IReadOnlyCollection<string>? availableFields = null)
    {
        _description.Text = description;
        _type = type;
        _availableFields = availableFields ?? [];
        LoadDefinition(definition ?? RuleSetDefinition.AlwaysTrue(type), _availableFields);
    }

    internal void SetAvailableFields(IReadOnlyCollection<string> availableFields) => _availableFields = availableFields;

    internal RuleSetDefinition BuildDefinition()
    {
        if (_tree.Nodes.Count == 0 || _tree.Nodes[0].Tag is not RuleGroupDefinition root)
        {
            root = new RuleGroupDefinition();
        }

        return new RuleSetDefinition
        {
            Type = _type,
            Root = VisualEditorSupport.Clone(root)
        };
    }

    private void BuildLayout()
    {
        _description.Dock = DockStyle.Top;
        _description.AutoSize = true;
        _description.MaximumSize = new Size(900, 0);
        _description.Padding = new Padding(8);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6), WrapContents = true };
        AddButton(toolbar, "Adicionar condição", (_, _) => AddRule());
        AddButton(toolbar, "Adicionar grupo", (_, _) => AddGroup());
        AddButton(toolbar, "Editar", (_, _) => EditSelected());
        AddButton(toolbar, "Excluir", (_, _) => DeleteSelected());
        AddButton(toolbar, "Limpar", (_, _) => Clear());

        _tree.Dock = DockStyle.Fill;
        _tree.HideSelection = false;
        _tree.FullRowSelect = true;
        _tree.DoubleClick += (_, _) => EditSelected();

        Controls.Add(_tree);
        Controls.Add(toolbar);
        Controls.Add(_description);
    }

    private static void AddButton(Control parent, string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += handler;
        parent.Controls.Add(button);
    }

    private void LoadDefinition(RuleSetDefinition definition, IReadOnlyCollection<string> fields)
    {
        _type = definition.Type;
        _availableFields = fields;
        _tree.Nodes.Clear();
        var root = VisualEditorSupport.Clone(definition.Root);
        _tree.Nodes.Add(CreateGroupNode(root, true));
        _tree.ExpandAll();
        _tree.SelectedNode = _tree.Nodes[0];
    }

    private static TreeNode CreateGroupNode(RuleGroupDefinition group, bool root)
    {
        var node = new TreeNode { Tag = group };
        UpdateGroupNodeText(node, root);
        foreach (var rule in group.Rules)
        {
            node.Nodes.Add(CreateRuleNode(rule));
        }
        foreach (var child in group.Groups)
        {
            node.Nodes.Add(CreateGroupNode(child, false));
        }
        return node;
    }

    private static TreeNode CreateRuleNode(RuleDefinition rule)
    {
        var node = new TreeNode { Tag = rule };
        UpdateRuleNodeText(node);
        return node;
    }

    private static void UpdateGroupNodeText(TreeNode node, bool root)
    {
        var group = (RuleGroupDefinition)node.Tag!;
        var label = group.Operator == LogicalOperator.And ? "TODAS (E)" : "QUALQUER (OU)";
        node.Text = $"{(root ? "Grupo principal" : "Grupo")}: {label}{(group.Negate ? " — NEGADO" : string.Empty)}";
        node.ImageIndex = 0;
    }

    private static void UpdateRuleNodeText(TreeNode node)
    {
        var rule = (RuleDefinition)node.Tag!;
        var expected = rule.ExpectedField is not null
            ? $"campo {rule.ExpectedField}"
            : string.IsNullOrWhiteSpace(rule.ExpectedValue) ? string.Empty : rule.ExpectedValue;
        node.Text = $"{rule.Field} {VisualEditorSupport.RuleOperatorText(rule.Operator)} {expected}".Trim();
    }

    private TreeNode SelectedGroupNode()
    {
        var selected = _tree.SelectedNode ?? _tree.Nodes[0];
        if (selected.Tag is RuleGroupDefinition) return selected;
        return selected.Parent ?? _tree.Nodes[0];
    }

    private void AddRule()
    {
        using var editor = new RuleEditorForm(new RuleDefinition { Operator = RuleOperator.Equal }, _availableFields);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Definition is null) return;

        var groupNode = SelectedGroupNode();
        var group = (RuleGroupDefinition)groupNode.Tag!;
        group.Rules.Add(editor.Definition);
        var node = CreateRuleNode(editor.Definition);
        groupNode.Nodes.Insert(group.Rules.Count - 1, node);
        groupNode.Expand();
        _tree.SelectedNode = node;
    }

    private void AddGroup()
    {
        using var editor = new RuleGroupEditorForm(LogicalOperator.And, false);
        if (editor.ShowDialog(this) != DialogResult.OK) return;

        var parentNode = SelectedGroupNode();
        var parent = (RuleGroupDefinition)parentNode.Tag!;
        var group = new RuleGroupDefinition { Operator = editor.SelectedOperator, Negate = editor.Negate };
        parent.Groups.Add(group);
        var node = CreateGroupNode(group, false);
        parentNode.Nodes.Add(node);
        parentNode.Expand();
        _tree.SelectedNode = node;
    }

    private void EditSelected()
    {
        var node = _tree.SelectedNode;
        if (node?.Tag is RuleDefinition rule)
        {
            using var editor = new RuleEditorForm(VisualEditorSupport.Clone(rule), _availableFields);
            if (editor.ShowDialog(this) == DialogResult.OK && editor.Definition is not null)
            {
                CopyRule(editor.Definition, rule);
                UpdateRuleNodeText(node);
            }
            return;
        }

        if (node?.Tag is RuleGroupDefinition group)
        {
            using var editor = new RuleGroupEditorForm(group.Operator, group.Negate);
            if (editor.ShowDialog(this) == DialogResult.OK)
            {
                group.Operator = editor.SelectedOperator;
                group.Negate = editor.Negate;
                UpdateGroupNodeText(node, node.Parent is null);
            }
        }
    }

    private static void CopyRule(RuleDefinition source, RuleDefinition destination)
    {
        destination.Field = source.Field;
        destination.Operator = source.Operator;
        destination.ExpectedValue = source.ExpectedValue;
        destination.ExpectedField = source.ExpectedField;
        destination.CaseSensitive = source.CaseSensitive;
        destination.Culture = source.Culture;
    }

    private void DeleteSelected()
    {
        var node = _tree.SelectedNode;
        if (node is null || node.Parent is null) return;
        if (MessageBox.Show(this, $"Excluir '{node.Text}'?", "Confirmação", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        if (node.Parent.Tag is RuleGroupDefinition parent)
        {
            if (node.Tag is RuleDefinition rule) parent.Rules.Remove(rule);
            if (node.Tag is RuleGroupDefinition group) parent.Groups.Remove(group);
        }
        node.Remove();
    }

    private void Clear()
    {
        if (MessageBox.Show(this, "Remover todas as condições? Um grupo sem condições é considerado verdadeiro.", "Limpar critérios", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var root = new RuleGroupDefinition { Operator = LogicalOperator.And };
        _tree.Nodes.Clear();
        _tree.Nodes.Add(CreateGroupNode(root, true));
        _tree.SelectedNode = _tree.Nodes[0];
    }
}
