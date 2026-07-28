using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class ChannelManagerForm : Form
{
    private readonly IFlowStore _store;
    private readonly ISecretProtector _secretProtector;
    private readonly IEvolutionInstanceService _evolutionInstanceService;
    private readonly DataGridView _grid = new();

    public ChannelManagerForm(
        IFlowStore store,
        ISecretProtector secretProtector,
        IEvolutionInstanceService evolutionInstanceService)
    {
        _store = store;
        _secretProtector = secretProtector;
        _evolutionInstanceService = evolutionInstanceService;

        Text = "Canais de notificação";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(850, 520);
        MinimumSize = new Size(700, 420);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8)
        };
        AddButton(toolbar, "Novo canal", async (_, _) => await CreateAsync());
        AddButton(toolbar, "Editar canal", async (_, _) => await EditAsync());
        AddButton(toolbar, "Excluir", async (_, _) => await DeleteAsync());
        AddButton(toolbar, "Status Evolution", async (_, _) => await CheckEvolutionStatusAsync());
        AddButton(toolbar, "QR Code Evolution", async (_, _) => await ShowEvolutionQrAsync());
        AddButton(toolbar, "Atualizar", async (_, _) => await RefreshAsync());

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ChannelRow.Name), HeaderText = "Nome", FillWeight = 160 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ChannelRow.Type), HeaderText = "Tipo", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(ChannelRow.Enabled), HeaderText = "Ativo", FillWeight = 40 });
        _grid.CellDoubleClick += async (_, _) => await EditAsync();

        var close = new Button { Text = "Fechar", Dock = DockStyle.Bottom, Height = 38, DialogResult = DialogResult.OK };
        Controls.Add(_grid);
        Controls.Add(toolbar);
        Controls.Add(close);
        Load += async (_, _) => await RefreshAsync();
    }

    private static void AddButton(Control parent, string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += handler;
        parent.Controls.Add(button);
    }

    private async Task RefreshAsync()
    {
        var channels = await _store.GetChannelConfigurationsAsync(CancellationToken.None);
        _grid.DataSource = channels.Select(x => new ChannelRow
        {
            Id = x.Id,
            Name = x.Name,
            Type = VisualEditorSupport.ChannelTypeText(x.Type),
            Enabled = x.Enabled
        }).ToList();
    }

    private async Task CreateAsync()
    {
        try
        {
            using var editor = new ChannelEditorForm(
                new ChannelConfiguration
                {
                    Id = Guid.NewGuid(),
                    Name = "Novo canal",
                    Type = ChannelType.Email,
                    Enabled = false,
                    SettingsJson = "{}"
                },
                _secretProtector);
            if (editor.ShowDialog(this) == DialogResult.OK && editor.Configuration is not null)
            {
                await _store.SaveChannelConfigurationAsync(editor.Configuration, CancellationToken.None);
                await RefreshAsync();
            }
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Cadastro de canal");
        }
    }

    private async Task EditAsync()
    {
        try
        {
            var selected = Selected();
            if (selected is null)
            {
                return;
            }
            var configuration = await _store.GetChannelConfigurationAsync(selected.Id, CancellationToken.None);
            if (configuration is null)
            {
                await RefreshAsync();
                return;
            }
            using var editor = new ChannelEditorForm(configuration, _secretProtector);
            if (editor.ShowDialog(this) == DialogResult.OK && editor.Configuration is not null)
            {
                await _store.SaveChannelConfigurationAsync(editor.Configuration, CancellationToken.None);
                await RefreshAsync();
            }
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Edição de canal");
        }
    }

    private async Task DeleteAsync()
    {
        var selected = Selected();
        if (selected is null)
        {
            return;
        }
        if (MessageBox.Show(this, $"Excluir o canal '{selected.Name}'?", "Confirmação", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }
        try
        {
            await _store.DeleteChannelConfigurationAsync(selected.Id, CancellationToken.None);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "FlowSentinel", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CheckEvolutionStatusAsync()
    {
        var configuration = await GetSelectedEvolutionAsync();
        if (configuration is null)
        {
            return;
        }
        try
        {
            var status = await _evolutionInstanceService.GetStatusAsync(configuration, CancellationToken.None);
            MessageBox.Show(
                this,
                $"Estado: {status.State}\nConectada: {(status.Connected ? "Sim" : "Não")}\n\n{status.RawResponse}",
                "Evolution API",
                MessageBoxButtons.OK,
                status.Connected ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Evolution API", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ShowEvolutionQrAsync()
    {
        var configuration = await GetSelectedEvolutionAsync();
        if (configuration is null)
        {
            return;
        }
        try
        {
            var status = await _evolutionInstanceService.GetStatusAsync(configuration, CancellationToken.None);
            if (status.Connected)
            {
                MessageBox.Show(
                    this,
                    $"A instância já está conectada. Estado: {status.State}",
                    "Evolution API",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var result = await _evolutionInstanceService.GetQrCodeAsync(configuration, CancellationToken.None);
            using var form = new EvolutionQrForm(result, _evolutionInstanceService, configuration);
            form.ShowDialog(this);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Evolution API", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task<ChannelConfiguration?> GetSelectedEvolutionAsync()
    {
        var selected = Selected();
        if (selected is null)
        {
            return null;
        }
        var configuration = await _store.GetChannelConfigurationAsync(selected.Id, CancellationToken.None);
        if (configuration?.Type != ChannelType.EvolutionApi)
        {
            MessageBox.Show(this, "Selecione uma configuração da Evolution API.", "FlowSentinel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }
        return configuration;
    }

    private ChannelRow? Selected() => _grid.CurrentRow?.DataBoundItem as ChannelRow;

    private sealed class ChannelRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public bool Enabled { get; init; }
    }
}
