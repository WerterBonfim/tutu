using System.Net.Sockets;
using System.Text.Json;
using Tutu.Api;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5080");

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
var activity = new ConsoleActivity();
builder.Services.AddSingleton(activity);
builder.Logging.AddProvider(new RingLoggerProvider(activity));
builder.Services.AddSingleton<PubSubConsole>();
builder.Services.AddSingleton<CatalogHub>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CatalogHub>());

var app = builder.Build();
var logger = app.Logger;

IResult Handle(Func<IResult> action)
{
    try
    {
        return action();
    }
    catch (ConsoleException ce)
    {
        return Results.Json(new { error = ce.Error, message = ce.Message }, statusCode: ce.Status);
    }
}

app.MapGet("/api/health", async (PubSubConsole console, CancellationToken ct) =>
    Results.Ok(await console.GetHealthAsync(ct)));

app.MapGet("/api/topics", (PubSubConsole console) =>
    Handle(() => Results.Ok(new { topics = console.ListTopics() })));

app.MapPost("/api/topics", (CreateTopicRequest body, PubSubConsole console) =>
    Handle(() => Results.Created($"/api/topics/{body.Id}", console.CreateTopic(body.Id))));

app.MapGet("/api/subscriptions", (PubSubConsole console) =>
    Handle(() => Results.Ok(new { subscriptions = console.ListSubscriptions() })));

app.MapPost("/api/subscriptions", (CreateSubscriptionRequest body, PubSubConsole console) =>
    Handle(() => Results.Created($"/api/subscriptions/{body.Id}", console.CreateSubscription(body.Id, body.TopicId))));

app.MapPost("/api/topics/{topicId}/observe", (string topicId, PubSubConsole console) =>
    Handle(() => Results.Ok(console.EnsureObservation(topicId))));

app.MapPost("/api/topics/{topicId}/messages", (string topicId, PublishRequest body, PubSubConsole console) =>
    Handle(() =>
    {
        var id = console.Publish(topicId, body.Data ?? "", body.Attributes);
        return Results.Ok(new { messageId = id });
    }));

app.MapPost("/api/subscriptions/{id}/pull", (string id, PullRequestDto? body, PubSubConsole console) =>
    Handle(() => Results.Ok(new { messages = console.Pull(id, body?.MaxMessages ?? 50) })));

app.MapPost("/api/subscriptions/{id}/ack", (string id, AckRequest body, PubSubConsole console) =>
    Handle(() =>
    {
        console.Ack(id, body.AckIds ?? []);
        return Results.NoContent();
    }));

app.MapPost("/api/subscriptions/{id}/nack", (string id, AckRequest body, PubSubConsole console) =>
    Handle(() => Results.Ok(new { deadLettered = console.Nack(id, body.AckIds ?? []) })));

app.MapDelete("/api/topics/{id}", (string id, PubSubConsole console) =>
    Handle(() =>
    {
        console.DeleteTopic(id);
        return Results.NoContent();
    }));

app.MapDelete("/api/subscriptions/{id}", (string id, PubSubConsole console) =>
    Handle(() =>
    {
        console.DeleteSubscription(id);
        return Results.NoContent();
    }));

app.MapPost("/api/admin/clear", (PubSubConsole console) =>
    Handle(() =>
    {
        console.Clear();
        return Results.NoContent();
    }));

app.MapGet("/api/catalog", (PubSubConsole console) =>
    Handle(() => Results.Ok(new { topics = console.ListTopics(), subscriptions = console.ListSubscriptions() })));

app.MapPost("/api/catalog", (CatalogImport body, PubSubConsole console) =>
    Handle(() =>
    {
        console.ImportCatalog(
            (body.Topics ?? []).Select(t => t.Id),
            (body.Subscriptions ?? []).Select(s => (s.Id, s.TopicId)));
        return Results.NoContent();
    }));

app.MapGet("/api/overview", (PubSubConsole console, ConsoleActivity stats) =>
{
    var counts = console.TryCounts();
    return Results.Ok(stats.Snapshot(counts.Topics, counts.Subscriptions));
});

