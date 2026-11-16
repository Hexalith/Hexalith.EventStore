using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Issues a run-private localhost certificate and retains normal TLS verification.</summary>
internal sealed class PrivateAuthenticationCertificate : IDisposable
{
    private readonly X509Certificate2 _certificate;
    private readonly string _password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    /// <summary>Creates one private server certificate beneath the invocation's scratch directory.</summary>
    internal PrivateAuthenticationCertificate(string directory)
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        names.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
        _certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        Path = System.IO.Path.Combine(directory, "server.pfx");
        File.WriteAllBytes(Path, _certificate.Export(X509ContentType.Pfx, _password));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>Gets the private PFX path.</summary>
    internal string Path { get; }

    /// <summary>Gets its runtime-generated encryption password.</summary>
    internal string Password => _password;

    /// <summary>Creates a client that trusts only this run's certificate, retaining chain and hostname checks.</summary>
    internal HttpClient CreateClient(Uri endpoint)
    {
        var chain = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust };
        chain.CustomTrustStore.Add(_certificate);
        var handler = new SocketsHttpHandler { SslOptions = new SslClientAuthenticationOptions { CertificateChainPolicy = chain } };
        return new HttpClient(handler) { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(15) };
    }

    /// <inheritdoc/>
    public void Dispose() => _certificate.Dispose();
}
