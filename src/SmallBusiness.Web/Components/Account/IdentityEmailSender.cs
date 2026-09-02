using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using SmallBusiness.Infrastructure.Identity;

namespace SmallBusiness.Web.Components.Account;

internal sealed class IdentityEmailSender(
    HttpClient httpClient,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<IdentityEmailSender> logger)
    : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your SBMS account", $"Please confirm your account by <a href=\"{confirmationLink}\">clicking here</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your SBMS password", $"Please reset your password by <a href=\"{resetLink}\">clicking here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Reset your SBMS password", $"Please reset your password using the following code: {WebUtility.HtmlEncode(resetCode)}");

    private async Task SendAsync(string toEmail, string subject, string htmlMessage)
    {
        var apiKey = configuration["Email:SendGrid:ApiKey"];
        var fromEmail = configuration["Email:FromEmail"];
        var fromName = configuration["Email:FromName"] ?? "Small Business Management System";

        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(fromEmail))
        {
            var response = await httpClient.PostAsJsonAsync(
                "https://api.sendgrid.com/v3/mail/send",
                new
                {
                    personalizations = new[]
                    {
                        new
                        {
                            to = new[] { new { email = toEmail } }
                        }
                    },
                    from = new { email = fromEmail, name = fromName },
                    subject,
                    content = new[]
                    {
                        new { type = "text/html", value = htmlMessage }
                    }
                });

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                logger.LogError(
                    "SendGrid email delivery failed with HTTP {StatusCode}. Recipient={RecipientEmail}. From={FromEmail}. ResponseBody={ResponseBody}",
                    (int)response.StatusCode,
                    toEmail,
                    fromEmail,
                    responseBody);

                throw new InvalidOperationException($"SendGrid email delivery failed with HTTP {(int)response.StatusCode}.");
            }

            return;
        }

        if (environment.IsDevelopment() &&
            string.Equals(configuration["Email:DevelopmentMode"], "Log", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "Development email generated for {EmailAddress}: {Subject}. Email body omitted to avoid logging confirmation or reset tokens.",
                toEmail,
                subject);
            return;
        }

        throw new InvalidOperationException(
            "Email delivery is not configured. Set Email:SendGrid:ApiKey and Email:FromEmail, or set Email:DevelopmentMode=Log in Development only.");
    }
}
