using System.Runtime.ExceptionServices;
using System.Windows.Forms;
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

    [Fact]
    public void SelectDisplayItemMaterializesUninitializedComboBoxDataSource() => RunInSta(() =>
    {
        using var comboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DataSource = new[]
            {
                new DisplayItem<ChannelType>(ChannelType.LocalWindows, "Windows"),
                new DisplayItem<ChannelType>(ChannelType.EvolutionApi, "Evolution"),
                new DisplayItem<ChannelType>(ChannelType.Telegram, "Telegram"),
                new DisplayItem<ChannelType>(ChannelType.Email, "E-mail")
            }
        };

        var selected = VisualEditorSupport.SelectDisplayItem(
            comboBox,
            ChannelType.Email,
            ChannelType.LocalWindows);

        Assert.True(selected);
        Assert.Equal(4, comboBox.Items.Count);
        Assert.Equal(3, comboBox.SelectedIndex);
        Assert.Equal(
            ChannelType.Email,
            Assert.IsType<DisplayItem<ChannelType>>(comboBox.SelectedItem).Value);
    });

    [Fact]
    public void SetDisplayItemsCreatesImmediatelySelectableComboBoxItems() => RunInSta(() =>
    {
        using var comboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

        VisualEditorSupport.SetDisplayItems(comboBox, new[]
        {
            new DisplayItem<SourceType>(SourceType.Excel, "Excel"),
            new DisplayItem<SourceType>(SourceType.Database, "Banco")
        });

        var selected = VisualEditorSupport.SelectDisplayItem(
            comboBox,
            SourceType.Database,
            SourceType.Excel);

        Assert.True(selected);
        Assert.Equal(2, comboBox.Items.Count);
        Assert.Equal(1, comboBox.SelectedIndex);
    });

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
