using System.Reflection;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// Invokes a .NET validator type by reflection. The type must declare a
/// public static method:
/// <code>
/// public static Dw4ValidationOutcome Validate(IEnumerable&lt;string&gt; fixturePaths)
/// </code>
/// (signature anchored on the test project's <see cref="Dw4ValidationOutcome"/>
/// record so the contract is visible from one place).
///
/// In the red phase, no such type exists. <see cref="Validate"/> will throw
/// <see cref="Dw4ValidatorNotConfiguredException"/>; tests are
/// <c>[Fact(Skip = ...)]</c> so this surfaces only when dev removes Skip
/// before wiring the validator.
/// </summary>
internal sealed class InProcessValidatorInvoker : IDw4ValidatorInvoker {
    private readonly string _typeQualifiedName;

    public InProcessValidatorInvoker(string typeQualifiedName) {
        _typeQualifiedName = typeQualifiedName ?? throw new ArgumentNullException(nameof(typeQualifiedName));
    }

    public string EntrypointDescription => $"dotnet:{_typeQualifiedName}";

    public Dw4ValidationOutcome Validate(IEnumerable<string> fixturePaths) {
        Type? validatorType = Type.GetType(_typeQualifiedName, throwOnError: false);
        if (validatorType is null) {
            throw new Dw4ValidatorNotConfiguredException(
                $"In-process validator type '{_typeQualifiedName}' not found. " +
                "Add a project reference from the test project to the validator " +
                "implementation, or fix the fully-qualified type name in entrypoint.txt.");
        }

        MethodInfo? method = validatorType.GetMethod(
            "Validate",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(IEnumerable<string>)],
            modifiers: null);
        if (method is null) {
            throw new Dw4ValidatorNotConfiguredException(
                $"Validator type '{_typeQualifiedName}' must declare " +
                "'public static Dw4ValidationOutcome Validate(IEnumerable<string> fixturePaths)'.");
        }

        object? result = method.Invoke(obj: null, parameters: [fixturePaths]);
        if (result is Dw4ValidationOutcome outcome) {
            return outcome;
        }

        throw new Dw4ValidatorNotConfiguredException(
            $"Validator '{_typeQualifiedName}.Validate' must return Dw4ValidationOutcome.");
    }
}
