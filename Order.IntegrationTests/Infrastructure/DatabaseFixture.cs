namespace Order.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a real SQL Server instance in Docker once for the entire test collection.
/// Applies consumer migrations on startup so the ProcessedEvents table is ready.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var ctx = CreateConsumerDbContext();
        await ctx.Database.MigrateAsync();
    }

    public ConsumerDbContext CreateConsumerDbContext() =>
        new(new DbContextOptionsBuilder<ConsumerDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
