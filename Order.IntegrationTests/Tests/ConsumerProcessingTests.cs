using Order.IntegrationTests.Infrastructure;

namespace Order.IntegrationTests.Tests;

/// <summary>
/// End-to-end test: produces a message to Kafka, starts a real KafkaConsumer host,
/// and polls the database until the ProcessedEvent appears.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class ConsumerProcessingTests
{
    private readonly KafkaFixture    _kafka;
    private readonly DatabaseFixture _db;

    public ConsumerProcessingTests(KafkaFixture kafka, DatabaseFixture db)
    {
        _kafka = kafka;
        _db    = db;
    }

    [Fact]
    public async Task Consumer_Should_Process_Event_And_Record_In_Database()
    {
        // Arrange — produce a valid event before starting the consumer
        var eventId = Guid.NewGuid().ToString();
        var payload = Payload(eventId, "ORD-E2E-001", "Widget Pro", 75.00m);

        await KafkaHelper.ProduceAsync(_kafka.BootstrapServers, "order-events", payload);

        // Act — start the consumer; AutoOffsetReset.Earliest means it reads from the beginning
        using var host = BuildConsumerHost(groupId: $"e2e-{Guid.NewGuid():N}");
        await host.StartAsync();

        var processed = await WaitForAsync(async () =>
        {
            using var ctx = _db.CreateConsumerDbContext();
            return await ctx.ProcessedEvents.AnyAsync(x => x.EventId == eventId);
        }, timeout: TimeSpan.FromSeconds(30));

        await host.StopAsync();

        // Assert
        Assert.True(processed, $"EventId {eventId} was not in ProcessedEvents within the timeout.");
    }

    [Fact]
    public async Task Consumer_Should_Store_ProcessedAt_Timestamp()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        var before  = DateTime.UtcNow;

        await KafkaHelper.ProduceAsync(
            _kafka.BootstrapServers, "order-events", Payload(eventId, "ORD-E2E-002", "Gadget", 120.00m));

        using var host = BuildConsumerHost(groupId: $"ts-{Guid.NewGuid():N}");
        await host.StartAsync();

        await WaitForAsync(async () =>
        {
            using var ctx = _db.CreateConsumerDbContext();
            return await ctx.ProcessedEvents.AnyAsync(x => x.EventId == eventId);
        }, TimeSpan.FromSeconds(30));

        await host.StopAsync();

        // Assert — timestamp is set and is after the test started
        using var ctx = _db.CreateConsumerDbContext();
        var record = await ctx.ProcessedEvents.SingleAsync(x => x.EventId == eventId);

        Assert.True(record.ProcessedAt >= before,
            "ProcessedAt should be on or after the time the test started.");
        Assert.True(record.ProcessedAt <= DateTime.UtcNow,
            "ProcessedAt should not be in the future.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private IHost BuildConsumerHost(string groupId) =>
        new HostBuilder()
            .ConfigureAppConfiguration(cfg =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Kafka:BootstrapServers"] = _kafka.BootstrapServers,
                    ["Kafka:Topic"]            = "order-events",
                    ["Kafka:GroupId"]          = groupId,
                    ["Kafka:Retry1Topic"]      = "order-events.retry-1",
                    ["Kafka:Retry5Topic"]      = "order-events.retry-5",
                    ["Kafka:DlqTopic"]         = "order-events.dlq"
                }))
            .ConfigureServices((ctx, services) =>
            {
                services.AddDbContext<ConsumerDbContext>(opt =>
                    opt.UseSqlServer(_db.ConnectionString));
                services.AddSingleton<KafkaRouter>();
                services.AddSingleton<OrderProcessingService>();
                services.AddHostedService<KafkaConsumer>();
            })
            .ConfigureLogging(log => log.SetMinimumLevel(LogLevel.Warning))
            .Build();

    private static string Payload(string eventId, string orderId, string product, decimal price) =>
        JsonSerializer.Serialize(new ConsumerOrderEvent
        {
            EventId     = eventId,
            OrderId     = orderId,
            ProductName = product,
            Price       = price
        });

    private static async Task<bool> WaitForAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            try
            {
                if (await condition()) return true;
                await Task.Delay(500, cts.Token);
            }
            catch (OperationCanceledException) { break; }
        }
        return false;
    }
}
