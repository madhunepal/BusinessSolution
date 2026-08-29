# Domain Model

## Foundational

### Business

Represents a tenant/organization.

Key concepts:
- Id
- Name
- Status
- CreatedAt
- configuration

### BusinessUser

Associates an application user with a business.

### Activity

Represents a business timeline event.

Conceptual fields:
- Id
- BusinessId
- ActivityType
- Description
- EntityType
- EntityId
- CreatedAt
- CreatedBy

### AuditLog

Security/administrative record of important changes.

Keep Activity and AuditLog conceptually separate:
- Activity = business timeline
- AuditLog = accountability/security record

## CRM

### Customer
A person or organization that buys from the business.

### Contact
A contact person associated with a customer organization.

### Lead
A prospective customer before conversion.

## Sales

### Product
A physical or non-physical sellable item.

### Service
A service offered by the business.

### Quote
Commercial proposal to a customer.

### QuoteItem
Line within a quote.

### SalesOrder
Confirmed customer order.

### SalesOrderItem
Line within an order.

## Operations

### Job
Operational work associated with a customer/order.

### JobTask
Action required to complete a job.

### Appointment
Scheduled time for a job/service/meeting.

## Finance

### Invoice
Request for payment.

### InvoiceItem
Invoice line.

### Payment
Money received against one or more invoices.

### Expense
Business expenditure.

## Procurement

### Vendor
Supplier.

### PurchaseOrder
Request/order sent to vendor.

### PurchaseOrderItem
Purchase order line.

## Inventory

### InventoryTransaction
Immutable-ish historical movement such as purchase, sale, return, adjustment, damage, or transfer.

Prefer deriving stock from transactions or maintaining a controlled balance plus transaction ledger with reconciliation.

## Employees

### Employee
Person working for the business.

### TimeEntry
Recorded work time.

## Documents

### Document
Metadata and storage reference for an uploaded file.

## Notifications

### Notification
Message/event intended for a user or external recipient.

## Key Relationships

Customer
→ Quotes
→ Sales Orders
→ Jobs
→ Appointments
→ Invoices
→ Payments
→ Activities
→ Documents

Quote
→ QuoteItems

SalesOrder
→ SalesOrderItems
→ Jobs

Job
→ JobTasks
→ Appointments
→ TimeEntries

Invoice
→ InvoiceItems
→ Payments

Vendor
→ PurchaseOrders

PurchaseOrder
→ PurchaseOrderItems
→ InventoryTransactions
