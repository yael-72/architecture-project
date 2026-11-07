# Skill: Testing Workflow

## Purpose
Teach how to validate the project with automated tests and maintain confidence as the system evolves.

## What this skill covers
- Writing unit tests for services and business logic
- Using xUnit and Moq effectively
- Testing happy path and failure scenarios
- Verifying that changes do not break existing behavior
- Running tests locally and in CI

## Core testing practices
- Prefer testing real behavior over mock-only behavior
- Cover both success and error paths
- Keep tests focused and readable
- Use meaningful assertions
- Run tests after every meaningful change

## Commands to know
```bash
dotnet test OrderService.Tests/OrderService.Tests.csproj
dotnet test StoreApiMicroservices.sln
```

## Example test areas
- Order creation with valid user and product
- Order creation when user is missing
- Order creation when product is missing
- Order lookup by ID and by user
- Error handling for invalid input

## Practical exercises
1. Add or update a unit test for a service behavior.
2. Verify that a failing test reveals a real code issue.
3. Fix the failing behavior and rerun the tests.
4. Check that the full solution still passes.
5. Review the CI pipeline output for the same test scenario.

## Checklist
- I can write unit tests for service logic.
- I can run the relevant test projects locally.
- I can explain the difference between a code bug and a test failure.
- I can use test results to guide debugging and validation.
