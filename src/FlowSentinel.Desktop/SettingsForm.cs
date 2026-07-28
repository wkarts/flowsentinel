using FlowSentinel.Infrastructure;

namespace FlowSentinel.Desktop;

internal sealed class SettingsForm : Form
{
    private readonly DesktopSettingsService _settingsService;
    private readonly WindowsServiceManager _serviceManager;
    private readonly AppPaths _paths;

    private readonly CheckBox _showSplash = new() { Text = "Exibir splash screen ao iniciar manualmente" };
    private readonly CheckBox _showSplashOnStartup = new() { Text = "Exibir splash screen na inicialização automática" };
    private readonly CheckBox _openManual = new() { Text = "Abrir o painel no início manual" };
    private readonly CheckBox _startMinimized = new() { Text = "Iniciar minimizado no tray ao entrar no Windows" };
    private readonly CheckBox _trayNotifications = new() { Text = "Exibir notificações na bandeja do Windows" };
    private readonly CheckBox _confirmExit = new() { Text = "Confirmar antes de encerrar a aplicação" };
    private readonly ComboBox _closeBehavior = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };

    private readonly CheckBox _startWithWindows = new() { Text = "Iniciar o FlowSentinel com o Windows" };
    private readonly Label _startupCommand = new() { AutoSize = true, MaximumSize = new Size(720, 0) };

    private readonly CheckBox _schedulerEnabled = new() { Text = "Habilitar agendador de automações" };
    private readonly NumericUpDown _schedulerSeconds = CreateNumber(1, 3600);
    private readonly CheckBox _dispatcherEnabled = new() { Text = "Habilitar processador de notificações" };
    private readonly NumericUpDown _dispatcherSeconds = CreateNumber(1, 3600);
    private readonly NumericUpDown _maxDeliveries = CreateNumber(1, 1000);
    private readonly NumericUpDown _maxParallel = CreateNumber(1, 64);
    private readonly CheckBox _applyProcessingToService = new()
    {
        Text = "Aplicar os mesmos parâmetros ao Windows Service",
        Checked = true
    };

    private readonly TextBox _serviceExecutable = new() { Dock = DockStyle.Fill };
    private readonly TextBox _serviceDataRoot = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _serviceStartupType = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly Label _serviceStatus = new()
    {
        Text = "Consultando...",
        AutoSize = true,
        Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold)
    };
    private readonly Label _operationStatus = new() { AutoSize = true, ForeColor = Color.DimGray };

    internal SettingsForm(
        DesktopSettingsService settingsService,
        WindowsServiceManager serviceManager,
        AppPaths paths)
    {
        _settingsService = settingsService;
        _serviceManager = serviceManager;
        _paths = paths;

        Text = "Configurações do FlowSentinel";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 610);
        Size = new Size(900, 680);
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath) ?? SystemIcons.Application;

        VisualEditorSupport.SetDisplayItems(_closeBehavior, new[]
        {
            new DisplayItem<DesktopCloseBehavior>(DesktopCloseBehavior.MinimizeToTray, "Minimizar para o tray"),
            new DisplayItem<DesktopCloseBehavior>(DesktopCloseBehavior.Ask, "Perguntar ao fechar"),
            new DisplayItem<DesktopCloseBehavior>(DesktopCloseBehavior.Exit, "Encerrar a aplicação")
        });
        _serviceStartupType.Items.AddRange(["Automatic", "Manual", "Disabled"]);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreateGeneralTab());
        tabs.TabPages.Add(CreateStartupTab());
        tabs.TabPages.Add(CreateProcessingTab());
        tabs.TabPages.Add(CreateServiceTab());
        tabs.TabPages.Add(CreateDirectoriesTab());

        var save = new Button { Text = "Salvar", AutoSize = true, Height = 34 };
        save.Click += (_, _) => SaveAndClose();
        var cancel = new Button { Text = "Cancelar", AutoSize = true, Height = 34, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        buttons.Controls.Add(_operationStatus);

        Controls.Add(tabs);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;

        LoadValues(_settingsService.Current);
        Shown += async (_, _) => await RefreshServiceStatusAsync();
    }

    private TabPage CreateGeneralTab()
    {
        var page = new TabPage("Geral");
        var panel = CreateStackPanel();
        panel.Controls.Add(CreateTitle("Experiência da aplicação"));
        panel.Controls.Add(_showSplash);
        panel.Controls.Add(_showSplashOnStartup);
        panel.Controls.Add(_openManual);
        panel.Controls.Add(_startMinimized);
        panel.Controls.Add(_trayNotifications);
        panel.Controls.Add(CreateLabeledControl("Ao clicar no X da janela:", _closeBehavior));
        panel.Controls.Add(_confirmExit);
        page.Controls.Add(panel);
        return page;
    }

    private TabPage CreateStartupTab()
    {
        var page = new TabPage("Inicialização");
        var panel = CreateStackPanel();
        panel.Controls.Add(CreateTitle("Inicialização com o Windows"));
        panel.Controls.Add(_startWithWindows);
        panel.Controls.Add(new Label
        {
            Text = "A inicialização é registrada apenas para o usuário atual e não exige privilégios administrativos.",
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            ForeColor = Color.DimGray
        });
        panel.Controls.Add(CreateTitle("Comando registrado"));
        panel.Controls.Add(_startupCommand);
        page.Controls.Add(panel);
        return page;
    }

    private TabPage CreateProcessingTab()
    {
        var page = new TabPage("Processamento");
        var panel = CreateStackPanel();
        panel.Controls.Add(CreateTitle("Agendador de automações"));
        panel.Controls.Add(_schedulerEnabled);
        panel.Controls.Add(CreateLabeledControl("Verificar automações a cada (segundos):", _schedulerSeconds));
        panel.Controls.Add(CreateTitle("Fila de notificações"));
        panel.Controls.Add(_dispatcherEnabled);
        panel.Controls.Add(CreateLabeledControl("Verificar entregas a cada (segundos):", _dispatcherSeconds));
        panel.Controls.Add(CreateLabeledControl("Máximo de entregas por ciclo:", _maxDeliveries));
        panel.Controls.Add(CreateLabeledControl("Máximo de envios paralelos:", _maxParallel));
        panel.Controls.Add(_applyProcessingToService);
        panel.Controls.Add(new Label
        {
            Text = "As alterações do Desktop entram em vigor sem reiniciar. O serviço lê o arquivo de parâmetros periodicamente.",
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            ForeColor = Color.DimGray
        });
        page.Controls.Add(panel);
        return page;
    }

    private TabPage CreateServiceTab()
    {
        var page = new TabPage("Windows Service");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(18),
            AutoScroll = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var browseExe = new Button { Text = "Procurar...", AutoSize = true };
        browseExe.Click += (_, _) => BrowseServiceExecutable();
        var browseRoot = new Button { Text = "Selecionar...", AutoSize = true };
        browseRoot.Click += (_, _) => BrowseServiceDataRoot();

        AddRow(panel, "Estado do serviço:", _serviceStatus, CreateAsyncButton("Atualizar", RefreshServiceStatusAsync));
        AddRow(panel, "Executável:", _serviceExecutable, browseExe);
        AddRow(panel, "Diretório de dados:", _serviceDataRoot, browseRoot);
        AddRow(panel, "Tipo de inicialização:", _serviceStartupType, new Panel());

        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        actions.Controls.Add(CreateAsyncButton("Instalar / Atualizar", InstallServiceAsync));
        actions.Controls.Add(CreateAsyncButton("Iniciar", () => RunServiceActionAsync(
            () => _serviceManager.StartAsync(),
            "Serviço iniciado.")));
        actions.Controls.Add(CreateAsyncButton("Parar", () => RunServiceActionAsync(
            () => _serviceManager.StopAsync(),
            "Serviço parado.")));
        actions.Controls.Add(CreateAsyncButton("Remover", RemoveServiceAsync));
        actions.Controls.Add(CreateButton("Abrir dados do serviço", () => OpenFolder(_serviceDataRoot.Text)));

        panel.Controls.Add(new Label { Text = "Operações:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, panel.RowCount);
        panel.Controls.Add(actions, 1, panel.RowCount);
        panel.SetColumnSpan(actions, 2);
        panel.RowCount++;

        var warning = new Label
        {
            Text = "Instalar, iniciar, parar ou remover o serviço solicita permissão de Administrador do Windows.",
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(3, 18, 3, 3)
        };
        panel.Controls.Add(warning, 0, panel.RowCount);
        panel.SetColumnSpan(warning, 3);
        panel.RowCount++;

        page.Controls.Add(panel);
        return page;
    }

    private TabPage CreateDirectoriesTab()
    {
        var page = new TabPage("Dados e logs");
        var panel = CreateStackPanel();
        panel.Controls.Add(CreateTitle("Arquivos locais do Desktop"));
        panel.Controls.Add(CreatePathBlock("Configurações:", _settingsService.SettingsPath));
        panel.Controls.Add(CreatePathBlock("Dados:", _paths.DataDirectory));
        panel.Controls.Add(CreatePathBlock("Logs:", _paths.LogDirectory));

        var actions = new FlowLayoutPanel { AutoSize = true };
        actions.Controls.Add(CreateButton("Abrir dados", () => OpenFolder(_paths.DataDirectory)));
        actions.Controls.Add(CreateButton("Abrir logs", () => OpenFolder(_paths.LogDirectory)));
        actions.Controls.Add(CreateButton("Abrir configurações", () => OpenFolder(Path.GetDirectoryName(_settingsService.SettingsPath)!)));
        panel.Controls.Add(actions);
        page.Controls.Add(panel);
        return page;
    }

    private void LoadValues(DesktopSettings settings)
    {
        _showSplash.Checked = settings.ShowSplashScreen;
        _showSplashOnStartup.Checked = settings.ShowSplashOnWindowsStartup;
        _openManual.Checked = settings.OpenMainWindowOnManualStart;
        _startMinimized.Checked = settings.StartMinimizedToTray;
        _trayNotifications.Checked = settings.ShowTrayNotifications;
        _confirmExit.Checked = settings.ConfirmBeforeExit;
        VisualEditorSupport.SelectDisplayItem(
            _closeBehavior,
            settings.CloseBehavior,
            DesktopCloseBehavior.MinimizeToTray);

        _startWithWindows.Checked = settings.StartWithWindows;
        _startupCommand.Text = StartupRegistration.BuildCommand(startMinimized: true);

        _schedulerEnabled.Checked = settings.AutomationSchedulerEnabled;
        _schedulerSeconds.Value = settings.AutomationSchedulerPollingSeconds;
        _dispatcherEnabled.Checked = settings.DeliveryDispatcherEnabled;
        _dispatcherSeconds.Value = settings.DeliveryDispatcherPollingSeconds;
        _maxDeliveries.Value = settings.MaxDeliveriesPerCycle;
        _maxParallel.Value = settings.MaxParallelDeliveries;

        _serviceExecutable.Text = settings.ServiceExecutablePath;
        _serviceDataRoot.Text = settings.ServiceDataRoot;
        _serviceStartupType.SelectedItem = settings.ServiceStartupType;
        if (_serviceStartupType.SelectedIndex < 0)
        {
            _serviceStartupType.SelectedIndex = 0;
        }
    }

    private void SaveAndClose()
    {
        try
        {
            var settings = _settingsService.Current;
            settings.ShowSplashScreen = _showSplash.Checked;
            settings.ShowSplashOnWindowsStartup = _showSplashOnStartup.Checked;
            settings.OpenMainWindowOnManualStart = _openManual.Checked;
            settings.StartMinimizedToTray = _startMinimized.Checked;
            settings.ShowTrayNotifications = _trayNotifications.Checked;
            settings.ConfirmBeforeExit = _confirmExit.Checked;
            settings.CloseBehavior = VisualEditorSupport.SelectedValue(
                _closeBehavior,
                DesktopCloseBehavior.MinimizeToTray);
            settings.StartWithWindows = _startWithWindows.Checked;
            settings.AutomationSchedulerEnabled = _schedulerEnabled.Checked;
            settings.AutomationSchedulerPollingSeconds = Decimal.ToInt32(_schedulerSeconds.Value);
            settings.DeliveryDispatcherEnabled = _dispatcherEnabled.Checked;
            settings.DeliveryDispatcherPollingSeconds = Decimal.ToInt32(_dispatcherSeconds.Value);
            settings.MaxDeliveriesPerCycle = Decimal.ToInt32(_maxDeliveries.Value);
            settings.MaxParallelDeliveries = Decimal.ToInt32(_maxParallel.Value);
            settings.ServiceExecutablePath = _serviceExecutable.Text;
            settings.ServiceDataRoot = _serviceDataRoot.Text;
            settings.ServiceStartupType = _serviceStartupType.SelectedItem?.ToString() ?? "Automatic";

            _settingsService.Save(settings);
            StartupRegistration.SetEnabled(settings.StartWithWindows, settings.StartMinimizedToTray);

            if (_applyProcessingToService.Checked)
            {
                try
                {
                    _settingsService.WriteServiceRuntimeSettings(settings);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        $"As configurações do Desktop foram salvas, mas não foi possível atualizar os parâmetros do serviço.\n\n{exception.Message}",
                        "Windows Service",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Configurações", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task InstallServiceAsync()
    {
        await RunServiceActionAsync(
            async () =>
            {
                await _serviceManager.InstallOrUpdateAsync(
                    _serviceExecutable.Text,
                    _serviceDataRoot.Text,
                    _serviceStartupType.SelectedItem?.ToString() ?? "Automatic");
            },
            "Serviço instalado ou atualizado.");
    }

    private async Task RemoveServiceAsync()
    {
        if (MessageBox.Show(
                this,
                "Remover o Windows Service do FlowSentinel? Os dados do serviço serão preservados.",
                "Confirmação",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        await RunServiceActionAsync(
            () => _serviceManager.UninstallAsync(_serviceExecutable.Text),
            "Serviço removido.");
    }

    private async Task RunServiceActionAsync(Func<Task> action, string successMessage)
    {
        try
        {
            Enabled = false;
            _operationStatus.Text = "Executando operação administrativa...";
            await action();
            _operationStatus.Text = successMessage;
            await RefreshServiceStatusAsync();
        }
        catch (OperationCanceledException exception)
        {
            _operationStatus.Text = exception.Message;
        }
        catch (Exception exception)
        {
            _operationStatus.Text = "Falha na operação.";
            MessageBox.Show(this, exception.Message, "Windows Service", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Enabled = true;
        }
    }

    private async Task RefreshServiceStatusAsync()
    {
        try
        {
            var status = await _serviceManager.QueryAsync();
            _serviceStatus.Text = status.DisplayText;
            _serviceStatus.ForeColor = status.State switch
            {
                WindowsServiceState.Running => Color.DarkGreen,
                WindowsServiceState.NotInstalled => Color.DimGray,
                WindowsServiceState.Stopped => Color.DarkOrange,
                _ => Color.FromArgb(5, 75, 145)
            };
        }
        catch (Exception exception)
        {
            _serviceStatus.Text = $"Não foi possível consultar: {exception.Message}";
            _serviceStatus.ForeColor = Color.DarkRed;
        }
    }

    private void BrowseServiceExecutable()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "FlowSentinel Service (FlowSentinel.Service.exe)|FlowSentinel.Service.exe|Executáveis (*.exe)|*.exe",
            FileName = _serviceExecutable.Text,
            Title = "Selecionar executável do serviço"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _serviceExecutable.Text = dialog.FileName;
        }
    }

    private void BrowseServiceDataRoot()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecione o diretório de dados do Windows Service",
            SelectedPath = _serviceDataRoot.Text,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _serviceDataRoot.Text = dialog.SelectedPath;
        }
    }

    private static FlowLayoutPanel CreateStackPanel() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(20)
    };

    private static Label CreateTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
        ForeColor = Color.FromArgb(5, 39, 83),
        Margin = new Padding(3, 12, 3, 8)
    };

    private static Control CreateLabeledControl(string label, Control control)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Width = 310,
            Padding = new Padding(0, 6, 0, 0)
        });
        panel.Controls.Add(control);
        return panel;
    }

    private static Control CreatePathBlock(string label, string path)
    {
        var panel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Width = 720 };
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold) });
        panel.Controls.Add(new Label { Text = path, AutoSize = true, MaximumSize = new Size(700, 0), ForeColor = Color.DimGray });
        return panel;
    }

    private static NumericUpDown CreateNumber(int minimum, int maximum) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Width = 110
    };

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 32 };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button CreateAsyncButton(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 32 };
        button.Click += async (_, _) => await action();
        return button;
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control value, Control action)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 7, 0, 0)
        }, 0, row);
        panel.Controls.Add(value, 1, row);
        panel.Controls.Add(action, 2, row);
    }

    private static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
