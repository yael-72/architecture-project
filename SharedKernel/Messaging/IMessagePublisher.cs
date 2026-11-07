namespace SharedKernel.Messaging;

/// <summary>Publishes a message to the specified RabbitMQ queue.</summary>
public interface IMessagePublisher
{
    Task PublishAsync<T>(string queueName, T message) where T : class;
}
