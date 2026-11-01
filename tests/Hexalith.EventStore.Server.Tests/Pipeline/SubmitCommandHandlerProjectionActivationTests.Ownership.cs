using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Configuration;
using System.Reflection.Emit;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline;

public sealed partial class SubmitCommandHandlerProjectionActivationTests
{
    [Theory]
    [InlineData("opt-out")]
    [InlineData("unstarted")]
    [InlineData("missing-hosted")]
    [InlineData("stopped")]
    public async Task ConstructedButUnownedOrUnqualifiedWorkerRetainsAwaitedReplay(string state)
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var outbox = new DaprProjectionActivationOutbox(backend, Options.Create(new ProjectionOptions()), TimeProvider.System);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replay = Substitute.For<IProjectionUpdateOrchestrator>();
        replay.UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), CancellationToken.None).Returns(_ =>
        { entered.TrySetResult(); return resume.Task; });
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<Server.Pipeline.Commands.SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(true, EventCount: 1));
        int releases = 0;
        using ServiceProvider provider = BuildProvider(backend, outbox, router, replay,
            Authority(() => { releases++; return Task.CompletedTask; }), optIn: state != "opt-out",
            startWorker: state != "unstarted", removeHostedWorker: state == "missing-hosted");
        ProjectionActivationWorkerCapability capability = provider.GetRequiredService<ProjectionActivationWorkerCapability>();
        capability.Worker.OwnsOutbox(outbox).ShouldBeTrue();
        capability.CanDefer(Substitute.For<IProjectionActivationOutbox>()).ShouldBeFalse();
        if (state == "stopped") await capability.Worker.StopAsync(CancellationToken.None);
        capability.CanDefer(outbox).ShouldBeFalse();

        Task<SubmitCommandResult> response = provider.GetRequiredService<SubmitCommandHandler>().Handle(Command(), default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            response.IsCompleted.ShouldBeFalse();
            releases.ShouldBe(0);
            resume.SetResult();
            await response.WaitAsync(TimeSpan.FromSeconds(5));
            releases.ShouldBe(1);
        }
        finally { resume.TrySetResult(); }
    }

    [Fact]
    public void OriginalPublicConstructorSignatureBindsExistingNewobjCaller()
    {
        // A compiled caller binds this exact CLR signature, even when source callers omit
        // the optional arguments. An appended optional argument would fail this lookup/JIT.
        Type[] originalSignature = [typeof(IRouteCatalogAuthority), typeof(ICommandStatusStore), typeof(ICommandArchiveStore),
            typeof(ICommandRouter), typeof(ICommandActivityTracker), typeof(IStreamActivityTracker),
            typeof(IProjectionUpdateOrchestrator), typeof(ILogger<SubmitCommandHandler>), typeof(ICommandCorrelationIndex),
            typeof(IProjectionActivationOutbox), typeof(IIdempotencyAdmissionCoordinator), typeof(IConfiguration)];
        var constructor = typeof(SubmitCommandHandler).GetConstructor(originalSignature).ShouldNotBeNull();
        typeof(SubmitCommandHandler).GetConstructors().ShouldHaveSingleItem();
        constructor.GetParameters().Skip(8).ShouldAllBe(parameter => parameter.IsOptional);
        var caller = new DynamicMethod("ExistingSubmitCaller", typeof(SubmitCommandHandler), [typeof(object[])]);
        ILGenerator il = caller.GetILGenerator();
        for (int index = 0; index < originalSignature.Length; index++)
        {
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4, index); il.Emit(OpCodes.Ldelem_Ref);
            il.Emit(OpCodes.Castclass, originalSignature[index]);
        }
        il.Emit(OpCodes.Newobj, constructor); il.Emit(OpCodes.Ret);
        var bind = caller.CreateDelegate<Func<object?[], SubmitCommandHandler>>();
        bind([Authority(() => Task.CompletedTask), new InMemoryCommandStatusStore(), new InMemoryCommandArchiveStore(),
            Substitute.For<ICommandRouter>(), null, null, Substitute.For<IProjectionUpdateOrchestrator>(),
            NullLogger<SubmitCommandHandler>.Instance, null, null, null, null]).ShouldNotBeNull();
    }
}
