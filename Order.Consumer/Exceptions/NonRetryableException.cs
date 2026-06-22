namespace Order.Consumer.Exceptions;

/// <summary>
/// Represents a permanent failure that should go directly to the DLQ.
/// Examples: invalid business data, constraint violations, unsupported message versions.
/// </summary>
public class NonRetryableException : Exception
{
    public NonRetryableException(string message, Exception? inner = null)
        : base(message, inner) { }
}
