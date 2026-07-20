using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class IdempotencyExecutionContextTests
{
    [Fact]
    public async Task ProtectAndValidate_CurrentNonZeroFence_BindsExactCommandBoundary()
    {
        var protector = CreateProtector();
        SubmitCommand command = Command();

        IdempotencyExecutionContext context = await protector.ProtectAsync(
            "tenant-a:v1:key-digest",
            7,
            "v1",
            command);

        await protector.ValidateAsync(context, command);
        context.FencingToken.ShouldBe(7);
        JsonSerializer.Serialize(context).ShouldNotContain(command.IdempotencyKey!);
    }

    [Fact]
    public async Task Validate_TamperedOrZeroFence_FailsBeforeUse()
    {
        var protector = CreateProtector();
        SubmitCommand command = Command();
        IdempotencyExecutionContext context = await protector.ProtectAsync(
            "tenant-a:v1:key-digest",
            7,
            "v1",
            command);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await protector.ValidateAsync(context with { FencingToken = 8 }, command));
        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await protector.ValidateAsync(context with { FencingToken = 0 }, command));
        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await protector.ValidateAsync(context, command with { AggregateId = "other" }));
    }

    private static IdempotencyExecutionContextProtector CreateProtector()
        => new(
            new StaticIdempotencyDigestKeyProvider(
                "v1",
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
                },
                []));

    private static SubmitCommand Command()
        => new(
            "01J00000000000000000000000",
            "tenant-a",
            "folders",
            "folder-a",
            "CreateFolderCommand",
            [1],
            "trace-a",
            "user-a",
            IdempotencyKey: "opaque-key-that-must-not-leak");
}
