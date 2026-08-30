using SmallBusiness.Application.Interfaces;
using SmallBusiness.Infrastructure;
using SmallBusiness.Infrastructure.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using SmallBusiness.Infrastructure.Identity;
using SmallBusiness.Web.Components.Account;

using SmallBusiness.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure: EF Core, SQL Server, Identity
builder.Services.AddInfrastructure(builder.Configuration);

// Application services
builder.Services.AddApplicationServices();

// Current user context
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContextService>();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


// Authorization
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddPermissionPolicies();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
