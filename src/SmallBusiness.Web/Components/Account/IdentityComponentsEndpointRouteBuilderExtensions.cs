using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Web.Components.Account.Pages;
using SmallBusiness.Web.Components.Account.Pages.Manage;
using SmallBusiness.Infrastructure.Identity;

namespace Microsoft.AspNetCore.Routing;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    // These endpoints are required by the Identity Razor components defined in the /Components/Account/Pages directory of this project.
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var accountGroup = endpoints.MapGroup("/Account");

        accountGroup.MapPost("/PerformExternalLogin", (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string provider,
            [FromForm] string returnUrl) =>
        {
            IEnumerable<KeyValuePair<string, StringValues>> query = [
                new("ReturnUrl", returnUrl),
                new("Action", ExternalLogin.LoginCallbackAction)];

            var redirectUrl = UriHelper.BuildRelative(
                context.Request.PathBase,
                "/Account/ExternalLogin",
                QueryString.Create(query));

            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return TypedResults.Challenge(properties, [provider]);
        });

        accountGroup.MapGet("/Logout", (
            HttpContext context,
            [FromServices] IAntiforgery antiforgery,
            [FromQuery] string? returnUrl) =>
        {
            var normalizedReturnUrl = NormalizeLocalReturnUrl(returnUrl);
            var encodedReturnUrl = HtmlEncoder.Default.Encode(normalizedReturnUrl);
            var tokens = antiforgery.GetAndStoreTokens(context);
            var fieldName = HtmlEncoder.Default.Encode(tokens.FormFieldName);
            var requestToken = HtmlEncoder.Default.Encode(tokens.RequestToken ?? string.Empty);

            var html = $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <title>Log Out — SBMS</title>
                    <link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css" />
                    <link rel="stylesheet" href="/app.css" />
                </head>
                <body>
                    <main class="container py-5">
                        <div class="row justify-content-center">
                            <div class="col-12 col-md-8 col-lg-5">
                                <div class="card shadow-sm">
                                    <div class="card-body">
                                        <h1 class="h3">Log out</h1>
                                        <p class="text-muted">End your current SBMS session.</p>
                                        <form method="post" action="/Account/Logout?returnUrl={{encodedReturnUrl}}" data-enhance="false">
                                            <input name="{{fieldName}}" type="hidden" value="{{requestToken}}" />
                                            <button type="submit" class="btn btn-danger">Log out</button>
                                            <a href="/" class="btn btn-outline-secondary ms-2">Cancel</a>
                                        </form>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </main>
                </body>
                </html>
                """;

            return Results.Content(html, "text/html");
        }).RequireAuthorization();

        accountGroup.MapPost("/Logout", async (
            HttpContext context,
            [FromServices] IAntiforgery antiforgery,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromQuery] string? returnUrl) =>
        {
            await antiforgery.ValidateRequestAsync(context);
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect($"~/{NormalizeLocalReturnUrl(returnUrl)}");
        }).RequireAuthorization();

        accountGroup.MapGet("/SelectBusiness", async (
            HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IApplicationDbContext dbContext,
            [FromQuery] Guid businessId,
            [FromQuery] string? returnUrl) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.Challenge();
            }

            var hasActiveMembership = await dbContext.BusinessUsers
                .IgnoreQueryFilters()
                .AnyAsync(bu => bu.BusinessId == businessId && bu.UserId == user.Id && bu.IsActive);
            var isSysAdmin = context.User.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == "SysAdmin");
            var businessExists = await dbContext.Businesses
                .IgnoreQueryFilters()
                .AnyAsync(b => b.Id == businessId);

            if ((!hasActiveMembership && !isSysAdmin) || !businessExists)
            {
                return Results.Forbid();
            }

            var claims = await userManager.GetClaimsAsync(user);
            var existingClaim = claims.FirstOrDefault(c => c.Type == "BusinessId");
            if (existingClaim is not null)
            {
                await userManager.RemoveClaimAsync(user, existingClaim);
            }

            await userManager.AddClaimAsync(user, new Claim("BusinessId", businessId.ToString()));
            await signInManager.RefreshSignInAsync(user);

            return TypedResults.LocalRedirect($"~/{NormalizeLocalReturnUrl(returnUrl)}");
        }).RequireAuthorization();

        var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();

        manageGroup.MapPost("/LinkExternalLogin", async (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string provider) =>
        {
            // Clear the existing external cookie to ensure a clean login process
            await context.SignOutAsync(IdentityConstants.ExternalScheme);

            var redirectUrl = UriHelper.BuildRelative(
                context.Request.PathBase,
                "/Account/Manage/ExternalLogins",
                QueryString.Create("Action", ExternalLogins.LinkLoginCallbackAction));

            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, signInManager.UserManager.GetUserId(context.User));
            return TypedResults.Challenge(properties, [provider]);
        });

        var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var downloadLogger = loggerFactory.CreateLogger("DownloadPersonalData");

        manageGroup.MapPost("/DownloadPersonalData", async (
            HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] AuthenticationStateProvider authenticationStateProvider) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
            }

            var userId = await userManager.GetUserIdAsync(user);
            downloadLogger.LogInformation("User with ID '{UserId}' asked for their personal data.", userId);

            // Only include personal data for download
            var personalData = new Dictionary<string, string>();
            var personalDataProps = typeof(ApplicationUser).GetProperties().Where(
                prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
            foreach (var p in personalDataProps)
            {
                personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
            }

            var logins = await userManager.GetLoginsAsync(user);
            foreach (var l in logins)
            {
                personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
            }

            personalData.Add("Authenticator Key", (await userManager.GetAuthenticatorKeyAsync(user))!);
            var fileBytes = JsonSerializer.SerializeToUtf8Bytes(personalData);

            context.Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
            return TypedResults.File(fileBytes, contentType: "application/json", fileDownloadName: "PersonalData.json");
        });

        return accountGroup;
    }

    private static string NormalizeLocalReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "dashboard";
        }

        if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            return "dashboard";
        }

        return returnUrl.TrimStart('/');
    }
}
