using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using Signavex.Domain.Configuration;
using Signavex.Infrastructure.Persistence;

namespace Signavex.Infrastructure.Email;

public class SendGridEmailSender : IEmailSender<ApplicationUser>
{
    private readonly EmailOptions _options;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(IOptions<EmailOptions> options, ILogger<SendGridEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var subject = "Confirm your Signavex account";
        var html = WrapTemplate($@"
            <h2>Welcome to Signavex!</h2>
            <p>Thanks for signing up. Please confirm your email address by clicking the link below:</p>
            <p style=""text-align: center; margin: 24px 0;"">
                <a href=""{confirmationLink}"" style=""display: inline-block; padding: 12px 24px; background-color: #6750A4; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: 600;"">
                    Confirm Email
                </a>
            </p>
            <p style=""font-size: 13px; color: #666;"">If you didn't create this account, you can safely ignore this email.</p>");

        await SendAsync(email, subject, html);
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var subject = "Reset your Signavex password";
        var html = WrapTemplate($@"
            <h2>Password Reset</h2>
            <p>We received a request to reset your password. Click the link below to choose a new password:</p>
            <p style=""text-align: center; margin: 24px 0;"">
                <a href=""{resetLink}"" style=""display: inline-block; padding: 12px 24px; background-color: #6750A4; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: 600;"">
                    Reset Password
                </a>
            </p>
            <p style=""font-size: 13px; color: #666;"">If you didn't request this, you can safely ignore this email. Your password won't change.</p>");

        await SendAsync(email, subject, html);
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var subject = "Your Signavex password reset code";
        var html = WrapTemplate($@"
            <h2>Password Reset Code</h2>
            <p>Your password reset code is:</p>
            <p style=""text-align: center; margin: 24px 0; font-size: 28px; font-weight: bold; letter-spacing: 4px; color: #6750A4;"">{resetCode}</p>
            <p style=""font-size: 13px; color: #666;"">If you didn't request this, you can safely ignore this email.</p>");

        await SendAsync(email, subject, html);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlContent)
    {
        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogWarning("Email not sent — SendGrid API key not configured. Subject: {Subject}, To: {Email}", subject, toEmail);
            return;
        }

        var client = new SendGridClient(_options.ApiKey);
        var from = new EmailAddress(_options.FromAddress, _options.FromName);
        var to = new EmailAddress(toEmail);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);

        var response = await client.SendEmailAsync(msg);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Email sent: {Subject} to {Email}", subject, toEmail);
        }
        else
        {
            var body = await response.Body.ReadAsStringAsync();
            _logger.LogWarning("Email send failed ({StatusCode}): {Subject} to {Email}. Response: {Body}",
                response.StatusCode, subject, toEmail, body);
        }
    }

    private static string WrapTemplate(string bodyContent)
    {
        return $@"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" /></head>
<body style=""margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #f5f5f5;"">
    <div style=""max-width: 560px; margin: 40px auto; background: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1);"">
        <div style=""background-color: #1C1B1F; padding: 20px 24px; text-align: center;"">
            <span style=""color: #ffffff; font-size: 20px; font-weight: bold;"">Signavex</span>
        </div>
        <div style=""padding: 32px 24px;"">
            {bodyContent}
        </div>
        <div style=""padding: 16px 24px; background-color: #f9f9f9; border-top: 1px solid #eee; text-align: center;"">
            <p style=""margin: 0; font-size: 11px; color: #999;"">
                Signavex is an educational stock screening tool. It does not provide financial advice.
            </p>
        </div>
    </div>
</body>
</html>";
    }
}
