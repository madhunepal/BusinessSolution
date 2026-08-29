# Reference Pattern: Customer Module

This document outlines the standard architectural and implementation patterns established in the Customer module. Future modules (e.g., Lead, Quote, Invoice, Product) must follow these patterns to ensure consistency, security, and maintainability. Do NOT repeatedly re-analyze the entire Customer module codebase; use this document as your guide.

## 1. Module Structure
The standard vertical slice follows Clean Architecture boundaries:
- **Domain**: Entities, Enums, and core logic (e.g., `Customer`, `CustomerType`, `TenantSequence`).
- **Application**: DTOs, Service Interfaces, and Service Implementations (e.g., `ICustomerService`, `CustomerService`).
- **Infrastructure**: EF Core `DbContext` `DbSet`s, Fluent API Configurations (`IEntityTypeConfiguration`), and Migrations.
- **Web**: Blazor Interactive Server UI components (`Pages/Customers/Index.razor`, etc.).
- **Tests**: Integration/Unit tests simulating the `ApplicationDbContext` and evaluating application layer validations, isolation, and activity logging.

## 2. Tenant Ownership
Strict tenant isolation is non-negotiable.
- **IHasBusinessId**: Any tenant-owned entity must implement `IHasBusinessId`.
- **ITenantContext**: Always inject `ITenantContext` into application services.
- **Server-Derived BusinessId**: When creating an entity, the `BusinessId` must be derived from `_tenantContext.CurrentBusinessId`. Never trust a client-supplied `BusinessId`.
- **Global Query Filters**: EF Core global query filters automatically restrict data access to the current tenant.
- **Cross-Tenant Records**: Attempting to read or modify a record belonging to another tenant should naturally result in a `KeyNotFoundException` because the query filter prevents it from being loaded.
- **IgnoreQueryFilters()**: Must NEVER be used for standard application operations. It is strictly reserved for safe administrative scenarios or specialized sequence generation where cross-tenant leakage is impossible.

## 3. Entity Lifecycle
- **BaseEntity**: Entities inherit from `BaseEntity` (provides `Id`, `CreatedAt`, `UpdatedAt`).
- **Soft Deactivation**: Prefer `IsActive = false` (or Status changes) over hard deletion (`_context.Remove()`) where historical integrity and referential integrity matter.
- **Immutable Tenant Ownership**: Once created, an entity's `BusinessId` must never be modified.

## 4. EF Core Conventions
- **IEntityTypeConfiguration**: Use fluent configuration classes inside `Infrastructure/Data/Configurations` rather than data annotations on Domain entities.
- **Constraints**: Explicitly define max string lengths, nullability, and appropriate indexes (especially unique indexes covering `BusinessId`).
- **Migrations**: Generate focused EF Core migrations for each new module implementation.
- **Read-Only Operations**: Use `.AsNoTracking()` where appropriate (e.g. read-only lookups or exports) to improve performance.

## 5. Application-Service Conventions
- **Explicit Use-Cases**: Services should expose explicit methods (`CreateCustomerAsync`, `UpdateCustomerAsync`).
- **DTO Separation**: Do not pass Domain entities to the Web layer. Use strict DTOs (e.g., `CustomerDto`, `CreateCustomerRequest`).
- **Application-Layer Validation**: Services MUST explicitly validate requests (e.g., `Validator.ValidateObject(...)` using `System.ComponentModel.DataAnnotations`) before executing logic. Do not rely solely on UI validation.
- **Tenant-Safe Lookup**: Update/Deactivate methods must retrieve the entity using the tenant context (implicitly applied by EF) to prevent unauthorized tampering.
- **Activity Creation**: Generate contextual business timeline events (`_context.Activities.Add(new Activity { ... })`) directly inside the application service methods that mutate state.
- **Async/Cancellation**: Operations should be fully asynchronous.

## 6. Blazor Conventions
Follow these functional UI boundaries within `/Pages/[Module]/`:
- **Index/List**: Paginated datagrid with search and filtering inputs.
- **Search/Filter**: Maintain search criteria in DTOs (e.g. `CustomerSearchRequest`) to synchronize with the application layer.
- **Create/Edit**: Use `<EditForm>` with `<DataAnnotationsValidator>`. Separate components logic for creating versus editing to avoid complex conditional state.
- **Details**: Read-only display of the aggregate.
- **Deactivate**: Clearly marked actions (typically on Details or Edit pages) for soft-deleting/deactivating records.
- **Authorization**: Apply `[Authorize]` appropriately. Rely on implicit UI masking if `BusinessId` is null, managed via layout.
- **States**: Handle loading spinners, empty state illustrations, and error messages gracefully.

## 7. Testing Conventions
- **Happy Path**: Ensure standard Create/Update/Read flows succeed.
- **Validation**: Verify that the Application Service rejects invalid requests directly.
- **Tenant Isolation**: Prove that `GetAsync` or `UpdateAsync` on another tenant's entity throws a `KeyNotFoundException`.
- **Lifecycle**: Verify that deactivation appropriately flags the record rather than hard deleting it.
- **Activity Persistence**: Verify that `Activity` logs are explicitly saved when mutating the entity.
- **Concurrency Behavior**: Use parallel invocations to ensure unique sequence generators or concurrency tokens behave correctly.
- **UI Tests**: Avoid writing rigid UI layout tests unless testing critical interactive behaviors.

## 8. Transaction Rules (CRITICAL)
- **Shared SaveChanges**: A service must NOT call `SaveChangesAsync()` in a way that risks committing unrelated, uncommitted tracked state from the shared scoped `IApplicationDbContext`.
- **Isolated Sequence Generation**: Infrastructure requiring independent atomic commits (like `TenantSequence` generation) MUST use an isolated database context scope (`IServiceScopeFactory.CreateScope()`) or another safe database mechanism. 
- **TenantSequence Mechanics**: The `TenantSequence` pattern utilizes:
  1. An isolated DbContext scope for transaction-boundary isolation.
  2. Optimistic concurrency utilizing the `RowVersion` timestamp.
  3. A robust retry loop catching `DbUpdateConcurrencyException`. 
  (Note: Do not incorrectly describe this pattern as pessimistic concurrency).

## 9. Patterns NOT to Copy
Do NOT copy or introduce the following:
- **Unnecessary Repositories**: Do not wrap EF Core in generic repository layers.
- **Generic CRUD Frameworks**: No generic MediatR or abstracted CRUD handlers in V1.
- **UI-Only Validation**: Trusting `<EditForm>` without running `Validator.ValidateObject` inside the Application Service.
- **Client-Supplied BusinessId**: Accepting `BusinessId` as a parameter from a DTO on creation.
- **Unnecessary IgnoreQueryFilters**: Disabling tenant filters outside of System Admin or low-level isolated sequence logic.
- **Shared-Context Sequence Commits**: Calling `SaveChangesAsync()` on a shared DbContext just to generate an ID.
- **Premature Full-Text Search Infrastructure**: Building ElasticSearch/Lucene wrappers when simple queries suffice for V1.

## 10. Deferred Technical Considerations
Keep the following in mind for future scaling phases (but do not over-engineer them now):
- **Search Scalability**: Current search relies on leading-wildcards (`EF.Functions.Like(..., "%term%")`), which skips database indexes. This will need replacement with Full-Text Search later.
- **E2E UI Testing**: Playwright/bUnit tests for the Blazor front-end are deferred until UI layouts stabilize.
- **Database-Provider Concurrency**: The optimistic concurrency loops should be verified for scalability on production-grade providers like SQL Server/PostgreSQL.
