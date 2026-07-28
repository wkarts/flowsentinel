using System.Text.Json;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal static class VisualEditorSupport
{
    internal static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, FlowJson.Options);
        return JsonSerializer.Deserialize<T>(json, FlowJson.Options)
               ?? throw new InvalidOperationException("Não foi possível copiar a configuração.");
    }

    internal static string RuleOperatorText(RuleOperator value) => value switch
    {
        RuleOperator.Equal => "É igual a",
        RuleOperator.NotEqual => "É diferente de",
        RuleOperator.Contains => "Contém",
        RuleOperator.NotContains => "Não contém",
        RuleOperator.StartsWith => "Começa com",
        RuleOperator.EndsWith => "Termina com",
        RuleOperator.IsEmpty => "Está vazio",
        RuleOperator.IsNotEmpty => "Não está vazio",
        RuleOperator.GreaterThan => "É maior que",
        RuleOperator.GreaterThanOrEqual => "É maior ou igual a",
        RuleOperator.LessThan => "É menor que",
        RuleOperator.LessThanOrEqual => "É menor ou igual a",
        RuleOperator.In => "Está entre os valores",
        RuleOperator.NotIn => "Não está entre os valores",
        RuleOperator.Regex => "Corresponde à expressão regular",
        RuleOperator.Exists => "Existe",
        RuleOperator.NotExists => "Não existe",
        RuleOperator.Changed => "Foi alterado",
        RuleOperator.Unchanged => "Não foi alterado",
        RuleOperator.ChangedFromTo => "Mudou de... para...",
        _ => value.ToString()
    };

    internal static string SourceTypeText(SourceType value) => value switch
    {
        SourceType.Excel => "Planilha Excel",
        SourceType.Csv => "Arquivo CSV",
        SourceType.Text => "Arquivo TXT",
        SourceType.Database => "Banco de dados",
        _ => value.ToString()
    };

    internal static string ChannelTypeText(ChannelType value) => value switch
    {
        ChannelType.LocalWindows => "Notificação do Windows",
        ChannelType.EvolutionApi => "WhatsApp - Evolution API",
        ChannelType.Telegram => "Telegram",
        ChannelType.Email => "E-mail SMTP",
        _ => value.ToString()
    };

    internal static string RecipientTypeText(RecipientType value) => value switch
    {
        RecipientType.Fixed => "Endereço fixo",
        RecipientType.Field => "Campo da fonte",
        RecipientType.Group => "Grupo de contatos",
        _ => value.ToString()
    };

    internal static int ToSeconds(decimal value, string unit) => unit switch
    {
        "Minutos" => checked((int)value * 60),
        "Horas" => checked((int)value * 3600),
        "Dias" => checked((int)value * 86400),
        _ => checked((int)value)
    };

    internal static (decimal Value, string Unit) FromSeconds(int seconds)
    {
        if (seconds > 0 && seconds % 86400 == 0) return (seconds / 86400, "Dias");
        if (seconds > 0 && seconds % 3600 == 0) return (seconds / 3600, "Horas");
        if (seconds > 0 && seconds % 60 == 0) return (seconds / 60, "Minutos");
        return (Math.Max(1, seconds), "Segundos");
    }

    internal static Label LabelFor(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 8, 3, 3)
    };

    internal static bool SelectDisplayItem<T>(ComboBox comboBox, T value, T fallback)
    {
        ArgumentNullException.ThrowIfNull(comboBox);

        var items = GetDisplayItems<T>(comboBox);
        var selected = ResolveDisplayItem(items, value, fallback);

        if (selected is null)
        {
            comboBox.SelectedIndex = -1;
            return false;
        }

        var selectedIndex = items.IndexOf(selected);
        if (selectedIndex >= 0)
        {
            comboBox.SelectedIndex = selectedIndex;
        }
        else
        {
            comboBox.SelectedItem = selected;
        }

        return true;
    }

    internal static DisplayItem<T>? ResolveDisplayItem<T>(
        IEnumerable<DisplayItem<T>> items,
        T value,
        T fallback)
    {
        ArgumentNullException.ThrowIfNull(items);

        var materialized = items as IReadOnlyList<DisplayItem<T>> ?? items.ToList();
        var comparer = EqualityComparer<T>.Default;
        return materialized.FirstOrDefault(item => comparer.Equals(item.Value, value))
               ?? materialized.FirstOrDefault(item => comparer.Equals(item.Value, fallback))
               ?? materialized.FirstOrDefault();
    }

    private static List<DisplayItem<T>> GetDisplayItems<T>(ComboBox comboBox)
    {
        if (comboBox.DataSource is IEnumerable<DisplayItem<T>> dataSource)
        {
            return dataSource.ToList();
        }

        return comboBox.Items.Cast<object>()
            .OfType<DisplayItem<T>>()
            .ToList();
    }

    internal static void ShowError(IWin32Window owner, Exception exception, string title = "FlowSentinel") =>
        MessageBox.Show(owner, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
}

internal sealed class DisplayItem<T>
{
    internal DisplayItem(T value, string text)
    {
        Value = value;
        Text = text;
    }

    public T Value { get; }
    public string Text { get; }
    public override string ToString() => Text;
}
