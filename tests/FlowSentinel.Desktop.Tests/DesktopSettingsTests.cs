using FlowSentinel.Infrastructure;

namespace FlowSentinel.Desktop.Tests;

public sealed class DesktopSettingsTests
{
    [Fact]
    public void DefaultsStartWithWindowsAndUseTray()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var service = new DesktopSettingsService(new AppPaths { RootDirectory = root });
            var settings = service.Current;

            Assert.True(settings.StartWithWindows);
            Assert.True(settings.StartMinimizedToTray);
            Assert.True(settings.ShowSplashScreen);
            Assert.Equal(DesktopCloseBehavior.MinimizeToTray, settings.CloseBehavior);
            Assert.EndsWith("FlowSentinel.Service.exe", settings.ServiceExecutablePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveAndReloadPreservesDesktopAndWorkerSettings()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var paths = new AppPaths { RootDirectory = root };
            var service = new DesktopSettingsService(paths);
            var settings = service.Current;
            settings.ShowSplashScreen = false;
            settings.StartWithWindows = false;
            settings.AutomationSchedulerPollingSeconds = 17;
            settings.MaxParallelDeliveries = 12;
            service.Save(settings);

            var reloaded = new DesktopSettingsService(paths);
            Assert.False(reloaded.Current.ShowSplashScreen);
            Assert.False(reloaded.Current.StartWithWindows);
            Assert.Equal(17, reloaded.AutomationSchedulerPollingSeconds);
            Assert.Equal(12, reloaded.MaxParallelDeliveries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StartupArgumentsOpenInTray()
    {
        var settings = new DesktopSettings
        {
            StartMinimizedToTray = true,
            OpenMainWindowOnManualStart = true
        };

        var options = DesktopLaunchOptions.Parse(["--startup"], settings);

        Assert.True(options.IsWindowsStartup);
        Assert.True(options.StartInTray);
    }

    [Fact]
    public void ManualLaunchOpensDashboardByDefault()
    {
        var settings = new DesktopSettings
        {
            StartMinimizedToTray = true,
            OpenMainWindowOnManualStart = true
        };

        var options = DesktopLaunchOptions.Parse(Array.Empty<string>(), settings);

        Assert.False(options.IsWindowsStartup);
        Assert.False(options.StartInTray);
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"FlowSentinel.Desktop.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
