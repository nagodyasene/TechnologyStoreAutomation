namespace TechnologyStore.Desktop.Features.Email;

/// <summary>
/// Interface for email sending services
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email asynchronously
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlBody">HTML body content</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendEmailAsync(string to, string subject, string htmlBody);
    
    /// <summary>
    /// Sends an email with CC recipients
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="cc">CC recipients (comma-separated)</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlBody">HTML body content</param>
    /// <returns>True if email was sent successfully</returns>
    Task<bool> SendEmailAsync(string to, string? cc, string subject, string htmlBody);
    
    /// <summary>
    /// Checks if the email service is properly configured and authenticated
    /// </summary>
    /// <returns>True if service is ready to send emails</returns>
    Task<bool> IsConfiguredAsync();
}
