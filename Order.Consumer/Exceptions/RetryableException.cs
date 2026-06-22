namespace Order.Consumer.Exceptions;

/// <summary>
/// Represents a transient failure that should be retried.
/// Examples: network timeouts, temporary DB outages, external service unavailability.
/// </summary>
public class RetryableException : Exception
{
    public RetryableException(string message, Exception? inner = null)
        : base(message, inner) { }
}
