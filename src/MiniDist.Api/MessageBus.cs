using System.Threading.Channels;

namespace MiniDist.Api;

public interface IMessageBus
{
    Task PublishAsync<T>(T message, CancellationToken ct);
    IAsyncEnumerable<object> SubscribeAllAsync(CancellationToken ct);
}

public class InMemoryMessageBus : IMessageBus
{
    private readonly Channel<object> _ch = Channel.CreateUnbounded<object>();

    public Task PublishAsync<T>(T message, CancellationToken ct)
        => _ch.Writer.WriteAsync(message!, ct).AsTask();

    public async IAsyncEnumerable<object> SubscribeAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (await _ch.Reader.WaitToReadAsync(ct))
            while (_ch.Reader.TryRead(out var msg))
                yield return msg;
    }
}
