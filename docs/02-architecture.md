# Architecture

## Style

Modular monolith with clean architecture principles.

## Logical Layers

### Web
Blazor UI, routing, presentation models, user interaction.

### Application
Use cases, commands/queries, validation, authorization orchestration, DTOs, application services.

### Domain
Entities, value objects, domain rules, domain events, state transitions.

### Infrastructure
EF Core, SQL Server, Identity persistence, file storage, external integrations.

## Dependency Direction

Web → Application → Domain

Infrastructure → Application/Domain

Domain must not depend on Web or Infrastructure.

## Module Boundaries

Each business module owns its business concepts and application workflows.

Shared infrastructure should remain small. Do not put business-specific logic into a "Common" project merely to avoid deciding ownership.

## Initial Solution Shape

SmallBusiness.sln

src/
  SmallBusiness.Web/
  SmallBusiness.Application/
  SmallBusiness.Domain/
  SmallBusiness.Infrastructure/

tests/
  SmallBusiness.Domain.Tests/
  SmallBusiness.Application.Tests/
  SmallBusiness.Web.Tests/

modules may initially live within the main projects. Introduce stronger module folders/namespaces as the codebase grows.

## Persistence

EF Core + SQL Server.

Use migrations.

Use transactions for multi-record operations that must succeed or fail together.

## Authentication

ASP.NET Core Identity.

Authorization should support roles and explicit permissions.

## Background Processing

Start without a dedicated background framework unless a requirement exists. Introduce Hangfire/Quartz only when scheduled/reliable background work justifies it.

## Integrations

External systems must be isolated behind application-facing interfaces/adapters.

Do not leak third-party SDK types through the domain model.
