using System.Collections.Concurrent;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();
var processedEventIds = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
int processedCount = 0;

app.MapDefaultEndpoints();
app.MapGet("/", () => "Hexalith EventStore Sample Domain Service");
app.MapPost(
	"/events/idempotency-demo",
	(IdempotencyDemoEvent message) =>
	{
		ArgumentNullException.ThrowIfNull(message);

		if (string.IsNullOrWhiteSpace(message.Id))
		{
			return Results.BadRequest(new { error = "CloudEvents id is required." });
		}

		if (!processedEventIds.TryAdd(message.Id, 0))
		{
			int currentProcessedCount = Volatile.Read(ref processedCount);
			return Results.Ok(new
			{
				duplicate = true,
				eventId = message.Id,
				processedCount = currentProcessedCount,
				note = "Duplicate event skipped (idempotent handling).",
			});
		}

		int updatedProcessedCount = Interlocked.Increment(ref processedCount);
		return Results.Ok(new
		{
			duplicate = false,
			eventId = message.Id,
			processedCount = updatedProcessedCount,
			note = "Event processed and marked as handled.",
		});
	});

app.MapGet(
	"/events/idempotency-demo/state",
	() => Results.Ok(new
	{
		processedCount = Volatile.Read(ref processedCount),
		uniqueEventIds = processedEventIds.Count,
	}));

app.Run();

internal sealed record IdempotencyDemoEvent(string Id, string? Type, object? Data);
