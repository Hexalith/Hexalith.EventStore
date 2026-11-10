// Approved Dapr Kubernetes cryptography supplement: 14eb081c36c1122b337c75ee1d341afc7db0a01ea107ed83596817ddcfd33881; sections 2 and 7.
using Google.Protobuf;
using Grpc.Core;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>One fake duplex call of <see cref="DaprCryptoTestSidecar"/>: it collects request messages and replays the sidecar's response chunks.</summary>
/// <remarks>Like Grpc.Net.Client with ThrowOperationCanceledOnCancellation, a cancelled token raises OperationCanceledException.</remarks>
internal sealed class DaprCryptoTestStream<TRequest, TResponse>(DaprCryptoTestSidecar sidecar, bool encrypt, Func<byte[], ulong, TResponse> response)
    : IClientStreamWriter<TRequest>, IAsyncStreamReader<TResponse>
    where TRequest : class
    where TResponse : class
{
    private readonly List<IMessage> _messages = [];
    private Queue<byte[]>? _chunks;
    private Exception? _end;
    private bool _completed;
    private ulong _sequence;

    /// <summary>Gets or sets unused write options.</summary>
    public WriteOptions? WriteOptions { get; set; }
    /// <summary>Gets the current response message.</summary>
    public TResponse Current { get; private set; } = null!;
    /// <summary>Gets the status the call ended with.</summary>
    internal Status Status { get; private set; } = Status.DefaultSuccess;

    /// <summary>Writes one request message without cancellation.</summary>
    public Task WriteAsync(TRequest message) => WriteAsync(message, CancellationToken.None);

    /// <summary>Writes one request message; a configured write fault raises the exception a racing client write sees.</summary>
    public Task WriteAsync(TRequest message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _messages.Add((IMessage)message);
        sidecar.Written(encrypt, (IMessage)message);
        return Task.CompletedTask;
    }

    /// <summary>Completes the request stream.</summary>
    public Task CompleteAsync()
    {
        _completed = true;
        return Task.CompletedTask;
    }

    /// <summary>Replays the next response chunk, then the call's terminal status.</summary>
    public async Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_chunks is null)
        {
            (IReadOnlyList<byte[]> chunks, Exception? end) = await sidecar.RespondAsync(encrypt, _messages, _completed, cancellationToken);
            _chunks = new(chunks);
            _end = end;
        }

        if (_chunks.TryDequeue(out byte[]? chunk))
        {
            sidecar.Outputs.Add(chunk);
            Current = response(chunk, _sequence++);
            return true;
        }

        if ((_end as RpcException ?? _end?.InnerException as RpcException) is { } rpc) { Status = rpc.Status; }
        if (_end is not null) { throw _end; }
        return false;
    }
}
