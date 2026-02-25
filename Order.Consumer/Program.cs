using Order.Consumer.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<KafkaConsumer>();

var host = builder.Build();
host.Run();