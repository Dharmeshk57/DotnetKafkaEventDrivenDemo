namespace Order.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a real Kafka broker in Docker once for the entire test collection.
/// Shared by all tests — use unique topic/group IDs per test to avoid interference.
/// </summary>
public class KafkaFixture : IAsyncLifetime
{
    private readonly KafkaContainer _container = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.7.1")
        .Build();

    public string BootstrapServers => _container.GetBootstrapAddress();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
