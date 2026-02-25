using Confluent.Kafka;
using System.Text.Json;
using Order.API.Models;

namespace Order.API.Services;

public class KafkaProducer
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;

    public KafkaProducer(IConfiguration configuration)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
        _topic = configuration["Kafka:Topic"]!;
    }

    public async Task ProduceAsync(OrderCreatedEvent orderEvent)
    {
        var message = JsonSerializer.Serialize(orderEvent);

        await _producer.ProduceAsync(_topic, new Message<Null, string>
        {
            Value = message
        });
    }
}