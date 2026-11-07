namespace SharedKernel.Events;

/// <summary>All saga event contracts and queue name constants.</summary>

// ── Events ────────────────────────────────────────────────────────────────────

public record OrderPlacedEvent
{
    public Guid CorrelationId { get; init; }
    public int OrderId { get; init; }
    public int UserId { get; init; }
    public List<OrderItemLine> Items { get; init; } = new();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public record InventoryReservedEvent
{
    public Guid CorrelationId { get; init; }
    public int OrderId { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public record InventoryRejectedEvent
{
    public Guid CorrelationId { get; init; }
    public int OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public record OrderConfirmedEvent
{
    public Guid CorrelationId { get; init; }
    public int OrderId { get; init; }
    public int UserId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public record OrderCancelledEvent
{
    public Guid CorrelationId { get; init; }
    public int OrderId { get; init; }
    public int UserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    /// <summary>Included so InventoryService can compensate (restore) stock.</summary>
    public List<OrderItemLine> Items { get; init; } = new();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public record OrderItemLine
{
    public int ProductId { get; init; }
    public int Quantity { get; init; }
}

// ── Queue Name Constants ───────────────────────────────────────────────────────

public static class QueueNames
{
    /// <summary>OrderService → InventoryService: order just created, check &amp; reserve stock.</summary>
    public const string OrderPlaced = "order.placed";

    /// <summary>InventoryService → OrderService: stock successfully reserved.</summary>
    public const string InventoryReserved = "inventory.reserved";

    /// <summary>InventoryService → OrderService: not enough stock, order must be cancelled.</summary>
    public const string InventoryRejected = "inventory.rejected";

    /// <summary>OrderService → NotificationService: order confirmed, notify customer.</summary>
    public const string OrderConfirmed = "order.confirmed";

    /// <summary>OrderService → NotificationService: order cancelled, notify customer.</summary>
    public const string OrderCancelledNotify = "order.cancelled.notify";

    /// <summary>OrderService → InventoryService: order cancelled, restore stock (compensation).</summary>
    public const string OrderCancelledInventory = "order.cancelled.inventory";
}
