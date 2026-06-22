using Order.IntegrationTests.Infrastructure;

namespace Order.IntegrationTests.Tests;

/// <summary>
/// Verifies that KafkaProducer.PublishAsync delivers a message to the broker
/// and that a Kafka consumer can read it back.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class PublisherTests
{
    private readonly KafkaFixture _kafka;

    public PublisherTests(KafkaFixture kafka, DatabaseFixture _)
    {
        _kafka = kafka;
    }

    [Fact]
    public async Task PublishAsync_Should_Deliver_Message_To_Topic()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        var payload = JsonSerializer.Serialize(new ConsumerOrderEvent
        {
            EventId     = eventId,
            OrderId     = "ORD-PUB-001",
            ProductName = "Integration Widget",
            Price       = 49.99m
        });

        var config   = BuildConfig(_kafka.BootstrapServers);
        using var producer = new Order.API.Services.KafkaProducer(config);

        // Act
        await producer.PublishAsync("order-events", payload);

        // Assert — consume from the topic and verify the payload arrived
        var message = KafkaHelper.ConsumeMatching(
            _kafka.BootstrapServers,
            topic:   "order-events",
            groupId: $"assert-pub-{Guid.NewGuid():N}",
            predicate: r => r.Message.Value.Contains(eventId));

        Assert.NotNull(message);
        Assert.Contains(eventId, message.Message.Value);
    }

    [Fact]
    public async Task PublishAsync_Should_Preserve_Full_Payload()
    {
        // Arrange
        var eventId     = Guid.NewGuid().ToString();
        var orderId     = "ORD-PUB-002";
        var productName = "Precision Sensor";
        var price       = 299.95m;

        var payload = JsonSerializer.Serialize(new ConsumerOrderEvent
        {
            EventId     = eventId,
            OrderId     = orderId,
            ProductName = productName,
            Price       = price
        });

        using var producer = new Order.API.Services.KafkaProducer(BuildConfig(_kafka.BootstrapServers));

        // Act
        await producer.PublishAsync("order-events", payload);

        // Assert — deserialize from Kafka and verify all fields survived the round trip
        var message = KafkaHelper.ConsumeMatching(
            _kafka.BootstrapServers,
            topic:   "order-events",
            groupId: $"assert-pub-{Guid.NewGuid():N}",
            predicate: r => r.Message.Value.Contains(eventId));

        Assert.NotNull(message);

        var received = JsonSerializer.Deserialize<ConsumerOrderEvent>(message.Message.Value);
        Assert.NotNull(received);
        Assert.Equal(eventId,     received.EventId);
        Assert.Equal(orderId,     received.OrderId);
        Assert.Equal(productName, received.ProductName);
        Assert.Equal(price,       received.Price);
    }

    private static IConfiguration BuildConfig(string bootstrapServers) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = bootstrapServers
            })
            .Build();
}
