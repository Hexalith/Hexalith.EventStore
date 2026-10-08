using System.Collections;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Exercises bounded admission without trusting a lying Count or enumerator termination.</summary>
internal sealed class DaprLogicalStreamingExtensions(string key) : IReadOnlyDictionary<string, string>
{
    /// <summary>Gets the number of actual enumerator values observed.</summary>
    internal int Enumerated { get; private set; }
    /// <inheritdoc/>
    public int Count => int.MaxValue;
    /// <inheritdoc/>
    public IEnumerable<string> Keys => throw new NotSupportedException();
    /// <inheritdoc/>
    public IEnumerable<string> Values => throw new NotSupportedException();
    /// <inheritdoc/>
    public string this[string ignored] => throw new NotSupportedException();
    /// <inheritdoc/>
    public bool ContainsKey(string ignored) => throw new NotSupportedException();
    /// <inheritdoc/>
    public bool TryGetValue(string ignored, out string value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() { while (true) { Enumerated++; yield return new KeyValuePair<string, string>(key, "1"); } }
    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
