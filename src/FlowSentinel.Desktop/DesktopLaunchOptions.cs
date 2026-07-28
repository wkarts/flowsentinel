namespace FlowSentinel.Desktop;

internal sealed class DesktopLaunchOptions
{
    internal bool IsWindowsStartup { get; init; }
    internal bool StartInTray { get; init; }
    internal bool SuppressSplash { get; init; }

    internal static DesktopLaunchOptions Parse(IEnumerable<string> arguments, DesktopSettings settings)
    {
        var args = arguments.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var startup = args.Contains("--startup");
        var forceTray = args.Contains("--tray");
        var forceShow = args.Contains("--show");
        var suppressSplash = args.Contains("--no-splash");

        var startInTray = !forceShow &&
                          (forceTray ||
                           (startup && settings.StartMinimizedToTray) ||
                           (!startup && !settings.OpenMainWindowOnManualStart));

        return new DesktopLaunchOptions
        {
            IsWindowsStartup = startup,
            StartInTray = startInTray,
            SuppressSplash = suppressSplash
        };
    }
}
