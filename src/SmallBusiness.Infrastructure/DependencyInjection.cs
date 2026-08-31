using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Infrastructure.Data;
using SmallBusiness.Infrastructure.Identity;
using SmallBusiness.Infrastructure.Services;

namespace SmallBusiness.Infrastructure;

/// <summary>
/// Registers Infrastructure and Application services into the DI container.
/// Called from the Web project's Program.cs (the composition root).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        static void ConfigureSqlServer(DbContextOptionsBuilder options, IConfiguration config)
        {
            var connectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is not configured. Set ConnectionStrings:DefaultConnection or ConnectionStrings__DefaultConnection.");
            }

            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly(
                    typeof(ApplicationDbContext).Assembly.FullName));
        }

        // EF Core + SQL Server
        services.AddDbContext<ApplicationDbContext>(options =>
            ConfigureSqlServer(options, configuration));
        services.AddDbContextFactory<ApplicationDbContext>(
            options => ConfigureSqlServer(options, configuration),
            ServiceLifetime.Scoped);

        // Expose IApplicationDbContext for the Application layer
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IApplicationDbContextFactory, ApplicationDbContextFactory>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // ASP.NET Core Identity
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }

    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(permission, policy =>
                    policy.RequireAuthenticatedUser()
                        .AddRequirements(new PermissionRequirement(permission)));
            }
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<BusinessService>();
        services.AddScoped<ITenantSequenceService, TenantSequenceService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICatalogItemService, CatalogItemService>();
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<ISalesOrderService, SalesOrderService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IInventoryService, InventoryService>();
        return services;
    }
}
