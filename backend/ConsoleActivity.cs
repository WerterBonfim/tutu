namespace Tutu.Api;

public static class DlqPolicy
{
    public const int MaxAttempts = 5;

    public static bool ShouldDeadLetter(int attempts) => attempts >= MaxAttempts;
}

public sealed record HeldMessage(
    string Id,
    string AckId,
    string TopicId,
    string SubscriptionId,
    string Data,
    Dictionary<string, string> Attributes,
    string PublishTime);

public sealed record QuarantineMessage(
    string Id,
    string TopicId,
    string SubscriptionId,
    string Data,
    Dictionary<string, string> Attributes,
    int Attempts,
    string Reason,
    string At);

public sealed class ConsoleActivity
{
    private readonly object _gate = new();
    private readonly Dictionary<string, HeldMessage> _byAck = new();
    private readonly Dictionary<string, string> _ackByMessage = new();
    private readonly Dictionary<string, int> _attempts = new();
    private readonly List<QuarantineMessage> _quarantine = new();
    private readonly List<string> _logs = new();
    private readonly int[] _published = new int[30];
    private readonly int[] _acked = new int[30];
    private long _slotMinute;

    public long Published { get; private set; }
    public long Pulled { get; private set; }
    public long Acked { get; private set; }
    public long Nacked { get; private set; }
    public long DeadLettered { get; private set; }
    public int Inflight { get; private set; }
    public int ChaosMs { get; set; }
    public bool StrictJson { get; set; }
    public bool LogsPaused { get; set; }

    public void NotePublish()
    {
        lock (_gate)
        {
            Touch();
            Published++;
            _published[29]++;
        }
    }

    public void RememberPull(HeldMessage message)
    {
        lock (_gate)
        {
            Touch();
            Pulled++;
            if (_ackByMessage.TryGetValue(message.Id, out var previous))
            {
                _byAck.Remove(previous);
            }
            else
            {
                Inflight++;
            }

            _byAck[message.AckId] = message;
            _ackByMessage[message.Id] = message.AckId;
        }
    }

    public void NoteAck(IReadOnlyList<string> ackIds)
    {
        lock (_gate)
        {
            Touch();
            foreach (var ackId in ackIds)
            {
                if (!_byAck.Remove(ackId, out var held))
                {
                    continue;
                }

                _ackByMessage.Remove(held.Id);
                _attempts.Remove(held.Id);
                if (Inflight > 0)
                {
                    Inflight--;
                }

                Acked++;
                _acked[29]++;
            }
        }
    }

    public (HeldMessage? Message, int Attempts, bool DeadLetter) NoteNack(string ackId)
    {
        lock (_gate)
        {
            Touch();
            Nacked++;
            if (!_byAck.TryGetValue(ackId, out var held))
            {
                return (null, 0, false);
            }

            var attempts = _attempts.GetValueOrDefault(held.Id) + 1;
            _attempts[held.Id] = attempts;
            return (held, attempts, DlqPolicy.ShouldDeadLetter(attempts));
        }
    }

    public void CommitDeadLetter(HeldMessage held, int attempts)
    {
        lock (_gate)
        {
            if (_byAck.Remove(held.AckId, out _))
            {
                _ackByMessage.Remove(held.Id);
                if (Inflight > 0)
                {
                    Inflight--;
                }
            }

            _attempts.Remove(held.Id);
            DeadLettered++;
            _quarantine.Add(new QuarantineMessage(
                held.Id,
                held.TopicId,
                held.SubscriptionId,
                held.Data,
                held.Attributes,
                attempts,
                "max_delivery_attempts",
                DateTime.UtcNow.ToString("O")));
        }
    }

    public void Restore(QuarantineMessage item)
    {
        lock (_gate)
        {
            _quarantine.Add(item);
        }
    }

    public IReadOnlyList<QuarantineMessage> Quarantine()
    {
        lock (_gate)
        {
            return _quarantine.ToList();
        }
    }

    public QuarantineMessage? TakeQuarantine(string id)
    {
        lock (_gate)
        {
            var index = _quarantine.FindIndex(item => item.Id == id);
            if (index < 0)
            {
                return null;
            }

            var item = _quarantine[index];
            _quarantine.RemoveAt(index);
            return item;
        }
    }

    public void PurgeQuarantine()
    {
        lock (_gate)
        {
            _quarantine.Clear();
        }
    }

    public void AddLog(string line)
    {
        lock (_gate)
        {
            if (LogsPaused)
            {
                return;
            }

            _logs.Add($"{DateTime.UtcNow:HH:mm:ss} {line}");
            if (_logs.Count > 200)
            {
                _logs.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<string> Logs()
    {
        lock (_gate)
        {
            return _logs.ToList();
        }
    }

    public void ClearLogs()
    {
        lock (_gate)
        {
            _logs.Clear();
        }
    }

    public OverviewSnapshot Snapshot(int topicCount, int subscriptionCount)
    {
        lock (_gate)
        {
            Touch();
            return new OverviewSnapshot(
                Published,
                Pulled,
                Acked,
                Nacked,
                DeadLettered,
                Inflight,
                topicCount,
                subscriptionCount,
                Environment.WorkingSet,
                Environment.ProcessId,
                ChaosMs,
                StrictJson,
                _published.ToArray(),
                _acked.ToArray());
        }
    }

    private void Touch()
    {
        var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        if (_slotMinute == 0)
        {
            _slotMinute = minute;
            return;
        }

        var delta = (int)Math.Min(30, minute - _slotMinute);
        if (delta <= 0)
        {
            return;
        }

        Shift(_published, delta);
        Shift(_acked, delta);
        _slotMinute = minute;
    }

    private static void Shift(int[] series, int delta)
    {
        if (delta >= series.Length)
        {
            Array.Clear(series);
            return;
        }

        Array.Copy(series, delta, series, 0, series.Length - delta);
        Array.Clear(series, series.Length - delta, delta);
    }
}

public sealed record OverviewSnapshot(
    long Published,
    long Pulled,
    long Acked,
    long Nacked,
    long DeadLettered,
    int Inflight,
    int TopicCount,
    int SubscriptionCount,
    long WorkingSetBytes,
    int Pid,
    int ChaosMs,
    bool StrictJson,
    int[] PublishSeries,
    int[] AckSeries);

public sealed class RingLoggerProvider(ConsoleActivity activity) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new RingLogger(activity, categoryName);

    public void Dispose()
    {
    }
}

file sealed class RingLogger(ConsoleActivity activity, string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        activity.AddLog($"{logLevel} {category}: {formatter(state, exception)}");
    }
}
