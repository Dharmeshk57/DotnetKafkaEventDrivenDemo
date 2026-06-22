namespace Order.API.Data;

public class OrderRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProductName { get; set; } = default!;
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
}
