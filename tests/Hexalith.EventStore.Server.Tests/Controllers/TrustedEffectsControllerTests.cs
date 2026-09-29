using System.Security.Claims;

using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>Gateway ingress tests for target-receipted trusted effects.</summary>
public sealed class TrustedEffectsControllerTests
{
    /// <summary>A denied admission discloses nothing and never signs a proof or reaches the target.</summary>
    [Fact]
    public async Task DeniedAdmissionForbidsBeforeProofOrTargetRoute()
    {
        ITrustedEffectAdmissionPolicy admission = Substitute.For<ITrustedEffectAdmissionPolicy>();
        ITrustedEffectGatewayProof proof = Substitute.For<ITrustedEffectGatewayProof>();
        ITrustedEffectRouter router = Substitute.For<ITrustedEffectRouter>();
        _ = admission.AdmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns<TrustedEffectAdmission>(_ => throw new InvalidOperationException("tenant or purpose denied"));
        TrustedEffectsController controller = CreateController(admission, proof, router, workload: "reactor");

        IActionResult result = await controller.SubmitAsync(Request(), CancellationToken.None);

        _ = result.ShouldBeOfType<ForbidResult>();
        _ = await proof.DidNotReceiveWithAnyArgs().SignAsync(default!, default);
        _ = await router.DidNotReceiveWithAnyArgs().RouteAsync(default!, default!, default!, default);
    }

    /// <summary>An expired, badly signed, or wrong-audience delegation is a denial rather than a server fault.</summary>
    [Fact]
    public async Task InvalidDelegationTokenForbidsBeforeProofOrTargetRoute()
    {
        ITrustedEffectAdmissionPolicy admission = Substitute.For<ITrustedEffectAdmissionPolicy>();
        ITrustedEffectGatewayProof proof = Substitute.For<ITrustedEffectGatewayProof>();
        ITrustedEffectRouter router = Substitute.For<ITrustedEffectRouter>();
        _ = admission.AdmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns<TrustedEffectAdmission>(_ => throw new SecurityTokenExpiredException("delegation expired"));
        TrustedEffectsController controller = CreateController(admission, proof, router, workload: "reactor");

        IActionResult result = await controller.SubmitAsync(Request(), CancellationToken.None);

        _ = result.ShouldBeOfType<ForbidResult>();
        _ = await proof.DidNotReceiveWithAnyArgs().SignAsync(default!, default);
        _ = await router.DidNotReceiveWithAnyArgs().RouteAsync(default!, default!, default!, default);
    }

    /// <summary>
    /// A target or lifecycle failure after admission is not converted into a result; it propagates
    /// to the global exception handler and no success is reported.
    /// </summary>
    [Fact]
    public async Task RouteFailurePropagatesWithoutReportingSuccess()
    {
        ITrustedEffectAdmissionPolicy admission = Substitute.For<ITrustedEffectAdmissionPolicy>();
        ITrustedEffectGatewayProof proof = Substitute.For<ITrustedEffectGatewayProof>();
        ITrustedEffectRouter router = Substitute.For<ITrustedEffectRouter>();
        TrustedEffectSubmitRequest request = Request();
        _ = admission.AdmitAsync(request.Submission, Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(call => new TrustedEffectAdmission(request.Submission, call.ArgAt<TrustedEffectContext>(1), "DIGEST"));
        _ = proof.SignAsync(Arg.Any<TrustedEffectAdmission>(), Arg.Any<CancellationToken>()).Returns("gateway-proof");
        _ = router.RouteAsync(request.Submission, Arg.Any<TrustedEffectContext>(), "gateway-proof", Arg.Any<CancellationToken>())
            .Returns<TrustedEffectResult>(_ => throw new InvalidOperationException("Trusted effect target partition is under tenant deletion."));
        TrustedEffectsController controller = CreateController(admission, proof, router, workload: "reactor");

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => controller.SubmitAsync(request, CancellationToken.None));

        _ = await proof.Received(1).SignAsync(Arg.Any<TrustedEffectAdmission>(), Arg.Any<CancellationToken>());
        _ = await router.Received(1).RouteAsync(
            request.Submission, Arg.Any<TrustedEffectContext>(), "gateway-proof", Arg.Any<CancellationToken>());
    }

