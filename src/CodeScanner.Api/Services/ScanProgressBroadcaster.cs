using System.Collections.Concurrent;
using System.Threading.Channels;
using CodeScanner.Api.Models.Dtos;

namespace CodeScanner.Api.Services;

public class ScanProgressBroadcaster
{
    private readonly ConcurrentDictionary<int, ConcurrentBag<Channel<ScanProgressEvent>>> _subscribers = new();

    public IDisposable Subscribe(int scanId, Channel<ScanProgressEvent> channel)
    {
        var bag = _subscribers.GetOrAdd(scanId, _ => []);
        bag.Add(channel);
        return new Subscription(this, scanId, channel);
    }

    public async Task BroadcastAsync(int scanId, ScanProgressEvent evt)
    {
        if (!_subscribers.TryGetValue(scanId, out var bag))
            return;

        foreach (var channel in bag)
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
        if (!_subscribers.TryGetValue(scanId, out var bag))
            return;

        foreach (var channel in bag)
        {
            channel.Writer.TryComplete();
        }
    }

    private void Unsubscribe(int scanId, Channel<ScanProgressEvent> channel)
    {
        channel.Writer.TryComplete();
        // ConcurrentBag doesn't support removal, but completed channels are harmless
    }

    private sealed class Subscription(ScanProgressBroadcaster broadcaster, int scanId, Channel<ScanProgressEvent> channel) : IDisposable
    {
        public void Dispose() => broadcaster.Unsubscribe(scanId, channel);
    }
}
