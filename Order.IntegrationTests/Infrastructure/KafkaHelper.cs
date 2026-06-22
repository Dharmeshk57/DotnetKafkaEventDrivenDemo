namespace Order.IntegrationTests.Infrastructure;

public static class KafkaHelper
{
    /// <summary>
    /// Produces a single message to the specified Kafka topic.
    /// </summary>
    public static async Task ProduceAsync(
        string bootstrapServers,
        string topic,
        string payload,
        Headers? headers = null)
    {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers
        }).Build();

        await producer.ProduceAsync(topic, new Message<string, string>
        {
            Key     = Guid.NewGuid().ToString(),
            Value   = payload,
            Headers = headers ?? new Headers()
        });
    }

    /// <summary>
    /// Consumes the first message on a topic, starting from the beginning.
    /// Returns null if no message arrives within the timeout.
    /// </summary>
    public static ConsumeResult<Ignore, string>? ConsumeOne(
        string bootstrapServers,
        string topic,
        string groupId,
        int timeoutMs = 10_000) =>
        ConsumeMatching(bootstrapServers, topic, groupId, _ => true, timeoutMs);

    /// <summary>
    /// Consumes messages from the beginning of a topic until one satisfies
    /// <paramref name="predicate"/> or the timeout expires.
    /// Use this to find a specific message when a topic may contain messages
    /// from prior tests in the same run.
    /// </summary>
    public static ConsumeResult<Ignore, string>? ConsumeMatching(
        string bootstrapServers,
        string topic,
        string groupId,
        Func<ConsumeResult<Ignore, string>, bool> predicate,
        int timeoutMs = 10_000)
    {
        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId          = groupId,
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();

        consumer.Subscribe(topic);

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            var result = consumer.Consume(TimeSpan.FromMilliseconds(300));

            if (result?.Message is not null && predicate(result))
            {
                consumer.Close();
                return result;
            }
        }

        consumer.Close();
        return null;
    }
}
