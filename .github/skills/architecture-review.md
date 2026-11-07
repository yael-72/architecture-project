# Skill: Architecture Review

## Purpose
Help the developer understand the overall architecture of this microservices project and explain how the main services interact.

## What this skill covers
- The role of each service in the solution
- How the API Gateway and BFF fit into the architecture
- How RabbitMQ, Redis, SQL Server, and Seq are used
- The order saga flow and why it is needed
- How services communicate and where boundaries should be preserved

## Key services
- ApiGateway: single entry point for clients
- UserAuthService: authentication and user management
- ProductCatalogService: product catalog and inventory-related product data
- OrderService: creates and manages orders and publishes saga events
- InventoryService: validates stock and reserves inventory
- NotificationService: handles notifications based on events
- BffService: aggregates data for the frontend

## Core concepts to know
- Microservices boundaries
- Synchronous vs asynchronous communication
- Event-driven choreography saga
- Caching with Redis
- Distributed logging with Seq
- Health checks and service observability

## Practical exercises
1. Explain the flow of an order from creation to confirmation.
2. Identify which service owns which responsibility.
3. Describe what would happen if one service becomes unavailable.
4. Explain why the project uses both SQL Server and Redis.
5. Describe how the gateway routes requests to backend services.

## Checklist
- I can explain the responsibility of each service.
- I can describe the order saga end to end.
- I can explain the purpose of the infrastructure services.
- I can identify where business logic should stay inside service boundaries.
