using Confluent.Kafka;
using System.Text;

namespace Order.Consumer.Messaging;

public static class MessageHeaders
{
    public const string RetryCount    = "x-retry-count";
    public const string ProcessAfter  = "x-process-after";
    public const string OriginalTopic = "x-original-topic";
    public const string ErrorMessage  = "x-error-message";

    public static string? GetValue(Headers headers, string key)
    {
        var header = headers.FirstOrDefault(h => h.Key == key);
        return header is null ? null : Encoding.UTF8.GetString(header.GetValueBytes());
    }

    public static int GetRetryCount(Headers headers)
    {
        var value = GetValue(headers, RetryCount);
        return int.TryParse(value, out var count) ? count : 0;
    }

    public static DateTime? GetProcessAfter(Headers headers)
    {
        var value = GetValue(headers, ProcessAfter);
        return DateTime.TryParse(value, out var dt) ? dt : null;
    }
}
