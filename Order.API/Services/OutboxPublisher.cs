using Microsoft.EntityFrameworkCore;
using Order.API.Data;

namespace Order.API.Services;

public class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisher> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    public OutboxPublisher(IServiceScopeFactory scopeFactory, ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox publisher started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await PublishPendingMessagesAsync(stoppingToken);
                await Task.Delay(_pollingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Outbox publisher stopping.");
        }
    }

    private async Task PublishPendingMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var producer = scope.ServiceProvider.GetRequiredService<KafkaProducer>();

        var pending = await dbContext.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(stoppingToken);

        foreach (var message in pending)
        {
            try
            {
                await producer.PublishAsync(message.Topic, message.Payload);

                message.PublishedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Outbox message published: {Id}", message.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to publish outbox message: {Id}", message.Id);
            }
        }
    }
}
