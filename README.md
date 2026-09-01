# Small Business Management System — Agent Starter

This repository is an AI-assisted development starter for a modular Small Business Management System.

## Product Goal

Help small businesses manage:

- customers
- sales
- quotes
- orders
- jobs
- scheduling
- invoices
- payments
- inventory
- purchasing
- employees
- expenses
- documents
- notifications
- reporting

## First Target

Start with service businesses such as:

- HVAC
- plumbing
- cleaning
- landscaping
- IT services
- consulting

## Golden Path

The first meaningful end-to-end workflow is:

Customer
→ Quote
→ Sales Order
→ Job
→ Appointment
→ Completion
→ Invoice
→ Payment

## How to Use the Agent

1. Read `AGENTS.md`.
2. Read the relevant files under `docs/`.
3. Give the agent one bounded task.
4. Ask for a plan before architectural changes.
5. Review the plan.
6. Let the agent implement.
7. Require build/tests.
8. Manually acceptance-test the business workflow.
9. Commit the completed feature.
10. Move to the next backlog item.

## Important

Do not ask the agent to build the whole ERP/CRM/SMB system in one prompt.

Build one vertical slice at a time.

## Deployment Configuration

The application uses the `DefaultConnection` connection string name in every environment.

For local development with SQL Server in Docker, provide secrets outside source control:

```bash
export MSSQL_SA_PASSWORD='<local strong password>'
docker compose up -d
export ConnectionStrings__DefaultConnection='Server=localhost,1433;Database=SmallBusinessDb;User Id=sa;Password=<local strong password>;TrustServerCertificate=True'
dotnet run --project src/SmallBusiness.Web
```

For Azure Sandbox/App Service, set `ConnectionStrings__DefaultConnection` in App Service configuration to the Azure SQL Database connection string. Set `SqlServer__EnableRetryOnFailure=true` for Azure SQL transient retry handling. Do not store SQL passwords, publish profiles, `.env` files, or Azure connection strings in the repository.

Run EF Core migrations as an explicit deployment step, preferably with a migration bundle or manual migration command against the sandbox database. The web app does not run migrations automatically at startup.
