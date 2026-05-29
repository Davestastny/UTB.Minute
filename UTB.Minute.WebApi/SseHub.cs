using System.Threading.Channels;

namespace UTB.Minute.WebApi;

/// <summary>
/// Singleton service that broadcasts Server-Sent Events to all connected clients.
/// </summary>
public sealed class SseHub
{
    private readonly List<Channel<string>> _channels = [];
    private readonly Lock _lock = new();

    public IAsyncEnumerable<string> Subscribe(CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        lock (_lock)
            _channels.Add(channel);

        ct.Register(() =>
        {
            lock (_lock)
            {
                _channels.Remove(channel);
                channel.Writer.TryComplete();
            }
        });

        return channel.Reader.ReadAllAsync(ct);
    }

    public void Broadcast(string eventType, object payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var message = $"event: {eventType}\ndata: {json}\n\n";

        lock (_lock)
        {
            foreach (var ch in _channels)
                ch.Writer.TryWrite(message);
        }
    }
}