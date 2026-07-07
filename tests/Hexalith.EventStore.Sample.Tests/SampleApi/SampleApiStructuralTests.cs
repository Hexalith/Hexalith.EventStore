using System.Reflection;
using System.Xml.Linq;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.EventStore.Sample.Api.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.SampleApi;

public sealed class SampleApiStructuralTests
{
    [Fact]
    public void SampleApiProject_UsesContractsClientServiceDefaultsAndGeneratorAnalyzerOnly()
    {
        XDocument project = XDocument.Load(SampleApiProjectPath());
        XElement[] projectReferences = project
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.Ordinal))
            .ToArray();

        ProjectReferenceFileNames(projectReferences).ShouldBe([
            "Hexalith.EventStore.Sample.Contracts.csproj",
            "Hexalith.EventStore.Client.csproj",
            "Hexalith.EventStore.RestApi.Generators.csproj",
            "Hexalith.EventStore.ServiceDefaults.csproj",
        ], ignoreOrder: true);

        XElement generatorReference = projectReferences.Single(static reference =>
            ((string?)reference.Attribute("Include"))?.Replace('\\', '/').EndsWith(
                "src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj",
                StringComparison.Ordinal) == true);
        ((string?)generatorReference.Attribute("OutputItemType")).ShouldBe("Analyzer");
        ((string?)generatorReference.Attribute("ReferenceOutputAssembly")).ShouldBe("false");

        ProjectReferenceFileNames(projectReferences).ShouldNotContain("Hexalith.EventStore.Sample.csproj");
        ProjectReferenceFileNames(projectReferences).ShouldNotContain("Hexalith.EventStore.Sample.BlazorUI.csproj");
    }

    [Fact]
    public void SampleApiAssembly_OptsIntoCounterRestApiScope()
    {
        RestApiAttribute attribute = typeof(DaprAppIdHandler).Assembly
            .GetCustomAttributes<RestApiAttribute>()
            .Single();

        attribute.RoutePrefix.ShouldBe("api/{tenant}/counter");
        attribute.Tag.ShouldBe("counter");
        attribute.TenantSource.ShouldBe(RestTenantSource.Route);
    }

    [Fact]
    public void SampleApiSource_HasNoRazorOrUiConcerns()
    {
        string sampleApiRoot = SampleApiRoot();

        Directory.EnumerateFiles(sampleApiRoot, "*.razor", SearchOption.AllDirectories)
            .Where(static file => !IsBuildArtifact(file))
            .ShouldBeEmpty("The external API host must not contain Razor UI components.");

        string projectText = File.ReadAllText(SampleApiProjectPath());
        projectText.ShouldNotContain("Microsoft.FluentUI.AspNetCore.Components");
        projectText.ShouldNotContain("Microsoft.AspNetCore.Components.Web");
        projectText.ShouldNotContain("Hexalith.EventStore.SignalR");

        string programText = File.ReadAllText(Path.Combine(sampleApiRoot, "Program.cs"));
        programText.ShouldContain("builder.Services.AddHttpContextAccessor();");
        programText.ShouldContain("builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)");
        programText.ShouldContain("builder.Services.AddAuthorization();");
        programText.ShouldContain(".AddHttpMessageHandler<InboundBearerForwardingHandler>()");
        programText.ShouldContain(".AddHttpMessageHandler(() => new DaprAppIdHandler(\"eventstore\", daprApiToken))");
    }

    [Fact]
    public void CounterRestController_IsOnlyGeneratedControllerAndUsesGatewayBoundary()
    {
        Type[] generatedControllers = typeof(DaprAppIdHandler).Assembly
            .GetTypes()
            .Where(static type => string.Equals(type.Namespace, "Hexalith.EventStore.Sample.Api.Generated", StringComparison.Ordinal)
                && typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();
        generatedControllers.Select(static type => type.Name).ShouldBe(["CounterRestController"]);

        Type controller = typeof(DaprAppIdHandler).Assembly.GetType(
            "Hexalith.EventStore.Sample.Api.Generated.CounterRestController",
            throwOnError: true)!;

        controller.GetCustomAttribute<ApiControllerAttribute>().ShouldNotBeNull();
        controller.GetCustomAttribute<AuthorizeAttribute>().ShouldNotBeNull();
        controller.GetCustomAttribute<RouteAttribute>().ShouldNotBeNull().Template.ShouldBe("api/{tenant}/counter");

        ConstructorInfo constructor = controller.GetConstructors().Single();
        ParameterInfo parameter = constructor.GetParameters().Single();
        parameter.ParameterType.ShouldBe(typeof(IEventStoreGatewayClient));

        AssertQueryAction(controller);
        AssertCommandAction(controller, "IncrementCounterCommandAsync", "{counterId}/increment");
        AssertCommandAction(controller, "DecrementCounterCommandAsync", "{counterId}/decrement");
        AssertCommandAction(controller, "ResetCounterCommandAsync", "{counterId}/reset");
        AssertCommandAction(controller, "CloseCounterCommandAsync", "{counterId}/close");
    }

    private static void AssertCommandAction(Type controller, string methodName, string routeTemplate)
    {
        MethodInfo command = controller.GetMethod(methodName)
            ?? throw new MissingMethodException(controller.FullName, methodName);
        command.GetCustomAttribute<HttpPostAttribute>().ShouldNotBeNull().Template.ShouldBe(routeTemplate);
        command.GetCustomAttribute<AuthorizeAttribute>().ShouldBeNull("Authorization is applied at generated controller level.");

        ParameterInfo[] parameters = command.GetParameters();
        parameters.Any(static parameter => HasFromRouteName(parameter, "tenant")).ShouldBeTrue();
        parameters.Any(static parameter => HasFromRouteName(parameter, "counterId")).ShouldBeTrue();
        parameters.Any(static parameter => parameter.GetCustomAttribute<FromBodyAttribute>() is not null).ShouldBeTrue();
    }

    private static void AssertQueryAction(Type controller)
    {
        MethodInfo query = controller.GetMethod("GetCounterStatusQueryQueryAsync")
            ?? throw new MissingMethodException(controller.FullName, "GetCounterStatusQueryQueryAsync");
        query.GetCustomAttribute<HttpGetAttribute>().ShouldNotBeNull().Template.ShouldBe("{entityId}");
        query.GetCustomAttribute<AuthorizeAttribute>().ShouldBeNull("Authorization is applied at generated controller level.");

        ParameterInfo[] parameters = query.GetParameters();
        parameters.Any(static parameter => HasFromRouteName(parameter, "tenant")).ShouldBeTrue();
        parameters.Any(static parameter => HasFromRouteName(parameter, "entityId")).ShouldBeTrue();
        parameters.Any(static parameter => HasFromHeaderName(parameter, "If-None-Match")).ShouldBeTrue();
    }

    private static bool HasFromHeaderName(ParameterInfo parameter, string name)
        => string.Equals(parameter.GetCustomAttribute<FromHeaderAttribute>()?.Name, name, StringComparison.Ordinal);

    private static bool HasFromRouteName(ParameterInfo parameter, string name)
        => string.Equals(parameter.GetCustomAttribute<FromRouteAttribute>()?.Name, name, StringComparison.Ordinal);

    private static bool IsBuildArtifact(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string[] ProjectReferenceFileNames(IEnumerable<XElement> references)
        => references
            .Select(static reference => Path.GetFileName(((string?)reference.Attribute("Include"))?.Replace('\\', '/') ?? string.Empty))
            .ToArray();

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.EventStore.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hexalith.EventStore.slnx from the test output path.");
    }

    private static string SampleApiProjectPath()
        => Path.Combine(SampleApiRoot(), "Hexalith.EventStore.Sample.Api.csproj");

    private static string SampleApiRoot()
        => Path.Combine(RepositoryRoot(), "samples", "Hexalith.EventStore.Sample.Api");
}
