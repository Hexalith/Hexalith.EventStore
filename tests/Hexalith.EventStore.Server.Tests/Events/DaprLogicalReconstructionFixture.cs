using System.Globalization;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Composes real source/evolution/private intake/codec/Apply/operation paths with explicit local declarations.</summary>
internal sealed class DaprLogicalReconstructionFixture : IAsyncDisposable
{
    /// <summary>Creates an addressed source and canonical operation binding.</summary>
    internal DaprLogicalReconstructionFixture(int events = 3, int workingBytes = 16384, bool mixedHistory = false,
        int maximumBufferBytes = 128 * 1024 * 1024, int payloadBytes = 2, CommandEnvelope? command = null)
    {
        Source = new DaprLogicalReplayFixture(events, payloadBytes, mixedHistory);
        if (!mixedHistory && payloadBytes > 2)
        {
            foreach (int sequence in Source.Events.Keys)
            {
                byte[] image = new byte[payloadBytes];
                image.AsSpan().Fill((byte)' ');
                "{}"u8.CopyTo(image);
                Source.Events[sequence] = Source.Events[sequence] with
                {
                    Payload = image
                };
            }
        }
        Binding = new RegisteredLogicalReplayBinding(typeof(DaprLogicalReconstructionTestState), Source.Service, "r", "test-state-json",
            "{}\n"u8.ToArray(), Create, Read, Write, Apply, 32, workingBytes);
        Owner = new DaprReplayOperationOwner(Store.Manager, "tenant", "operation", maximumBufferBytes, Binding, command);
    }

    /// <summary>Gets the real addressed source and shared catalog.</summary>
    internal DaprLogicalReplayFixture Source
    {
        get;
    }

    /// <summary>Gets serialized persisted values separate from the actor cache.</summary>
    internal DaprReplayTestStore Store
    {
        get;
    }
    = new();
    /// <summary>Gets exact supplied canonical bindings.</summary>
    internal RegisteredLogicalReplayBinding Binding
    {
        get;
    }

    /// <summary>Gets the same-save participant owner.</summary>
    internal DaprReplayOperationOwner Owner
    {
        get;
    }

    /// <summary>Gets observed Apply calls.</summary>
    internal int Applies
    {
        get; private set;
    }

    /// <summary>Gets observed state reads.</summary>
    internal int Reads
    {
        get; private set;
    }

    /// <summary>Gets observed state writes.</summary>
    internal int Writes
    {
        get; private set;
    }

    /// <summary>Gets all application callback tokens.</summary>
    internal List<CancellationToken> Tokens
    {
        get;
    }
    = [];
    /// <summary>Gets or sets a callback at each explicit state boundary.</summary>
    internal Action<string, CancellationToken>? Hook
    {
        get; set;
    }

    /// <summary>Gets or sets an Apply call that mutates then throws.</summary>
    internal int ThrowOnApply
    {
        get; set;
    }

    /// <summary>Gets or sets whether the state writer emits noncanonical padding.</summary>
    internal bool Noncanonical
    {
        get; set;
    }

    /// <summary>Gets or sets deterministic noncanonical padding for snapshot replacement roundtrip controls.</summary>
    internal bool AlwaysNoncanonical { get; set; }

    /// <summary>Gets or sets whether the writer emits malformed state JSON.</summary>
    internal bool Malformed
    {
        get; set;
    }

    /// <summary>Gets or sets whether the writer emits a framed JSON string containing invalid UTF-8.</summary>
    internal bool InvalidUtf8
    {
        get; set;
    }

    /// <summary>Gets callback-borrowed state views for expiry controls.</summary>
    internal List<IReadOnlyPayload> Borrowed
    {
        get;
    }
    = [];

    /// <summary>Gets callback-borrowed state writers for immediate expiry controls.</summary>
    internal List<IBoundedPayloadWriter> BorrowedWriters { get; } = [];

    private object Create(CancellationToken token)
    {
        Observe("create", token);
        return new DaprLogicalReconstructionTestState();
    }

    private object Read(IReadOnlyPayload payload, CancellationToken token)
    {
        Reads++;
        Borrowed.Add(payload);
        Observe("read", token);
        Span<byte> image = stackalloc byte[32];
        payload.CopyTo(0, image[..payload.Length]);
        var reader = new Utf8JsonReader(image[..payload.Length]);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject || !reader.Read() || !reader.ValueTextEquals("value")
            || !reader.Read() || !reader.TryGetInt32(out int value) || !reader.Read() || reader.TokenType != JsonTokenType.EndObject || reader.Read())
        {
            throw new JsonException("invalid supplied state schema");
        }

        return new DaprLogicalReconstructionTestState
        {
            Value = value
        };
    }

    private void Write(object value, IBoundedPayloadWriter writer, CancellationToken token)
    {
        Writes++;
        BorrowedWriters.Add(writer);
        Observe("write", token);
        if (InvalidUtf8)
        {
            writer.Write([123, 34, 118, 97, 108, 117, 101, 34, 58, 34, 255, 34, 125]);
            writer.Complete();
            return;
        }

        string image = Malformed ? "invalid" : "{\"value\":" + ((DaprLogicalReconstructionTestState)value).Value.ToString(CultureInfo.InvariantCulture) + "}";
        if (AlwaysNoncanonical || Noncanonical && Writes % 2 == 1)
        {
            image += " ";
        }

        writer.Write(Encoding.UTF8.GetBytes(image));
        writer.Complete();
    }

    private object Apply(object state, object value, CancellationToken token)
    {
        Applies++;
        Observe("apply", token);
        var counter = (DaprLogicalReconstructionTestState)state;
        counter.Value += ((DaprLogicalReplayTestValue)value).Delta;
        if (Applies == ThrowOnApply)
        {
            counter.Value = 999;
            throw new InvalidOperationException("mutating Apply sentinel");
        }

        return counter;
    }

    private void Observe(string boundary, CancellationToken token)
    {
        Tokens.Add(token);
        Hook?.Invoke(boundary, token);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Store.FailReads = false;
        await Owner.DisposeAsync();
        Source.Dispose();
    }
}
