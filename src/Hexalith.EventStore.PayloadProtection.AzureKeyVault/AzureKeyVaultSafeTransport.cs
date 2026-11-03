// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 11 and 15.
using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using Azure.Core.Pipeline;
using Azure.Core;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Bounded HTTP/1.1 over platform TLS, avoiding System.Net.Http request/connection instrumentation and SDK-owned body copies.</summary>
/// <remarks>No redirects, connection reuse, proxy, decompression or alternate provider path. Platform network/dump policy remains operator-owned.</remarks>
internal sealed class AzureKeyVaultSafeTransport(AzureKeyVaultPayloadProtectionOptions options, bool identity = false) : HttpPipelineTransport
{
    private const int MaximumHeaderBytes = 16384;
    internal const int MaximumBodyBytes = 65536;
    /// <inheritdoc/>
    public override Request CreateRequest() => new AzureKeyVaultRequest();
    /// <inheritdoc/>
    public override void Process(HttpMessage message) => ProcessAsync(message).AsTask().GetAwaiter().GetResult();
    /// <inheritdoc/>
    public override async ValueTask ProcessAsync(HttpMessage message)
    {
        var request = (AzureKeyVaultRequest)message.Request;
        Uri uri = request.Uri.ToUri();
        ValidateTarget(uri, request.Method);
        CancellationToken acquisitionToken = identity ? AzureKeyVaultTokenAcquisition.RequireSingleRequest() : message.CancellationToken;
        byte[] outbound = new byte[MaximumHeaderBytes];
        byte[] inbound = new byte[MaximumHeaderBytes + MaximumBodyBytes + 1];
        using var timeout = new CancellationTokenSource(options.RequestTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(message.CancellationToken, timeout.Token, acquisitionToken);
        CancellationToken token = linked.Token;
        try
        {
            using var socket = new TcpClient();
            await socket.ConnectAsync(uri.Host, uri.Port, token).ConfigureAwait(false);
            using NetworkStream network = socket.GetStream();
            Stream stream = network;
            SslStream? tls = null;
            try
            {
                if (uri.Scheme == "https")
                {
                    tls = new SslStream(network, leaveInnerStreamOpen: true);
                    await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = uri.IdnHost, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13, ApplicationProtocols = [SslApplicationProtocol.Http11] }, token).ConfigureAwait(false);
                    stream = tls;
                }

                int used = Append(outbound, 0, request.Method.Method + " " + uri.PathAndQuery + " HTTP/1.1\r\nHost: " + uri.Authority + "\r\nConnection: close\r\n");
                foreach (HttpHeader header in request.Headers)
                {
                    if (header.Name.Equals("Host", StringComparison.OrdinalIgnoreCase) || header.Name.Equals("Connection", StringComparison.OrdinalIgnoreCase) || header.Name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) || header.Name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (header.Name.Length == 0 || header.Name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') || header.Value.Any(c => c < 0x20 || c > 0x7e))
                    {
                        throw AzureKeyVaultOptionsValidator.Invalid();
                    }

                    used = Append(outbound, used, header.Name);
                    used = Append(outbound, used, ": ");
                    used = Append(outbound, used, header.Value);
                    used = Append(outbound, used, "\r\n");
                }

                if (request.Content is not null)
                {
                    if (!request.Content.TryComputeLength(out long length) || length < 0 || length > MaximumBodyBytes)
                    {
                        throw AzureKeyVaultOptionsValidator.Invalid();
                    }

                    used = Append(outbound, used, "Content-Length: " + length.ToString(CultureInfo.InvariantCulture) + "\r\n");
                }

                used = Append(outbound, used, "\r\n");
                request.Dispatched = true;
                await stream.WriteAsync(outbound.AsMemory(0, used), token).ConfigureAwait(false);
                if (request.Content is not null)
                {
                    await request.Content.WriteToAsync(stream, token).ConfigureAwait(false);
                }

                await stream.FlushAsync(token).ConfigureAwait(false);
                int count = 0;
                while (true)
                {
                    if (count == inbound.Length)
                    {
                        throw AzureKeyVaultOptionsValidator.Invalid();
                    }

                    int read = await stream.ReadAsync(inbound.AsMemory(count), token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    count += read;
                }

                message.Response = ParseResponse(inbound.AsSpan(0, count));
                if (!identity && request.Method == RequestMethod.Get && message.Response.Status == 401 && message.Response.Headers.TryGetValue("WWW-Authenticate", out string? challenge))
                {
                    _ = AzureKeyVaultMetadataChallenge.Validate(challenge, options);
                }

                if (identity && message.Response.Status != 200)
                {
                    PayloadBackendFailure failure = message.Response.Status is 408 or 429 or >= 500 and <= 599 ? PayloadBackendFailure.Unavailable : PayloadBackendFailure.Denied;
                    message.Response.Dispose();
                    AzureKeyVaultTokenAcquisition.Stop(failure);
                    throw new AzureKeyVaultTransportException(failure);
                }
            }
            finally
            {
                if (tls is not null)
                {
                    await tls.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)when (message.CancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(message.CancellationToken);
        }
        catch (AuthenticationException)
        {
            if (identity)
            {
                AzureKeyVaultTokenAcquisition.Stop(PayloadBackendFailure.Denied);
            }

            throw new AzureKeyVaultTransportException(PayloadBackendFailure.Denied);
        }
        catch (Exception exception)when (exception is IOException or SocketException or OperationCanceledException)
        {
            if (identity)
            {
                AzureKeyVaultTokenAcquisition.Stop(PayloadBackendFailure.Unavailable);
            }

            throw new AzureKeyVaultTransportException(!identity && request.Dispatched && request.Method == RequestMethod.Post ? PayloadBackendFailure.Ambiguous : PayloadBackendFailure.Unavailable);
        }
        catch (AzureKeyVaultTransportException)
        {
            throw;
        }
        catch
        {
            throw new AzureKeyVaultTransportException(PayloadBackendFailure.ConsistencyMismatch);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(outbound);
            CryptographicOperations.ZeroMemory(inbound);
        }
    }

    private void ValidateTarget(Uri uri, RequestMethod method)
    {
        if (identity)
        {
            // Only platform-local managed identity services. No arbitrary authority/credential endpoint is added to options.
            if (uri.Scheme != "http" || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || !IPAddress.TryParse(uri.Host, out IPAddress? address) || !(IPAddress.IsLoopback(address) || (address.AddressFamily == AddressFamily.InterNetwork && address.GetAddressBytes()[0] == 169 && address.GetAddressBytes()[1] == 254)))
            {
                throw new AzureKeyVaultTransportException(PayloadBackendFailure.Denied);
            }

            return;
        }

        if (uri.Scheme != "https" || uri.Authority != options.VaultUri.Authority || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || uri.Query != "?api-version=2025-07-01")
        {
            throw new AzureKeyVaultTransportException(PayloadBackendFailure.ConsistencyMismatch);
        }

        string path = uri.AbsolutePath;
        string prefix = "/keys/" + options.KeyName;
        if (method == RequestMethod.Get && path == prefix)
        {
            return;
        }

        if (method == RequestMethod.Get)
        {
            _ = AzureKeyVaultKeyProfile.Version(options, options.VaultUri.GetLeftPart(UriPartial.Authority) + path);
            return;
        }

        foreach (string suffix in new[]
        {
            "/wrapkey",
            "/unwrapkey"
        }

        )
        {
            if (method == RequestMethod.Post && path.EndsWith(suffix, StringComparison.Ordinal))
            {
                _ = AzureKeyVaultKeyProfile.Version(options, options.VaultUri.GetLeftPart(UriPartial.Authority) + path[..^suffix.Length]);
                return;
            }
        }

        throw new AzureKeyVaultTransportException(PayloadBackendFailure.ConsistencyMismatch);
    }

    private static int Append(byte[] target, int offset, string value)
    {
        int length = Encoding.ASCII.GetByteCount(value);
        if (offset + length > target.Length)
        {
            throw AzureKeyVaultOptionsValidator.Invalid();
        }

        return offset + Encoding.ASCII.GetBytes(value, target.AsSpan(offset));
    }

    /// <summary>Parses strict bounded content-length/chunked/close framing, clearing temporary decoded data on every exit.</summary>
    internal static AzureKeyVaultOwnedResponse ParseResponse(ReadOnlySpan<byte> raw)
    {
        int split = raw.IndexOf("\r\n\r\n"u8);
        if (split < 0 || split > MaximumHeaderBytes)
        {
            throw AzureKeyVaultOptionsValidator.Invalid();
        }

        ReadOnlySpan<byte> header = raw[..split];
        int first = header.IndexOf("\r\n"u8);
        if (first < 0 || header.Length < 12 || !header[..9].SequenceEqual("HTTP/1.1 "u8) || !int.TryParse(Encoding.ASCII.GetString(header.Slice(9, 3)), NumberStyles.None, CultureInfo.InvariantCulture, out int status) || status < 200 || status > 599)
        {
            throw AzureKeyVaultOptionsValidator.Invalid();
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        long? length = null;
        bool chunked = false;
        ReadOnlySpan<byte> remaining = header[(first + 2)..];
        while (!remaining.IsEmpty)
        {
            int newline = remaining.IndexOf("\r\n"u8);
            ReadOnlySpan<byte> line = newline < 0 ? remaining : remaining[..newline];
            remaining = newline < 0 ? [] : remaining[(newline + 2)..];
            int colon = line.IndexOf((byte)':');
            if (colon < 1 || line[..colon].ContainsAnyExceptInRange((byte)33, (byte)126))
            {
                throw AzureKeyVaultOptionsValidator.Invalid();
            }

            string name = Encoding.ASCII.GetString(line[..colon]);
            ReadOnlySpan<byte> value = line[(colon + 1)..].Trim((byte)' ');
            if (value.ContainsAnyExceptInRange((byte)32, (byte)126))
            {
                throw AzureKeyVaultOptionsValidator.Invalid();
            }

            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                if (length is not null || !long.TryParse(Encoding.ASCII.GetString(value), NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) || parsed > MaximumBodyBytes)
                {
                    throw AzureKeyVaultOptionsValidator.Invalid();
                }

                length = parsed;
            }
            else if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                if (chunked || !value.SequenceEqual("chunked"u8))
                {
                    throw AzureKeyVaultOptionsValidator.Invalid();
                }

                chunked = true;
            }
            else if (name.Equals("Retry-After", StringComparison.OrdinalIgnoreCase) || name.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase) || name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                if (!headers.TryAdd(name, name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ? "application/json" : Encoding.ASCII.GetString(value)))
                {
                    throw AzureKeyVaultOptionsValidator.Invalid();
                }
            }
        }

        if (headers.TryGetValue("Retry-After", out string? rawRetry))
        {
            TimeSpan? delay = AzureKeyVaultFailureClassifier.RetryAfter(rawRetry, DateTimeOffset.UtcNow);
            headers.Remove("Retry-After");
            if (delay is not null)
            {
                headers["Retry-After"] = ((int)delay.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            }
        }

        if (chunked && length is not null)
        {
            throw AzureKeyVaultOptionsValidator.Invalid();
        }

        byte[]? body = null;
        try
        {
            ReadOnlySpan<byte> data = raw[(split + 4)..];
            if (chunked)
            {
                byte[] staging = new byte[MaximumBodyBytes];
                try
                {
                    int used = 0;
                    while (true)
                    {
                        int lineEnd = data.IndexOf("\r\n"u8);
                        if (lineEnd < 1 || lineEnd > 8 || !int.TryParse(Encoding.ASCII.GetString(data[..lineEnd]), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int size) || size < 0)
                        {
                            throw AzureKeyVaultOptionsValidator.Invalid();
                        }

                        data = data[(lineEnd + 2)..];
                        if (size == 0)
                        {
                            if (!data.SequenceEqual("\r\n"u8))
                            {
                                throw AzureKeyVaultOptionsValidator.Invalid();
                            }

                            break;
                        }

                        if (size > MaximumBodyBytes - used || data.Length < size + 2 || !data.Slice(size, 2).SequenceEqual("\r\n"u8))
                        {
                            throw AzureKeyVaultOptionsValidator.Invalid();
                        }

                        data[..size].CopyTo(staging.AsSpan(used));
                        used += size;
                        data = data[(size + 2)..];
                    }

                    body = staging.AsSpan(0, used).ToArray();
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(staging);
                }
            }
            else
            {
                if (data.Length > MaximumBodyBytes || (length is long expected && data.Length != expected))
                {
                    throw AzureKeyVaultOptionsValidator.Invalid();
                }

                body = data.ToArray();
            }

            if (status >= 300)
            {
                CryptographicOperations.ZeroMemory(body);
                body = "{\"error\":{\"code\":\"ProviderFailure\",\"message\":\"The provider operation failed.\"}}"u8.ToArray();
            }

            var response = new AzureKeyVaultOwnedResponse(status, body, headers);
            body = null;
            return response;
        }
        finally
        {
            if (body is not null)
            {
                CryptographicOperations.ZeroMemory(body);
            }
        }
    }
}
