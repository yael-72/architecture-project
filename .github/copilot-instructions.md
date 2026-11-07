# Copilot Instructions for ArchitectureProject

## Project overview
This repository is a .NET 8 microservices-based e-commerce sample with the following services:
- ApiGateway
- UserAuthService
- ProductCatalogService
- OrderService
- InventoryService
- NotificationService
- BffService

It also includes Docker Compose infrastructure for SQL Server, Redis, RabbitMQ, and Seq.

## Architecture guidelines
- Keep service boundaries clear. Do not move logic across services unless the change is explicitly intended.
- Prefer existing patterns for controllers, services, repositories, DTOs, and messaging handlers.
- Shared cross-cutting concerns should stay in SharedKernel when possible.
- If a change affects API routing, update the relevant Ocelot config in ApiGateway/ocelot*.json.

## Development conventions
- Preserve the current folder structure for each service.
- Keep names and namespaces consistent with the existing project layout.
- Prefer small, focused changes and verify them before finishing.
- If you add new endpoints, make sure they are documented and accessible through the gateway when appropriate.

## Testing and verification
- Run the relevant test project after making code changes.
- For the order service tests, use:
  - dotnet test OrderService.Tests/OrderService.Tests.csproj
- For broader verification, use:
  - dotnet test StoreApiMicroservices.sln
- If changing Docker or infrastructure behavior, validate with:
  - docker compose config
  - docker compose up --build

## Messaging and observability
- RabbitMQ-based saga events must keep their existing names and payload shape unless explicitly changed.
- Preserve structured logging and correlation IDs so traces remain usable in Seq.
- Health endpoints at /health should remain available for service monitoring.

## Data and persistence
- When changing EF Core models, update the relevant migrations and verify that startup still works.
- Be careful with database initialization and concurrent startup behavior in the catalog replicas.

## Before finishing a task
- Re-run the relevant tests or build steps.
- Mention any new environment variables, ports, or startup requirements.
- Call out any remaining risks or manual steps needed to validate the change.
