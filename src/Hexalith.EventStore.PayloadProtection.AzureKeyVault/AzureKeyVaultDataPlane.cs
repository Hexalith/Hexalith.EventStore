// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
using System.Security.Cryptography;
using Azure.Core.Pipeline;
using Azure.Core;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Emits only explicit versioned remote RSA-OAEP-256 POSTs; no CryptographyClient or local RSA path exists.</summary>
internal sealed class AzureKeyVaultDataPlane
{
    private readonly HttpPipeline _pipeline;
    private readonly TimeProvider _clock;
    private readonly Action<byte[]>? _cleared;
    /// <summary>Creates the fixed-scope no-challenge pipeline with explicit transport and zero nested retries.</summary>
    internal AzureKeyVaultDataPlane(AzureKeyVaultPayloadProtectionOptions options, TokenCredential credential, HttpPipelineTransport transport, TimeProvider? clock = null, Action<byte[]>? cleared = null)
    {
        string scope = AzureKeyVaultOptionsValidator.Validate(options);
        var clientOptions = new KeyClientOptionsForPipeline();
        AzureKeyVaultCredentialFactory.Configure(clientOptions, transport);
        _pipeline = HttpPipelineBuilder.Build(clientOptions, [], [new AzureKeyVaultNoChallengePolicy(AzureKeyVaultCredentialFactory.Bound(credential), scope)], new ResponseClassifier());
        _clock = clock ?? TimeProvider.System;
        _cleared = cleared;
    }

    /// <summary>Completes remote I/O before clearing any request/response copy, including cancellation-ignoring transports.</summary>
    internal async ValueTask<PayloadBackendResult> ExecuteAsync(string id, ReadOnlyMemory<byte> input, bool wrap, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        byte[] copied = input.ToArray();
        byte[] responseBytes = new byte[AzureKeyVaultSafeTransport.MaximumBodyBytes + 1];
        using HttpMessage message = _pipeline.CreateMessage();
        try
        {
            message.BufferResponse = false;
            message.Request.Method = RequestMethod.Post;
            message.Request.Uri.Reset(new Uri(id + (wrap ? "/wrapkey" : "/unwrapkey") + "?api-version=2025-07-01"));
            message.Request.Headers.SetValue("Content-Type", "application/json");
            message.Request.Content = new AzureKeyVaultOwnedRequestContent(AzureKeyVaultWireCodec.Encode(copied), _cleared);
            await _pipeline.SendAsync(message, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            int status = message.Response.Status;
            if (status != 200)
            {
                message.Response.Headers.TryGetValue("Retry-After", out string? retryAfter);
                return new(AzureKeyVaultFailureClassifier.Status(status, wrap), retryAfter: AzureKeyVaultFailureClassifier.RetryAfter(retryAfter, _clock.GetUtcNow()));
            }

            Stream? stream = message.Response.ContentStream;
            if (stream is null)
            {
                return new(PayloadBackendFailure.ConsistencyMismatch);
            }

            int count = 0;
            while (count < responseBytes.Length)
            {
                int read = await stream.ReadAsync(responseBytes.AsMemory(count), token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                count += read;
            }

            token.ThrowIfCancellationRequested();
            if (count > AzureKeyVaultSafeTransport.MaximumBodyBytes)
            {
                return new(PayloadBackendFailure.ConsistencyMismatch);
            }

            byte[] output = AzureKeyVaultWireCodec.Decode(responseBytes.AsSpan(0, count), id, wrap ? 384 : 32);
            return new(PayloadBackendFailure.None, output, id);
        }
        catch (OperationCanceledException)when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException(token);
        }
        catch (AzureKeyVaultTransportException exception)
        {
            return new(exception.Failure == PayloadBackendFailure.Ambiguous && !wrap ? PayloadBackendFailure.Unavailable : exception.Failure);
        }
        catch (Exception exception)
        {
            return new(AzureKeyVaultFailureClassifier.Exception(exception, message.Request is AzureKeyVaultRequest { Dispatched: true }, wrap));
        }
        finally
        {
            message.Request.Content?.Dispose();
            if (message.HasResponse)
            {
                message.Response.Dispose();
            }

            CryptographicOperations.ZeroMemory(copied);
            _cleared?.Invoke(copied);
            CryptographicOperations.ZeroMemory(responseBytes);
            _cleared?.Invoke(responseBytes);
        }
    }
}
