using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Verifies the private client accepts its owned certificate and refuses another run's certificate.</summary>
public sealed class PrivateAuthenticationCertificateTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Client_ValidatesTheExactRunCertificate(bool sameRun)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("authentication-tls-");
        try
        {
            using var expected = new PrivateAuthenticationCertificate(directory.FullName);
            DirectoryInfo otherDirectory = directory.CreateSubdirectory("other");
            using var other = new PrivateAuthenticationCertificate(otherDirectory.FullName);
            using X509Certificate2 serverCertificate = X509CertificateLoader.LoadPkcs12FromFile(
                sameRun ? expected.Path : other.Path, sameRun ? expected.Password : other.Password);
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Task<bool> server = ServeAsync(listener, serverCertificate, timeout.Token);
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using HttpClient client = expected.CreateClient(new Uri($"https://localhost:{port}"));
            if (sameRun)
                (await client.GetAsync("/", timeout.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
            else
                await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("/", timeout.Token));
            (await server).ShouldBe(sameRun);
        }
        finally { directory.Delete(recursive: true); }
    }

    private static async Task<bool> ServeAsync(TcpListener listener, X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        using TcpClient connection = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = new SslStream(connection.GetStream());
        try
        {
            await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, cancellationToken);
            var buffer = new byte[2048];
            _ = await stream.ReadAsync(buffer, cancellationToken);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"), cancellationToken);
            return true;
        }
        catch (AuthenticationException) { return false; }
        catch (IOException) { return false; }
    }
}
