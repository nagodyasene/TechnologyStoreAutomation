using TechnologyStore.Desktop.Features.Email;

namespace TechnologyStore.Desktop.Config;

/// <summary>
/// Wraps the Desktop IEmailService to implement Shared.Interfaces.IEmailService
/// This bridges the gap between the Desktop and Shared namespaces
/// </summary>
internal class SharedEmailServiceWrapper : TechnologyStore.Shared.Interfaces.IEmailService
{
    private readonly IEmailService _desktopEmailService;

    public SharedEmailServiceWrapper(IEmailService desktopEmailService)
    {
        _desktopEmailService = desktopEmailService;
    }

    public Task<bool> SendEmailAsync(string to, string subject, string htmlBody)
    {
        return _desktopEmailService.SendEmailAsync(to, subject, htmlBody);
    }

    public Task<bool> SendEmailAsync(string to, string? cc, string subject, string htmlBody)
    {
        // Desktop IEmailService may not support CC, just forward to simple version
        return _desktopEmailService.SendEmailAsync(to, subject, htmlBody);
    }

    public Task<bool> IsConfiguredAsync()
    {
        return _desktopEmailService.IsConfiguredAsync();
    }
}

