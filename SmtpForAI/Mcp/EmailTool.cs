using System.ComponentModel;
using ModelContextProtocol.Server;
using SmtpForAI.Configuration;
using SmtpForAI.Security;
using SmtpForAI.Services;

namespace SmtpForAI.Mcp;

/// <summary>
/// MCP tools exposed by <c>SmtpForAI mcp</c>. Every tool goes through the same
/// <see cref="MailSender"/> / <see cref="MailValidation"/> / <see cref="SmtpSettings"/>
/// pipeline as the CLI, so the allowlist / per-message limits / fail-closed empty-
/// allowlist behavior cannot diverge between CLI and MCP. There is intentionally
/// no <c>set_config</c> tool — credentials and policy stay CLI-only so an AI prompt
/// cannot relax them.
/// </summary>
[McpServerToolType]
internal sealed class EmailTool
{
    private readonly AppConfiguration _app;
    private readonly MailSender _sender;

    public EmailTool(AppConfiguration app, MailSender sender)
    {
        _app = app;
        _sender = sender;
    }

    [McpServerTool(Name = "send_email")]
    [Description(
        "Send an email over SMTP. Recipients are checked against the configured allowlist " +
        "and per-message limits; sending is refused if the tool is not configured. " +
        "Set dryRun=true to validate against the policy without sending.")]
    public SendEmailResponse SendEmail(
        [Description("Primary (To) recipients. Required.")]
        string[] to,
        [Description("Subject line.")]
        string subject,
        [Description("Message body. Plain text unless isHtml=true.")]
        string body,
        [Description("Cc recipients.")]
        string[]? cc = null,
        [Description("Bcc recipients.")]
        string[]? bcc = null,
        [Description("Treat the body as HTML.")]
        bool isHtml = false,
        [Description("Override the configured From address.")]
        string? from = null,
        [Description("Absolute paths to files to attach. Each file must exist and respect MaxAttachmentBytes.")]
        string[]? attachments = null,
        [Description("Validate against the policy without actually sending. Recommended before a real send.")]
        bool dryRun = false)
    {
        var request = MailRequest.Create(
            to: to,
            cc: cc,
            bcc: bcc,
            subject: subject ?? "",
            body: body ?? "",
            isHtml: isHtml,
            from: from,
            attachments: attachments);

        var settings = _app.LoadSettings();
        var result = dryRun ? _sender.DryRun(request, settings) : _sender.Send(request, settings);
        return SendEmailResponse.From(result);
    }

    [McpServerTool(Name = "validate_recipient")]
    [Description(
        "Check whether an email address is well-formed and would be accepted by the " +
        "recipient allowlist. Does not send anything.")]
    public ValidateRecipientResponse ValidateRecipient(
        [Description("Email address to check.")]
        string address)
    {
        var settings = _app.LoadSettings();

        if (!MailValidation.TryNormalizeAddress(address ?? "", out var normalized, out var domain))
            return new ValidateRecipientResponse(false, false, $"Invalid email address: '{address}'.", null, null);

        var allowed = MailValidation.IsRecipientAllowed(normalized, settings);
        var reason = allowed
            ? null
            : settings.HasEmptyAllowlist
                ? "The allowlist is empty; no recipients are allowed. Configure Security:AllowedRecipients or Security:AllowedDomains."
                : "Recipient is not on the allowlist.";
        return new ValidateRecipientResponse(true, allowed, reason, normalized, domain);
    }

    [McpServerTool(Name = "get_config_status")]
    [Description(
        "Report whether SmtpForAI is configured and ready to send. Never returns the SMTP password.")]
    public ConfigStatusResponse GetConfigStatus()
    {
        var s = _app.LoadSettings();
        return new ConfigStatusResponse(
            Configured: s.IsConfigured,
            Missing: s.MissingRequiredFields().ToArray(),
            HasPassword: s.HasPassword,
            Host: NullIfEmpty(s.Host),
            Port: s.Port,
            UseSsl: s.UseSsl,
            FromAddress: NullIfEmpty(s.FromAddress),
            AllowedRecipientsCount: s.AllowedRecipients.Count,
            AllowedDomainsCount: s.AllowedDomains.Count,
            MaxRecipients: s.MaxRecipients,
            MaxAttachmentBytes: s.MaxAttachmentBytes);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}

internal sealed record SendEmailResponse(
    bool Ok,
    string? Error,
    string? MessageId,
    int Recipients,
    bool DryRun)
{
    public static SendEmailResponse From(SendResult r) =>
        new(r.Ok, r.Error, r.MessageId, r.Recipients, r.DryRun);
}

internal sealed record ValidateRecipientResponse(
    bool WellFormed,
    bool Allowed,
    string? Reason,
    string? NormalizedAddress,
    string? Domain);

internal sealed record ConfigStatusResponse(
    bool Configured,
    string[] Missing,
    bool HasPassword,
    string? Host,
    int Port,
    bool UseSsl,
    string? FromAddress,
    int AllowedRecipientsCount,
    int AllowedDomainsCount,
    int MaxRecipients,
    long MaxAttachmentBytes);
