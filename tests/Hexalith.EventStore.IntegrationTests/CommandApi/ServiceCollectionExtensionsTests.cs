extern alias commandapi;

using commandapi::Hexalith.EventStore.CommandApi.ErrorHandling;

using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

public class ServiceCollectionExtensionsTests {
    [Fact]
    public void AddCommandApi_RegistersExceptionHandlers_InCorrectOrder() {
        // Arrange
        var services = new ServiceCollection();

        // Add minimal required services for AddCommandApi
        _ = services.AddLogging();
        _ = services.AddSingleton<ICommandStatusStore>(new InMemoryCommandStatusStore());
        _ = services.AddSingleton<ICommandArchiveStore>(new InMemoryCommandArchiveStore());

        // Act
        _ = services.AddCommandApi();

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
