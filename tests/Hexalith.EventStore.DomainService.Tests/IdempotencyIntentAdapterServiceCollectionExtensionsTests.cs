using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class IdempotencyIntentAdapterServiceCollectionExtensionsTests {
    [Fact]
    public void AddIdempotencyIntentAdapter_RegistersSingletonAdapter() {
        var services = new ServiceCollection();

        _ = services.AddIdempotencyIntentAdapter<RecordingAdapter>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IIdempotencyIntentAdapter[] adapters = [.. provider.GetServices<IIdempotencyIntentAdapter>()];

        adapters.Length.ShouldBe(1);
        adapters[0].ShouldBeOfType<RecordingAdapter>();
        adapters[0].CommandType.ShouldBe("Hexalith.Folders.Commands.CreateFolder");
    }

    private sealed class RecordingAdapter : IIdempotencyIntentAdapter {
        public string CommandType => "Hexalith.Folders.Commands.CreateFolder";

        public string AdapterId => "hexalith-folders";

        public string OperationId => "create-folder";

        public int DescriptorVersion => 1;

        public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;

        public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command) {
            ArgumentNullException.ThrowIfNull(command);
            return new IdempotencyCanonicalIntent(
                $"{command.Tenant}/{command.Domain}/{command.AggregateId}",
                "{}"u8.ToArray(),
                SemanticOptions: null,
                PolicyVersion: "test-v1",
                DelegatedTaskScope: null,
                CredentialScope: null);
        }
    }
}
