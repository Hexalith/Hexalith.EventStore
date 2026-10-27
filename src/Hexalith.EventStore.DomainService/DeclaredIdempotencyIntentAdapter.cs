using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Serialization;

namespace Hexalith.EventStore.DomainService;

/// <summary>Shared typed payload binding and canonical encoding for explicit owner semantic policies.</summary>
/// <typeparam name="TCommand">The exact payload type used by the actual handler.</typeparam>
public sealed class DeclaredIdempotencyIntentAdapter<TCommand> : IDeclaredIdempotencyIntentAdapter
{
    private readonly Func<TCommand, IReadOnlyDictionary<string, object?>> _semanticFields;

    /// <summary>Creates an offline adapter; this does not register it or enable admission.</summary>
    public DeclaredIdempotencyIntentAdapter(CommandIntentDeclaration declaration, Func<TCommand, IReadOnlyDictionary<string, object?>> semanticFields)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(semanticFields);
        declaration = declaration with
        {
            Route = declaration.Route with
            {
                FallbackTypes = Array.AsReadOnly(declaration.Route.FallbackTypes.ToArray()),
                Execution = declaration.Route.Execution is { } execution ? execution with
                {
                    Aliases = Array.AsReadOnly(execution.Aliases.ToArray()),
                    ProjectionOutputs = Array.AsReadOnly(execution.ProjectionOutputs.ToArray()),
                } : null,
            },
            DescriptorSchema = declaration.DescriptorSchema with
            {
                Fields = Array.AsReadOnly(declaration.DescriptorSchema.Fields.ToArray()),
            },
        };
        RouteCatalogValidator.ValidateDeclaration(declaration.Route);
        RouteCatalogValidator.ValidateDescriptor(declaration.DescriptorSchema);
        if (declaration.DescriptorSchema.Fields.Count(field => field.Name == "target" && field.Type == "string") != 1)
        {
            throw new ArgumentException("The descriptor must declare the canonical target as a string.", nameof(declaration));
        }
        IdempotencyDescriptorField? subject = declaration.DescriptorSchema.Fields.SingleOrDefault(field => field.Name == "acting-subject");
        if (declaration.RequiresAuthenticatedSubject ? subject?.Type != "string" : subject is not null)
        {
            throw new ArgumentException("Authenticated-subject policy must agree with the reserved acting-subject string descriptor.", nameof(declaration));
        }
        if (declaration.Route.Kind != "command" || declaration.Route.Execution?.HandlerType != typeof(TCommand).FullName)
        {
            throw new ArgumentException("The declaration must identify the exact command payload type.", nameof(declaration));
        }
        Declaration = declaration;
        _semanticFields = semanticFields;
    }

    /// <inheritdoc/>
    public CommandIntentDeclaration Declaration { get; }
    /// <inheritdoc/>
    public string CommandType => Declaration.Route.MessageType!;
    /// <inheritdoc/>
    public string AdapterId => Declaration.AdapterId;
    /// <inheritdoc/>
    public string OperationId => Declaration.OperationId;
    /// <inheritdoc/>
    public int DescriptorVersion => Declaration.DescriptorSchema.Version;
    /// <inheritdoc/>
    public IdempotencyReplayRetentionTier RetentionTier => Declaration.RetentionTier;

    /// <inheritdoc/>
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Domain != Declaration.Route.Domain || !CommandDiscriminatorCompatibility.Matches(Declaration.Route, command.CommandType))
        {
            throw new InvalidOperationException("The command does not match this declared domain and handler.");
        }
        TCommand payload = JsonSerializer.Deserialize<TCommand>(command.Payload, EventStorePayloadSerialization.Options)
            ?? throw new InvalidOperationException("The command payload is missing.");
        var fields = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach ((string name, object? value) in _semanticFields(payload))
        {
            fields.Add(name, value);
        }
        if (Declaration.RequiresAuthenticatedSubject)
        {
            if (string.IsNullOrWhiteSpace(command.AuthenticatedSubject))
            {
                throw new InvalidOperationException("An authenticated acting subject is required.");
            }
            fields.Add("acting-subject", command.AuthenticatedSubject);
        }
        IdempotencyDescriptorField[] expected = Declaration.DescriptorSchema.Fields.Where(f => f.Name != "target").ToArray();
        if (expected.Length != fields.Count || expected.Any(field => !fields.TryGetValue(field.Name, out object? value) || !ValidValue(field.Type, value)))
        {
            throw new InvalidOperationException("Semantic fields do not match the owner descriptor schema.");
        }
        string target = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId).ToString();
        return new(target, JsonSerializer.SerializeToUtf8Bytes(fields), null, Declaration.PolicyVersion, null, null);
    }

    private static bool ValidValue(string type, object? value) => type switch
    {
        "string" => value is string,
        "boolean" => value is bool,
        "int64" => value is long,
        "string-list" => value is IReadOnlyList<string> strings && strings.All(s => s is not null),
        _ => false,
    };
}
