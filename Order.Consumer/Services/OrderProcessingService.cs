using Microsoft.EntityFrameworkCore;
using Order.Consumer.Data;
using System.Text.Json;

namespace Order.Consumer.Services;

/// <summary>
/// Shared processing logic used by both the main consumer and retry consumers
/// to avoid duplication of the idempotency check and business logic.
/// </summary>
public class OrderProcessingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderProcessingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Deserializes, checks idempotency, applies business logic, and marks the event processed.
    /// Throws JsonException for malformed payloads (non-retryable).
    /// Throws NonRetryableException for invalid business data (non-retryable).
    /// All other exceptions (e.g., SqlException) are retryable.
    /// </summary>
    public async Task ProcessAsync(string rawPayload, CancellationToken ct)
    {
        // JsonException bubbles as non-retryable — RetryPolicy will send directly to DLQ
        var order = JsonSerializer.Deserialize<OrderCreatedEvent>(rawPayload)
            ?? throw new JsonException("Deserialized event was null.");

        ValidateOrder(order);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var alreadyProcessed = await dbContext.ProcessedEvents
            .AnyAsync(x => x.EventId == order.EventId, ct);

        if (alreadyProcessed)
        {
            _logger.LogInformation("Duplicate event skipped: {EventId}", order.EventId);
            return;
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            ApplyBusinessLogic(order);

            dbContext.ProcessedEvents.Add(new ProcessedEvent
            {
                EventId = order.EventId,
                ProcessedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Order processed — EventId: {EventId} | OrderId: {OrderId}",
                order.EventId, order.OrderId);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static void ValidateOrder(OrderCreatedEvent order)
    {
        if (string.IsNullOrWhiteSpace(order.OrderId))
            throw new Exceptions.NonRetryableException("OrderId is required.");

        if (order.Price <= 0)
            throw new Exceptions.NonRetryableException($"Invalid price: {order.Price}.");
    }

    private void ApplyBusinessLogic(OrderCreatedEvent order)
    {
        _logger.LogInformation(
            "Order received — Id: {OrderId} | Product: {Product} | Price: {Price}",
            order.OrderId, order.ProductName, order.Price);
    }
}
