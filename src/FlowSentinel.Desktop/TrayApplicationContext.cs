using FlowSentinel.Application;
using FlowSentinel.Infrastructure;

namespace FlowSentinel.Desktop;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly MainForm _mainForm;
    private readonly DesktopNotificationSink _notificationSink;
    private readonly AppPaths _paths;
    private readonly DesktopSettingsService _settingsService;
    private readonly WindowsServiceManager _serviceManager;
    private readonly DesktopLaunchOptions _launchOptions;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _notificationTimer;
    private readonly ToolStripMenuItem _startupMenuItem;
    private bool _initialStateApplied;

    public TrayApplicationContext(
        MainForm mainForm,
        DesktopNotificationSink notificationSink,
        AppPaths paths,
        DesktopSettingsService settingsService,
        WindowsServiceManager serviceManager,
        DesktopLaunchOptions launchOptions)
    {
        _mainForm = mainForm;
        _notificationSink = notificationSink;
        _paths = paths;
        _settingsService = settingsService;
        _serviceManager = serviceManager;
        _launchOptions = launchOptions;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir painel", null, (_, _) => OpenDashboard());
        menu.Items.Add("Executar selecionada", null, async (_, _) => await _mainForm.ExecuteSelectedAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Configurações", null, (_, _) => OpenSettings());
        menu.Items.Add("Sobre o FlowSentinel", null, (_, _) => OpenAbout());
        _startupMenuItem = new ToolStripMenuItem("Iniciar com o Windows")
        {
            Checked = StartupRegistration.IsEnabled(),
            CheckOnClick = true
        };
        _startupMenuItem.Click += (_, _) => ToggleStartupFromTray();
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Abrir pasta de dados", null, (_, _) => OpenFolder(_paths.DataDirectory));
        menu.Items.Add("Abrir logs", null, (_, _) => OpenFolder(_paths.LogDirectory));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => RequestExit());

        _notifyIcon = new NotifyIcon
        {
            Text = "FlowSentinel",
            Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath) ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenDashboard();

        _notificationTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _notificationTimer.Tick += (_, _) => FlushNotifications();
        _notificationTimer.Start();

        _mainForm.SettingsRequested += (_, _) => OpenSettings();
        _mainForm.AboutRequested += (_, _) => OpenAbout();
        _mainForm.ExitRequested += (_, _) => RequestExit(forceConfirmation: false);
        System.Windows.Forms.Application.Idle += ApplyInitialState;
    }

    private void ApplyInitialState(object? sender, EventArgs eventArgs)
    {
        if (_initialStateApplied)
        {
            return;
        }

        _initialStateApplied = true;
        System.Windows.Forms.Application.Idle -= ApplyInitialState;

        if (_launchOptions.StartInTray)
        {
            var settings = _settingsService.Current;
            if (settings.ShowTrayNotifications)
            {
                _notifyIcon.BalloonTipTitle = "FlowSentinel";
                _notifyIcon.BalloonTipText = "A aplicação foi iniciada e está operando na bandeja do Windows.";
                _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                _notifyIcon.ShowBalloonTip(3500);
            }
            return;
        }

        OpenDashboard();
    }

    private void OpenDashboard()
    {
        if (_mainForm.Visible)
        {
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.Activate();
            return;
        }

        _mainForm.Show();
        _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.Activate();
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settingsService, _serviceManager, _paths);
        if (form.ShowDialog(_mainForm.Visible ? _mainForm : null) == DialogResult.OK)
        {
            _startupMenuItem.Checked = StartupRegistration.IsEnabled();
        }
    }

    private void OpenAbout()
    {
        using var form = new AboutForm();
        form.ShowDialog(_mainForm.Visible ? _mainForm : null);
    }

    private void ToggleStartupFromTray()
    {
        try
        {
            var settings = _settingsService.Current;
            settings.StartWithWindows = _startupMenuItem.Checked;
            _settingsService.Save(settings);
            StartupRegistration.SetEnabled(settings.StartWithWindows, settings.StartMinimizedToTray);
        }
        catch (Exception exception)
        {
            _startupMenuItem.Checked = StartupRegistration.IsEnabled();
            MessageBox.Show(
                exception.Message,
                "FlowSentinel",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void FlushNotifications()
    {
        if (!_settingsService.Current.ShowTrayNotifications)
        {
            while (_notificationSink.TryDequeue(out _))
            {
            }
            return;
        }

        while (_notificationSink.TryDequeue(out var notification))
        {
            _notifyIcon.BalloonTipTitle = Truncate(notification.Title, 63);
            _notifyIcon.BalloonTipText = Truncate(notification.Message, 255);
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(5000);
        }
    }

    private void RequestExit(bool forceConfirmation = true)
    {
        var settings = _settingsService.Current;
        if (forceConfirmation && settings.ConfirmBeforeExit && MessageBox.Show(
                _mainForm.Visible ? _mainForm : null,
                "Encerrar o FlowSentinel? O processamento do Desktop será interrompido.",
                "Confirmação",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        ExitThread();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    protected override void ExitThreadCore()
    {
        System.Windows.Forms.Application.Idle -= ApplyInitialState;
        _notificationTimer.Stop();
        _notificationTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _mainForm.PrepareForExit();
        _mainForm.Close();
        base.ExitThreadCore();
    }
}
