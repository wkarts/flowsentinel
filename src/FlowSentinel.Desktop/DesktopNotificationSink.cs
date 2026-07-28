using System.Collections.Concurrent;
using FlowSentinel.Application;

namespace FlowSentinel.Desktop;

internal sealed class DesktopNotificationSink : ILocalNotificationSink
{
    private readonly ConcurrentQueue<DesktopNotification> _queue = new();

    public Task ShowAsync(string title, string message, CancellationToken cancellationToken)
    {
        _queue.Enqueue(new DesktopNotification(title, message));
        return Task.CompletedTask;
    }

    public bool TryDequeue(out DesktopNotification notification)
    {
        if (_queue.TryDequeue(out var item))
        {
            notification = item;
            return true;
        }

        notification = null!;
        return false;
    }
}

internal sealed record DesktopNotification(string Title, string Message);
