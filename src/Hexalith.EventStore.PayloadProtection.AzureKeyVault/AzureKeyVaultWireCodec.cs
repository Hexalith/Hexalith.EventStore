// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Encodes and parses crypto JSON over owned spans; never creates an immutable DEK/base64 string or DOM.</summary>
internal static class AzureKeyVaultWireCodec
{
    /// <summary>Copies and encodes input into the exact alg/value request shape.</summary>
    internal static byte[] Encode(ReadOnlySpan<byte> input)
    {
        int encodedLength = Base64Url.GetEncodedLength(input.Length);
        ReadOnlySpan<byte> prefix = "{\"alg\":\"RSA-OAEP-256\",\"value\":\""u8;
        byte[] result = new byte[prefix.Length + encodedLength + 2];
        prefix.CopyTo(result);
        int written = Base64Url.EncodeToUtf8(input, result.AsSpan(prefix.Length));
        "\"}"u8.CopyTo(result.AsSpan(prefix.Length + written));
        return result;
    }

    /// <summary>Validates the service-returned exact kid, optional algorithm and bounded canonical output.</summary>
    internal static byte[] Decode(ReadOnlySpan<byte> json, string expectedId, int length)
    {
        byte[]? result = null;
        try
        {
            var reader = new Utf8JsonReader(json, new JsonReaderOptions { MaxDepth = 4 });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw AzureKeyVaultOptionsValidator.Invalid();
            }

            bool kid = false, value = false, algorithm = false;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName || reader.ValueIsEscaped)
                {
                    throw AzureKeyVaultOptionsValidator.Invalid();
                }

                int field = reader.ValueTextEquals("kid"u8) ? 1 : reader.ValueTextEquals("value"u8) ? 2 : reader.ValueTextEquals("alg"u8) ? 3 : 0;
                if (!reader.Read() || reader.TokenType != JsonTokenType.String || reader.ValueIsEscaped)
                {
                    throw AzureKeyVaultOptionsValidator.Invalid();
                }

                switch (field)
                {
                    case 1 when !kid:
                        kid = reader.ValueTextEquals(expectedId);
                        if (!kid)
                        {
                            throw AzureKeyVaultOptionsValidator.Invalid();
                        }

                        break;
                    case 2 when !value:
                        value = true;
                        if (reader.ValueSpan.Length != Base64Url.GetEncodedLength(length))
                        {
                            throw AzureKeyVaultOptionsValidator.Invalid();
                        }

                        result = new byte[length];
                        if (!Base64Url.TryDecodeFromUtf8(reader.ValueSpan, result, out int written) || written != length)
                        {
                            throw AzureKeyVaultOptionsValidator.Invalid();
                        }

                        byte[] canonical = new byte[reader.ValueSpan.Length];
                        try
                        {
                            _ = Base64Url.EncodeToUtf8(result, canonical);
                            if (!canonical.AsSpan().SequenceEqual(reader.ValueSpan))
                            {
                                throw AzureKeyVaultOptionsValidator.Invalid();
                            }
                        }
                        finally
                        {
                            CryptographicOperations.ZeroMemory(canonical);
                        }

                        break;
                    case 3 when !algorithm:
                        algorithm = true;
                        if (!reader.ValueTextEquals("RSA-OAEP-256"u8))
                        {
                            throw AzureKeyVaultOptionsValidator.Invalid();
                        }

                        break;
                    default:
                        throw AzureKeyVaultOptionsValidator.Invalid();
                }
            }

            if (!kid || !value || reader.TokenType != JsonTokenType.EndObject || reader.Read())
            {
                throw AzureKeyVaultOptionsValidator.Invalid();
            }

            byte[] output = result!;
            result = null;
            return output;
        }
        finally
        {
            if (result is not null)
            {
                CryptographicOperations.ZeroMemory(result);
            }
        }
    }
}
