using Confluent.Kafka;
using Order.API.Security;

namespace Order.API.Services;

public class KafkaProducer : IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaProducer(IConfiguration configuration)
    {
        var config = new ProducerConfig
        {
            BootstrapServers  = configuration["Kafka:BootstrapServers"],
            Acks              = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = int.MaxValue,

            CompressionType = CompressionType.Zstd,
            LingerMs        = 5,
            BatchSize       = 64 * 1024
        };

        config.ApplySecurity(configuration);

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(string topic, string payload)
    {
        await _producer.ProduceAsync(topic, new Message<string, string>
        {
            Value = payload
        });
    }

    public void Dispose() => _producer.Dispose();
}
