namespace Order.IntegrationTests.Infrastructure;

/// <summary>
/// Shares a single Kafka container and SQL Server container across all test classes
/// in the "Integration" collection. Containers start once, all tests run, then stop.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationCollection
    : ICollectionFixture<KafkaFixture>,
      ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Integration";
}
