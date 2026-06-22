using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Order.API.Data;
using Order.API.Models;

namespace Order.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public OrdersController(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] OrderCreatedEvent request)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            var order = new OrderRecord
            {
                ProductName = request.ProductName,
                Price = request.Price,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Orders.Add(order);

            var eventPayload = JsonSerializer.Serialize(new OrderCreatedEvent
            {
                EventId = request.EventId,
                OrderId = order.Id.ToString(),
                ProductName = order.ProductName,
                Price = order.Price
            });

            _dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Topic = _configuration["Kafka:Topic"]!,
                Payload = eventPayload,
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Order created", orderId = order.Id });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
