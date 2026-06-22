using Order.IntegrationTests.Infrastructure;

namespace Order.IntegrationTests.Tests;

/// <summary>
/// Verifies that KafkaRouter routes failed messages to the correct topic
/// based on exception type and current retry count.
///
/// Each test builds a fake ConsumeResult with the appropriate x-retry-count header,
/// calls RouteFailedMessageAsync, then reads back from the destination topic to
/// assert the message arrived with the correct headers.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class RetryRoutingTests
{
    private readonly KafkaFixture _kafka;

    public RetryRoutingTests(KafkaFixture kafka, DatabaseFixture _)
    {
        _kafka = kafka;
    }

    // ── Retry escalation ───────────────────────────────────────────────────────

    [Fact]
    public async Task RetryableFailure_FirstAttempt_Should_Route_To_Retry1()
    {
        var eventId = Guid.NewGuid().ToString();
        var result  = FakeConsumeResult("order-events", Payload(eventId), retryCount: 0);
        using var router = BuildRouter();

        await router.RouteFailedMessageAsync(result, new RetryableException("DB timeout"), CancellationToken.None);

        var message = KafkaHelper.ConsumeMatching(
            _kafka.BootstrapServers,
            topic:     "order-events.retry-1",
            groupId:   $"assert-{Guid.NewGuid():N}",
            predicate: r => r.Message.Value.Contains(eventId));

        Assert.NotNull(message);
        Assert.Equal(1, MessageHeaders.GetRetryCount(message.Message.Headers));
        Assert.NotNull(MessageHeaders.GetProcessAfter(message.Message.Headers));
        Assert.Equal("order-events",
            MessageHeaders.GetValue(message.Message.Headers, MessageHeaders.OriginalTopic));
    }

    [Fact]
    public async Task RetryableFailure_AfterRetry1_Should_Route_To_Retry5()
    {
        // Simulate a message that was already routed through retry-1 (retryCount = 1)
        var eventId = Guid.NewGuid().ToString();
        var result  = FakeConsumeResult("order-events.retry-1", Payload(eventId), retryCount: 1);
        using var router = BuildRouter();

        await router.RouteFailedMessageAsync(result, new RetryableException("Service unavailable"), CancellationToken.None);

        var message = KafkaHelper.ConsumeMatching(
            _kafka.BootstrapServers,
            topic:     "order-events.retry-5",
            groupId:   $"assert-{Guid.NewGuid():N}",
            predicate: r => r.Message.Value.Contains(eventId));

        Assert.NotNull(message);
        Assert.Equal(2, MessageHeaders.GetRetryCount(message.Message.Headers));
        Assert.NotNull(MessageHeaders.GetProcessAfter(message.Message.Headers));
    }

    [Fact]
    public async Task RetryableFailure_AfterRetry5_Should_Route_To_DLQ()
    {
        // Simulate all retries exhausted (retryCount = 2 — retry-5 is the last stop)
        var eventId = Guid.NewGuid().ToString();
        var result  = FakeConsumeResult("order-events.retry-5", Payload(eventId), retryCount: 2);
        using var router = BuildRouter();

        await router.RouteFailedMessageAsync(result, new RetryableException("Persistent failure"), CancellationToken.None);

        var message = KafkaHelper.ConsumeMatching(
            _kafka.BootstrapServers,
            topic:     "order-events.dlq",
            groupId:   $"assert-{Guid.NewGuid():N}",
            predicate: r => r.Message.Value.Contains(eventId));

        Assert.NotNull(message);
        Assert.Equal(3, MessageHeaders.GetRetryCount(message.Message.Headers));
        Assert.Null(MessageHeaders.GetProcessAfter(message.Message.Headers));
    }

    // ── Non-retryable → DLQ immediately ───────────────────────────────────────

    [Fact]
    public async Task NonRetryable_JsonException_Should_Skip_Retries_And_Go_To_DLQ()
    {
        var eventId = Guid.NewGuid().ToString();
        var result  = FakeConsumeResult("order-events", $"{{ broken-{eventId} }}", retryCount: 0);
        using var router = BuildRouter();

        await router.RouteFailedMessageAsync(result, new JsonException("Unexpected token"), CancellationToken.None);

        var message = KafkaHelper.ConsumeMatching(
            _kafka.BootstrapServers,
            topic:     "order-events.dlq",
            groupId:   $"assert-{Guid.NewGuid():N}",
            predicate: r => r.Message.Value.Contains(eventId));

        Assert.NotNull(message);
        // retryCount not incremented for non-retryable failures
        Assert.Equal(0, MessageHeaders.GetRetryCount(message.Message.Headers));
        // No delay header on DLQ messages
        Assert.Null(MessageHeaders.GetProcessAfter(message.Message.Headers));
        // Error message is captured in headers
        Assert.Contains("Unexpected token",
            MessageHeaders.GetValue(message.Message.Headers, MessageHeaders.ErrorMessage));
    }

    [Fact]
    public async Task NonRetryable_NonRetryableException_Should_Go_To_DLQ()
    {
        var eventId = Guid.NewGuid().ToString();
        var result  = FakeConsumeResult("order-events", Payload(eventId), retryCount: 0);
        using var router = BuildRouter();

        await router.RouteFailedMessageAsync(
            result,
            new NonRetryableException("OrderId missing"),
            CancellationToken.None);

        var message = KafkaHelper.ConsumeMatching(
            _kafka.BootstrapServers,
            topic:     "order-events.dlq",
            groupId:   $"assert-{Guid.NewGuid():N}",
            predicate: r => r.Message.Value.Contains(eventId));

        Assert.NotNull(message);
        Assert.Equal(0, MessageHeaders.GetRetryCount(message.Message.Headers));
        Assert.Contains("OrderId missing",
            MessageHeaders.GetValue(message.Message.Headers, MessageHeaders.ErrorMessage));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private KafkaRouter BuildRouter() =>
        new KafkaRouter(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Kafka:BootstrapServers"] = _kafka.BootstrapServers,
                    ["Kafka:Retry1Topic"]      = "order-events.retry-1",
                    ["Kafka:Retry5Topic"]      = "order-events.retry-5",
                    ["Kafka:DlqTopic"]         = "order-events.dlq"
                })
                .Build(),
            NullLogger<KafkaRouter>.Instance);

    /// <summary>
    /// Creates a fake ConsumeResult that simulates a message arriving from Kafka
    /// with the specified retry count already in its headers.
    /// </summary>
    private static ConsumeResult<Ignore, string> FakeConsumeResult(
        string topic, string payload, int retryCount)
    {
        var headers = new Headers();

        if (retryCount > 0)
            headers.Add(
                MessageHeaders.RetryCount,
                Encoding.UTF8.GetBytes(retryCount.ToString()));

        return new ConsumeResult<Ignore, string>
        {
            Topic   = topic,
            Message = new Message<Ignore, string>
            {
                Value   = payload,
                Headers = headers
            }
        };
    }

    private static string Payload(string eventId) =>
        JsonSerializer.Serialize(new ConsumerOrderEvent
        {
            EventId     = eventId,
            OrderId     = $"ORD-{eventId[..8]}",
            ProductName = "Retry Test Product",
            Price       = 25.00m
        });
}
