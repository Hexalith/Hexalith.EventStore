
using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

public class EventStoreProjectionTests {
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
        var events = new object[] {
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
        var events = new object?[] {
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
}
