using Confluent.Kafka;
using Order.Consumer.Messaging;
using Order.Consumer.Security;
using System.Text;

namespace Order.Consumer.Services;

public sealed class KafkaRouter : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _retry1Topic;
    private readonly string _retry5Topic;
    private readonly string _dlqTopic;
    private readonly ILogger<KafkaRouter> _logger;

    public KafkaRouter(IConfiguration configuration, ILogger<KafkaRouter> logger)
    {
        _logger = logger;
        _retry1Topic = configuration["Kafka:Retry1Topic"]!;
        _retry5Topic = configuration["Kafka:Retry5Topic"]!;
        _dlqTopic    = configuration["Kafka:DlqTopic"]!;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            Acks             = Acks.All
        };

        producerConfig.ApplySecurity(configuration);

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    /// <summary>
    /// Routes a failed message to the next retry topic or DLQ based on:
    /// - Whether the exception is retryable (RetryPolicy classification)
    /// - How many retries have already occurred (x-retry-count header)
    /// </summary>
    public async Task RouteFailedMessageAsync(
        ConsumeResult<Ignore, string> failed,
        Exception ex,
        CancellationToken ct)
    {
        var retryCount    = MessageHeaders.GetRetryCount(failed.Message.Headers);
        var originalTopic = MessageHeaders.GetValue(failed.Message.Headers, MessageHeaders.OriginalTopic)
                            ?? failed.Topic;

        if (!RetryPolicy.IsRetryable(ex))
        {
            _logger.LogWarning(
                "Non-retryable failure — routing to DLQ. " +
                "Topic: {Topic} | RetryCount: {Count} | Error: {Error}",
                failed.Topic, retryCount, ex.Message);

            await PublishAsync(_dlqTopic, failed.Message.Value,
                originalTopic, retryCount, ex.Message, processAfter: null, ct);
            return;
        }

        var nextCount = retryCount + 1;

        switch (nextCount)
        {
            case 1:
                _logger.LogWarning(
                    "Transient failure — routing to retry-1. RetryCount: {Count} | Error: {Error}",
                    nextCount, ex.Message);
                await PublishAsync(_retry1Topic, failed.Message.Value,
                    originalTopic, nextCount, ex.Message, DateTime.UtcNow.AddMinutes(1), ct);
                break;

            case 2:
                _logger.LogWarning(
                    "Retry-1 exhausted — routing to retry-5. RetryCount: {Count} | Error: {Error}",
                    nextCount, ex.Message);
                await PublishAsync(_retry5Topic, failed.Message.Value,
                    originalTopic, nextCount, ex.Message, DateTime.UtcNow.AddMinutes(5), ct);
                break;

            default:
                _logger.LogWarning(
                    "All retries exhausted — routing to DLQ. RetryCount: {Count} | Error: {Error}",
                    nextCount, ex.Message);
                await PublishAsync(_dlqTopic, failed.Message.Value,
                    originalTopic, nextCount, ex.Message, processAfter: null, ct);
                break;
        }
    }

    private async Task PublishAsync(
        string destination,
        string payload,
        string originalTopic,
        int retryCount,
        string errorMessage,
        DateTime? processAfter,
        CancellationToken ct)
    {
        var headers = new Headers
        {
            { MessageHeaders.RetryCount,    Encoding.UTF8.GetBytes(retryCount.ToString()) },
            { MessageHeaders.OriginalTopic, Encoding.UTF8.GetBytes(originalTopic) },
            { MessageHeaders.ErrorMessage,  Encoding.UTF8.GetBytes(errorMessage[..Math.Min(errorMessage.Length, 500)]) }
        };

        if (processAfter.HasValue)
            headers.Add(MessageHeaders.ProcessAfter,
                Encoding.UTF8.GetBytes(processAfter.Value.ToString("O")));

        await _producer.ProduceAsync(destination,
            new Message<string, string> { Value = payload, Headers = headers }, ct);
    }

    public void Dispose() => _producer.Dispose();
}
