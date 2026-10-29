using System.Reflection;
using System.Security.Claims;

using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogPublicConstructionTests
{
    public static TheoryData<Type> EntryPoints => new()
    {
        typeof(CommandRouter), typeof(SubmitCommandHandler), typeof(IdempotencyAdmissionCoordinator),
        typeof(IdempotencyIntentAdapterRegistry), typeof(ProjectionUpdateOrchestrator), typeof(NamedProjectionDispatchCoordinator),
        typeof(DomainServiceResolver), typeof(DaprDomainServiceInvoker), typeof(QueriesController),
        typeof(HandlerAwareQueryRouter), typeof(DaprDomainQueryInvoker), typeof(DaprDomainQueryHandlerRegistry),
    };

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void EverySupportedConstructorAndOrdinaryTypeRegistrationRequiresNonnullAuthority(Type entryPoint)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        ConstructorInfo constructor = entryPoint.GetConstructors().ShouldHaveSingleItem();
        ParameterInfo authority = constructor.GetParameters().Single(p => p.ParameterType == typeof(IRouteCatalogAuthority));
        authority.IsOptional.ShouldBeFalse();
        object?[] arguments = constructor.GetParameters().Select(p => p == authority ? null : Dependency(p)).ToArray();
        Should.Throw<TargetInvocationException>(() => constructor.Invoke(arguments)).InnerException.ShouldBeOfType<ArgumentNullException>();
        var services = new ServiceCollection();
        foreach (var parameter in constructor.GetParameters().Where(p => p != authority))
            if (Dependency(parameter) is { } dependency) services.AddSingleton(parameter.ParameterType, dependency);
        services.AddTransient(entryPoint);
        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService(entryPoint));
    }

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public async Task PublicDirectAndTypeBasedEntryPointsRefuseUndeclaredWorkBeforeDownstreamEffects(Type entryPoint)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        foreach (bool typeBased in new[] { false, true })
        {
            ConstructorInfo constructor = entryPoint.GetConstructors().Single();
            var services = new ServiceCollection(); var effects = new List<object>();
            foreach (var parameter in constructor.GetParameters())
            {
                object? dependency = parameter.ParameterType == typeof(IRouteCatalogAuthority) ? fixture.Authorities[0] : Dependency(parameter);
                if (dependency is null) continue;
                services.AddSingleton(parameter.ParameterType, dependency);
                if (parameter.ParameterType.IsInterface && dependency is not IRouteCatalogAuthority
                    && dependency is not ITenantValidator && dependency is not IRbacValidator
                    && !parameter.ParameterType.IsGenericType) effects.Add(dependency);
            }
            services.AddTransient(entryPoint); using var provider = services.BuildServiceProvider();
            object instance = typeBased ? provider.GetRequiredService(entryPoint) : constructor.Invoke(constructor.GetParameters()
                .Select(p => provider.GetService(p.ParameterType) ?? (p.HasDefaultValue ? p.DefaultValue : null)).ToArray());
            await Should.ThrowAsync<InvalidOperationException>(() => InvokeUndeclaredAsync(instance));
            foreach (object dependency in effects) dependency.ReceivedCalls().ShouldBeEmpty();
            (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        }
    }

    private static object? Dependency(ParameterInfo parameter)
    {
        Type type = parameter.ParameterType;
        if (type == typeof(TimeProvider)) return TimeProvider.System;
        if (type == typeof(CanonicalIdempotencyIntentEncoder)) return new CanonicalIdempotencyIntentEncoder();
        if (type == typeof(IdempotencyKeyProtector)) return new IdempotencyKeyProtector(Substitute.For<IIdempotencyDigestKeyProvider>());
        if (type == typeof(IEnumerable<IIdempotencyIntentAdapter>)) return SampleCatalog.CreateAdapters().Cast<IIdempotencyIntentAdapter>().ToArray();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) return Array.CreateInstance(type.GetGenericArguments()[0], 0);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ILogger<>))
            return typeof(NullLogger<>).MakeGenericType(type.GetGenericArguments()).GetField("Instance")!.GetValue(null);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IOptions<>))
            return Activator.CreateInstance(typeof(OptionsWrapper<>).MakeGenericType(type.GetGenericArguments()), Activator.CreateInstance(type.GetGenericArguments()[0]));
        if (parameter.HasDefaultValue) return parameter.DefaultValue;
        if (type == typeof(ITenantValidator))
        {
            var validator = Substitute.For<ITenantValidator>();
            validator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>()).Returns(TenantValidationResult.Allowed);
            return validator;
        }
        if (type == typeof(IRbacValidator))
        {
            var validator = Substitute.For<IRbacValidator>();
            validator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>()).Returns(RbacValidationResult.Allowed);
            return validator;
        }
        return Substitute.For([type], []);
    }

    private static async Task InvokeUndeclaredAsync(object instance)
    {
        var command = new SubmitCommand("01M61800000000000000000001", "tenant-a", "counter", "aggregate", "undeclared", "{}"u8.ToArray(), "correlation", "user");
        switch (instance)
        {
            case CommandRouter router: await router.RouteCommandAsync(command); break;
            case SubmitCommandHandler handler: await handler.Handle(command, default); break;
            case IdempotencyAdmissionCoordinator admission: await admission.AdmitAsync(command); break;
            case IdempotencyIntentAdapterRegistry adapters: await adapters.ResolveAsync(command); break;
            case ProjectionUpdateOrchestrator projection: await projection.DeliverProjectionAsync(new("tenant-a", "undeclared", "aggregate")); break;
            case NamedProjectionDispatchCoordinator named: await named.SupportsMappedOnlyStreamedRebuildAsync(new("tenant-a", "undeclared", "aggregate"), new("foreign", "project/v2", "tenant-a", "undeclared", "v1"), "foreign", default); break;
            case DomainServiceResolver resolver: await resolver.ResolveAsync("tenant-a", "undeclared", "v1"); break;
            case DaprDomainServiceInvoker invoker: await invoker.InvokeAsync(command.ToCommandEnvelope(), null); break;
            case HandlerAwareQueryRouter router: await router.RouteQueryAsync(new("tenant-a", "counter", "aggregate", "undeclared", [], "correlation", "user")); break;
            case DaprDomainQueryInvoker invoker: await invoker.InvokeAsync(new("tenant-a", "counter", "aggregate", "undeclared", [], "correlation", "user")); break;
            case DaprDomainQueryHandlerRegistry registry: await registry.SupportsQueryAsync("counter", "undeclared"); break;
            case QueriesController controller:
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user")], "test")) } };
                await controller.Submit(new("tenant-a", "counter", "aggregate", "undeclared", null), null, default); break;
            default: throw new ArgumentException("An entry point needs its concrete zero-effect invocation.");
        }
    }
}
