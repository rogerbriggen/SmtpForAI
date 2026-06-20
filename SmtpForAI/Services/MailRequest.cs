namespace SmtpForAI.Services;

/// <summary>
/// Plain input record for <see cref="MailSender"/>. Built by either the CLI
/// (<c>SendCommand</c>) or the MCP tool (<c>EmailTool</c>) so both surfaces
/// share one validation + send code path.
/// </summary>
internal sealed record MailRequest(
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc,
    string Subject,
    string Body,
    bool IsHtml,
    string? From,
    IReadOnlyList<string> AttachmentPaths)
{
    public static MailRequest Create(
        IEnumerable<string>? to = null,
        IEnumerable<string>? cc = null,
        IEnumerable<string>? bcc = null,
        string subject = "",
        string body = "",
        bool isHtml = false,
        string? from = null,
        IEnumerable<string>? attachments = null) => new(
            (to ?? Array.Empty<string>()).ToArray(),
            (cc ?? Array.Empty<string>()).ToArray(),
            (bcc ?? Array.Empty<string>()).ToArray(),
            subject,
            body,
            isHtml,
            from,
            (attachments ?? Array.Empty<string>()).ToArray());
}