app.MapGet("/api/dlq/export", (ConsoleActivity stats) =>
{
    var lines = stats.Quarantine().Select(item => JsonSerializer.Serialize(item));
    return Results.Text(string.Join('\n', lines), "application/x-ndjson");
});

app.MapPost("/api/dlq/purge", (ConsoleActivity stats) =>
{
    stats.PurgeQuarantine();
    return Results.NoContent();
});

app.MapGet("/api/dlq", (ConsoleActivity stats) => Results.Ok(new { messages = stats.Quarantine() }));

app.MapPost("/api/dlq/{id}/replay", (string id, PubSubConsole console) =>
    Handle(() => Results.Ok(console.Replay(id))));

app.MapGet("/api/logs", (ConsoleActivity stats) =>
    Results.Ok(new { lines = stats.Logs(), paused = stats.LogsPaused }));

app.MapPost("/api/logs/pause", (ConsoleActivity stats) =>
{
    stats.LogsPaused = !stats.LogsPaused;
    return Results.Ok(new { paused = stats.LogsPaused });
});

app.MapPost("/api/logs/clear", (ConsoleActivity stats) =>
{
    stats.ClearLogs();
    return Results.NoContent();
});

app.MapGet("/api/settings", (PubSubConsole console, ConsoleActivity stats) =>
{
    var snap = stats.Snapshot(0, 0);
    return Results.Ok(new
    {
        projectId = console.ProjectId,
        hostPort = console.HostPort,
        pid = snap.Pid,
        workingSetBytes = snap.WorkingSetBytes,
        chaosMs = stats.ChaosMs,
        strictJson = stats.StrictJson,
        logsPaused = stats.LogsPaused
    });
});

app.MapPut("/api/settings", (SettingsUpdate body, ConsoleActivity stats) =>
{
    if (body.ChaosMs is int ms)
    {
        stats.ChaosMs = Math.Clamp(ms, 0, 5000);
    }

    if (body.StrictJson is bool strict)
    {
        stats.StrictJson = strict;
    }

    return Results.NoContent();
});

app.MapGet("/metrics", (ConsoleActivity stats) =>
{
    var snap = stats.Snapshot(0, 0);
    var text = $"""
        tutu_published_total {snap.Published}
        tutu_pulled_total {snap.Pulled}
        tutu_acked_total {snap.Acked}
        tutu_nacked_total {snap.Nacked}
        tutu_dead_lettered_total {snap.DeadLettered}
        tutu_inflight {snap.Inflight}
        """;
    return Results.Text(text, "text/plain");
});

app.MapGet("/events", async (HttpContext http, CatalogHub hub, CancellationToken ct) =>
{
    http.Response.Headers.ContentType = "text/event-stream";
    http.Response.Headers.CacheControl = "no-cache";
    await http.Response.StartAsync(ct);
    await foreach (var frame in hub.Subscribe(ct))
    {
        await http.Response.WriteAsync($"event: {frame.Type}\ndata: {frame.Json}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }
});

try
{
    app.Run();
}
catch (IOException ex) when (IsPortInUse(ex))
{
    logger.LogError(ex, "PortInUse urls={Urls}", builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5080");
    return 1;
}

return 0;

static bool IsPortInUse(Exception ex)
{
    for (var current = ex; current is not null; current = current.InnerException)
    {
        if (current is SocketException socket && socket.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            return true;
        }

        if (current.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

internal sealed record CreateTopicRequest(string Id);
internal sealed record CreateSubscriptionRequest(string Id, string TopicId);
internal sealed record PublishRequest(string? Data, Dictionary<string, string>? Attributes);
internal sealed record PullRequestDto(int MaxMessages);
internal sealed record AckRequest(List<string>? AckIds);
internal sealed record CatalogImport(List<TopicImport>? Topics, List<SubImport>? Subscriptions);
internal sealed record TopicImport(string Id);
internal sealed record SubImport(string Id, string TopicId);
internal sealed record SettingsUpdate(int? ChaosMs, bool? StrictJson);
