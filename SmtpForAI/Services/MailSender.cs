using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SmtpForAI.Configuration;
using SmtpForAI.Security;

namespace SmtpForAI.Services;

/// <summary>
/// The single code path that turns a <see cref="MailRequest"/> into either a
/// real SMTP send or a validation-only "dry run". Both the CLI and the MCP
/// server call into here so the security policy can't diverge between them.
/// </summary>
internal sealed class MailSender
{
    /// <summary>
    /// Validate without touching the network. Returns Ok if a real
    /// <see cref="Send"/> with the same request would pass the policy gate.
    /// </summary>
    public SendResult DryRun(MailRequest request, SmtpSettings settings) =>
        Prepare(request, settings, dryRun: true, out var ctx) ?? SendResult.SuccessfulDryRun(ctx.RecipientCount);

    /// <summary>Validate and send.</summary>
    public SendResult Send(MailRequest request, SmtpSettings settings)
    {
        var failure = Prepare(request, settings, dryRun: false, out var ctx);
        if (failure is not null) return failure;

        // --- build the message (address parse errors are input/policy errors) ---
        MimeMessage message;
        try
        {
            message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromDisplayName ?? string.Empty, ctx.From!));
            AddAll(message.To, request.To);
            AddAll(message.Cc, request.Cc);
            AddAll(message.Bcc, request.Bcc);
            message.Subject = request.Subject;

            var builder = new BodyBuilder();
            if (request.IsHtml)
                builder.HtmlBody = request.Body;
            else
                builder.TextBody = request.Body;
            foreach (var path in request.AttachmentPaths)
                builder.Attachments.Add(path);
            message.Body = builder.ToMessageBody();
        }
        catch (ParseException ex)
        {
            return SendResult.Fail(SendErrorKind.Config, $"Could not build the message: {ex.Message}", ctx.RecipientCount);
        }

        // --- send ---
        try
        {
            using var client = new SmtpClient();
            client.Connect(settings.Host, settings.Port, ResolveSocketOptions(settings));
            client.Authenticate(settings.Username, settings.Password!);
            client.Send(message);
            client.Disconnect(quit: true);
        }
        catch (Exception ex)
        {
            return SendResult.Fail(SendErrorKind.SendFailure, $"SMTP send failed: {ex.Message}", ctx.RecipientCount);
        }

        return SendResult.Success(message.MessageId, ctx.RecipientCount);
    }

    // ---- shared validation pipeline ----

    private readonly record struct PrepCtx(int RecipientCount, string? From);

    /// <summary>
    /// Runs the policy checks shared by Send and DryRun. Returns null on success
    /// (and fills <paramref name="ctx"/>), otherwise a failure result.
    /// </summary>
    private static SendResult? Prepare(MailRequest request, SmtpSettings settings, bool dryRun, out PrepCtx ctx)
    {
        ctx = default;

        foreach (var path in request.AttachmentPaths)
        {
            if (!File.Exists(path))
                return SendResult.Fail(SendErrorKind.Config, $"Attachment not found: {path}", dryRun: dryRun);
        }
        var attachmentSizes = request.AttachmentPaths
            .Select(p => new FileInfo(p).Length)
            .ToList();

        var allRecipients = request.To.Concat(request.Cc).Concat(request.Bcc).ToList();

        var policyError = MailValidation.Validate(allRecipients, attachmentSizes, settings);
        if (policyError is not null)
            return SendResult.Fail(SendErrorKind.Config, policyError, allRecipients.Count, dryRun);

        if (dryRun)
        {
            ctx = new PrepCtx(allRecipients.Count, null);
            return null;
        }

        if (!settings.IsConfigured)
            return SendResult.Fail(SendErrorKind.Config,
                $"Not configured. Missing: {string.Join(", ", settings.MissingRequiredFields())}. Run 'config' first.",
                allRecipients.Count);

        var from = string.IsNullOrEmpty(request.From) ? settings.FromAddress : request.From;
        if (string.IsNullOrEmpty(from))
            return SendResult.Fail(SendErrorKind.Config, "No From address (set Smtp:FromAddress or pass --from).",
                allRecipients.Count);

        if (!MailValidation.TryNormalizeAddress(from, out _, out _))
            return SendResult.Fail(SendErrorKind.Config, $"Invalid From address: '{from}'.", allRecipients.Count);

        ctx = new PrepCtx(allRecipients.Count, from);
        return null;
    }

    private static SecureSocketOptions ResolveSocketOptions(SmtpSettings s)
    {
        if (!s.UseSsl)
            return SecureSocketOptions.None;
        return s.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
    }

    private static void AddAll(InternetAddressList list, IEnumerable<string> addresses)
    {
        foreach (var address in addresses)
            list.Add(MailboxAddress.Parse(address));
    }
}
