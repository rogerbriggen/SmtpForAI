using MimeKit;
using SmtpForAI.Configuration;

namespace SmtpForAI.Security;

/// <summary>
/// Abuse-prevention checks applied before any message is sent.
/// Pure functions so they can be unit tested without sending mail.
/// </summary>
internal static class MailValidation
{
    /// <summary>
    /// Parses and normalizes a single email address. Returns false for anything
    /// MimeKit cannot parse, or that does not have exactly one <c>@</c> with a
    /// non-empty local part and domain (e.g. <c>bad@@example.com</c>).
    /// </summary>
    public static bool TryNormalizeAddress(string address, out string normalized, out string domain)
    {
        normalized = "";
        domain = "";

        if (string.IsNullOrWhiteSpace(address) ||
            !MailboxAddress.TryParse(address, out var mailbox) ||
            mailbox is null)
        {
            return false;
        }

        var addr = mailbox.Address;
        if (string.IsNullOrEmpty(addr))
            return false;

        var firstAt = addr.IndexOf('@');
        var lastAt = addr.LastIndexOf('@');
        if (firstAt <= 0 || firstAt != lastAt || lastAt == addr.Length - 1)
            return false; // zero, multiple, or edge-positioned '@'

        normalized = addr;
        domain = addr[(lastAt + 1)..];
        return true;
    }

    /// <summary>
    /// A recipient passes if it is a well-formed address that matches an entry in
    /// <c>AllowedRecipients</c> (exact, case-insensitive) or whose domain is in
    /// <c>AllowedDomains</c>. With both lists empty, nothing is allowed (fail-closed).
    /// Malformed addresses never pass.
    /// </summary>
    public static bool IsRecipientAllowed(string address, SmtpSettings settings)
    {
        if (!TryNormalizeAddress(address, out var normalized, out var domain))
            return false;

        foreach (var allowed in settings.AllowedRecipients)
        {
            if (string.Equals(allowed, normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var allowedDomain in settings.AllowedDomains)
        {
            if (string.Equals(allowedDomain.TrimStart('@'), domain, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Validates address syntax, recipient count, the allowlist, and attachment sizes.
    /// Returns null when everything passes, otherwise a human-readable reason.
    /// All failures here are input/policy errors (the caller maps them to exit code 2).
    /// </summary>
    public static string? Validate(
        IReadOnlyCollection<string> recipients,
        IReadOnlyCollection<long> attachmentSizes,
        SmtpSettings settings)
    {
        if (recipients.Count == 0)
            return "No recipients specified.";

        if (recipients.Count > settings.MaxRecipients)
            return $"Too many recipients: {recipients.Count} (max {settings.MaxRecipients}).";

        foreach (var recipient in recipients)
        {
            if (!TryNormalizeAddress(recipient, out _, out _))
                return $"Invalid email address: '{recipient}'.";

            if (!IsRecipientAllowed(recipient, settings))
            {
                return settings.HasEmptyAllowlist
                    ? $"Recipient '{recipient}' is not allowed: the allowlist is empty. " +
                      "Add Security:AllowedRecipients or Security:AllowedDomains via the 'config' command."
                    : $"Recipient '{recipient}' is not on the allowlist.";
            }
        }

        foreach (var size in attachmentSizes)
        {
            if (size > settings.MaxAttachmentBytes)
                return $"Attachment exceeds the maximum size of {settings.MaxAttachmentBytes} bytes ({size} bytes).";
        }

        return null;
    }
}
