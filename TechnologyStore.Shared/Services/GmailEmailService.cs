using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Config;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using System.Text;

namespace TechnologyStore.Shared.Services;

/// <summary>
/// Gmail API-based email service for sending emails through a user's Gmail account.
/// Supports OAuth 2.0 authentication and test mode for development.
/// </summary>
public class GmailEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<GmailEmailService> _logger;
    private GmailService? _gmailService;

    // Gmail API scopes required for sending emails
    private static readonly string[] Scopes = { Google.Apis.Gmail.v1.GmailService.Scope.GmailSend };
    private const string ApplicationName = "TechnologyStoreAutomation";

    public GmailEmailService(EmailSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = AppLogger.CreateLogger<GmailEmailService>();
    }

    /// <summary>
    /// Initializes the Gmail service with OAuth 2.0 credentials
    /// </summary>
    private async Task<GmailService?> GetGmailServiceAsync()
    {
        if (_gmailService != null)
            return _gmailService;

        try
        {
            var credentialsPath = _settings.GmailCredentialsPath;

            if (!File.Exists(credentialsPath))
            {
                _logger.LogError("Gmail credentials file not found at: {Path}", credentialsPath);
                return null;
            }

            UserCredential credential;

            using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
            {
                // The token is stored in the specified folder
                var secrets = await GoogleClientSecrets.FromStreamAsync(stream);
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets.Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(_settings.TokenStorePath, true));
            }

            _logger.LogInformation("Gmail API authorized successfully");

            _gmailService = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });

            return _gmailService;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Gmail service");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendEmailAsync(string to, string subject, string htmlBody)
    {
        return await SendEmailAsync(to, null, subject, htmlBody);
    }

    /// <inheritdoc />
    public async Task<bool> SendEmailAsync(string to, string? cc, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            _logger.LogWarning("Cannot send email: recipient address is empty");
            return false;
        }

        // Test mode - log instead of sending
        if (_settings.TestMode)
        {
            _logger.LogInformation(
                "📧 TEST MODE - Email would be sent:\n" +
                "   To: {To}\n" +
                "   CC: {Cc}\n" +
                "   Subject: {Subject}\n" +
                "   Body Preview: {BodyPreview}...",
                to,
                cc ?? "(none)",
                subject,
                htmlBody.Length > 100 ? htmlBody[..100] : htmlBody);

            return true; // Pretend it succeeded in test mode
        }

        try
        {
            var gmailService = await GetGmailServiceAsync();
            if (gmailService == null)
            {
                _logger.LogError("Gmail service not available - cannot send email");
                return false;
            }

            var message = CreateMimeMessage(to, cc, subject, htmlBody);
            var gmailMessage = new Google.Apis.Gmail.v1.Data.Message
            {
                Raw = Base64UrlEncode(message)
            };

            await gmailService.Users.Messages.Send(gmailMessage, "me").ExecuteAsync();

            _logger.LogInformation("Email sent successfully to {To} with subject: {Subject}", to, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsConfiguredAsync()
    {
        // In test mode, we're always "configured"
        if (_settings.TestMode)
        {
            _logger.LogDebug("Email service is in test mode - configuration check passes");
            return true;
        }

        // Check if credentials file exists
        if (!File.Exists(_settings.GmailCredentialsPath))
        {
            _logger.LogWarning("Gmail credentials file not found: {Path}", _settings.GmailCredentialsPath);
            return false;
        }

        // Try to initialize the service
        var service = await GetGmailServiceAsync();
        return service != null;
    }

    /// <summary>
    /// Creates a MIME message string for the email
    /// </summary>
    private string CreateMimeMessage(string to, string? cc, string subject, string htmlBody)
    {
        var sb = new StringBuilder();

        // Headers
        sb.AppendLine($"From: {_settings.SenderEmail}");
        sb.AppendLine($"To: {to}");

        if (!string.IsNullOrWhiteSpace(cc))
        {
            sb.AppendLine($"Cc: {cc}");
        }

        sb.AppendLine($"Subject: {subject}");
        sb.AppendLine("MIME-Version: 1.0");
        sb.AppendLine("Content-Type: text/html; charset=utf-8");
        sb.AppendLine();

        // Body
        sb.Append(htmlBody);

        return sb.ToString();
    }

    /// <summary>
    /// Encodes the message to base64url format required by Gmail API
    /// </summary>
    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }
}
