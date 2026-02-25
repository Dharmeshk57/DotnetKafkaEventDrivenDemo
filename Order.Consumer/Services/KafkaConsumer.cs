using Confluent.Kafka;
using System.Text.Json;
using Order.Consumer;
using Microsoft.Extensions.Hosting;

namespace Order.Consumer.Services;

public class KafkaConsumer : BackgroundService
{
    private readonly IConfiguration _configuration;

    public KafkaConsumer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"],
            GroupId = _configuration["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(_configuration["Kafka:Topic"]);

        return Task.Run(() =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value);

                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine($"Order Received: {orderEvent?.OrderId}");
                Console.WriteLine($"Product: {orderEvent?.ProductName}");
                Console.WriteLine($"Price: {orderEvent?.Price}");
                Console.WriteLine("--------------------------------------------------");
            }
        }, stoppingToken);
    }
}