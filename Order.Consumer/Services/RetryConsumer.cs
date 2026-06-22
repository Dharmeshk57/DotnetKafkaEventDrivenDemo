using Confluent.Kafka;
using Order.Consumer.Messaging;
using Order.Consumer.Security;

namespace Order.Consumer.Services;

public class RetryConsumer : BackgroundService
{
    private readonly RetryConsumerOptions _options;
    private readonly IConfiguration _configuration;
    private readonly OrderProcessingService _processingService;
    private readonly KafkaRouter _router;
    private readonly ILogger<RetryConsumer> _logger;

    public RetryConsumer(
        RetryConsumerOptions options,
        IConfiguration configuration,
        OrderProcessingService processingService,
        KafkaRouter router,
        ILogger<RetryConsumer> logger)
    {
        _options           = options;
        _configuration     = configuration;
        _processingService = processingService;
        _router            = router;
        _logger            = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"],
            GroupId          = $"{_configuration["Kafka:GroupId"]}.{_options.GroupIdSuffix}",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        consumerConfig.ApplySecurity(_configuration);

        using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();

        consumer.Subscribe(_options.Topic);

        _logger.LogInformation("Retry consumer started. Topic: {Topic}", _options.Topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                await HandleRetryMessageAsync(consumer, result, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Retry consumer stopping. Topic: {Topic}", _options.Topic);
            consumer.Close();
        }
    }

    private async Task HandleRetryMessageAsync(
        IConsumer<Ignore, string> consumer,
        ConsumeResult<Ignore, string> result,
        CancellationToken stoppingToken)
    {
        await WaitUntilReadyAsync(result.Message.Headers, stoppingToken);

        try
        {
            await _processingService.ProcessAsync(result.Message.Value, stoppingToken);
            consumer.Commit(result);

            _logger.LogInformation(
                "Retry message processed successfully. Topic: {Topic}", _options.Topic);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Retry processing failed. Topic: {Topic} | Error: {Error}",
                _options.Topic, ex.Message);

            await _router.RouteFailedMessageAsync(result, ex, stoppingToken);
            consumer.Commit(result);
        }
    }

    private async Task WaitUntilReadyAsync(Headers headers, CancellationToken stoppingToken)
    {
        var processAfter = MessageHeaders.GetProcessAfter(headers);
        if (processAfter is null) return;

        var delay = processAfter.Value - DateTime.UtcNow;
        if (delay <= TimeSpan.Zero) return;

        _logger.LogInformation(
            "Message not ready. Waiting {Minutes}m {Seconds}s before processing.",
            (int)delay.TotalMinutes, delay.Seconds);

        await Task.Delay(delay, stoppingToken);
    }
}
