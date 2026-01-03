namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Optional diagnostics interface for email services to expose last failure reason.
/// </summary>
public interface IEmailServiceDiagnostics
{
    /// <summary>
    /// A user-safe message describing the last email failure (no secrets).
    /// </summary>
    string? LastErrorMessage { get; }
}


