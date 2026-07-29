using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;
using Microsoft.Extensions.Logging;

namespace FlowSentinel.Infrastructure;

internal sealed class JsonContactDirectory : IContactDirectory
{
    private readonly AppPaths _paths;
    private readonly ILogger<JsonContactDirectory> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ContactDirectoryDefinition? _cache;
    private DateTime _cacheWriteTimeUtc;

    public JsonContactDirectory(AppPaths paths, ILogger<JsonContactDirectory> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<ContactDirectoryDefinition> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _paths.EnsureDirectories();
            var path = _paths.ContactDirectoryPath;
            if (!File.Exists(path))
            {
                var empty = new ContactDirectoryDefinition();
                await SaveCoreAsync(empty, cancellationToken);
                return Clone(empty);
            }

            var writeTime = File.GetLastWriteTimeUtc(path);
            if (_cache is null || writeTime != _cacheWriteTimeUtc)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(path, cancellationToken);
                    var loaded = JsonSerializer.Deserialize<ContactDirectoryDefinition>(json, FlowJson.Options)
                                 ?? new ContactDirectoryDefinition();
                    Normalize(loaded);
                    loaded.Validate();
                    _cache = loaded;
                    _cacheWriteTimeUtc = writeTime;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Não foi possível carregar o catálogo de contatos em {Path}.", path);
                    throw new InvalidOperationException(
                        "O catálogo de contatos está inválido. Corrija ou restaure o arquivo contacts.json.",
                        exception);
                }
            }

            return Clone(_cache);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(ContactDirectoryDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Normalize(definition);
        definition.Validate();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await SaveCoreAsync(definition, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveCoreAsync(ContactDirectoryDefinition definition, CancellationToken cancellationToken)
    {
        _paths.EnsureDirectories();
        var path = _paths.ContactDirectoryPath;
        var temporaryPath = path + ".tmp";
        var backupPath = path + ".bak";
        var json = JsonSerializer.Serialize(definition, FlowJson.Options);

        await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
        if (File.Exists(path))
        {
            File.Copy(path, backupPath, overwrite: true);
            File.Move(temporaryPath, path, overwrite: true);
        }
        else
        {
            File.Move(temporaryPath, path);
        }

        _cache = Clone(definition);
        _cacheWriteTimeUtc = File.GetLastWriteTimeUtc(path);
    }

    private static void Normalize(ContactDirectoryDefinition definition)
    {
        definition.Version = Math.Max(1, definition.Version);
        definition.Contacts ??= [];
        definition.Groups ??= [];

        foreach (var contact in definition.Contacts)
        {
            if (contact.Id == Guid.Empty)
            {
                contact.Id = Guid.NewGuid();
            }
            contact.Name = contact.Name?.Trim() ?? string.Empty;
            contact.Notes = contact.Notes?.Trim() ?? string.Empty;
            contact.AllowedAutomationIds ??= [];
            contact.AllowedAutomationIds = contact.AllowedAutomationIds.Distinct().ToList();
            contact.Addresses ??= [];
            foreach (var channel in contact.Addresses.Keys.ToArray())
            {
                contact.Addresses[channel] = (contact.Addresses[channel] ?? [])
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (contact.Addresses[channel].Count == 0)
                {
                    contact.Addresses.Remove(channel);
                }
            }
        }

        foreach (var group in definition.Groups)
        {
            group.Id = group.Id?.Trim() ?? string.Empty;
            group.Name = group.Name?.Trim() ?? string.Empty;
            group.ContactIds ??= [];
            group.AllowedAutomationIds ??= [];
            group.ContactIds = group.ContactIds.Distinct().ToList();
            group.AllowedAutomationIds = group.AllowedAutomationIds.Distinct().ToList();
            group.Contacts ??= [];
        }
    }

    private static ContactDirectoryDefinition Clone(ContactDirectoryDefinition definition)
    {
        var json = JsonSerializer.Serialize(definition, FlowJson.Options);
        return JsonSerializer.Deserialize<ContactDirectoryDefinition>(json, FlowJson.Options)
               ?? new ContactDirectoryDefinition();
    }
}
