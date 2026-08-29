# Small Business Management System — Agent Instructions

## 1. Mission

Build a production-oriented Small Business Management System (SBMS) for small service businesses. The first vertical slice targets businesses such as HVAC, plumbing, cleaning, landscaping, IT services, and consulting.

The core business journey is:

Customer → Quote → Sales Order → Job → Schedule → Job Completion → Invoice → Payment

The application must be modular, maintainable, testable, secure, and suitable for incremental expansion.

## 2. Technology Baseline

- .NET 8+ / C#
- Blazor
- ASP.NET Core
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity
- xUnit for unit tests
- bUnit for Blazor component tests
- Docker where useful
- GitHub Actions for CI/CD

Do not introduce a new framework, ORM, database, authentication system, UI component library, or messaging platform without explicit approval.

## 3. Architecture

Use a modular monolith.

Preferred dependency direction:

Web → Application → Domain
Infrastructure → Application/Domain

Business rules belong in the Domain/Application layers, not in Razor components.

Keep module boundaries explicit. Avoid microservices.

Do not introduce a generic repository/unit-of-work abstraction merely for architectural fashion. EF Core DbContext/IDbContextFactory may be used directly where appropriate.

## 4. Multi-tenancy

The product is intended to support multiple businesses eventually.

Business/Organization is a first-class concept. Business-owned entities should carry BusinessId where appropriate.

Never allow a query or command to cross business boundaries.

Treat tenant isolation as a security requirement.

## 5. Core Modules

1. Identity & Administration
2. Business / Organization
3. CRM
4. Sales
5. Jobs & Operations
6. Scheduling
7. Finance
8. Inventory
9. Purchasing
10. Employees
11. Documents
12. Notifications
13. Reporting

Do not implement all modules at once.

## 6. Core Domain Objects

Initial domain vocabulary:

Business
User
Role
Permission
BusinessUser
Customer
Contact
Lead
Activity
Product
Service
Quote
QuoteItem
SalesOrder
SalesOrderItem
Job
JobTask
Appointment
Employee
Invoice
InvoiceItem
Payment
Expense
Vendor
PurchaseOrder
PurchaseOrderItem
InventoryTransaction
Document
Notification
AuditLog

The domain model is subject to the approved design in /docs/04-domain-model.md.

## 7. Development Rules

1. Inspect existing code before changing it.
2. Follow established project patterns.
3. Make the smallest coherent change required.
4. Do not modify unrelated modules.
5. Do not silently change architecture.
6. Do not add packages without approval.
7. Use async APIs for I/O.
8. Respect cancellation tokens where the surrounding API supports them.
9. Validate input at appropriate boundaries.
10. Enforce authorization server-side; UI hiding is not authorization.
11. Enforce BusinessId/tenant isolation on all business-owned data access.
12. Avoid N+1 queries.
13. Use projection for read-heavy queries where appropriate.
14. Do not expose EF entities unnecessarily to UI/API boundaries.
15. Keep business rules out of Razor markup/code-behind where possible.
16. Prefer explicit domain/application operations over arbitrary status mutation.
17. Important state changes should create Activity/Audit records where defined.
18. Do not hard-delete records that require historical/audit retention.
19. Never commit secrets, connection strings with credentials, tokens, certificates, or production data.
20. Do not rewrite working code simply to make it look different.

## 8. Feature Workflow

For every feature:

1. Read AGENTS.md and relevant /docs files.
2. Inspect the current implementation.
3. Identify reusable patterns.
4. Produce a concise implementation plan.
5. Wait for approval when the task is architectural or ambiguous.
6. Implement the smallest coherent vertical slice.
7. Add/update tests.
8. Run build.
9. Run relevant tests.
10. Review changed files.
11. Report:
   - what changed
   - why
   - tests/build results
   - known limitations
   - follow-up work

## 9. Definition of Done

A feature is not complete until applicable items are satisfied:

- [ ] Requirement is clear
- [ ] Domain model is correct
- [ ] Database changes are correct
- [ ] Business rules are enforced
- [ ] Application logic is implemented
- [ ] UI is implemented
- [ ] Validation is implemented
- [ ] Authorization is implemented
- [ ] Tenant isolation is verified
- [ ] Activity/audit behavior is implemented
- [ ] Tests are added/updated
- [ ] Build succeeds
- [ ] Relevant tests pass
- [ ] No unrelated files changed
- [ ] Documentation/backlog updated

## 10. Testing Rules

At minimum, test:

- happy path
- invalid input
- important business rule violations
- state transitions
- authorization
- tenant isolation
- important EF/database behavior
- important Blazor interactions

Do not create meaningless tests that only verify property assignment.

## 11. Database Rules

- Use explicit primary keys and foreign keys.
- Use appropriate indexes.
- Add unique constraints where business rules require them.
- Configure relationships explicitly when conventions are insufficient.
- Avoid storing calculated values unless there is a clear reason.
- Transactional workflows must be atomic where required.
- Do not edit migrations that have already been applied to shared/production environments; create a new migration.
- Preserve historical financial and operational records.

## 12. Status Rules

Statuses must represent real business states.

Do not expose arbitrary string status editing to users.

Where a workflow has meaningful transitions, implement explicit operations such as:

AcceptQuote
RejectQuote
ConvertQuote
StartJob
CompleteJob
IssueInvoice
RecordPayment

Invalid transitions must be rejected.

## 13. UI Rules

Use consistent layouts and reusable components.

Typical list page:

- title
- primary action
- search
- filters
- table/grid
- pagination
- row actions
- empty/loading/error states

Typical detail page:

- summary
- important actions
- related records
- activity timeline
- documents where relevant

Do not overload a page with unrelated responsibilities.

## 14. Git Rules

Prefer one feature branch per coherent feature.

Good commit examples:

feat: add customer entity
feat: add customer management
test: add customer application tests
fix: prevent cross-business customer lookup

Avoid giant commits such as "build entire CRM".

## 15. Agent Behavior

If the task is ambiguous, state the ambiguity and make the minimum reasonable assumption.

If a requested change conflicts with the architecture, stop and explain the conflict before implementing.

Do not claim tests passed unless they were actually run.

Do not claim a feature is complete if important Definition-of-Done items remain unfinished.
