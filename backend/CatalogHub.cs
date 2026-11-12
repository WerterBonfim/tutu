using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace Tutu.Api;

public sealed record CatalogFrame(string Type, string Json);

public sealed class CatalogHub(PubSubConsole console, ILogger<CatalogHub> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, Channel<CatalogFrame>> _clients = new();
    private readonly object _gate = new();
    private HealthDto _health = new(false, console.ProjectId, console.HostPort, "starting");
    private List<TopicDto> _topics = [];
    private List<SubscriptionDto> _subscriptions = [];

    public async IAsyncEnumerable<CatalogFrame> Subscribe(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<CatalogFrame>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
        _clients[id] = channel;
        WriteSnapshot(channel.Writer);

        try
        {
            await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return frame;
            }
        }
        finally
        {
            _clients.TryRemove(id, out _);
            channel.Writer.TryComplete();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var health = await console.GetHealthAsync(stoppingToken);
                List<TopicDto> topics = [];
                List<SubscriptionDto> subs = [];
                if (health.Online)
                {
                    topics = console.ListTopics();
                    subs = console.ListSubscriptions();
                }

                lock (_gate)
                {
                    _health = health;
                    _topics = topics;
                    _subscriptions = subs;
                }

                Broadcast("health", health);
                Broadcast("topics", new { topics });
                Broadcast("subscriptions", new { subscriptions = subs });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "CatalogPollFailed");
                var offline = new HealthDto(false, console.ProjectId, console.HostPort, ex.Message);
                lock (_gate)
                {
                    _health = offline;
                }

                Broadcast("health", offline);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void WriteSnapshot(ChannelWriter<CatalogFrame> writer)
    {
        HealthDto health;
        List<TopicDto> topics;
        List<SubscriptionDto> subs;
        lock (_gate)
        {
            health = _health;
            topics = _topics;
            subs = _subscriptions;
        }

        writer.TryWrite(Frame("snapshot", new { health, topics, subscriptions = subs }));
    }

    private void Broadcast(string type, object payload)
    {
        var frame = Frame(type, payload);
        foreach (var client in _clients.Values)
        {
            client.Writer.TryWrite(frame);
        }
    }

    private static CatalogFrame Frame(string type, object payload) =>
        new(type, JsonSerializer.Serialize(payload, JsonOptions.Camel));
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Camel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
