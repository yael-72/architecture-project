using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SharedKernel.Middleware;

namespace SharedKernel.Messaging;

/// <summary>
/// Thread-safe singleton RabbitMQ publisher.
/// Reconnects automatically if the channel is closed.
/// </summary>
public sealed class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly string _hostName;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public RabbitMqPublisher(string hostName, ILogger<RabbitMqPublisher> logger)
    {
        _hostName = hostName;
        _logger = logger;
        EnsureConnectedWithRetry(retries: 10, delaySeconds: 5);
    }

    private void EnsureConnectedWithRetry(int retries, int delaySeconds)
    {
        for (int attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                EnsureConnected();
                return;
            }
            catch (Exception ex) when (attempt < retries)
            {
                _logger.LogWarning(ex,
                    "RabbitMQ not ready (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                    attempt, retries, delaySeconds);
                Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
            }
        }
        // Final attempt — let any exception propagate naturally
        EnsureConnected();
    }

    private void EnsureConnected()
    {
        lock (_lock)
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true)
                return;

            _channel?.Dispose();
            _connection?.Dispose();

            var factory = new ConnectionFactory
            {
                HostName = _hostName,
                DispatchConsumersAsync = true
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }
    }

    public Task PublishAsync<T>(string queueName, T message) where T : class
    {
        EnsureConnected();

        lock (_lock)
        {
            var channel = _channel ?? throw new InvalidOperationException("RabbitMQ channel is not open.");
            channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.CorrelationId = CorrelationContext.CorrelationId.ToString();

            channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: props, body: body);
        }

        _logger.LogInformation(
            "Published {EventType} → queue '{Queue}' [CorrelationId: {CorrelationId}]",
            typeof(T).Name, queueName, CorrelationContext.CorrelationId);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
