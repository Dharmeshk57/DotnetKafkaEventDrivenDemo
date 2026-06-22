using Confluent.Kafka;
using Order.Consumer.Security;

namespace Order.Consumer.Services;

public class KafkaConsumer : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly OrderProcessingService _processingService;
    private readonly KafkaRouter _router;
    private readonly ILogger<KafkaConsumer> _logger;

    public KafkaConsumer(
        IConfiguration configuration,
        OrderProcessingService processingService,
        KafkaRouter router,
        ILogger<KafkaConsumer> logger)
    {
        _configuration    = configuration;
        _processingService = processingService;
        _router           = router;
        _logger           = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"],
            GroupId          = _configuration["Kafka:GroupId"],
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        consumerConfig.ApplySecurity(_configuration);

        using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();

        consumer.Subscribe(_configuration["Kafka:Topic"]);

        _logger.LogInformation("Kafka consumer started. Topic: {Topic}", _configuration["Kafka:Topic"]);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                await ProcessMessageAsync(consumer, result, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Kafka consumer stopping.");
            consumer.Close();
        }
    }

    private async Task ProcessMessageAsync(
        IConsumer<Ignore, string> consumer,
        ConsumeResult<Ignore, string> result,
        CancellationToken stoppingToken)
    {
        try
        {
            await _processingService.ProcessAsync(result.Message.Value, stoppingToken);
            consumer.Commit(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed. Routing message to retry or DLQ.");
            await _router.RouteFailedMessageAsync(result, ex, stoppingToken);
            consumer.Commit(result);
        }
    }
}
