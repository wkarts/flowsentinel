using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Infrastructure.Channels;

internal sealed class LocalWindowsChannel : INotificationChannel
{
    private readonly ILocalNotificationSink _sink;

    public LocalWindowsChannel(ILocalNotificationSink sink)
    {
        _sink = sink;
    }

    public ChannelType ChannelType => ChannelType.LocalWindows;

    public async Task<DeliveryResult> SendAsync(
        ChannelConfiguration configuration,
        DeliveryRequest request,
        CancellationToken cancellationToken)
    {
        await _sink.ShowAsync(request.Subject, request.Message, cancellationToken);
        return DeliveryResult.Sent();
    }
}

internal sealed class NullLocalNotificationSink : ILocalNotificationSink
{
    public Task ShowAsync(string title, string message, CancellationToken cancellationToken) => Task.CompletedTask;
}
