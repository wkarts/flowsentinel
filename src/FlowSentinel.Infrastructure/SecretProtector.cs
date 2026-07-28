using System.Security.Cryptography;
using System.Text;
using FlowSentinel.Application;

namespace FlowSentinel.Infrastructure;

public sealed class WindowsDpapiSecretProtector : ISecretProtector
{
    private const string LegacyPrefix = "dpapi:";
    private const string UserPrefix = "dpapi-user:";
    private const string MachinePrefix = "dpapi-machine:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FlowSentinel.LocalSecrets.v1");

    public string Protect(
        string plainText,
        SecretProtectionScope scope = SecretProtectionScope.CurrentUser)
    {
        EnsureWindows();
        var dataProtectionScope = scope == SecretProtectionScope.LocalMachine
            ? DataProtectionScope.LocalMachine
            : DataProtectionScope.CurrentUser;
        var prefix = scope == SecretProtectionScope.LocalMachine
            ? MachinePrefix
            : UserPrefix;
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(bytes, Entropy, dataProtectionScope);
        return prefix + Convert.ToBase64String(encrypted);
    }

    public string Unprotect(string protectedText)
    {
        EnsureWindows();
        var (prefix, scope) = ResolveScope(protectedText);
        var encrypted = Convert.FromBase64String(protectedText[prefix.Length..]);
        var bytes = ProtectedData.Unprotect(encrypted, Entropy, scope);
        return Encoding.UTF8.GetString(bytes);
    }

    public string UnprotectIfNeeded(string value) =>
        IsProtected(value) ? Unprotect(value) : value;

    private static bool IsProtected(string value) =>
        value.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith(UserPrefix, StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith(MachinePrefix, StringComparison.OrdinalIgnoreCase);

    private static (string Prefix, DataProtectionScope Scope) ResolveScope(string value)
    {
        if (value.StartsWith(MachinePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (MachinePrefix, DataProtectionScope.LocalMachine);
        }

        if (value.StartsWith(UserPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (UserPrefix, DataProtectionScope.CurrentUser);
        }

        if (value.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (LegacyPrefix, DataProtectionScope.CurrentUser);
        }

        throw new ArgumentException("O valor não possui um prefixo DPAPI reconhecido.", nameof(value));
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows DPAPI está disponível somente no Windows.");
        }
    }
}
