# ADR-004 — Redis: Distributed Cache (Key-Value NoSQL)

**Status:** Accepted  
**Date:** 2026-07-05  
**Services:** ProductCatalogService, OrderService, BffService

---

## Context

Three services perform repeated reads against SQL Server for data that changes infrequently (product details, categories). Without a cache:
- Every product page load hits SQL Server.
- OrderService calls ProductCatalogService via HTTP for every item in every order being validated.
- BffService calls both OrderService and ProductCatalogService for each dashboard request.

The system needs a fast, shared cache that all service instances can read from (required because ProductCatalogService runs as two replicas).

---

## Decision

Use **Redis 7** as a distributed, in-memory key-value cache with TTL-based expiration, implementing the **cache-aside pattern**.

---

## Rationale

### Redis as a NoSQL key-value store

Redis is a **key-value NoSQL database** — a distinct family from the relational SQL Server databases. This satisfies the polyglot persistence requirement:

| Database Family | Technology | Services |
|---|---|---|
| Relational | SQL Server | UserAuth, Orders, Inventory, Catalog (primary) |
| Key-value (NoSQL) | Redis | Catalog cache, Order product cache, BFF cache |

### Why the cache-aside pattern

Cache-aside puts the application in control of what is cached and when:

```
Read:
  1. Check Redis key  →  HIT: return immediately (logged: "CACHE HIT")
  2. MISS: read from SQL, write to Redis with TTL, return
Write:
  1. Update SQL Server
  2. DELETE Redis key (invalidate) — next read will re-populate
```

Compared to read-through or write-through:
- **Read-through** couples the cache to the database driver — not supported by Redis natively.
- **Write-through** would double-write on every mutation; unnecessary for a catalog that rarely changes.
- **Cache-aside** keeps the logic explicit and testable (see `CachedProductService` decorator).

### CAP theorem position for Redis

Redis is **AP** (Available + Partition tolerant):
- If Redis is unavailable, the service falls through to SQL Server — no data is lost, only performance degrades.
- If a replica falls behind, it may serve slightly stale data — acceptable for read-heavy catalog browsing where millisecond-stale product descriptions are harmless.

### Consistency model

Redis provides **eventual consistency** in cluster mode, but in this deployment it runs as a single node: all reads see the latest written value immediately (strong consistency for single-node). Cache invalidation on write ensures stale data is never served for more than the TTL duration.

### Why Redis over Memcached

- Redis supports **data structures** (strings, hashes, lists) — future enhancements like a shopping-cart store could reuse the same Redis instance.
- Redis supports **pub/sub** — potential future use for real-time stock notifications.
- Redis is the industry standard for .NET distributed caching via `IDistributedCache`.

### Invalidation strategy

| Trigger | Action |
|---|---|
| `ProductService.UpdateProductAsync` | Delete `product:{id}` key |
| `ProductService.DeleteProductAsync` | Delete `product:{id}` key + `products:all` |
| `CategoryService.UpdateCategoryAsync` | Delete `category:{id}` + `categories:all` |
| TTL expiry (default 5 min) | Key auto-expires; next read re-populates |

---

## Consequences

- Cache hit rate > 90% in normal operation (products don't change frequently).
- Redis is a **single point of failure** in the current deployment. Mitigation: set `abortOnConnectFail = false` (already configured) so services degrade gracefully to direct SQL reads.
- Memory usage is low: each product record is < 1 KB serialised JSON; 10,000 products ≈ 10 MB.
