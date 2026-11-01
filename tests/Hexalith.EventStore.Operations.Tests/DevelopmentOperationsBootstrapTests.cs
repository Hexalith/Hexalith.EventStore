using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>Runs the real Operations bootstrap and its actual endpoint capture for each explicit receiver profile.</summary>
public sealed class DevelopmentOperationsBootstrapTests
{
    /// <summary>The supported finite profile observes actual endpoints; the preexisting MVC-only composite refuses Operations.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualBootstrapKeepsTheSupportedFiniteProfileAndRefusesMvcOnlyComposite(bool composite)
    {
        string directory = Path.Combine(Path.GetTempPath(), "operations-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            string component = Path.Combine(directory, "component.json"), acl = Path.Combine(directory, "acl.json");
            await File.WriteAllTextAsync(component, """{"metadata":{"name":"route-catalog"}}""", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(acl, """{"spec":{"accessControl":{"defaultAction":"deny","policies":[]}}}""", TestContext.Current.CancellationToken);
            var configuration = new Dictionary<string, string?>
                {
                    ["EventStore:DomainService:AppId"] = "operations-test",
                    ["DAPR_APP_ID"] = "operations-test",
                    ["DAPR_HTTP_ENDPOINT"] = "http://127.0.0.1:1",
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory,
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:Role"] = "operations-receiver",
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:HostId"] = "operations",
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:CoordinatorHostId"] = "eventstore",
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:AttemptId"] = "operations-observation",
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:RequiredHostIds:0"] = "eventstore",
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:RequiredHostIds:1"] = "operations",
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:SelectedAppIds:0"] = "eventstore-test",
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:SelectedAppIds:1"] = "operations-test",
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:ComponentPath"] = component,
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = acl,
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:DeclaredInvocations"] = "[]",
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:ReceiverProfile"] = composite ? RouteCatalogTechnicalHttpPolicy.FenceVersion : null,
                    ["EventStore:RouteCatalog:DevelopmentBootstrap:BindingVersion"] = composite ? "2" : null,
                };
            if (!composite)
            {
                configuration.Remove("EventStore:RouteCatalog:DevelopmentBootstrap:ReceiverProfile");
                configuration.Remove("EventStore:RouteCatalog:DevelopmentBootstrap:BindingVersion");
            }
            await using var factory = new OperationsBootstrapFactory(configuration);
            using var client = factory.CreateClient();
            string path = Path.Combine(directory, "operations.observation.json");
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            bound.CancelAfter(TimeSpan.FromSeconds(10));
            if (composite)
            {
                var fence = factory.Services.GetRequiredService<FiniteRouteCatalogReceiver>();
                var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = factory.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() => stopping.TrySetResult());
                await stopping.Task.WaitAsync(bound.Token);
                var refusal = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fence.WaitForCaptureAsync(bound.Token));
                refusal.Message.ShouldBe("No installed SDK technical MVC action qualifies this receiver.");
                File.Exists(path).ShouldBeFalse();
                File.Exists(Path.Combine(directory, "activation.json")).ShouldBeFalse();
                File.Exists(Path.Combine(directory, "operations.committed.json")).ShouldBeFalse();
                return;
            }
            while (!File.Exists(path)) await Task.Delay(25, bound.Token);
            var actual = JsonSerializer.Deserialize<RouteCatalogHostObservation>(await File.ReadAllBytesAsync(path, bound.Token)).ShouldNotBeNull();
            actual.HostId.ShouldBe("operations");
            actual.AppId.ShouldBe("operations-test");
            actual.ReceiverSurface.ShouldNotBeNull().FenceVersion.ShouldBe(composite
                ? RouteCatalogTechnicalHttpPolicy.FenceVersion : FiniteRouteCatalogReceiver.TechnicalVersion);
            var endpoints = actual.ReceiverSurface.Endpoints.Where(endpoint => endpoint.Http is not null).ToArray();
            endpoints.Length.ShouldBe(5);
            endpoints.ShouldAllBe(endpoint => endpoint.Http!.Owner == "Hexalith.EventStore.Operations.Endpoints.DeadLetterOperationsEndpointExtensions"
                && endpoint.Policies.Contains("EventStoreHuman") && !endpoint.Anonymous);
            actual.RequiredHostIds.ShouldBe(["eventstore", "operations"]);
            File.Exists(Path.Combine(directory, "activation.json")).ShouldBeFalse();
            File.Exists(Path.Combine(directory, "operations.committed.json")).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
