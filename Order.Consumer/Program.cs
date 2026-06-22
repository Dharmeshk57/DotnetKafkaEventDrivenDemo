using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Consumer.Data;
using Order.Consumer.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Singletons: stateless or manage their own thread-safety
builder.Services.AddSingleton<KafkaRouter>();
builder.Services.AddSingleton<OrderProcessingService>();

// Main consumer
builder.Services.AddHostedService<KafkaConsumer>();

// Retry consumers — same class, different topics and consumer group suffixes
builder.Services.AddSingleton<IHostedService>(sp =>
    ActivatorUtilities.CreateInstance<RetryConsumer>(
        sp, new RetryConsumerOptions(
            Topic: sp.GetRequiredService<IConfiguration>()["Kafka:Retry1Topic"]!,
            GroupIdSuffix: "retry-1")));

builder.Services.AddSingleton<IHostedService>(sp =>
    ActivatorUtilities.CreateInstance<RetryConsumer>(
        sp, new RetryConsumerOptions(
            Topic: sp.GetRequiredService<IConfiguration>()["Kafka:Retry5Topic"]!,
            GroupIdSuffix: "retry-5")));

// DLQ observer — logs all dead-lettered messages
builder.Services.AddHostedService<DeadLetterConsumer>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
