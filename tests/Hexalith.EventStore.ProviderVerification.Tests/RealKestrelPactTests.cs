using System.Text;

using Shouldly;

namespace Hexalith.EventStore.ProviderVerification.Tests;

public sealed class RealKestrelPactTests
{
    [Fact]
    public async Task ProductionPipeline_MinimalMatchingPact_StopsAndClosesPort()
    {
        const string providerState = "command-unauthorized";
        const string description = "minimal command unauthorized contract";
        string directory = Path.Combine(Path.GetTempPath(), $"eventstore-kestrel-pact-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string pactPath = Path.Combine(directory, "minimal-pact.json");
        byte[] pactBytes = Encoding.UTF8.GetBytes(
            $$"""
            {
              "consumer": { "name": "Hexalith.FrontComposer.Shell" },
              "provider": { "name": "Hexalith.EventStore" },
              "interactions": [
                {
                  "type": "Synchronous/HTTP",
                  "description": "{{description}}",
                  "providerStates": [ { "name": "{{providerState}}" } ],
                  "request": {
                    "method": "POST",
                    "path": "/api/v1/commands",
                    "headers": {
                      "Authorization": "Bearer FC_CONTRACT_TOKEN",
                      "Content-Type": "application/json"
                    },
                    "body": {
                      "messageId": "01HXCNTRCT0000000000000000",
                      "tenant": "tenant-contract-a",
                      "domain": "orders",
                      "aggregateId": "order-1",
                      "commandType": "Hexalith.FrontComposer.Shell.Tests.Pact.EventStorePactContractTests+ShipOrderCommand",
                      "payload": {
                        "tenantId": "tenant-contract-a",
                        "aggregateId": "order-1",
                        "quantity": 3
                      },
                      "correlationId": null,
                      "extensions": null
                    }
                  },
                  "response": { "status": 401, "headers": {} }
                }
              ],
              "metadata": { "pactSpecification": { "version": "4.0" } }
            }
            """);
        await File.WriteAllBytesAsync(pactPath, pactBytes, TestContext.Current.CancellationToken);
        ProviderVerificationHost? host = null;
        try
        {
            var coordinator = new ProviderStateCoordinator(
                new HashSet<string>([providerState], StringComparer.Ordinal));
            var timeline = new ProviderVerificationTimeline();
            timeline.BeginStartup();
            host = await ProviderVerificationHost.StartAsync(
                coordinator,
                FindRepositoryRoot(),
                TimeSpan.FromSeconds(15),
                TestContext.Current.CancellationToken,
                timeline);
            Uri address = host.BaseAddress;
            var interaction = new InteractionDefinition(
                description,
                providerState,
                "POST",
                "/api/v1/commands",
                Path.GetFileName(pactPath),
                VerificationInputLoader.ComputeSha256(pactBytes));

            InteractionVerificationResult result = await PactInteractionVerifier.VerifyAsync(
                1,
                interaction,
                directory,
                address,
                coordinator,
                TimeSpan.FromSeconds(15));

            await host.StopAsync(TimeSpan.FromSeconds(10));
            await host.DisposeAsync();
            host = null;

            result.ResultCode.ShouldBe("interaction.passed");
            VerificationCompleteness.IsComplete(1, [result]).ShouldBeTrue();
            (await ProviderVerificationHost.IsPortClosedAsync(address, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        }
        finally
        {
            if (host is not null)
            {
                try
                {
                    await host.StopAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception)
                {
                    // The primary test result retains the failure; disposal is still attempted.
                }

                await host.DisposeAsync();
            }

            Directory.Delete(directory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("EventStore repository root was not found.");
    }
}
