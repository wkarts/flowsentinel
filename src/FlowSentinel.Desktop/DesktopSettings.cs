using FlowSentinel.Application;

namespace FlowSentinel.Desktop;

internal enum DesktopCloseBehavior
{
    MinimizeToTray,
    Ask,
    Exit
}

internal sealed class DesktopSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool ShowSplashScreen { get; set; } = true;
    public bool ShowSplashOnWindowsStartup { get; set; }
    public bool StartWithWindows { get; set; } = true;
    public bool StartMinimizedToTray { get; set; } = true;
    public bool OpenMainWindowOnManualStart { get; set; } = true;
    public bool ShowTrayNotifications { get; set; } = true;
    public DesktopCloseBehavior CloseBehavior { get; set; } = DesktopCloseBehavior.MinimizeToTray;
    public bool ConfirmBeforeExit { get; set; }
    public bool AutomationSchedulerEnabled { get; set; } = true;
    public int AutomationSchedulerPollingSeconds { get; set; } = 5;
    public bool DeliveryDispatcherEnabled { get; set; } = true;
    public int DeliveryDispatcherPollingSeconds { get; set; } = 2;
    public int MaxDeliveriesPerCycle { get; set; } = 50;
    public int MaxParallelDeliveries { get; set; } = 8;
    public string ServiceExecutablePath { get; set; } = string.Empty;
    public string ServiceDataRoot { get; set; } = string.Empty;
    public string ServiceStartupType { get; set; } = "Automatic";

    public void Normalize()
    {
        SchemaVersion = Math.Max(1, SchemaVersion);
        AutomationSchedulerPollingSeconds = Math.Clamp(AutomationSchedulerPollingSeconds, 1, 3600);
        DeliveryDispatcherPollingSeconds = Math.Clamp(DeliveryDispatcherPollingSeconds, 1, 3600);
        MaxDeliveriesPerCycle = Math.Clamp(MaxDeliveriesPerCycle, 1, 1000);
        MaxParallelDeliveries = Math.Clamp(MaxParallelDeliveries, 1, 64);
        ServiceExecutablePath = Environment.ExpandEnvironmentVariables(ServiceExecutablePath?.Trim() ?? string.Empty);
        ServiceDataRoot = Environment.ExpandEnvironmentVariables(ServiceDataRoot?.Trim() ?? string.Empty);
        ServiceStartupType = ServiceStartupType switch
        {
            "Manual" => "Manual",
            "Disabled" => "Disabled",
            _ => "Automatic"
        };
    }

    public DesktopSettings Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        ShowSplashScreen = ShowSplashScreen,
        ShowSplashOnWindowsStartup = ShowSplashOnWindowsStartup,
        StartWithWindows = StartWithWindows,
        StartMinimizedToTray = StartMinimizedToTray,
        OpenMainWindowOnManualStart = OpenMainWindowOnManualStart,
        ShowTrayNotifications = ShowTrayNotifications,
        CloseBehavior = CloseBehavior,
        ConfirmBeforeExit = ConfirmBeforeExit,
        AutomationSchedulerEnabled = AutomationSchedulerEnabled,
        AutomationSchedulerPollingSeconds = AutomationSchedulerPollingSeconds,
        DeliveryDispatcherEnabled = DeliveryDispatcherEnabled,
        DeliveryDispatcherPollingSeconds = DeliveryDispatcherPollingSeconds,
        MaxDeliveriesPerCycle = MaxDeliveriesPerCycle,
        MaxParallelDeliveries = MaxParallelDeliveries,
        ServiceExecutablePath = ServiceExecutablePath,
        ServiceDataRoot = ServiceDataRoot,
        ServiceStartupType = ServiceStartupType
    };
}

internal sealed class DesktopSettingsService : IWorkerRuntimeSettings
{
    private readonly object _sync = new();
    private readonly string _settingsPath;
    private DesktopSettings _settings;

    public DesktopSettingsService(FlowSentinel.Infrastructure.AppPaths paths)
    {
        _settingsPath = Path.Combine(paths.RootDirectory, "desktop-settings.json");
        _settings = LoadFromDisk();
    }

    internal string SettingsPath => _settingsPath;

    internal DesktopSettings Current
    {
        get
        {
            lock (_sync)
            {
                return _settings.Clone();
            }
        }
    }

    public bool AutomationSchedulerEnabled => Read(x => x.AutomationSchedulerEnabled);
    public int AutomationSchedulerPollingSeconds => Read(x => x.AutomationSchedulerPollingSeconds);
    public bool DeliveryDispatcherEnabled => Read(x => x.DeliveryDispatcherEnabled);
    public int DeliveryDispatcherPollingSeconds => Read(x => x.DeliveryDispatcherPollingSeconds);
    public int MaxDeliveriesPerCycle => Read(x => x.MaxDeliveriesPerCycle);
    public int MaxParallelDeliveries => Read(x => x.MaxParallelDeliveries);

    internal void Save(DesktopSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        lock (_sync)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var temporaryPath = _settingsPath + ".tmp";
            var json = System.Text.Json.JsonSerializer.Serialize(settings, DesktopSettingsJson.Options);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _settingsPath, overwrite: true);
            _settings = settings.Clone();
        }
    }

    internal void EnsureSaved()
    {
        if (!File.Exists(_settingsPath))
        {
            Save(Current);
        }
    }

    internal void WriteServiceRuntimeSettings(DesktopSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        if (string.IsNullOrWhiteSpace(settings.ServiceDataRoot))
        {
            return;
        }

        var root = Path.GetFullPath(settings.ServiceDataRoot);
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "service-settings.json");
        var temporaryPath = path + ".tmp";
        var content = new
        {
            automationSchedulerEnabled = settings.AutomationSchedulerEnabled,
            automationSchedulerPollingSeconds = settings.AutomationSchedulerPollingSeconds,
            deliveryDispatcherEnabled = settings.DeliveryDispatcherEnabled,
            deliveryDispatcherPollingSeconds = settings.DeliveryDispatcherPollingSeconds,
            maxDeliveriesPerCycle = settings.MaxDeliveriesPerCycle,
            maxParallelDeliveries = settings.MaxParallelDeliveries
        };
        var json = System.Text.Json.JsonSerializer.Serialize(content, DesktopSettingsJson.Options);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private T Read<T>(Func<DesktopSettings, T> selector)
    {
        lock (_sync)
        {
            return selector(_settings);
        }
    }

    private DesktopSettings LoadFromDisk()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<DesktopSettings>(
                    json,
                    DesktopSettingsJson.Options);

                if (loaded is not null)
                {
                    ApplyDefaults(loaded);
                    loaded.Normalize();
                    return loaded;
                }
            }
        }
        catch
        {
            // Configuração inválida não deve impedir a inicialização.
        }

        var settings = CreateDefaults();
        settings.Normalize();
        return settings;
    }

    private static DesktopSettings CreateDefaults() => new()
    {
        ServiceExecutablePath = Path.Combine(AppContext.BaseDirectory, "service", "FlowSentinel.Service.exe"),
        ServiceDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FlowSentinel")
    };

    private static void ApplyDefaults(DesktopSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ServiceExecutablePath))
        {
            settings.ServiceExecutablePath = Path.Combine(
                AppContext.BaseDirectory,
                "service",
                "FlowSentinel.Service.exe");
        }

        if (string.IsNullOrWhiteSpace(settings.ServiceDataRoot))
        {
            settings.ServiceDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "FlowSentinel");
        }
    }
}

internal static class DesktopSettingsJson
{
    internal static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        }
    };
}
