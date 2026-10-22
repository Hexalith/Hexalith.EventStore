using System.Security.Cryptography;

namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>Validates JSON-unescaped canonical Base64 and decoded length while a scalar spans transport windows.</summary>
internal sealed class IncrementalDomainBase64Admission : IDomainJsonScalarAdmission, IDisposable
{
    private readonly DomainResponseBufferBudget _budget;
    private byte[]? _owner;
    private readonly int[] _quartet = new int[4];
    private int _quartetLength;
    private int _length;
    private int _observed;
    private int _unicodeDigits;
    private int _unicodeValue;
    private bool _escape;
    private bool _opened;
    private bool _closed;
    private bool _padded;
    private bool _failed;
    private bool _completed;

    /// <summary>Reserves the admitted readable ceiling before any scalar callback can produce bytes.</summary>
    internal IncrementalDomainBase64Admission(DomainResponseBufferBudget budget, int maximumDecodedBytes)
    {
        ArgumentNullException.ThrowIfNull(budget);
        if (maximumDecodedBytes is < 0 or > 64 * 1024 * 1024) { throw new ArgumentOutOfRangeException(nameof(maximumDecodedBytes)); }
        _budget = budget;
        budget.Reserve(maximumDecodedBytes);
        try { _owner = new byte[maximumDecodedBytes]; }
        catch { budget.Release(maximumDecodedBytes); throw; }
    }

    /// <inheritdoc/>
    public void Observe(ReadOnlySpan<byte> cumulativeJsonScalar)
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        if (_failed || _completed) { throw new FormatException("A failed payload scalar cannot be resumed."); }
        try
        {
            if (cumulativeJsonScalar.Length < _observed) { throw new FormatException("The payload scalar prefix changed."); }
            foreach (byte value in cumulativeJsonScalar[_observed..])
            {
                if (!_opened)
                {
                    if (value != '"') { throw new FormatException("A payload must be a Base64 JSON string."); }
                    _opened = true;
                    continue;
                }
                if (_closed) { throw new FormatException("Unexpected bytes after payload scalar."); }
                if (_unicodeDigits > 0)
                {
                    int digit = value is >= (byte)'0' and <= (byte)'9' ? value - '0'
                        : value is >= (byte)'a' and <= (byte)'f' ? value - 'a' + 10
                        : value is >= (byte)'A' and <= (byte)'F' ? value - 'A' + 10 : -1;
                    if (digit < 0) { throw new FormatException("Invalid Unicode escape in Base64 scalar."); }
                    _unicodeValue = (_unicodeValue << 4) | digit;
                    if (--_unicodeDigits == 0)
                    {
                        if (_unicodeValue > 127) { throw new FormatException("Base64 alphabet must be ASCII."); }
                        Accept((byte)_unicodeValue);
                    }
                    continue;
                }
                if (_escape)
                {
                    _escape = false;
                    if (value == 'u') { _unicodeDigits = 4; _unicodeValue = 0; }
                    else if (value == '/') { Accept((byte)'/'); }
                    else { throw new FormatException("Nonalphabet JSON escape in Base64 scalar."); }
                    continue;
                }
                if (value == '\\') { _escape = true; continue; }
                if (value == '"')
                {
                    if (_quartetLength != 0) { throw new FormatException("Base64 requires canonical padding."); }
                    _closed = true;
                    continue;
                }
                Accept(value);
            }
            _observed = cumulativeJsonScalar.Length;
        }
        catch { _failed = true; throw; }
    }

    /// <summary>Returns a charged detached output only after the full scalar closes successfully.</summary>
    internal byte[] Complete()
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        if (_failed || _completed || !_closed || _escape || _unicodeDigits != 0) { throw new FormatException("Incomplete or invalid Base64 payload scalar."); }
        _completed = true;
        _budget.Reserve(_length);
        try { return _owner.AsSpan(0, _length).ToArray(); }
        catch { _budget.Release(_length); throw; }
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
        Array.Clear(_quartet);
    }

    /// <summary>Validates alphabet/padding and emits at most three privately owned bytes per completed quartet.</summary>
    private void Accept(byte value)
    {
        if (_padded) { throw new FormatException("Base64 padding must be final."); }
        int decoded = value is >= (byte)'A' and <= (byte)'Z' ? value - 'A'
            : value is >= (byte)'a' and <= (byte)'z' ? value - 'a' + 26
            : value is >= (byte)'0' and <= (byte)'9' ? value - '0' + 52
            : value == '+' ? 62 : value == '/' ? 63 : value == '=' ? -2 : -1;
        if (decoded == -1) { throw new FormatException("Payload must use the canonical Base64 alphabet without whitespace."); }
        _quartet[_quartetLength++] = decoded;
        if (_quartetLength < 4) { return; }
        if (_quartet[0] < 0 || _quartet[1] < 0 || _quartet[2] == -2 && _quartet[3] != -2)
        {
            throw new FormatException("Invalid Base64 padding.");
        }
        int count = _quartet[2] == -2 ? 1 : _quartet[3] == -2 ? 2 : 3;
        if (count == 1 && (_quartet[1] & 15) != 0 || count == 2 && (_quartet[2] & 3) != 0)
        {
            throw new FormatException("Noncanonical Base64 trailing bits.");
        }
        if (count > _owner!.Length - _length) { throw new InvalidOperationException("PayloadLimit: decoded payload exceeds the selected readable ceiling."); }
        _owner[_length++] = (byte)((_quartet[0] << 2) | (_quartet[1] >> 4));
        if (count > 1) { _owner[_length++] = (byte)((_quartet[1] << 4) | (_quartet[2] >> 2)); }
        if (count > 2) { _owner[_length++] = (byte)((_quartet[2] << 6) | _quartet[3]); }
        _quartetLength = 0;
        _padded = count != 3;
    }
}
