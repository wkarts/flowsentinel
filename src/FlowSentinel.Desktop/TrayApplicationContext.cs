using FlowSentinel.Application;
using FlowSentinel.Infrastructure;

namespace FlowSentinel.Desktop;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly MainForm _mainForm;
    private readonly DesktopNotificationSink _notificationSink;
    private readonly AppPaths _paths;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _notificationTimer;

    public TrayApplicationContext(
        MainForm mainForm,
        DesktopNotificationSink notificationSink,
        AppPaths paths)
    {
        _mainForm = mainForm;
        _notificationSink = notificationSink;
        _paths = paths;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir painel", null, (_, _) => OpenDashboard());
        menu.Items.Add("Executar selecionada", null, async (_, _) => await _mainForm.ExecuteSelectedAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Abrir pasta de dados", null, (_, _) => OpenFolder(_paths.DataDirectory));
        menu.Items.Add("Abrir logs", null, (_, _) => OpenFolder(_paths.LogDirectory));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitThread());

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
    }

    private void OpenDashboard()
    {
        if (_mainForm.Visible)
        {
            _mainForm.Activate();
            return;
        }

        _mainForm.Show();
        _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.Activate();
    }

    private void FlushNotifications()
    {
        while (_notificationSink.TryDequeue(out var notification))
        {
            _notifyIcon.BalloonTipTitle = Truncate(notification.Title, 63);
            _notifyIcon.BalloonTipText = Truncate(notification.Message, 255);
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(5000);
        }
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
        _notificationTimer.Stop();
        _notificationTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _mainForm.PrepareForExit();
        _mainForm.Close();
        base.ExitThreadCore();
    }
}
