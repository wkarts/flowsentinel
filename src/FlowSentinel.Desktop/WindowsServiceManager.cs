using System.Diagnostics;
using System.Text;

namespace FlowSentinel.Desktop;

internal enum WindowsServiceState
{
    NotInstalled,
    Stopped,
    StartPending,
    StopPending,
    Running,
    Paused,
    Unknown
}

internal sealed record WindowsServiceStatus(
    WindowsServiceState State,
    string DisplayText,
    string RawOutput);

internal sealed class WindowsServiceManager
{
    internal const string ServiceName = "FlowSentinel";

    internal async Task<WindowsServiceStatus> QueryAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunCapturedAsync("sc.exe", $"query {ServiceName}", cancellationToken);
        var output = $"{result.StandardOutput}{Environment.NewLine}{result.StandardError}";

        if (output.Contains("1060", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("não existe", StringComparison.OrdinalIgnoreCase))
        {
            return new WindowsServiceStatus(
                WindowsServiceState.NotInstalled,
                "Não instalado",
                output.Trim());
        }

        var state = output switch
        {
            var value when value.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) => WindowsServiceState.Running,
            var value when value.Contains("START_PENDING", StringComparison.OrdinalIgnoreCase) => WindowsServiceState.StartPending,
            var value when value.Contains("STOP_PENDING", StringComparison.OrdinalIgnoreCase) => WindowsServiceState.StopPending,
            var value when value.Contains("STOPPED", StringComparison.OrdinalIgnoreCase) => WindowsServiceState.Stopped,
            var value when value.Contains("PAUSED", StringComparison.OrdinalIgnoreCase) => WindowsServiceState.Paused,
            _ => WindowsServiceState.Unknown
        };

        return new WindowsServiceStatus(state, GetDisplayText(state), output.Trim());
    }

    internal async Task InstallOrUpdateAsync(
        string serviceExecutablePath,
        string dataRoot,
        string startupType,
        CancellationToken cancellationToken = default)
    {
        var executable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(serviceExecutablePath));
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("O executável do serviço não foi encontrado.", executable);
        }

        var scriptPath = Path.Combine(Path.GetDirectoryName(executable)!, "install-service.ps1");
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("O script install-service.ps1 não foi encontrado.", scriptPath);
        }

        var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(dataRoot));
        var arguments = $"-NoProfile -ExecutionPolicy Bypass -File {Quote(scriptPath)} " +
                        $"-BinaryPath {Quote(executable)} -ServiceName {Quote(ServiceName)} " +
                        $"-DataRoot {Quote(root)}";

        await RunElevatedAsync("powershell.exe", arguments, cancellationToken);
        await ConfigureStartupTypeAsync(startupType, cancellationToken);
    }

    internal Task StartAsync(CancellationToken cancellationToken = default) =>
        RunElevatedPowerShellAsync($"Start-Service -Name '{ServiceName}' -ErrorAction Stop", cancellationToken);

    internal Task StopAsync(CancellationToken cancellationToken = default) =>
        RunElevatedPowerShellAsync($"Stop-Service -Name '{ServiceName}' -Force -ErrorAction Stop", cancellationToken);

    internal async Task UninstallAsync(
        string serviceExecutablePath,
        CancellationToken cancellationToken = default)
    {
        var executable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(serviceExecutablePath));
        var directory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory;
        var scriptPath = Path.Combine(directory, "uninstall-service.ps1");

        if (File.Exists(scriptPath))
        {
            var arguments = $"-NoProfile -ExecutionPolicy Bypass -File {Quote(scriptPath)} " +
                            $"-ServiceName {Quote(ServiceName)}";
            await RunElevatedAsync("powershell.exe", arguments, cancellationToken);
            return;
        }

        await RunElevatedPowerShellAsync(
            $"$service = Get-Service -Name '{ServiceName}' -ErrorAction SilentlyContinue; " +
            "if ($service) { Stop-Service $service -Force -ErrorAction SilentlyContinue; " +
            $"sc.exe delete '{ServiceName}' | Out-Null }}",
            cancellationToken);
    }

    internal Task ConfigureStartupTypeAsync(
        string startupType,
        CancellationToken cancellationToken = default)
    {
        var normalized = startupType switch
        {
            "Manual" => "Manual",
            "Disabled" => "Disabled",
            _ => "Automatic"
        };

        return RunElevatedPowerShellAsync(
            $"Set-Service -Name '{ServiceName}' -StartupType {normalized} -ErrorAction Stop",
            cancellationToken);
    }

    private static Task RunElevatedPowerShellAsync(string command, CancellationToken cancellationToken) =>
        RunElevatedAsync(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command {Quote(command)}",
            cancellationToken);

    private static async Task RunElevatedAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal
            }
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Não foi possível iniciar a operação administrativa.");
            }
        }
        catch (System.ComponentModel.Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("A operação administrativa foi cancelada pelo usuário.", exception);
        }

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"A operação administrativa terminou com o código {process.ExitCode}.");
        }
    }

    private static async Task<ProcessResult> RunCapturedAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Não foi possível iniciar {fileName}.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "`\"") + "\"";

    private static string GetDisplayText(WindowsServiceState state) => state switch
    {
        WindowsServiceState.NotInstalled => "Não instalado",
        WindowsServiceState.Stopped => "Parado",
        WindowsServiceState.StartPending => "Iniciando",
        WindowsServiceState.StopPending => "Parando",
        WindowsServiceState.Running => "Em execução",
        WindowsServiceState.Paused => "Pausado",
        _ => "Estado desconhecido"
    };

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