    /// <summary>A request without an attested caller workload is refused before admission.</summary>
    [Fact]
    public async Task MissingAttestedWorkloadForbidsBeforeAdmission()
    {
        ITrustedEffectAdmissionPolicy admission = Substitute.For<ITrustedEffectAdmissionPolicy>();
        ITrustedEffectGatewayProof proof = Substitute.For<ITrustedEffectGatewayProof>();
        ITrustedEffectRouter router = Substitute.For<ITrustedEffectRouter>();
        TrustedEffectsController controller = CreateController(admission, proof, router, workload: null);

        IActionResult result = await controller.SubmitAsync(Request(), CancellationToken.None);

        _ = result.ShouldBeOfType<ForbidResult>();
        _ = await admission.DidNotReceiveWithAnyArgs().AdmitAsync(default!, default!, default);
        _ = await router.DidNotReceiveWithAnyArgs().RouteAsync(default!, default!, default!, default);
    }

    /// <summary>
    /// The workload comes from authentication, the proof binds the admitted effect, and the target
    /// receipt result is returned without consulting a gateway status record.
    /// </summary>
    [Fact]
    public async Task AuthorizedEffectRoutesTargetReceiptWithAttestedWorkload()
    {
        ITrustedEffectAdmissionPolicy admission = Substitute.For<ITrustedEffectAdmissionPolicy>();
        ITrustedEffectGatewayProof proof = Substitute.For<ITrustedEffectGatewayProof>();
        ITrustedEffectRouter router = Substitute.For<ITrustedEffectRouter>();
        TrustedEffectSubmitRequest request = Request();
        string effectId = EffectIdentityCodec.ComputeEffectId(request.Submission.Identity);
        var expected = new TrustedEffectResult(effectId, TrustedEffectDisposition.Success, Replayed: true, null);
        _ = admission.AdmitAsync(request.Submission, Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
            .Returns(call => new TrustedEffectAdmission(request.Submission, call.ArgAt<TrustedEffectContext>(1), "DIGEST"));
        _ = proof.SignAsync(Arg.Any<TrustedEffectAdmission>(), Arg.Any<CancellationToken>()).Returns("gateway-proof");
        _ = router.RouteAsync(request.Submission, Arg.Any<TrustedEffectContext>(), "gateway-proof", Arg.Any<CancellationToken>())
            .Returns(expected);
        TrustedEffectsController controller = CreateController(admission, proof, router, workload: "reactor");

        IActionResult result = await controller.SubmitAsync(request, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);
        _ = await admission.Received(1).AdmitAsync(
            request.Submission,
            Arg.Is<TrustedEffectContext>(context => context.Workload == "reactor"
                && context.Purpose == request.Purpose
                && context.CausationId == request.CausationId
                && context.DelegationToken == request.DelegationToken),
            Arg.Any<CancellationToken>());
        _ = await proof.Received(1).SignAsync(
            Arg.Is<TrustedEffectAdmission>(signed => signed.Submission == request.Submission
                && signed.Context.Workload == "reactor"),
            Arg.Any<CancellationToken>());
        typeof(TrustedEffectsController).GetConstructors().Single().GetParameters()
            .Select(static parameter => parameter.ParameterType)
            .ShouldBe([typeof(ITrustedEffectAdmissionPolicy), typeof(ITrustedEffectGatewayProof), typeof(ITrustedEffectRouter)]);
    }

    private static TrustedEffectsController CreateController(
        ITrustedEffectAdmissionPolicy admission,
        ITrustedEffectGatewayProof proof,
        ITrustedEffectRouter router,
        string? workload)
    {
        Claim[] claims = workload is null ? [] : [new Claim("dapr_caller_app_id", workload)];
        return new TrustedEffectsController(admission, proof, router)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "DaprInternal")),
                },
            },
        };
    }

    private static TrustedEffectSubmitRequest Request()
    {
        var identity = new EffectIdentity(
            "tenant-a", "works", "source-1", 1,
            EffectKindCatalog.DateResume, "works", "target-1", 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        return new TrustedEffectSubmitRequest(
            new TrustedEffectSubmission(identity, "ResumeWorkItem", [123, 125], messageId, messageId),
            "date-resume",
            "source-event",
            "signed-token");
    }
}
