namespace Order.Consumer;

public class OrderCreatedEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public double Price { get; set; }
}