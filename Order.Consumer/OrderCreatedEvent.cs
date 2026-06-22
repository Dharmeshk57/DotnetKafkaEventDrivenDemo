namespace Order.Consumer;

public class OrderCreatedEvent
{
    public string EventId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}