# Skill: Service Runbook

## Purpose
Teach how to run, verify, and troubleshoot the full project locally.

## What this skill covers
- Starting the infrastructure and all services
- Checking that services are healthy
- Testing the APIs through Swagger and the gateway
- Verifying logging and messaging infrastructure
- Troubleshooting common startup issues

## Commands to know
```bash
docker compose up --build
docker compose ps
docker compose logs -f

dotnet test StoreApiMicroservices.sln
dotnet test OrderService.Tests/OrderService.Tests.csproj
```

## Verification checklist
- All Docker containers are running and healthy
- The gateway is available at http://localhost:5000
- Swagger endpoints are reachable for the relevant services
- Seq is reachable at http://localhost:5341
- RabbitMQ management is reachable at http://localhost:15672
- Health endpoints return success for key services

## Common issues to troubleshoot
- SQL Server takes time to initialize
- A service fails because dependencies are not healthy yet
- Port conflicts prevent a service from starting
- Seq or RabbitMQ is not receiving events because of configuration issues
- A service fails to build due to compile errors

## Practical exercises
1. Start the full stack with Docker Compose.
2. Verify the health endpoints of all services.
3. Call the gateway and inspect the response.
4. Inspect logs in Seq for a request flow.
5. Reproduce and fix one common startup issue.

## Checklist
- I can start the project end to end.
- I can verify service health and connectivity.
- I can diagnose common runtime issues.
- I can explain how to validate the architecture locally.
