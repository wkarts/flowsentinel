using Microsoft.Win32;

namespace FlowSentinel.Desktop;

internal static class StartupRegistration
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FlowSentinel";

    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    internal static string? GetRegisteredCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
        return key?.GetValue(ValueName) as string;
    }

    internal static string BuildCommand(bool startMinimized) =>
        $"\"{System.Windows.Forms.Application.ExecutablePath}\" --startup" +
        (startMinimized ? " --tray" : " --show");

    internal static void SetEnabled(bool enabled, bool startMinimized = true)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, BuildCommand(startMinimized), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
