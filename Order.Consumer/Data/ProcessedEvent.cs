namespace Order.Consumer.Data;

public class ProcessedEvent
{
    public string EventId { get; set; } = default!;
    public DateTime ProcessedAt { get; set; }
}
