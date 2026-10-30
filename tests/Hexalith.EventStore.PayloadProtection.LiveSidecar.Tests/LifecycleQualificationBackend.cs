using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Ephemeral test-only KEKs keep plaintext DEKs out of persisted PF-02 state; this is not custody evidence.</summary>
internal sealed class LifecycleQualificationBackend : PayloadProtectionBackend, IDisposable
{
    private readonly Dictionary<string, byte[]> _keys;
    /// <summary>Gets or sets a deliberately incorrect provider selection for negative tests only.</summary>
    internal string? WrapVersionOverride { get; set; }
    /// <summary>Creates independent test key identities, preserving environment-wide inventory ownership.</summary>
    internal LifecycleQualificationBackend(string qualifier)
    {
        OldVersion = "fixture:" + qualifier + ":version-01"; NewVersion = "fixture:" + qualifier + ":version-02";
        CurrentVersion = OldVersion;
        _keys = new(StringComparer.Ordinal) { [OldVersion] = RandomNumberGenerator.GetBytes(32), [NewVersion] = RandomNumberGenerator.GetBytes(32) };
    }
    /// <summary>Gets the exact old fixture key identity.</summary>
    internal string OldVersion { get; }
    /// <summary>Gets the exact new fixture key identity.</summary>
    internal string NewVersion { get; }
    private readonly Dictionary<string, (string Provider, byte[] Wrapped)> _outcomes = new(StringComparer.Ordinal);
    /// <summary>Gets the fixture-owned output arrays, inspected for zeroing after each operation.</summary>
    internal List<byte[]> Outputs { get; } = [];
    /// <summary>Gets or sets the current explicitly selected test KEK.</summary>
    internal string CurrentVersion { get; set; }
    /// <summary>Gets or sets an unobserved successful wrap response.</summary>
    internal bool AmbiguousNextWrap { get; set; }
    /// <summary>Gets or sets an injected provider-verification boundary, including a cancellation-ignoring response.</summary>
    internal Func<string, CancellationToken, ValueTask>? Unwrapping { get; set; }
    /// <summary>Gets the wrap count, proving recovery never blindly repeats the mutation.</summary>
    internal int WrapCount { get; private set; }
    /// <inheritdoc/>
    internal override string BackendId => "pf02-fixture";
    /// <inheritdoc/>
    internal override string EndpointFingerprint => new('a', 64);
    /// <inheritdoc/>
    internal override ValueTask<PayloadBackendResult> WrapAsync(string operationId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken)
    {
        return WrapExactAsync(operationId, CurrentVersion, dek, cancellationToken);
    }
    /// <inheritdoc/>
    internal override ValueTask<PayloadBackendResult> WrapExactAsync(string operationId, string providerKeyVersionId, ReadOnlyMemory<byte> dek, CancellationToken cancellationToken)
    {
        lock (_outcomes)
        {
            cancellationToken.ThrowIfCancellationRequested(); WrapCount++;
            providerKeyVersionId = WrapVersionOverride ?? providerKeyVersionId;
            if (_outcomes.Count >= 1024) { throw new InvalidOperationException("Test backend capacity exceeded."); }
            byte[] wrapped = new byte[60]; RandomNumberGenerator.Fill(wrapped.AsSpan(0, 12));
            using (var aes = new AesGcm(_keys[providerKeyVersionId], 16)) { aes.Encrypt(wrapped.AsSpan(0, 12), dek.Span, wrapped.AsSpan(28), wrapped.AsSpan(12, 16)); }
            _outcomes.Add(operationId, (providerKeyVersionId, wrapped.ToArray()));
            if (AmbiguousNextWrap) { AmbiguousNextWrap = false; CryptographicOperations.ZeroMemory(wrapped); return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Ambiguous)); }
            Outputs.Add(wrapped);
            return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, wrapped, providerKeyVersionId));
        }
    }
    /// <inheritdoc/>
    internal override async ValueTask<PayloadBackendResult> UnwrapAsync(string operationId, string providerKeyVersionId, ReadOnlyMemory<byte> wrappedDek, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] dek = new byte[32];
        byte[] ownedWrapped = wrappedDek.ToArray();
        try
        {
            if (Unwrapping is not null) { await Unwrapping(operationId, cancellationToken); }
            using var aes = new AesGcm(_keys[providerKeyVersionId], 16);
            aes.Decrypt(ownedWrapped.AsSpan(0, 12), ownedWrapped.AsSpan(28), ownedWrapped.AsSpan(12, 16), dek);
            Outputs.Add(dek);
            return new PayloadBackendResult(PayloadBackendFailure.None, dek, providerKeyVersionId);
        }
        catch { CryptographicOperations.ZeroMemory(dek); throw; }
        finally { CryptographicOperations.ZeroMemory(ownedWrapped); }
    }
    /// <inheritdoc/>
    internal override ValueTask<PayloadBackendResult> ReconcileAsync(string operationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_outcomes.TryGetValue(operationId, out var outcome)) { return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Missing)); }
        byte[] output = outcome.Wrapped.ToArray(); Outputs.Add(output);
        return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, output, outcome.Provider));
    }
    /// <summary>Clears every owned fixture KEK and retained opaque reconciliation result.</summary>
    public void Dispose()
    {
        foreach (byte[] key in _keys.Values) { CryptographicOperations.ZeroMemory(key); }
        foreach (var result in _outcomes.Values) { CryptographicOperations.ZeroMemory(result.Wrapped); }
    }
}
