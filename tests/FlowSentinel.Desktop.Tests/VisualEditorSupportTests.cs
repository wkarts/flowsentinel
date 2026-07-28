using FlowSentinel.Domain;

namespace FlowSentinel.Desktop.Tests;

public sealed class VisualEditorSupportTests
{
    [Fact]
    public void ResolveDisplayItemReturnsRequestedValue()
    {
        var items = new[]
        {
            new DisplayItem<MissingRecordBehavior>(MissingRecordBehavior.Ignore, "Ignorar"),
            new DisplayItem<MissingRecordBehavior>(MissingRecordBehavior.Resolve, "Concluir")
        };

        var selected = VisualEditorSupport.ResolveDisplayItem(
            items,
            MissingRecordBehavior.Resolve,
            MissingRecordBehavior.Ignore);

        Assert.NotNull(selected);
        Assert.Equal(MissingRecordBehavior.Resolve, selected.Value);
    }

    [Fact]
    public void ResolveDisplayItemUsesFallbackForUnknownLegacyValue()
    {
        var items = new[]
        {
            new DisplayItem<ChannelType>(ChannelType.LocalWindows, "Windows"),
            new DisplayItem<ChannelType>(ChannelType.Email, "E-mail")
        };

        var selected = VisualEditorSupport.ResolveDisplayItem(
            items,
            (ChannelType)999,
            ChannelType.LocalWindows);

        Assert.NotNull(selected);
        Assert.Equal(ChannelType.LocalWindows, selected.Value);
    }

    [Fact]
    public void ResolveDisplayItemReturnsNullForEmptyCollection()
    {
        var selected = VisualEditorSupport.ResolveDisplayItem(
            Array.Empty<DisplayItem<SourceType>>(),
            SourceType.Excel,
            SourceType.Excel);

        Assert.Null(selected);
    }
}
