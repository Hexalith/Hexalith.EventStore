using System.Security.Cryptography;

using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public sealed partial class DomainServiceWorkloadAssertionHandlerTests
{
    [Theory]
    [InlineData("limit", false)]
    [InlineData("oversized", false)]
    [InlineData("encoded-control", false)]
    [InlineData("encoded-null", false)]
    [InlineData("unicode-control", false)]
    [InlineData("oversized", true)]
    [InlineData("encoded-control", true)]
    public async Task AssertionUnavailableDiagnosticBoundsAudienceWithoutChangingTargetOrIssuance(string shape, bool issued)
    {
        string audience = shape switch
        {
            "limit" => new string('x', 128),
            "oversized" => new string('x', 4096),
            "encoded-control" => "sample\r\nforged-record",
            "encoded-null" => "sample\0suffix",
            _ => "sample\u0085suffix",
        };
        string credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        string assertion = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<string?>(issued ? assertion : null));
        using var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var logger = factory.CreateLogger<DomainServiceWorkloadAssertionHandler>();
        using var provider = new ServiceCollection().AddSingleton(issuer).AddSingleton(logger).BuildServiceProvider();
        var capture = new CapturingHandler();
        using var invoker = new HttpMessageInvoker(new DomainServiceWorkloadAssertionHandler(provider) { InnerHandler = capture });
        var uri = new Uri("http://127.0.0.1:3501/v1.0/invoke/" + Uri.EscapeDataString(audience) + "/method/process");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("Authorization", "Bearer " + credential);
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, credential);
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);
        request.RequestUri.ShouldBe(uri);
        capture.Authorization.ShouldBeNull();
        capture.Assertions.ShouldBe(issued ? [assertion] : []);
        await issuer.Received(1).IssueAsync(Arg.Is<WorkloadAssertionRequest>(value => value.Audience == audience
            && value.Operation == EventStoreWorkloadOperations.DomainServiceProcess), Arg.Any<CancellationToken>());
        if (issued) logs.Messages.ShouldBeEmpty();
        else
        {
            string message = logs.Messages.ShouldHaveSingleItem();
            message.ShouldContain("Audience=" + (shape == "limit" ? audience : "<invalid-audience>"));
            message.Length.ShouldBeLessThan(384);
            message.Any(char.IsControl).ShouldBeFalse();
            message.ShouldNotContain(credential);
            message.ShouldNotContain(assertion);
        }
    }

    [Fact]
    public async Task AssertionUnavailableDiagnosticNeverLeaksIssuerResolutionExceptionOrForwardedCredentials()
    {
        string privateDetail = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        string credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        using var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var logger = factory.CreateLogger<DomainServiceWorkloadAssertionHandler>();
        var services = new ServiceCollection();
        services.AddSingleton(logger);
        services.AddSingleton<IWorkloadAssertionIssuer>(_ => throw new InvalidOperationException(privateDetail));
        using var provider = services.BuildServiceProvider();
        var capture = new CapturingHandler();
        using var invoker = new HttpMessageInvoker(new DomainServiceWorkloadAssertionHandler(provider) { InnerHandler = capture });
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:3501/v1.0/invoke/sample/method/process");
        request.Headers.Add("Authorization", "Bearer " + credential);
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, credential);
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);
        capture.Authorization.ShouldBeNull(); capture.Assertions.ShouldBeEmpty();
        string message = logs.Messages.ShouldHaveSingleItem();
        message.ShouldContain("Audience=sample");
        message.ShouldNotContain(privateDetail);
        message.ShouldNotContain(credential);
        message.ShouldNotContain(nameof(InvalidOperationException));
    }
}
