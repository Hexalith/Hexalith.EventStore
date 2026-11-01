using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Observes actual sent/received bytes while retaining only finite safe hashes, never signed material.</summary>
internal sealed class QualificationHttpTransportObserver(QualificationRuntime runtime) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Post && request.Method != HttpMethod.Get) return await base.SendAsync(request, cancellationToken);
        DateTimeOffset began = DateTimeOffset.UtcNow;
        byte[] input = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        using var json = request.Method == HttpMethod.Post ? JsonDocument.Parse(input) : null;
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        try
        {
            byte[] output = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            runtime.RecordHttpExchange(new(request.RequestUri!.AbsoluteUri, began, DateTimeOffset.UtcNow, (int)response.StatusCode,
                QualificationRuntime.Hash(input), QualificationRuntime.Hash(output),
                request.Method == HttpMethod.Post ? QualificationRuntime.Hash(JsonSerializer.SerializeToUtf8Bytes(HistoricalHttpOperationCapture.Request(json!.RootElement))) : QualificationRuntime.Hash("null"u8.ToArray()),
                request.Method == HttpMethod.Post ? QualificationRuntime.Hash(JsonSerializer.SerializeToUtf8Bytes(HistoricalHttpOperationCapture.Response(Encoding.UTF8.GetString(output)))) : QualificationRuntime.Hash("null"u8.ToArray()), request.Method.Method));
            return response;
        }
        catch { response.Dispose(); throw; }
    }
}
