using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

public sealed partial class SecretsProtectionTests
{
    /// <summary>Empty arbitrary calls do not establish a runtime credential source.</summary>
    [Theory]
    [InlineData(".cs", "Resolve()")]
    [InlineData(".razor", "Resolve()")]
    [InlineData(".cs", "call.Arg()")]
    [InlineData(".razor", "call.Arg()")]
    [InlineData(".cs", "call.Arg<Other.CancellationToken>()")]
    [InlineData(".razor", "call.Arg<Other.CancellationToken>()")]
    [InlineData(".cs", "call.Arg<string>()")]
    [InlineData(".razor", "call.Arg<string>()")]
    public void EmptyCallRecognition_RejectsUnrecognizedSources(string extension, string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        string path = "tests/empty-call" + extension;
        FindViolations(path, Assignment("to" + "ken", expression)).ShouldBe([path + ":1"]);
    }

    /// <summary>Resource selection remains exact and does not conceal literal material.</summary>
    [Theory]
    [InlineData(".cs")]
    [InlineData(".razor")]
    public void ParameterResourceSelection_RequiresExactLiteralFreeExpression(string extension)
    {
        string path = "tests/resource-selection" + extension;
        string expression = "builder.Resources.OfType<ParameterResource>()";
        string name = "to" + "ken";
        FindViolations(path, Assignment(name, expression)).ShouldBeEmpty();
        foreach (string rejected in new[]
        {
            "builder.Other.OfType<ParameterResource>()",
            "builder.Resources.OfType<Other.ParameterResource>()",
            "builder.Resources.OfType<string>()",
            "builder.Resources.OfType<ParameterResource>(\"" + RandomSecret() + "\")",
            expression + " + \"" + RandomSecret() + "\"",
        })
        {
            FindViolations(path, Assignment(name, rejected)).ShouldBe([path + ":1"]);
        }

        FindViolations("capture.log", Assignment(name, expression)).ShouldBe(["capture.log:1"]);
        FindViolations(path, Assignment(name, expression) + "\n" + Assignment("pass" + "word", "\"" + RandomSecret() + "\""))
            .ShouldBe([path + ":2"]);
    }
}
