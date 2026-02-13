using System.Collections.Concurrent;
using System.Threading.Channels;
using CodeScanner.Api.Models.Dtos;

namespace CodeScanner.Api.Services;

public class ScanProgressBroadcaster
{
    private readonly ConcurrentDictionary<int, ScanSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<int, bool> _completedScans = new();

    public bool IsCompleted(int scanId) => _completedScans.ContainsKey(scanId);

    public IDisposable Subscribe(int scanId, Channel<ScanProgressEvent> channel)
    {
        // If the scan already completed, immediately complete the channel
        if (_completedScans.ContainsKey(scanId))
        {
            channel.Writer.TryComplete();
            return new NoOpDisposable();
        }

        var sub = _subscriptions.GetOrAdd(scanId, _ => new ScanSubscription());
        sub.Channels.Add(channel);
        return new Unsubscriber(channel);
    }

    public async Task BroadcastAsync(int scanId, ScanProgressEvent evt)
    {
        if (!_subscriptions.TryGetValue(scanId, out var sub))
            return;

        foreach (var channel in sub.Channels)
        {
            try
            {
                await channel.Writer.WriteAsync(evt);
            }
            catch (ChannelClosedException) { }
        }
    }

    public void Complete(int scanId)
    {
        _completedScans.TryAdd(scanId, true);

        if (_subscriptions.TryRemove(scanId, out var sub))
        {
            foreach (var channel in sub.Channels)
            {
                channel.Writer.TryComplete();
            }
        }
    }

    private sealed class ScanSubscription
    {
        public ConcurrentBag<Channel<ScanProgressEvent>> Channels { get; } = [];
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly Channel<ScanProgressEvent> _channel;
        public Unsubscriber(Channel<ScanProgressEvent> channel) => _channel = channel;
        public void Dispose() => _channel.Writer.TryComplete();
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
