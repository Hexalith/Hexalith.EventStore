using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Events;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Models serialized durable values separately from actor cache/staging for exact recovery end-state assertions.</summary>
internal sealed class DaprReplayTestStore
{
    private readonly Dictionary<string, byte[]> _pending = [];
    private readonly Dictionary<string, object> _cache = [];

    /// <summary>Creates an actor API test store with independent persisted serialized images.</summary>
    internal DaprReplayTestStore()
    {
        Manager = Substitute.For<IActorStateManager>(); Bind<DaprReplayOperationRecord>(); Bind<DaprReplayPageLedger>(); Bind<byte[]>();
        _ = Manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call => { if (FailReads) { throw new IOException("unavailable readback"); } _pending.Clear(); _cache.Clear(); return Task.CompletedTask; });
        _ = Manager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            Saves++; CancellationToken token = call.Arg<CancellationToken>(); token.ThrowIfCancellationRequested();
            if (SaveMode != "no-commit")
            {
                foreach ((string key, byte[] bytes) in _pending) { if (SaveMode != "partial" || key.Contains("ledger", StringComparison.Ordinal)) { Durable[key] = bytes.ToArray(); } }
            }
            _pending.Clear(); AfterSave?.Invoke();
            if (SaveMode != "normal") { throw new IOException("save acknowledgement lost"); }
            return Task.CompletedTask;
        });
    }

    /// <summary>Gets the actual Dapr actor API seam used by the protocol.</summary>
    internal IActorStateManager Manager { get; }
    /// <summary>Gets independent persisted JSON images.</summary>
    internal Dictionary<string, byte[]> Durable { get; } = [];
    /// <summary>Gets or sets normal, no-commit, partial or commit-throw behavior.</summary>
    internal string SaveMode { get; set; } = "normal";
    /// <summary>Gets or sets fresh-cache/read failure.</summary>
    internal bool FailReads { get; set; }
    /// <summary>Gets or sets a cancellation/loss hook after durable staging commits.</summary>
    internal Action? AfterSave { get; set; }
    /// <summary>Gets or sets a callback after durable state has entered the actor cache.</summary>
    internal Action<string>? OnRead { get; set; }
    /// <summary>Gets actual actor save calls.</summary>
    internal int Saves { get; private set; }

    /// <summary>Gets actor cache-owned bytes for lifetime controls without transferring their ownership.</summary>
    internal byte[] CachedBytes(string key) => (byte[])_cache[key];

    /// <summary>Reads independent persisted state for outcome assertions.</summary>
    internal T Get<T>(string key) => JsonSerializer.Deserialize<T>(Durable[key])!;
    /// <summary>Writes a malformed persisted participant only for refusal controls.</summary>
    internal void Put<T>(string key, T value) => Durable[key] = JsonSerializer.SerializeToUtf8Bytes(value);

    private void Bind<T>()
    {
        _ = Manager.SetStateAsync(Arg.Any<string>(), Arg.Any<T>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.Arg<CancellationToken>().ThrowIfCancellationRequested(); string key = call.ArgAt<string>(0); T value = call.ArgAt<T>(1);
            _pending[key] = JsonSerializer.SerializeToUtf8Bytes(value); _cache[key] = value!; return Task.CompletedTask;
        });
        _ = Manager.TryGetStateAsync<T>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.Arg<CancellationToken>().ThrowIfCancellationRequested(); if (FailReads) { throw new IOException("unavailable readback"); }
            string key = call.ArgAt<string>(0);
            if (_cache.TryGetValue(key, out object? cached)) { return new ConditionalValue<T>(true, (T)cached); }
            if (!Durable.TryGetValue(key, out byte[]? bytes)) { return new ConditionalValue<T>(false, default!); }
            T value = JsonSerializer.Deserialize<T>(bytes)!; _cache[key] = value!; OnRead?.Invoke(key); return new ConditionalValue<T>(true, value);
        });
    }
}
