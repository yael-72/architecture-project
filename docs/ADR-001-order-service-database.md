# ADR-001 — OrderService: SQL Server (Relational Database)

**Status:** Accepted  
**Date:** 2026-07-05  
**Service:** OrderService

---

## Context

`OrderService` records orders, order items, total amounts, and status transitions. Each `POST /orders` involves:

1. Writing one `Order` row and N `OrderItem` rows atomically.
2. Updating `Order.Status` as the saga progresses (`Pending → Confirmed / Cancelled`).
3. Supporting queries such as *"all orders for user X"* and *"total spent by user X"*.

The choice of database directly impacts the correctness of financial data.

---

## Decision

Use **SQL Server 2022** (a relational RDBMS) as the data store for `OrderService`.

---

## Rationale

### ACID is non-negotiable for money

An order creation must be **atomic**: either the `Order` row and all `OrderItem` rows are committed together, or none of them are. A partially-written order (e.g. two out of three items saved, then a crash) is corrupt financial data.

SQL Server enforces full **ACID** semantics:

| Property | Guarantee | Why it matters here |
|---|---|---|
| **Atomicity** | All-or-nothing within a transaction | Order + items saved together or not at all |
| **Consistency** | Constraints always hold | Foreign keys keep items attached to valid orders |
| **Isolation** | Concurrent writes see correct data | Two saga callbacks can't corrupt the same order row |
| **Durability** | Committed data survives crashes | Payment records must not disappear on restart |

### CAP theorem position

SQL Server sits at the **CP** corner of the CAP triangle: it favours **Consistency** and **Partition tolerance** at the cost of reduced availability during network splits. For financial data, losing consistency (showing wrong balances, duplicate orders) is far more dangerous than a brief unavailability window.

### Why not BASE / NoSQL

A BASE (Basically Available, Soft state, Eventually consistent) document store like MongoDB could be used, but its eventual consistency model means that in certain failure scenarios, a read immediately after a write might return stale data. For order status (where a customer might see "Confirmed" and then "Pending" on a retry) this is unacceptable.

### Strong schema = domain safety net

The strict schema (column types, NOT NULL, FK constraints) acts as a second layer of validation. No service can accidentally store a `null` order amount or an `OrderItem` pointing to a non-existent order.

---

## Consequences

- Horizontal write scaling requires sharding or read replicas (acceptable — order write volume is low compared to catalog reads).
- Schema migrations are needed for model changes (managed via EF Core migrations, run at startup).
- Orders and inventory live in **separate databases** — no cross-service joins (database-per-service pattern enforced).
