namespace SmallBusiness.Infrastructure.Identity;

/// <summary>
/// Code-defined permission constants. These form the stable vocabulary
/// for authorization. Which roles get which permissions is configured
/// separately via role-permission mappings.
/// </summary>
public static class Permissions
{
    // Business administration
    public const string BusinessView = "Business.View";
    public const string BusinessEdit = "Business.Edit";

    // User administration
    public const string UsersView = "Users.View";
    public const string UsersManage = "Users.Manage";

    // CRM
    public const string CustomersView = "Customers.View";
    public const string CustomersCreate = "Customers.Create";
    public const string CustomersEdit = "Customers.Edit";
    public const string CustomersDelete = "Customers.Delete";

    // Sales
    public const string QuotesView = "Quotes.View";
    public const string QuotesCreate = "Quotes.Create";
    public const string QuotesEdit = "Quotes.Edit";
    public const string QuotesApprove = "Quotes.Approve";
    public const string OrdersView = "Orders.View";
    public const string OrdersCreate = "Orders.Create";

    // Operations
    public const string JobsView = "Jobs.View";
    public const string JobsCreate = "Jobs.Create";
    public const string JobsEdit = "Jobs.Edit";
    public const string JobsComplete = "Jobs.Complete";
    public const string ScheduleView = "Schedule.View";
    public const string ScheduleManage = "Schedule.Manage";

    // Finance
    public const string InvoicesView = "Invoices.View";
    public const string InvoicesCreate = "Invoices.Create";
    public const string PaymentsView = "Payments.View";
    public const string PaymentsRecord = "Payments.Record";

    // Inventory
    public const string InventoryView = "Inventory.View";
    public const string InventoryManage = "Inventory.Manage";

    // People
    public const string EmployeesView = "Employees.View";
    public const string EmployeesManage = "Employees.Manage";

    // Reports
    public const string ReportsView = "Reports.View";

    /// <summary>
    /// All known permissions, for iteration/seeding.
    /// </summary>
    public static readonly string[] All =
    [
        BusinessView, BusinessEdit,
        UsersView, UsersManage,
        CustomersView, CustomersCreate, CustomersEdit, CustomersDelete,
        QuotesView, QuotesCreate, QuotesEdit, QuotesApprove,
        OrdersView, OrdersCreate,
        JobsView, JobsCreate, JobsEdit, JobsComplete,
        ScheduleView, ScheduleManage,
        InvoicesView, InvoicesCreate,
        PaymentsView, PaymentsRecord,
        InventoryView, InventoryManage,
        EmployeesView, EmployeesManage,
        ReportsView
    ];
}
