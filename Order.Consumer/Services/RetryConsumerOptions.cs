namespace Order.Consumer.Services;

public record RetryConsumerOptions(string Topic, string GroupIdSuffix);
