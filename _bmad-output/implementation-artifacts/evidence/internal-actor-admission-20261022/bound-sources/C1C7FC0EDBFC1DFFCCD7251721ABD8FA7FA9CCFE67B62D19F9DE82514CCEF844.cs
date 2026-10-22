using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>Tokenizes a bounded response before DTO/string/payload materialization using charged private capacity.</summary>
/// <remarks>Current token slices expire on the next read; duplicate/schema/Base64 admission belongs to the protocol parser.</remarks>
internal sealed class IncrementalDomainJsonTokenReader : IDisposable
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly Stream _source;
    private readonly DomainResponseBufferBudget _budget;
    private readonly CancellationToken _token;
    private byte[]? _owner;
    private int _start;
    private int _end;
    private int _valueOffset;
    private int _valueLength;
    private bool _final;
    private long _consumed;
    private JsonReaderState _state = new(new JsonReaderOptions { MaxDepth = 64 });

    /// <summary>Reserves the initial 64 KiB owner before reading any raw token.</summary>
    internal IncrementalDomainJsonTokenReader(Stream source, DomainResponseBufferBudget budget, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(budget);
        _source = source;
        _budget = budget;
        _token = token;
        token.ThrowIfCancellationRequested();
        budget.Reserve(64 * 1024);
        try { _owner = new byte[64 * 1024]; }
        catch { budget.Release(64 * 1024); throw; }
    }

    /// <summary>Gets the current strictly parsed token kind.</summary>
    internal JsonTokenType TokenType { get; private set; }
    /// <summary>Gets the zero-based JSON depth; root containers are depth zero.</summary>
    internal int Depth { get; private set; }
    /// <summary>Gets the exact original byte offset of the current token.</summary>
    internal long TokenStart { get; private set; }
    /// <summary>Gets total bytes consumed, including whitespace.</summary>
    internal long BytesConsumed => _consumed;
    /// <summary>Gets whether the current private scalar contains JSON escapes.</summary>
    internal bool ValueIsEscaped { get; private set; }
    /// <summary>Gets whether the current optional string was discarded after its raw limit.</summary>
    internal bool SkippedOversizedString { get; private set; }
    /// <summary>Gets an invocation-bounded read-only private raw scalar slice.</summary>
    internal ReadOnlySpan<byte> RawValue
    {
        get
        {
            ObjectDisposedException.ThrowIf(_owner is null, this);
            return _owner.AsSpan(_valueOffset, _valueLength);
        }
    }

    /// <summary>Reads one token while charging old/new capacity before any growth and checking the original token.</summary>
    internal async ValueTask<bool> ReadAsync(long maximumRawTokenBytes = long.MaxValue, IDomainJsonScalarAdmission? scalarAdmission = null,
        long maximumConsumedBytes = long.MaxValue, bool skipOversizedString = false)
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        _token.ThrowIfCancellationRequested();
        SkippedOversizedString = false;
        while (true)
        {
            if (skipOversizedString && _end - _start > maximumRawTokenBytes
                && await SkipOversizedStringAsync().ConfigureAwait(false))
            {
                return true;
            }
            if (TryRead())
            {
                long chargedConsumed = _consumed - (scalarAdmission is not null && TokenType == JsonTokenType.String ? _valueLength + 2L : 0);
                if (chargedConsumed > maximumConsumedBytes) { throw new InvalidOperationException("MetadataLimit: cumulative encoded metadata exceeds its selected cap."); }
                if (TokenType is JsonTokenType.String or JsonTokenType.PropertyName && _valueLength + 2L > maximumRawTokenBytes)
                {
                    if (skipOversizedString && TokenType == JsonTokenType.String)
                    {
                        SkippedOversizedString = true;
                        return true;
                    }
                    throw new InvalidOperationException("PayloadLimit: encoded scalar exceeds its field-selected bound.");
                }
                if (scalarAdmission is not null)
                {
                    if (TokenType != JsonTokenType.String) { throw new FormatException("The selected payload scalar is not a string."); }
                    scalarAdmission.Observe(_owner.AsSpan(_valueOffset - 1, _valueLength + 2));
                }
                return true;
            }
            if (_consumed > maximumConsumedBytes) { throw new InvalidOperationException("MetadataLimit: cumulative encoded whitespace/metadata exceeds its selected cap."); }
            if (_end - _start > maximumRawTokenBytes)
            {
                if (skipOversizedString && await SkipOversizedStringAsync().ConfigureAwait(false)) { return true; }
                throw new InvalidOperationException("PayloadLimit: incomplete encoded scalar exceeds its field-selected bound.");
            }
            if (scalarAdmission is not null && _end > _start)
            {
                scalarAdmission.Observe(_owner.AsSpan(_start, _end - _start));
            }
            if (_final) { return false; }
            Compact();
            if (_end == _owner.Length) { Grow(); }
            int read = await _source.ReadAsync(_owner.AsMemory(_end, Math.Min(64 * 1024, _owner.Length - _end)), _token).ConfigureAwait(false);
            _token.ThrowIfCancellationRequested();
            if (read == 0) { _final = true; }
            else { _end += read; }
        }
    }

    /// <summary>Consumes an excessive optional JSON string without retaining or growing its scalar bytes.</summary>
    private async ValueTask<bool> SkipOversizedStringAsync()
    {
        int quote = _owner!.AsSpan(_start, _end - _start).IndexOf((byte)'"');
        if (quote < 0) { return false; }
        quote += _start;
        // Let the JSON reader validate any colon/whitespace before the string and advance
        // its grammar state using a synthetic empty string after the original is skipped.
        byte[] prefix = _owner.AsSpan(_start, quote - _start).ToArray();
        long start = _consumed + quote - _start;
        _consumed += quote - _start;
        _start = quote;
        var utf8 = StrictUtf8.GetDecoder();
        int unicodeDigits = 0;
        int unicodeValue = 0;
        bool escaped = false;
        bool pendingHigh = false;
        bool requireLowEscape = false;
        bool requireLowU = false;
        bool inside = false;
        while (true)
        {
            ReadOnlySpan<byte> span = _owner.AsSpan(_start, _end - _start);
            int consumed = 0;
            while (consumed < span.Length)
            {
                byte value = span[consumed];
                if (!inside) { inside = true; consumed++; continue; }
                if (unicodeDigits > 0)
                {
                    unicodeValue = (unicodeValue << 4) | HexDigit(value);
                    unicodeDigits--;
                    consumed++;
                    if (unicodeDigits == 0)
                    {
                        if (pendingHigh)
                        {
                            if (unicodeValue is < 0xdc00 or > 0xdfff) { throw new JsonException("Unpaired high surrogate."); }
                            pendingHigh = false;
                        }
                        else if (unicodeValue is >= 0xd800 and <= 0xdbff)
                        {
                            pendingHigh = true;
                            requireLowEscape = true;
                        }
                        else if (unicodeValue is >= 0xdc00 and <= 0xdfff) { throw new JsonException("Unpaired low surrogate."); }
                        unicodeValue = 0;
                    }
                    continue;
                }
                if (requireLowEscape)
                {
                    if (value != (byte)'\\') { throw new JsonException("Unpaired high surrogate."); }
                    requireLowEscape = false;
                    requireLowU = true;
                    consumed++;
                    continue;
                }
                if (requireLowU)
                {
                    if (value != (byte)'u') { throw new JsonException("Unpaired high surrogate."); }
                    requireLowU = false;
                    unicodeDigits = 4;
                    consumed++;
                    continue;
                }
                if (escaped)
                {
                    if (value == (byte)'u') { unicodeDigits = 4; }
                    else if (value is not ((byte)'"' or (byte)'\\' or (byte)'/' or (byte)'b' or (byte)'f' or (byte)'n' or (byte)'r' or (byte)'t'))
                    {
                        throw new JsonException("Invalid JSON escape.");
                    }
                    escaped = false;
                    consumed++;
                    continue;
                }
                if (value == (byte)'"' || value == (byte)'\\')
                {
                    ValidateUtf8Segment(utf8, ReadOnlySpan<byte>.Empty, flush: true);
                    utf8.Reset();
                    if (value == (byte)'"')
                    {
                        _consumed += consumed + 1;
                        _start += consumed + 1;
                        byte[] synthetic = new byte[prefix.Length + 2];
                        prefix.CopyTo(synthetic, 0);
                        synthetic[^2] = (byte)'"';
                        synthetic[^1] = (byte)'"';
                        var reader = new Utf8JsonReader(synthetic, isFinalBlock: true, _state);
                        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                        {
                            throw new JsonException("Invalid optional state scalar.");
                        }
                        _state = reader.CurrentState;
                        TokenType = JsonTokenType.String;
                        Depth = reader.CurrentDepth;
                        TokenStart = start;
                        ValueIsEscaped = false;
                        _valueLength = 0;
                        SkippedOversizedString = true;
                        return true;
                    }
                    escaped = true;
                    consumed++;
                    continue;
                }
                if (value < 0x20) { throw new JsonException("Unescaped control byte in JSON string."); }
                int run = consumed + 1;
                while (run < span.Length && span[run] >= 0x20 && span[run] is not ((byte)'"' or (byte)'\\')) { run++; }
                ValidateUtf8Segment(utf8, span.Slice(consumed, run - consumed), flush: false);
                consumed = run;
            }
            _consumed += consumed;
            _start = _end = 0;
            _token.ThrowIfCancellationRequested();
            int read = await _source.ReadAsync(_owner.AsMemory(0, 64 * 1024), _token).ConfigureAwait(false);
            if (read == 0) { throw new JsonException("Incomplete optional state scalar."); }
            _end = read;
        }
    }

    /// <summary>Advances a strict stateful decoder using a fixed destination across chunk boundaries.</summary>
    private static void ValidateUtf8Segment(Decoder decoder, ReadOnlySpan<byte> input, bool flush)
    {
        Span<char> output = stackalloc char[256];
        try
        {
            bool completed;
            do
            {
                decoder.Convert(input, output, flush, out int consumed, out _, out completed);
                input = input[consumed..];
            } while (!completed);
        }
        catch (DecoderFallbackException exception)
        {
            throw new JsonException("Invalid UTF8 in optional state scalar.", exception);
        }
    }

    private static int HexDigit(byte value)
        => value is >= (byte)'0' and <= (byte)'9' ? value - '0'
            : value is >= (byte)'a' and <= (byte)'f' ? value - 'a' + 10
            : value is >= (byte)'A' and <= (byte)'F' ? value - 'A' + 10
            : throw new JsonException("Invalid Unicode escape.");

    /// <summary>Decodes a name/value only after reserving conservative UTF16 capacity and object overhead.</summary>
    internal (string Value, long Charge) DecodeString()
    {
        if (TokenType is not (JsonTokenType.String or JsonTokenType.PropertyName)) { throw new InvalidOperationException("Current token is not a string."); }
        long charge = checked(_valueLength * 2L + 64);
        _budget.Reserve(charge);
        int framedLength = checked(_valueLength + 2);
        byte[]? framed = null;
        bool reserved = false;
        try
        {
            // Charge the short-lived quoted decoder input independently of its retained string.
            _budget.Reserve(framedLength);
            reserved = true;
            framed = new byte[framedLength];
            framed[0] = (byte)'"';
            RawValue.CopyTo(framed.AsSpan(1));
            framed[^1] = (byte)'"';
            var reader = new Utf8JsonReader(framed, isFinalBlock: true, state: default);
            _ = reader.Read();
            return (reader.GetString()!, charge);
        }
        catch { _budget.Release(charge); throw; }
        finally
        {
            if (framed is not null) { CryptographicOperations.ZeroMemory(framed); }
            if (reserved) { _budget.Release(framedLength); }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        byte[]? owner = _owner;
        _owner = null;
        if (owner is not null)
        {
            CryptographicOperations.ZeroMemory(owner);
            _budget.Release(owner.Length);
        }
    }

    /// <summary>Runs the strict parser synchronously so no borrowed span crosses an await.</summary>
    private bool TryRead()
    {
        var reader = new Utf8JsonReader(_owner.AsSpan(_start, _end - _start), _final, _state);
        bool found = reader.Read();
        TokenStart = checked(_consumed + reader.TokenStartIndex);
        int consumed = checked((int)reader.BytesConsumed);
        _consumed = checked(_consumed + consumed);
        if (!found)
        {
            _start += consumed;
            _state = reader.CurrentState;
            return false;
        }

        TokenType = reader.TokenType;
        Depth = reader.CurrentDepth;
        ValueIsEscaped = reader.ValueIsEscaped;
        _valueOffset = checked(_start + (int)reader.TokenStartIndex + (TokenType is JsonTokenType.String or JsonTokenType.PropertyName ? 1 : 0));
        _valueLength = reader.ValueSpan.Length;
        if (TokenType is JsonTokenType.String or JsonTokenType.PropertyName)
        {
            ValidateUnicode(reader.ValueSpan);
        }
        _start += consumed;
        _state = reader.CurrentState;
        return true;
    }

    /// <summary>Checks raw UTF8 and escaped UTF16 scalar validity without decoded string allocation.</summary>
    private static void ValidateUnicode(ReadOnlySpan<byte> raw)
    {
        _ = StrictUtf8.GetCharCount(raw);
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] != '\\') { continue; }
            if (++i >= raw.Length) { throw new JsonException("Incomplete JSON escape."); }
            if (raw[i] != 'u') { continue; }
            int value = Hex(raw.Slice(i + 1, 4));
            i += 4;
            if (value is >= 0xdc00 and <= 0xdfff) { throw new JsonException("Unpaired low surrogate."); }
            if (value is >= 0xd800 and <= 0xdbff)
            {
                if (i + 6 >= raw.Length || raw[i + 1] != '\\' || raw[i + 2] != 'u') { throw new JsonException("Unpaired high surrogate."); }
                int low = Hex(raw.Slice(i + 3, 4));
                if (low is < 0xdc00 or > 0xdfff) { throw new JsonException("Unpaired high surrogate."); }
                i += 6;
            }
        }
    }

    /// <summary>Reads one parser-validated four-digit hexadecimal escape.</summary>
    private static int Hex(ReadOnlySpan<byte> digits)
    {
        int value = 0;
        foreach (byte digit in digits)
        {
            value = (value << 4) | (digit is >= (byte)'0' and <= (byte)'9' ? digit - '0'
                : digit is >= (byte)'a' and <= (byte)'f' ? digit - 'a' + 10 : digit - 'A' + 10);
        }
        return value;
    }

    /// <summary>Moves only unconsumed token bytes and clears vacated owner capacity.</summary>
    private void Compact()
    {
        int remaining = _end - _start;
        _owner.AsSpan(_start, remaining).CopyTo(_owner);
        CryptographicOperations.ZeroMemory(_owner.AsSpan(remaining, _end - remaining));
        _start = 0;
        _end = remaining;
    }

    /// <summary>Reserves both old and replacement owners before growing an incomplete token.</summary>
    private void Grow()
    {
        int capacity = checked(_owner!.Length * 2);
        _budget.Reserve(capacity);
        byte[] replacement;
        try { replacement = new byte[capacity]; }
        catch { _budget.Release(capacity); throw; }
        _owner.AsSpan(0, _end).CopyTo(replacement);
        CryptographicOperations.ZeroMemory(_owner);
        _budget.Release(_owner.Length);
        _owner = replacement;
    }
}
