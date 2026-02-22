extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

using commandapi::Hexalith.EventStore.CommandApi.ErrorHandling;
using commandapi::Microsoft.Extensions.DependencyInjection;

using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

public class ServiceCollectionExtensionsTests {
    [Fact]
    public void AddCommandApi_RegistersExceptionHandlers_InCorrectOrder() {
        // Arrange
        var services = new ServiceCollection();

        // Add minimal required services for AddCommandApi
        services.AddLogging();
        services.AddSingleton<ICommandStatusStore>(new InMemoryCommandStatusStore());
        services.AddSingleton<ICommandArchiveStore>(new InMemoryCommandArchiveStore());

        // Act
        services.AddCommandApi();

        // Assert - verify handler registration order
        ServiceDescriptor[] handlers = services
            .Where(d => d.ServiceType == typeof(IExceptionHandler))
            .ToArray();

        handlers.Length.ShouldBeGreaterThan(0);

        // Expected order: ValidationExceptionHandler -> AuthorizationExceptionHandler -> ConcurrencyConflictExceptionHandler -> GlobalExceptionHandler
        int validationIndex = Array.FindIndex(handlers, d => d.ImplementationType == typeof(ValidationExceptionHandler));
        int authorizationIndex = Array.FindIndex(handlers, d => d.ImplementationType == typeof(AuthorizationExceptionHandler));
        int concurrencyIndex = Array.FindIndex(handlers, d => d.ImplementationType == typeof(ConcurrencyConflictExceptionHandler));
        int globalIndex = Array.FindIndex(handlers, d => d.ImplementationType == typeof(GlobalExceptionHandler));

        validationIndex.ShouldBeGreaterThanOrEqualTo(0);
        authorizationIndex.ShouldBeGreaterThanOrEqualTo(0);
        concurrencyIndex.ShouldBeGreaterThanOrEqualTo(0);
        globalIndex.ShouldBeGreaterThanOrEqualTo(0);

        // Verify order: Validation < Authorization < ConcurrencyConflict < Global
        validationIndex.ShouldBeLessThan(authorizationIndex, "ValidationExceptionHandler should be before AuthorizationExceptionHandler");
        authorizationIndex.ShouldBeLessThan(concurrencyIndex, "AuthorizationExceptionHandler should be before ConcurrencyConflictExceptionHandler");
        concurrencyIndex.ShouldBeLessThan(globalIndex, "ConcurrencyConflictExceptionHandler should be before GlobalExceptionHandler");
    }
}
