using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Infrastructure.Data;
using SmallBusiness.Infrastructure.Identity;

namespace SmallBusiness.Web.Tests;

public class IdentityFlowTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    [Fact]
    public async Task Registration_PersistsUserAndGeneratesConfirmationToken()
    {
        await using var provider = CreateIdentityProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "new@example.test", Email = "new@example.test" };

        var result = await userManager.CreateAsync(user, "Password1");

        Assert.True(result.Succeeded);
        Assert.NotNull(await userManager.FindByEmailAsync("new@example.test"));

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        Assert.False(string.IsNullOrWhiteSpace(encoded));
    }

    [Fact]
    public async Task UnconfirmedAccount_CannotSignInWhenConfirmationIsRequired()
    {
        await using var provider = CreateIdentityProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = provider.GetRequiredService<SignInManager<ApplicationUser>>();
        var user = await CreateUserAsync(userManager, "unconfirmed@example.test");

        var result = await signInManager.CheckPasswordSignInAsync(user, "Password1", lockoutOnFailure: false);

        Assert.True(result.IsNotAllowed);
    }

    [Fact]
    public async Task ConfirmedAccount_CanSignIn()
    {
        await using var provider = CreateIdentityProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = provider.GetRequiredService<SignInManager<ApplicationUser>>();
        var user = await CreateUserAsync(userManager, "confirmed@example.test");
        await ConfirmEmailAsync(userManager, user);

        var result = await signInManager.CheckPasswordSignInAsync(user, "Password1", lockoutOnFailure: false);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task WrongPassword_FailsForConfirmedAccount()
    {
        await using var provider = CreateIdentityProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = provider.GetRequiredService<SignInManager<ApplicationUser>>();
        var user = await CreateUserAsync(userManager, "wrong-password@example.test");
        await ConfirmEmailAsync(userManager, user);

        var result = await signInManager.CheckPasswordSignInAsync(user, "NotThePassword1", lockoutOnFailure: false);

        Assert.True(result.Succeeded is false);
    }

    [Fact]
    public async Task DuplicateEmail_IsRejected()
    {
        await using var provider = CreateIdentityProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        await CreateUserAsync(userManager, "duplicate@example.test");

        var duplicate = new ApplicationUser { UserName = "duplicate@example.test", Email = "duplicate@example.test" };
        var result = await userManager.CreateAsync(duplicate, "Password1");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == nameof(IdentityErrorDescriber.DuplicateEmail));
    }

    [Fact]
    public async Task ForgotPassword_ForConfirmedUserGeneratesResetToken()
    {
        await using var provider = CreateIdentityProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await CreateUserAsync(userManager, "reset@example.test");
        await ConfirmEmailAsync(userManager, user);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        Assert.False(string.IsNullOrWhiteSpace(encoded));
    }

    [Fact]
    public async Task ForgotPassword_ForUnknownEmailRemainsNonEnumeratingInPageFlow()
    {
        await using var provider = CreateIdentityProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var page = ReadComponent("Account/Pages/ForgotPassword.razor");

        Assert.Null(await userManager.FindByEmailAsync("missing@example.test"));
        Assert.Contains("user is null || !(await UserManager.IsEmailConfirmedAsync(user))", page);
        Assert.Contains("Don't reveal that the user does not exist or is not confirmed", page);
        Assert.Contains("RedirectManager.RedirectTo(\"Account/ForgotPasswordConfirmation\")", page);
    }

    [Fact]
    public async Task ResendConfirmation_GeneratesFreshConfirmationTokens()
    {
        await using var provider = CreateIdentityProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await CreateUserAsync(userManager, "resend@example.test");

        var first = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var second = await userManager.GenerateEmailConfirmationTokenAsync(user);

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.False(string.IsNullOrWhiteSpace(second));
    }

    [Fact]
    public void Register_DoesNotAutomaticallySignInBeforeEmailConfirmation()
    {
        var source = ReadComponent("Account/Pages/Register.razor");

        Assert.DoesNotContain("SignInManager.SignInAsync", source);
        Assert.Contains("EmailSender.SendConfirmationLinkAsync", source);
        Assert.Contains("Account/RegisterConfirmation", source);
    }

    [Fact]
    public void Login_RoutesFirstBusinessAndExistingBusinessUsersCorrectly()
    {
        var source = ReadComponent("Account/Pages/Login.razor");

        Assert.Contains("GetDefaultSignedInUrlAsync", source);
        Assert.Contains("\"/onboarding/business\"", source);
        Assert.Contains("\"/dashboard\"", source);
        Assert.Contains("dbContext.BusinessUsers", source);
        Assert.Contains("bu.UserId == user.Id && bu.IsActive", source);
    }

    [Fact]
    public async Task BusinessMembership_QueryDistinguishesFirstLoginFromExistingBusinessUser()
    {
        await using var provider = CreateIdentityProvider();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();
        var firstLoginUserId = "first-login-user";
        var existingUserId = "existing-user";
        dbContext.Businesses.Add(new Business { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Acme" });
        dbContext.BusinessUsers.Add(new BusinessUser
        {
            BusinessId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId = existingUserId,
            Role = "Owner",
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var firstLoginHasBusiness = await dbContext.BusinessUsers
            .IgnoreQueryFilters()
            .AnyAsync(bu => bu.UserId == firstLoginUserId && bu.IsActive);
        var existingHasBusiness = await dbContext.BusinessUsers
            .IgnoreQueryFilters()
            .AnyAsync(bu => bu.UserId == existingUserId && bu.IsActive);

        Assert.False(firstLoginHasBusiness);
        Assert.True(existingHasBusiness);
    }

    [Fact]
    public void Program_RegistersConfigurableEmailSenderAndRequiresConfirmedAccounts()
    {
        var program = ReadFile("src", "SmallBusiness.Web", "Program.cs");
        var infrastructure = ReadFile("src", "SmallBusiness.Infrastructure", "DependencyInjection.cs");
        var sender = ReadComponent("Account/IdentityEmailSender.cs");

        Assert.Contains("AddHttpClient<IEmailSender<ApplicationUser>, IdentityEmailSender>()", program);
        Assert.DoesNotContain("IdentityNoOpEmailSender", program);
        Assert.Contains("options.SignIn.RequireConfirmedAccount = true", infrastructure);
        Assert.Contains("Email:SendGrid:ApiKey", sender);
        Assert.Contains("Email:FromEmail", sender);
        Assert.Contains("Email:DevelopmentMode", sender);
    }

    [Fact]
    public void ExternalLoginPicker_HidesProviderUiWhenNoProvidersAreConfigured()
    {
        var source = ReadComponent("Account/Shared/ExternalLoginPicker.razor");

        Assert.Contains("@if (externalLogins.Length > 0)", source);
        Assert.DoesNotContain("There are no external authentication services configured", source);
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, "Password1");

        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        return user;
    }

    private static async Task ConfirmEmailAsync(UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var result = await userManager.ConfirmEmailAsync(user, token);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
    }

    private static ServiceProvider CreateIdentityProvider()
    {
        var services = new ServiceCollection();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.CurrentBusinessId).Returns((Guid?)null);

        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        services.AddSingleton<ITenantContext>(tenantContext.Object);
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }

    private static string ReadComponent(string relativePath) =>
        ReadFile("src", "SmallBusiness.Web", "Components", relativePath);

    private static string ReadFile(params string[] pathParts) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. pathParts]));
}
