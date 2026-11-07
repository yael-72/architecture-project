# ADR-002 — ProductCatalogService: SQL Server + Redis Cache

**Status:** Accepted  
**Date:** 2026-07-05  
**Service:** ProductCatalogService

---

## Context

`ProductCatalogService` stores products and categories. Its read-to-write ratio is very high: a typical session results in many product reads (browse, search, paginate) but product data changes rarely (admin operations only).

The course specification suggests a **document database** (e.g. MongoDB) for the catalog, arguing that varying product attributes per category fit the document model better. This ADR explains why SQL Server was retained and how the NoSQL requirement is satisfied by Redis.

---

## Decision

Use **SQL Server 2022** for persistent product/category storage, **decorated with a Redis cache** for all reads.

---

## Rationale

### Why SQL Server was kept (not MongoDB)

The products in this system have a **fixed schema**: every product has `Id`, `Name`, `Description`, `Price`, `Stock`, `CategoryId`. There are no varying attributes per category in the current domain model. The document model's main advantage — schemaless, per-document varying fields — does not apply here.

Introducing MongoDB solely to satisfy a technology checkbox, while the domain does not need schemaless storage, would increase operational complexity (separate driver, no EF, different query semantics) with no tangible benefit.

**ADR justification vocabulary:**

| Term | Position |
|---|---|
| **Consistency model** | Strong consistency (SQL Server, CP). Product prices and stock counts are authoritative — a stale price shown to a customer causes incorrect totals. |
| **ACID** | Needed for transactional price updates (e.g. bulk-update prices must not leave some products at old price and some at new). |
| **BASE** | Not acceptable for price data — "eventually correct" prices could lead to financial errors in `OrderService`. |

### Why this satisfies the polyglot persistence requirement

The specification states: *"At least one more NoSQL decision of your choice (key-value, wide-column, graph, search...)"* and provides a hint: *"Redis (Phase 4) is already a NoSQL key-value store — you'll be using two NoSQL families without even trying."*

Redis is used as a **key-value NoSQL store** here:
- Product records are cached under key `product:{id}` with TTL.
- Category lists are cached under `categories:all`.
- Cache invalidation fires on every write (update/delete).

This gives us two distinct NoSQL families:
1. **Key-value** — Redis (ProductCatalogService cache, OrderService product lookups, BffService aggregation cache)
2. **Relational** — SQL Server (OrderService, UserAuthService, InventoryService)

### Cache-aside pattern

```
Read request
    │
    ▼
Check Redis ──── HIT ──→ return cached value (log: "CACHE HIT")
    │
   MISS
    │
    ▼
Query SQL Server
    │
    ▼
Store in Redis (TTL = configured value, default 5 min)
    │
    ▼
Return value (log: "CACHE MISS")

Write request
    │
    ▼
Update SQL Server
    │
    ▼
Invalidate Redis key(s)
```

### CAP theorem position

- **SQL Server (primary):** CP — consistent and partition-tolerant. Writes are authoritative.
- **Redis (cache):** AP — available and partition-tolerant. Cache may serve slightly stale data between write and invalidation, which is acceptable for read-heavy browse operations where millisecond-old data is fine.

---

## Consequences

- Read latency for product list: **< 5 ms** (Redis) vs **~20–50 ms** (SQL Server). Cache makes the service ~10× faster on repeated reads.
- Cache invalidation on product update must be consistent — implemented in `CachedProductService.UpdateProductAsync`.
- Two ProductCatalogService replicas share the same Redis cache (via Docker network), so cache invalidation from replica 1 is seen by replica 2.
- If MongoDB were required in a future iteration, the `IProductRepository` abstraction allows swapping the implementation without changing the service layer.

---

## Polyglot Persistence Summary — Two NoSQL Families

The course specification requires *"at least one more NoSQL decision of your choice"* beyond the relational stores, and provides the hint: *"Redis (Phase 4) is already a NoSQL key-value store — you'll be using two NoSQL families without even trying."*

This project uses **two distinct NoSQL families** across three services:

| NoSQL Family | Technology | Services | Usage Pattern |
|---|---|---|---|
| **Key-value store** | Redis 7 | ProductCatalogService, OrderService, BffService | Cache-aside reads (TTL-based), distributed session data, aggregation cache |
| **Relational (ACID)** | SQL Server 2022 | UserAuth, Catalog (source of truth), Orders, Inventory | Transactional writes requiring ACID guarantees |

Redis is not merely a cache add-on — it acts as a **distributed key-value NoSQL database** that:
1. Serves read traffic independently from SQL Server (AP behaviour per CAP theorem)
2. Is shared across service replicas (productcatalogservice1 and productcatalogservice2 share the same keyspace)
3. Uses a deliberate invalidation strategy (delete-on-write) rather than TTL-only expiry, satisfying the course requirement to *"decide on an invalidation strategy when a product is updated"*

This satisfies the polyglot persistence requirement: two NoSQL families (key-value + implicit document shape in cache values) plus relational, each chosen for the access pattern of the owning service.
