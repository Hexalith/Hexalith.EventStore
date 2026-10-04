using System.Globalization;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Normalizes exact JSON decimals with nonallocating expansion admission.</summary>
internal static class EventCanonicalNumber
{
    /// <summary>Measures plain decimal expansion without allocating its expanded coefficient.</summary>
    internal static int Measure(string raw, int maximumCharacters)
    {
        return Analyze(raw, maximumCharacters).Length;
    }

    /// <summary>Checks mathematical integrality without constructing expanded decimal text.</summary>
    internal static bool IsInteger(string raw)
    {
        var value = Analyze(raw, int.MaxValue);
        return value.Digits == 0 || value.Position >= value.Digits;
    }

    /// <summary>Renders only after admitting the exact expanded character count.</summary>
    internal static string Normalize(string raw, int maximumCharacters)
    {
        var value = Analyze(raw, maximumCharacters);
        if (value.Digits == 0)
        {
            return "0";
        }

        return string.Create(value.Length, (Raw: raw, Value: value), static (output, state) =>
        {
            var number = state.Value;
            int index = 0;
            if (number.Negative) { output[index++] = '-'; }
            if (number.Position <= 0)
            {
                output[index++] = '0';
                output[index++] = '.';
                output.Slice(index, checked((int)-number.Position)).Fill('0');
                index += checked((int)-number.Position);
            }

            int digits = 0;
            for (int i = number.First; i <= number.Last; i++)
            {
                if (state.Raw[i] == '.') { continue; }
                if (digits > 0 && digits == number.Position && number.Position < number.Digits)
                {
                    output[index++] = '.';
                }

                output[index++] = state.Raw[i];
                digits++;
            }

            output[index..].Fill('0');
        });
    }

    /// <summary>Calculates sign, significant digits and decimal position from valid JSON syntax.</summary>
    private static (bool Negative, int First, int Last, int Digits, long Position, int Length) Analyze(string raw, int maximumCharacters)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCharacters);
        bool negative = raw.StartsWith('-');
        int start = negative ? 1 : 0;
        int exponentAt = raw.IndexOfAny(['e', 'E']);
        int end = exponentAt < 0 ? raw.Length : exponentAt;
        int point = raw.IndexOf('.', start, end - start);
        int fractionalDigits = point < 0 ? 0 : end - point - 1;
        int first = start;
        while (first < end && raw[first] is '0' or '.') { first++; }
        if (first == end)
        {
            if (maximumCharacters < 1) { throw new ArgumentException("RegistryLimit: canonical decimal exceeds capacity."); }
            return (false, first, first, 0, 0, 1);
        }

        int last = end - 1;
        int trailingZeros = 0;
        while (last >= first && raw[last] is '0' or '.')
        {
            if (raw[last] == '0') { trailingZeros++; }
            last--;
        }

        int digits = last - first + 1 - (point >= first && point <= last ? 1 : 0);
        long exponent = 0;
        if (exponentAt >= 0 && !long.TryParse(raw.AsSpan(exponentAt + 1), NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out exponent))
        {
            throw new ArgumentException("RegistryLimit: canonical decimal expansion exceeds capacity.", nameof(raw));
        }

        try
        {
            long position = checked(digits + exponent - fractionalDigits + trailingZeros);
            long length = position >= digits ? position : position > 0 ? digits + 1L : checked(2L - position + digits);
            length = checked(length + (negative ? 1 : 0));
            if (length > maximumCharacters)
            {
                throw new ArgumentException("RegistryLimit: canonical decimal expansion exceeds capacity.", nameof(raw));
            }

            return (negative, first, last, digits, position, checked((int)length));
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException("RegistryLimit: canonical decimal expansion exceeds capacity.", nameof(raw), exception);
        }
    }
}
