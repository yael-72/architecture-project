using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Events;
using SharedKernel.Messaging;
using SharedKernel.Middleware;

namespace NotificationService.Messaging;

/// <summary>
/// Saga Step 5b — Consumes OrderCancelled and notifies the customer of the rejection.
/// </summary>
public class OrderCancelledConsumer : RabbitMqConsumerBase<OrderCancelledEvent>
{
    public OrderCancelledConsumer(
        string hostName,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderCancelledConsumer> logger)
        : base(hostName, QueueNames.OrderCancelledNotify, scopeFactory, logger)
    {
    }

    protected override async Task HandleAsync(
        OrderCancelledEvent message,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        CorrelationContext.CorrelationId = message.CorrelationId;

        var notificationStore = scope.ServiceProvider.GetRequiredService<Services.NotificationStore>();
        var notification = new Data.NotificationDocument
        {
            OrderId = message.OrderId,
            EventType = "OrderCancelled",
            Message = $"Order cancelled for user {message.UserId}. Reason: {message.Reason}.",
            OccurredAt = DateTime.UtcNow,
            Payload = new { message.UserId, message.Reason, message.CorrelationId }
        };

        await notificationStore.SaveAsync(notification);

        Logger.LogWarning(
            "NOTIFICATION SENT → Order #{OrderId} CANCELLED for UserId {UserId}. " +
            "Reason: {Reason}. [CorrelationId: {CorrelationId}]",
            message.OrderId, message.UserId, message.Reason, message.CorrelationId);
    }
}
