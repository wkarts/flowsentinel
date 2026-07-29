namespace FlowSentinel.Infrastructure;

public sealed class AppPaths
{
    public required string RootDirectory { get; init; }
    public string DataDirectory => Path.Combine(RootDirectory, "data");
    public string LogDirectory => Path.Combine(RootDirectory, "logs");
    public string BackupDirectory => Path.Combine(RootDirectory, "backups");
    public string DatabasePath => Path.Combine(DataDirectory, "flowsentinel.db");
    public string ContactDirectoryPath => Path.Combine(DataDirectory, "contacts.json");

    public static AppPaths ForDesktop() => CreateFromEnvironmentOrDefault(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static AppPaths ForService() => CreateFromEnvironmentOrDefault(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(BackupDirectory);
    }

    private static AppPaths CreateFromEnvironmentOrDefault(string baseDirectory)
    {
        var configured = Environment.GetEnvironmentVariable("FLOWSENTINEL_DATA_ROOT");
        return new AppPaths
        {
            RootDirectory = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(baseDirectory, "FlowSentinel")
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured))
        };
    }
}

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    public bool IsOwner { get; }

    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsOwner = createdNew;
    }

    public void Dispose()
    {
        if (IsOwner)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
    }
}
