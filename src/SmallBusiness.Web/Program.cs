using SmallBusiness.Application.Interfaces;
using SmallBusiness.Infrastructure;
using SmallBusiness.Infrastructure.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
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

builder.Services.AddHealthChecks();

// Authorization
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddHttpClient<IEmailSender<ApplicationUser>, IdentityEmailSender>();
builder.Services.AddPermissionPolicies();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });
    app.UseHsts();
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHealthChecks("/health");

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
