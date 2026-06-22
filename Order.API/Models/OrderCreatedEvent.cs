namespace Order.API.Models;

public class OrderCreatedEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string OrderId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}