using InventoryService.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Events;
using SharedKernel.Messaging;
using SharedKernel.Middleware;

namespace InventoryService.Messaging;

/// <summary>
/// Saga Step 2 — Consumes OrderPlaced from OrderService.
/// Checks and deducts stock for every item in the order.
/// Publishes InventoryReserved (happy path) or InventoryRejected (compensation path).
/// </summary>
public class InventoryOrderPlacedConsumer : RabbitMqConsumerBase<OrderPlacedEvent>
{
    private readonly IMessagePublisher _publisher;

    public InventoryOrderPlacedConsumer(
        string hostName,
        IMessagePublisher publisher,
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryOrderPlacedConsumer> logger)
        : base(hostName, QueueNames.OrderPlaced, scopeFactory, logger)
    {
        _publisher = publisher;
    }

    protected override async Task HandleAsync(
        OrderPlacedEvent message,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        CorrelationContext.CorrelationId = message.CorrelationId;
        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

        Logger.LogInformation(
            "Checking inventory for OrderId {OrderId} ({ItemCount} items) [CorrelationId: {CorrelationId}]",
            message.OrderId, message.Items.Count, message.CorrelationId);

        // Phase 1 — validate all items before touching any stock (all-or-nothing)
        foreach (var item in message.Items)
        {
            var stock = await inventoryService.GetByProductIdAsync(item.ProductId);
            if (stock == null || stock.AvailableQuantity < item.Quantity)
            {
                var reason = stock == null
                    ? $"Product {item.ProductId} has no inventory record"
                    : $"Insufficient stock for product {item.ProductId}: available={stock.AvailableQuantity}, requested={item.Quantity}";

                Logger.LogWarning(
                    "INVENTORY REJECTED for OrderId {OrderId}: {Reason} [CorrelationId: {CorrelationId}]",
                    message.OrderId, reason, message.CorrelationId);

                await _publisher.PublishAsync(QueueNames.InventoryRejected, new InventoryRejectedEvent
                {
                    CorrelationId = message.CorrelationId,
                    OrderId = message.OrderId,
                    Reason = reason
                });
                return;
            }
        }

        // Phase 2 — all items available, deduct stock atomically
        foreach (var item in message.Items)
        {
            await inventoryService.DeductStockAsync(item.ProductId, item.Quantity);
        }

        Logger.LogInformation(
            "INVENTORY RESERVED for OrderId {OrderId} [CorrelationId: {CorrelationId}]",
            message.OrderId, message.CorrelationId);

        await _publisher.PublishAsync(QueueNames.InventoryReserved, new InventoryReservedEvent
        {
            CorrelationId = message.CorrelationId,
            OrderId = message.OrderId
        });
    }
}
