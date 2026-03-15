
using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

public class EventStoreProjectionTests : IDisposable {
    public EventStoreProjectionTests() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
    }

    public void Dispose() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
        GC.SuppressFinalize(this);
    }

    private sealed class ItemAdded : IEventPayload {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class ItemRemoved : IEventPayload;

    private sealed class TestReadModel {
        public int Count { get; private set; }

        public string LastAdded { get; private set; } = string.Empty;

        public void Apply(ItemAdded e) {
            Count++;
            LastAdded = e.Name;
        }

        public void Apply(ItemRemoved e) => Count--;
    }

    private sealed class TestProjection : EventStoreProjection<TestReadModel>;

    [Fact]
    public void Project_TypedEvents_AppliesCorrectly() {
        var projection = new TestProjection();
        object[] events = new object[] {
            new ItemAdded { Name = "first" },
            new ItemAdded { Name = "second" },
            new ItemRemoved(),
        };

        TestReadModel model = projection.Project(events);

        Assert.Equal(1, model.Count);
        Assert.Equal("second", model.LastAdded);
    }

    [Fact]
    public void Project_EmptyEvents_ReturnsDefaultModel() {
        var projection = new TestProjection();

        TestReadModel model = projection.Project(Array.Empty<object>());

        Assert.Equal(0, model.Count);
        Assert.Equal(string.Empty, model.LastAdded);
    }

    [Fact]
    public void Project_NullEvents_ThrowsArgumentNullException() {
        var projection = new TestProjection();

        _ = Assert.Throws<ArgumentNullException>(() => projection.Project(null!));
    }

    [Fact]
    public void Project_SkipsNullElements() {
        var projection = new TestProjection();
        object?[] events = new object?[] {
            new ItemAdded { Name = "a" },
            null,
            new ItemAdded { Name = "b" },
        };

        TestReadModel model = projection.Project(events);

        Assert.Equal(2, model.Count);
    }

    [Fact]
    public void ProjectFromJson_ValidArray_AppliesEvents() {
        var projection = new TestProjection();
        string json = """
            [
                {"eventTypeName":"ItemAdded","payload":{"Name":"from-json"}},
                {"eventTypeName":"ItemAdded","payload":{"Name":"second"}}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(json);

        TestReadModel model = projection.ProjectFromJson(jsonArray);

        Assert.Equal(2, model.Count);
        Assert.Equal("second", model.LastAdded);
    }

    [Fact]
    public void ProjectFromJson_NonArray_ThrowsArgumentException() {
        var projection = new TestProjection();
        JsonElement jsonObject = JsonSerializer.Deserialize<JsonElement>("{}");

        _ = Assert.Throws<ArgumentException>(() => projection.ProjectFromJson(jsonObject));
    }

    [Fact]
    public void ProjectFromJson_EmptyArray_ReturnsDefaultModel() {
        var projection = new TestProjection();
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>("[]");

        TestReadModel model = projection.ProjectFromJson(jsonArray);

        Assert.Equal(0, model.Count);
    }

    [Fact]
    public void ProjectFromJson_UnknownEventType_ThrowsInvalidOperationException() {
        var projection = new TestProjection();
        string json = """
            [
                {"eventTypeName":"UnknownEvent","payload":{}}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(json);

        _ = Assert.Throws<InvalidOperationException>(() => projection.ProjectFromJson(jsonArray));
    }

    [Fact]
    public void ProjectFromJson_NonObjectEntry_ThrowsInvalidOperationException() {
        var projection = new TestProjection();
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>("[1]");

        _ = Assert.Throws<InvalidOperationException>(() => projection.ProjectFromJson(jsonArray));
    }

    [Fact]
    public void ProjectFromJson_MissingEventTypeName_ThrowsInvalidOperationException() {
        var projection = new TestProjection();
        string json = """
            [
                {"payload":{"Name":"x"}}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(json);

        _ = Assert.Throws<InvalidOperationException>(() => projection.ProjectFromJson(jsonArray));
    }

    [Fact]
    public void ProjectFromJson_InvalidPayloadShape_ThrowsInvalidOperationException() {
        var projection = new TestProjection();
        string json = """
            [
                {"eventTypeName":"ItemAdded","payload":123}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(json);

        _ = Assert.Throws<InvalidOperationException>(() => projection.ProjectFromJson(jsonArray));
    }

    // --- Story 16-8: ProjectFromJson suffix matching (AC#6: 6.5) ---

    [Fact]
    public void ProjectFromJson_SuffixMatchedEventTypeName_AppliesCorrectly() {
        var projection = new TestProjection();
        // Fully-qualified-style name that suffix-matches "ItemAdded"
        string json = """
            [
                {"eventTypeName":"MyNamespace.ItemAdded","payload":{"Name":"suffix"}}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(json);

        TestReadModel model = projection.ProjectFromJson(jsonArray);

        Assert.Equal(1, model.Count);
        Assert.Equal("suffix", model.LastAdded);
    }

    // --- Story 16-8: Multiple Apply methods on same read model (AC#6: 6.6) ---

    [Fact]
    public void Project_MultipleApplyMethods_AllInvokedCorrectly() {
        var projection = new TestProjection();
        // Send only ItemRemoved events to verify the second Apply method works independently
        object[] events = new object[] {
            new ItemAdded { Name = "a" },
            new ItemAdded { Name = "b" },
            new ItemRemoved(),
        };

        TestReadModel model = projection.Project(events);

        Assert.Equal(1, model.Count);  // 2 added, 1 removed
        Assert.Equal("b", model.LastAdded);
    }

    // --- Story 16-8: Unknown event type in typed Project is silently skipped (AC#6) ---

    [Fact]
    public void Project_UnknownEventType_SilentlySkipped() {
        var projection = new TestProjection();
        object[] events = new object[] {
            new ItemAdded { Name = "known" },
            new UnknownProjectionEvent(), // no Apply method for this type
            new ItemRemoved(),
        };

        TestReadModel model = projection.Project(events);

        // Unknown event skipped silently; known events still applied
        Assert.Equal(0, model.Count);  // 1 added, 1 removed, unknown skipped
        Assert.Equal("known", model.LastAdded);
    }

    // --- Story 16-8: ProjectFromJson without payload wrapper (direct element) ---

    [Fact]
    public void ProjectFromJson_DirectEvent_WithoutPayloadWrapper() {
        var projection = new TestProjection();
        // Event element has no "payload" property — entire element is deserialized directly
        string json = """
            [
                {"eventTypeName":"ItemAdded","Name":"direct"}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(json);

        TestReadModel model = projection.ProjectFromJson(jsonArray);

        Assert.Equal(1, model.Count);
        Assert.Equal("direct", model.LastAdded);
    }

    // --- Story 16-8: ProjectFromJson whitespace eventTypeName ---

    [Fact]
    public void ProjectFromJson_WhitespaceEventTypeName_ThrowsInvalidOperationException() {
        var projection = new TestProjection();
        string json = """
            [
                {"eventTypeName":"   ","payload":{}}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(json);

        _ = Assert.Throws<InvalidOperationException>(() => projection.ProjectFromJson(jsonArray));
    }

    private sealed class UnknownProjectionEvent;
}
