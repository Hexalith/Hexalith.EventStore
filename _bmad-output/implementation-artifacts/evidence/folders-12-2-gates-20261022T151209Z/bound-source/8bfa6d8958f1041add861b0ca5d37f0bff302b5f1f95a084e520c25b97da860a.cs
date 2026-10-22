using Hexalith.EventStore.Client.Commands;
using Hexalith.EventStore.Contracts.Commands;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Commands;

internal sealed record ValidCounterCommand(string AggregateId) : ICommandContract
{
    public static string CommandType => "create-counter";

    public static string Domain => "counter";
}

internal sealed record InvalidKebabCaseCommand(string AggregateId) : ICommandContract
{
    public static string CommandType => "CreateCounter";

    public static string Domain => "counter";
}

internal sealed record EmptyDomainCommand(string AggregateId) : ICommandContract
{
    public static string CommandType => "create-counter";

    public static string Domain => "";
}

internal sealed record ColonCommandTypeCommand(string AggregateId) : ICommandContract
{
    public static string CommandType => "counter:create";

    public static string Domain => "counter";
}

internal sealed record ColonDomainCommand(string AggregateId) : ICommandContract
{
    public static string CommandType => "create-counter";

    public static string Domain => "counter:admin";
}

internal sealed record NullCommandTypeCommand(string AggregateId) : ICommandContract
{
    public static string CommandType => null!;

    public static string Domain => "counter";
}

internal sealed record NullDomainCommand(string AggregateId) : ICommandContract
{
    public static string CommandType => "create-counter";

    public static string Domain => null!;
}

public class CommandContractResolverTests : IDisposable
{
    public CommandContractResolverTests() => CommandContractResolver.ClearCache();

    public void Dispose()
    {
        CommandContractResolver.ClearCache();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Resolve_ValidContract_ReturnsCorrectMetadata()
    {
        CommandContractMetadata metadata = CommandContractResolver.Resolve<ValidCounterCommand>();

        metadata.CommandType.ShouldBe("create-counter");
        metadata.Domain.ShouldBe("counter");
    }

    [Fact]
    public void Resolve_CalledTwice_ReturnsCachedInstance()
    {
        CommandContractMetadata first = CommandContractResolver.Resolve<ValidCounterCommand>();
        CommandContractMetadata second = CommandContractResolver.Resolve<ValidCounterCommand>();

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void Resolve_InvalidKebabCase_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => CommandContractResolver.Resolve<InvalidKebabCaseCommand>());

    [Fact]
    public void Resolve_EmptyDomain_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => CommandContractResolver.Resolve<EmptyDomainCommand>());

    [Fact]
    public void Resolve_CommandTypeWithColon_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => CommandContractResolver.Resolve<ColonCommandTypeCommand>());

    [Fact]
    public void Resolve_DomainWithColon_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => CommandContractResolver.Resolve<ColonDomainCommand>());

    [Fact]
    public void Resolve_NullCommandType_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() => CommandContractResolver.Resolve<NullCommandTypeCommand>());

    [Fact]
    public void Resolve_NullDomain_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() => CommandContractResolver.Resolve<NullDomainCommand>());
}
