using Confluent.Kafka;
using Order.Consumer.Messaging;
using Order.Consumer.Security;

namespace Order.Consumer.Services;

public class DeadLetterConsumer : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DeadLetterConsumer> _logger;

    public DeadLetterConsumer(IConfiguration configuration, ILogger<DeadLetterConsumer> logger)
    {
        _configuration = configuration;
        _logger        = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"],
            GroupId          = $"{_configuration["Kafka:GroupId"]}.dlq",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        consumerConfig.ApplySecurity(_configuration);

        using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();

        consumer.Subscribe(_configuration["Kafka:DlqTopic"]);

        _logger.LogInformation("Dead letter consumer started. Topic: {Topic}",
            _configuration["Kafka:DlqTopic"]);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                LogDeadLetterMessage(result);
                consumer.Commit(result);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dead letter consumer stopping.");
            consumer.Close();
        }
    }

    private void LogDeadLetterMessage(ConsumeResult<Ignore, string> result)
    {
        var retryCount    = MessageHeaders.GetValue(result.Message.Headers, MessageHeaders.RetryCount)    ?? "0";
        var originalTopic = MessageHeaders.GetValue(result.Message.Headers, MessageHeaders.OriginalTopic) ?? result.Topic;
        var errorMessage  = MessageHeaders.GetValue(result.Message.Headers, MessageHeaders.ErrorMessage)  ?? "unknown";

        _logger.LogError(
            "DEAD LETTER — OriginalTopic: {OriginalTopic} | RetryCount: {RetryCount} | " +
            "Error: {Error} | Payload: {Payload}",
            originalTopic, retryCount, errorMessage, result.Message.Value);
    }
}
