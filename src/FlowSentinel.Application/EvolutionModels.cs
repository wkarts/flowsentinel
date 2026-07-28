using FlowSentinel.Domain;

namespace FlowSentinel.Application;

public sealed class EvolutionInstanceStatus
{
    public bool Connected { get; init; }
    public string State { get; init; } = "unknown";
    public string RawResponse { get; init; } = string.Empty;
}

public sealed class EvolutionQrCodeResult
{
    public string? Base64Image { get; init; }
    public string? PairingCode { get; init; }
    public string RawResponse { get; init; } = string.Empty;
}

public interface IEvolutionInstanceService
{
    Task<EvolutionInstanceStatus> GetStatusAsync(
        ChannelConfiguration configuration,
        CancellationToken cancellationToken);

    Task<EvolutionQrCodeResult> GetQrCodeAsync(
        ChannelConfiguration configuration,
        CancellationToken cancellationToken);
}
