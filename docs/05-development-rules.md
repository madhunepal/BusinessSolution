# Development Rules

## Naming

Use clear domain names. Avoid abbreviations.

Prefer:
CustomerService
SalesOrder
InventoryTransaction

Avoid:
CustSvc
SO
InvTxn

## C#

- nullable reference types enabled
- modern C# syntax where it improves readability
- async for I/O
- cancellation tokens for long-running operations
- guard clauses where appropriate
- avoid deeply nested logic

## EF Core

- explicit relationships when business meaning matters
- indexes for important lookup patterns
- projections for read models
- avoid accidental lazy-loading/N+1 behavior
- no database calls from Razor markup
- keep DbContext lifetime appropriate to Blazor application model

## Business Logic

Business rules must be centralized.

For example, do not allow a component to decide that every quote can be accepted. The application/domain workflow must enforce it.

## Error Handling

Return useful domain/application errors.

Do not expose raw SQL exceptions or stack traces to users.

## Validation

Validate:
- required values
- ranges
- formats
- business invariants
- state transitions
- tenant ownership

## Security

Treat authorization and tenant isolation as mandatory.

Never trust BusinessId supplied by a client.

Determine the active business from the authenticated context/authorized server-side context.

## UI

Use consistent patterns.

Do not create a unique interaction style for every module.

## Performance

Do not optimize prematurely, but avoid obvious:
- N+1 queries
- loading entire tables
- unbounded queries
- unnecessary database round trips

## Documentation

Update relevant documentation when behavior or architecture changes.
