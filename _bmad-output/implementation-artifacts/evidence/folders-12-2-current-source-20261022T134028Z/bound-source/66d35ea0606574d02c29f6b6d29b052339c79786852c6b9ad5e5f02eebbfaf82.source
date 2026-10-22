using System.Text;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Retains only a bounded tail of child-process diagnostics.</summary>
internal sealed class Oq8BoundedLog
{
    internal const int MaximumCharacters = 32_768;
    private readonly StringBuilder _text = new();

    /// <summary>Appends one diagnostic line while retaining the configured tail bound.</summary>
    /// <param name="value">The diagnostic line.</param>
    public void Append(string? value)
    {
        if (value is null)
        {
            return;
        }

        lock (_text)
        {
            _ = _text.AppendLine(value);
            if (_text.Length > MaximumCharacters)
            {
                _text.Remove(0, _text.Length - MaximumCharacters);
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        lock (_text)
        {
            return _text.ToString();
        }
    }
}
