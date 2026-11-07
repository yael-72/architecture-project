using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderService.Interfaces;
using SharedKernel.Events;
using SharedKernel.Messaging;
using SharedKernel.Middleware;

namespace OrderService.Messaging;

/// <summary>
/// Saga Step 3a (Happy Path) — Consumes InventoryReserved from InventoryService.
/// Updates order status to Confirmed, then publishes OrderConfirmed
/// so NotificationService can inform the customer.
/// </summary>
public class InventoryReservedConsumer : RabbitMqConsumerBase<InventoryReservedEvent>
{
    private readonly IMessagePublisher _publisher;

    public InventoryReservedConsumer(
        string hostName,
        IMessagePublisher publisher,
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryReservedConsumer> logger)
        : base(hostName, QueueNames.InventoryReserved, scopeFactory, logger)
    {
        _publisher = publisher;
    }

    protected override async Task HandleAsync(
        InventoryReservedEvent message,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        CorrelationContext.CorrelationId = message.CorrelationId;
        var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        var order = await repo.GetByIdAsync(message.OrderId);
        if (order == null)
        {
            Logger.LogWarning("InventoryReserved received for unknown OrderId {OrderId}", message.OrderId);
            return;
        }

        order.Status = "Confirmed";
        await repo.UpdateAsync(order);

        Logger.LogInformation(
            "ORDER CONFIRMED: OrderId {OrderId} → status set to Confirmed [CorrelationId: {CorrelationId}]",
            order.Id, message.CorrelationId);

        await _publisher.PublishAsync(QueueNames.OrderConfirmed, new OrderConfirmedEvent
        {
            CorrelationId = message.CorrelationId,
            OrderId = order.Id,
            UserId = order.UserId,
            TotalAmount = order.TotalAmount
        });
    }
}
