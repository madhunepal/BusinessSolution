namespace SmallBusiness.Infrastructure.Identity;

/// <summary>
/// Default role names for the application.
/// Maps to typical small service business organizational roles.
/// </summary>
public static class AppRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Technician = "Technician";
    public const string Office = "Office";

    public static readonly string[] All = [Owner, Admin, Manager, Technician, Office];

    /// <summary>
    /// Default permission assignments per role.
    /// Owner and Admin get everything; others get subsets.
    /// </summary>
    public static Dictionary<string, string[]> DefaultRolePermissions => new()
    {
        [Owner] = Permissions.All,
        [Admin] = Permissions.All,
        [Manager] =
        [
            Permissions.BusinessView,
            Permissions.CustomersView, Permissions.CustomersCreate, Permissions.CustomersEdit,
            Permissions.QuotesView, Permissions.QuotesCreate, Permissions.QuotesEdit, Permissions.QuotesApprove,
            Permissions.OrdersView, Permissions.OrdersCreate,
            Permissions.JobsView, Permissions.JobsCreate, Permissions.JobsEdit, Permissions.JobsComplete,
            Permissions.ScheduleView, Permissions.ScheduleManage,
            Permissions.InvoicesView, Permissions.InvoicesCreate,
            Permissions.PaymentsView, Permissions.PaymentsRecord,
            Permissions.InventoryView, Permissions.InventoryManage,
            Permissions.EmployeesView,
            Permissions.ReportsView
        ],
        [Technician] =
        [
            Permissions.BusinessView,
            Permissions.CustomersView,
            Permissions.JobsView, Permissions.JobsComplete,
            Permissions.ScheduleView,
            Permissions.InventoryView
        ],
        [Office] =
        [
            Permissions.BusinessView,
            Permissions.CustomersView, Permissions.CustomersCreate, Permissions.CustomersEdit,
            Permissions.QuotesView, Permissions.QuotesCreate, Permissions.QuotesEdit,
            Permissions.OrdersView, Permissions.OrdersCreate,
            Permissions.JobsView,
            Permissions.ScheduleView, Permissions.ScheduleManage,
            Permissions.InvoicesView, Permissions.InvoicesCreate,
            Permissions.PaymentsView, Permissions.PaymentsRecord,
            Permissions.ReportsView
        ]
    };
}
