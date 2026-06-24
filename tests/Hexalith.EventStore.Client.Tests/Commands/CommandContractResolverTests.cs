using Hexalith.EventStore.Client.Commands;
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Client.Tests.Commands;

internal sealed record ValidCounterCommand(string AggregateId) : ICommandContract {
    public static string CommandType => "create-counter";
    public static string Domain => "counter";
}

internal sealed record InvalidKebabCaseCommand(string AggregateId) : ICommandContract {
    public static string CommandType => "CreateCounter";
    public static string Domain => "counter";
}

internal sealed record EmptyDomainCommand(string AggregateId) : ICommandContract {
    public static string CommandType => "create-counter";
    public static string Domain => "";
}

internal sealed record ColonCommandTypeCommand(string AggregateId) : ICommandContract {
    public static string CommandType => "counter:create";
    public static string Domain => "counter";
}

internal sealed record NullCommandTypeCommand(string AggregateId) : ICommandContract {
    public static string CommandType => null!;
    public static string Domain => "counter";
}

public class CommandContractResolverTests : IDisposable {
    public CommandContractResolverTests() => CommandContractResolver.ClearCache();

    public void Dispose() {
        CommandContractResolver.ClearCache();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Resolve_ValidContract_ReturnsCorrectMetadata() {
        CommandContractMetadata metadata = CommandContractResolver.Resolve<ValidCounterCommand>();

        Assert.Equal("create-counter", metadata.CommandType);
        Assert.Equal("counter", metadata.Domain);
    }

    [Fact]
    public void Resolve_CalledTwice_ReturnsCachedInstance() {
        CommandContractMetadata first = CommandContractResolver.Resolve<ValidCounterCommand>();
        CommandContractMetadata second = CommandContractResolver.Resolve<ValidCounterCommand>();

        Assert.Same(first, second);
    }

    [Fact]
    public void Resolve_InvalidKebabCase_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
        CommandContractResolver.Resolve<InvalidKebabCaseCommand>);

    [Fact]
    public void Resolve_EmptyDomain_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
        CommandContractResolver.Resolve<EmptyDomainCommand>);

    [Fact]
    public void Resolve_CommandTypeWithColon_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
        CommandContractResolver.Resolve<ColonCommandTypeCommand>);

    [Fact]
    public void Resolve_NullCommandType_ThrowsArgumentNullException() => _ = Assert.Throws<ArgumentNullException>(
        CommandContractResolver.Resolve<NullCommandTypeCommand>);
}
