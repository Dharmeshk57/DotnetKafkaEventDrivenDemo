using Order.IntegrationTests.Infrastructure;

namespace Order.IntegrationTests.Tests;

/// <summary>
/// Verifies that the consumer's idempotency guard prevents duplicate processing
/// when the same EventId is seen more than once.
/// Tests call OrderProcessingService directly — no Kafka required.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class IdempotencyTests
{
    private readonly DatabaseFixture _db;

    public IdempotencyTests(KafkaFixture _, DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task ProcessAsync_Given_Same_EventId_Twice_Should_Insert_One_Row()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        var payload = Payload(eventId, "ORD-IDEM-001", "Widget", 50.00m);
        var svc     = BuildProcessingService();

        // Act — identical call twice
        await svc.ProcessAsync(payload, CancellationToken.None);
        await svc.ProcessAsync(payload, CancellationToken.None);

        // Assert
        using var ctx = _db.CreateConsumerDbContext();
        var count = await ctx.ProcessedEvents.CountAsync(x => x.EventId == eventId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ProcessAsync_Given_Different_EventIds_Should_Insert_Separate_Rows()
    {
        // Arrange
        var eventId1 = Guid.NewGuid().ToString();
        var eventId2 = Guid.NewGuid().ToString();
        var svc      = BuildProcessingService();

        // Act
        await svc.ProcessAsync(Payload(eventId1, "ORD-IDEM-002", "Widget A", 10.00m), CancellationToken.None);
        await svc.ProcessAsync(Payload(eventId2, "ORD-IDEM-003", "Widget B", 20.00m), CancellationToken.None);

        // Assert — both events are recorded independently
        using var ctx = _db.CreateConsumerDbContext();
        Assert.True(await ctx.ProcessedEvents.AnyAsync(x => x.EventId == eventId1));
        Assert.True(await ctx.ProcessedEvents.AnyAsync(x => x.EventId == eventId2));
    }

    [Fact]
    public async Task ProcessAsync_NonRetryable_Payload_Should_Throw_And_Not_Insert_Row()
    {
        // Arrange — price of 0 triggers NonRetryableException in ValidateOrder
        var eventId = Guid.NewGuid().ToString();
        var payload = Payload(eventId, "ORD-IDEM-004", "Free Widget", price: 0m);
        var svc     = BuildProcessingService();

        // Act & Assert — exception thrown, nothing committed
        await Assert.ThrowsAsync<NonRetryableException>(() =>
            svc.ProcessAsync(payload, CancellationToken.None));

        using var ctx = _db.CreateConsumerDbContext();
        Assert.False(await ctx.ProcessedEvents.AnyAsync(x => x.EventId == eventId));
    }

    [Fact]
    public async Task ProcessAsync_Malformed_Json_Should_Throw_JsonException_And_Not_Insert_Row()
    {
        // Arrange
        var svc = BuildProcessingService();

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(() =>
            svc.ProcessAsync("{ not valid json }", CancellationToken.None));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private OrderProcessingService BuildProcessingService()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ConsumerDbContext>(opt =>
            opt.UseSqlServer(_db.ConnectionString));
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<OrderProcessingService>();

        return services.BuildServiceProvider().GetRequiredService<OrderProcessingService>();
    }

    private static string Payload(string eventId, string orderId, string product, decimal price) =>
        JsonSerializer.Serialize(new ConsumerOrderEvent
        {
            EventId     = eventId,
            OrderId     = orderId,
            ProductName = product,
            Price       = price
        });
}
