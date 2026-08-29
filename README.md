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
