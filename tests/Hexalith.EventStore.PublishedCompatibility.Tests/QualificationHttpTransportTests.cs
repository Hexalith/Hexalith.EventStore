using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Uses a genuine bounded loopback HTTP connection to verify safe independent byte capture.</summary>
public sealed class QualificationHttpTransportTests
{
    [Theory]
    [InlineData(200, false)]
    [InlineData(500, false)]
    [InlineData(302, false)]
    [InlineData(200, true)]
    [InlineData(404, true)]
    [InlineData(500, true)]
    [InlineData(302, true)]
    public async Task ActualHttpBytesAreObservedWithoutRetainingSignedMaterial(int status, bool get)
    {
        await using var runtime = new QualificationRuntime();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        const string sentinel = "synthetic-private-proof-never-export";
        string input = get ? "" : "{\"mode\":\"valid\",\"cursor\":\"" + sentinel + "\"}";
        string output = "{\"decoded\":true,\"position\":\"987\",\"cursor\":\"" + sentinel + "\"}";
        async Task<string> ServeAsync()
        {
            using var connection = await listener.AcceptTcpClientAsync(deadline.Token);
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, leaveOpen: true);
            int length = -1;
            for (int count = 0; count < 64; count++)
            {
                string line = (await reader.ReadLineAsync(deadline.Token))!;
                if (line.Length == 0) break;
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line[15..], System.Globalization.CultureInfo.InvariantCulture);
            }
            if (get) length.ShouldBeInRange(-1, 0); else length.ShouldBeInRange(1, 4096);
            if (get) length = 0;
            char[] actual = new char[length]; if (length > 0) (await reader.ReadBlockAsync(actual, deadline.Token)).ShouldBe(length);
            byte[] body = Encoding.UTF8.GetBytes(output);
            string redirect = status == 302 ? "Location: http://127.0.0.1:1/foreign\r\n" : "";
            byte[] header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Result\r\n{redirect}Content-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, deadline.Token); await stream.WriteAsync(body, deadline.Token); await stream.FlushAsync(deadline.Token);
            return QualificationRuntime.Hash(Encoding.UTF8.GetBytes(actual));
        }
        Task<string> server = ServeAsync();
        using var client = new HttpClient(new QualificationHttpTransportObserver(runtime));
        using var request = new HttpRequestMessage(get ? HttpMethod.Get : HttpMethod.Post, "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/cursor-model/owned")
            { Content = get ? null : new ByteArrayContent(Encoding.UTF8.GetBytes(input)) };
        using var response = await client.SendAsync(request, deadline.Token);
        ((int)response.StatusCode).ShouldBe(status); (await response.Content.ReadAsStringAsync(deadline.Token)).ShouldBe(output);
        var exchange = runtime.HttpExchanges.ShouldHaveSingleItem();
        exchange.RequestSha256.ShouldBe(await server); exchange.ResponseSha256.ShouldBe(QualificationRuntime.Hash(Encoding.UTF8.GetBytes(output)));
        exchange.Method.ShouldBe(get ? "GET" : "POST"); exchange.Status.ShouldBe(status); exchange.Ended.ShouldBeGreaterThanOrEqualTo(exchange.Started);
        JsonSerializer.Serialize(exchange).ShouldNotContain(sentinel);
    }
}
