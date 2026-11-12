using System.Text.Json;
using System.Text.RegularExpressions;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;

namespace Tutu.Api;

public sealed class ConsoleException(int status, string error, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Error { get; } = error;
}

public sealed record TopicDto(string Id, string Name);

public sealed record SubscriptionDto(
    string Id,
    string Name,
    string TopicId,
    int AckDeadlineSeconds,
    bool IsObservation);

public sealed record MessageDto(
    string Id,
    string AckId,
    string PublishTime,
    IReadOnlyDictionary<string, string> Attributes,
    string Data,
    int SizeBytes,
    string Encoding,
    string SubscriptionId);

public sealed record HealthDto(bool Online, string ProjectId, string HostPort, string? Error);

public sealed class PubSubConsole
{
    public const string ObservationPrefix = "tutu-";
    private static readonly Regex ResourceId = new("^[A-Za-z][A-Za-z0-9._~+-]{2,254}$", RegexOptions.Compiled);

    private readonly ILogger<PubSubConsole> _logger;
    private readonly ConsoleActivity _activity;
    private readonly string _projectId;
    private readonly string _hostPort;
    private readonly PublisherServiceApiClient _publisher;
    private readonly SubscriberServiceApiClient _subscriber;

    public PubSubConsole(ILogger<PubSubConsole> logger, IConfiguration config, ConsoleActivity activity)
    {
        _logger = logger;
        _activity = activity;
        _projectId = config["GOOGLE_CLOUD_PROJECT"] ?? "pedidos-local";
        var emulatorHost = config["PUBSUB_EMULATOR_HOST"] ?? "localhost:8085";
        _hostPort = emulatorHost.Contains(':') ? emulatorHost[(emulatorHost.LastIndexOf(':') + 1)..] : "8085";
        Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", emulatorHost);

        _publisher = new PublisherServiceApiClientBuilder { EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        _subscriber = new SubscriberServiceApiClientBuilder { EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    }

    public string ProjectId => _projectId;
    public string HostPort => _hostPort;

    public static string ObservationId(string topicId) => $"{ObservationPrefix}{topicId}";

    public static bool IsObservation(string subscriptionId) =>
        subscriptionId.StartsWith(ObservationPrefix, StringComparison.Ordinal);

    public static void GuardObservation(string subscriptionId)
    {
        if (IsObservation(subscriptionId))
        {
            return;
        }

        throw new ConsoleException(
            StatusCodes.Status403Forbidden,
            "NotObservation",
            $"Pull/ack só é permitido em subscriptions {ObservationPrefix}*. Recusado: {subscriptionId}");
    }

    public async Task<HealthDto> GetHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.ListTopicsAsync($"projects/{_projectId}")
                .ReadPageAsync(1, cancellationToken);
            return new HealthDto(true, _projectId, _hostPort, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmulatorUnreachable project={Project} port={Port}", _projectId, _hostPort);
            return new HealthDto(false, _projectId, _hostPort, ex.Message);
        }
    }

    public (int Topics, int Subscriptions) TryCounts()
    {
        try
        {
            var topics = _publisher.ListTopics($"projects/{_projectId}").Count();
            var subs = _subscriber.ListSubscriptions($"projects/{_projectId}").Count();
            return (topics, subs);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OverviewCountsFailed project={Project}", _projectId);
            return (0, 0);
        }
    }

    public List<TopicDto> ListTopics()
    {
        try
        {
            return _publisher.ListTopics($"projects/{_projectId}")
                .Select(t =>
                {
                    var name = TopicName.Parse(t.Name);
                    return new TopicDto(name.TopicId, t.Name);
                })
                .ToList();
        }
        catch (Exception ex)
        {
            throw Wrap("ListTopicsFailed", ex);
        }
    }

    public TopicDto CreateTopic(string id)
    {
        EnsureResourceId(id, "tópico");
        try
        {
            var created = _publisher.CreateTopic(new TopicName(_projectId, id));
            var name = TopicName.Parse(created.Name);
            return new TopicDto(name.TopicId, created.Name);
        }
        catch (Exception ex)
        {
            throw Wrap("CreateTopicFailed", ex, ("topic", id));
        }
    }

    public List<SubscriptionDto> ListSubscriptions()
    {
        try
        {
            return _subscriber.ListSubscriptions($"projects/{_projectId}")
                .Select(MapSubscription)
                .ToList();
        }
        catch (Exception ex)
        {
            throw Wrap("ListSubscriptionsFailed", ex);
        }
    }

    public SubscriptionDto CreateSubscription(string id, string topicId)
    {
        EnsureResourceId(id, "subscription");
        EnsureResourceId(topicId, "tópico");
        try
        {
            var created = _subscriber.CreateSubscription(
                new SubscriptionName(_projectId, id),
                new TopicName(_projectId, topicId),
                pushConfig: null,
                ackDeadlineSeconds: 30);
            return MapSubscription(created);
        }
        catch (Exception ex)
        {
            throw Wrap("CreateSubscriptionFailed", ex, ("subscription", id), ("topic", topicId));
        }
    }

    public SubscriptionDto EnsureObservation(string topicId)
    {
        EnsureResourceId(topicId, "tópico");
        var id = ObservationId(topicId);
        try
        {
            return MapSubscription(_subscriber.GetSubscription(new SubscriptionName(_projectId, id)));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return CreateSubscription(id, topicId);
        }
        catch (Exception ex)
        {
            throw Wrap("EnsureObservationFailed", ex, ("topic", topicId), ("subscription", id));
        }
    }

    public string Publish(string topicId, string data, Dictionary<string, string>? attributes)
    {
        EnsureResourceId(topicId, "tópico");
        DelayChaos();
        if (_activity.StrictJson)
        {
            try
            {
                using var _ = JsonDocument.Parse(string.IsNullOrWhiteSpace(data) ? "null" : data);
            }
            catch (JsonException)
            {
                throw new ConsoleException(StatusCodes.Status400BadRequest, "InvalidJson", "Payload não é JSON.");
            }
        }

        try
        {
            var message = new PubsubMessage { Data = ByteString.CopyFromUtf8(data ?? "") };
            if (attributes is not null)
            {
                foreach (var pair in attributes)
                {
                    message.Attributes[pair.Key] = pair.Value ?? "";
                }
            }

            var response = _publisher.Publish(new TopicName(_projectId, topicId), new[] { message });
            _activity.NotePublish();
            return response.MessageIds[0];
        }
        catch (Exception ex)
        {
            throw Wrap("PublishFailed", ex, ("topic", topicId));
        }
    }

    public List<MessageDto> Pull(string subscriptionId, int maxMessages)
    {
        GuardObservation(subscriptionId);
        DelayChaos();
        try
        {
            var response = _subscriber.Pull(new PullRequest
            {
                SubscriptionAsSubscriptionName = new SubscriptionName(_projectId, subscriptionId),
                MaxMessages = Math.Clamp(maxMessages <= 0 ? 50 : maxMessages, 1, 100)
            });

            return response.ReceivedMessages.Select(m =>
            {
                var bytes = m.Message.Data.ToByteArray();
                var payload = m.Message.Data.ToStringUtf8();
                var attributes = m.Message.Attributes.ToDictionary(p => p.Key, p => p.Value);
                _activity.RememberPull(new HeldMessage(
                    m.Message.MessageId,
                    m.AckId,
                    TopicFromObservation(subscriptionId),
                    subscriptionId,
                    payload,
                    attributes,
                    m.Message.PublishTime?.ToDateTime().ToString("O") ?? ""));
                return new MessageDto(
                    m.Message.MessageId,
                    m.AckId,
                    m.Message.PublishTime?.ToDateTime().ToString("O") ?? "",
                    attributes,
                    payload,
                    bytes.Length,
                    "utf8",
                    subscriptionId);
            }).ToList();
        }
        catch (Exception ex)
        {
            throw Wrap("PullFailed", ex, ("subscription", subscriptionId));
        }
    }

    public void Ack(string subscriptionId, IReadOnlyList<string> ackIds)
    {
        GuardObservation(subscriptionId);
        if (ackIds.Count == 0)
        {
            return;
        }

        try
        {
            _subscriber.Acknowledge(new SubscriptionName(_projectId, subscriptionId), ackIds);
            _activity.NoteAck(ackIds);
        }
        catch (Exception ex)
        {
            throw Wrap("AckFailed", ex, ("subscription", subscriptionId));
        }
    }

    public List<string> Nack(string subscriptionId, IReadOnlyList<string> ackIds)
    {
        GuardObservation(subscriptionId);
        if (ackIds.Count == 0)
        {
            return [];
        }

        var dead = new List<string>();
        var redeliver = new List<string>();
        foreach (var ackId in ackIds)
        {
            var (held, attempts, deadLetter) = _activity.NoteNack(ackId);
            if (held is not null && deadLetter)
            {
                Park(held);
                _activity.CommitDeadLetter(held, attempts);
                dead.Add(held.Id);
            }
            else
            {
                redeliver.Add(ackId);
            }
        }

        try
        {
            if (dead.Count > 0)
            {
                var parked = ackIds.Where(id => !redeliver.Contains(id)).ToList();
                _subscriber.Acknowledge(new SubscriptionName(_projectId, subscriptionId), parked);
            }

            if (redeliver.Count > 0)
            {
                _subscriber.ModifyAckDeadline(new SubscriptionName(_projectId, subscriptionId), redeliver, 0);
            }
        }
        catch (Exception ex)
        {
            throw Wrap("NackFailed", ex, ("subscription", subscriptionId));
        }

        return dead;
    }

    public void DeleteTopic(string id)
    {
        EnsureResourceId(id, "tópico");
        try
        {
            foreach (var sub in ListSubscriptions().Where(s => s.TopicId == id))
            {
                _subscriber.DeleteSubscription(new SubscriptionName(_projectId, sub.Id));
            }

            _publisher.DeleteTopic(new TopicName(_projectId, id));
        }
        catch (Exception ex)
        {
            throw Wrap("DeleteTopicFailed", ex, ("topic", id));
        }
    }

    public void DeleteSubscription(string id)
    {
        EnsureResourceId(id, "subscription");
        try
        {
            _subscriber.DeleteSubscription(new SubscriptionName(_projectId, id));
        }
        catch (Exception ex)
        {
            throw Wrap("DeleteSubscriptionFailed", ex, ("subscription", id));
        }
    }

    public void Clear()
    {
        foreach (var topic in ListTopics())
        {
            DeleteTopic(topic.Id);
        }
    }

    public void ImportCatalog(IEnumerable<string> topics, IEnumerable<(string Id, string TopicId)> subscriptions)
    {
        foreach (var id in topics)
        {
            try
            {
                CreateTopic(id);
            }
            catch (ConsoleException ex) when (ex.Status == StatusCodes.Status409Conflict)
            {
            }
        }

        foreach (var (id, topicId) in subscriptions)
        {
            try
            {
                CreateSubscription(id, topicId);
            }
            catch (ConsoleException ex) when (ex.Status == StatusCodes.Status409Conflict)
            {
            }
        }
    }

    public QuarantineMessage Replay(string id)
    {
        var item = _activity.TakeQuarantine(id)
            ?? throw new ConsoleException(StatusCodes.Status404NotFound, "NotInQuarantine", $"Mensagem {id} não está na quarentena.");
        try
        {
            Publish(item.TopicId, item.Data, item.Attributes);
            return item;
        }
        catch
        {
            _activity.Restore(item);
            throw;
        }
    }

    private void Park(HeldMessage held)
    {
        var dlqTopic = $"tutu-dlq-{held.TopicId}";
        try
        {
            try
            {
                _publisher.CreateTopic(new TopicName(_projectId, dlqTopic));
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
            {
            }

            var message = new PubsubMessage { Data = ByteString.CopyFromUtf8(held.Data ?? "") };
            message.Attributes["source-message-id"] = held.Id;
            message.Attributes["dead-letter-source"] = held.TopicId;
            foreach (var pair in held.Attributes)
            {
                if (!message.Attributes.ContainsKey(pair.Key))
                {
                    message.Attributes[pair.Key] = pair.Value ?? "";
                }
            }

            _publisher.Publish(new TopicName(_projectId, dlqTopic), new[] { message });
        }
        catch (Exception ex)
        {
            throw Wrap("DeadLetterFailed", ex, ("topic", held.TopicId), ("message", held.Id));
        }
    }

    private void DelayChaos()
    {
        var ms = Math.Clamp(_activity.ChaosMs, 0, 5000);
        if (ms > 0)
        {
            Thread.Sleep(ms);
        }
    }

    private static string TopicFromObservation(string subscriptionId) =>
        subscriptionId.StartsWith(ObservationPrefix, StringComparison.Ordinal)
            ? subscriptionId[ObservationPrefix.Length..]
            : subscriptionId;

    private static SubscriptionDto MapSubscription(Subscription sub)
    {
        var name = SubscriptionName.Parse(sub.Name);
        var topic = TopicName.Parse(sub.Topic);
        return new SubscriptionDto(
            name.SubscriptionId,
            sub.Name,
            topic.TopicId,
            sub.AckDeadlineSeconds,
            IsObservation(name.SubscriptionId));
    }

    private static void EnsureResourceId(string id, string kind)
    {
        if (string.IsNullOrWhiteSpace(id) || !ResourceId.IsMatch(id))
        {
            throw new ConsoleException(
                StatusCodes.Status400BadRequest,
                "InvalidId",
                $"Id de {kind} inválido: '{id}'. Use 3–255 caracteres, começando com letra.");
        }
    }

    private ConsoleException Wrap(string error, Exception ex, params (string Key, string Value)[] fields)
    {
        var detail = string.Join(" ", fields.Select(f => $"{f.Key}={f.Value}"));
        _logger.LogError(ex, "{Error} {Detail}", error, detail);

        return ex switch
        {
            ConsoleException ce => ce,
            RpcException rpc when rpc.StatusCode == StatusCode.NotFound =>
                new ConsoleException(StatusCodes.Status404NotFound, error, rpc.Status.Detail),
            RpcException rpc when rpc.StatusCode == StatusCode.AlreadyExists =>
                new ConsoleException(StatusCodes.Status409Conflict, error, rpc.Status.Detail),
            RpcException rpc when rpc.StatusCode == StatusCode.InvalidArgument =>
                new ConsoleException(StatusCodes.Status400BadRequest, error, rpc.Status.Detail),
            RpcException rpc when rpc.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded =>
                new ConsoleException(StatusCodes.Status503ServiceUnavailable, error, "Emulador inacessível."),
            _ => new ConsoleException(StatusCodes.Status502BadGateway, error, ex.Message)
        };
    }
}
