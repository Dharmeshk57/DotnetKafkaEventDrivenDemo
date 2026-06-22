using Order.Consumer.Exceptions;
using System.Text.Json;

namespace Order.Consumer.Services;

public static class RetryPolicy
{
    /// <summary>
    /// Returns true for transient failures that warrant a retry.
    /// Returns false for permanent failures that should go directly to the DLQ.
    /// </summary>
    public static bool IsRetryable(Exception exception) => exception switch
    {
        // ── Non-retryable: message/data problems ──────────────────────────
        JsonException           => false,   // Invalid message format
        ArgumentException       => false,   // Invalid business data
        NotSupportedException   => false,   // Unsupported message version
        NonRetryableException   => false,   // Explicitly marked by business logic

        // ── Always propagate shutdown ──────────────────────────────────────
        OperationCanceledException => false,

        // ── Retryable: transient infrastructure failures ───────────────────
        // SqlException, HttpRequestException, TimeoutException, IOException, etc.
        _ => true
    };
}
