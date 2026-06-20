namespace SmtpForAI.Services;

/// <summary>
/// Why a send failed. Maps to the CLI exit code and to the MCP error category.
/// </summary>
internal enum SendErrorKind
{
    None = 0,
    Config = 1,        // misconfiguration, policy/allowlist violation, bad input
    SendFailure = 2,   // SMTP itself failed (auth, TLS, network, server-side reject)
}

/// <summary>
/// Result of a <see cref="MailSender"/> call. CLI maps this to exit codes and
/// JSON; MCP tools return the same fields as a structured response.
/// </summary>
internal sealed record SendResult(
    bool Ok,
    SendErrorKind ErrorKind,
    string? Error,
    string? MessageId,
    int Recipients,
    bool DryRun)
{
    public static SendResult Success(string? messageId, int recipients) =>
        new(true, SendErrorKind.None, null, messageId, recipients, false);

    public static SendResult SuccessfulDryRun(int recipients) =>
        new(true, SendErrorKind.None, null, null, recipients, true);

    public static SendResult Fail(SendErrorKind kind, string error, int recipients = 0, bool dryRun = false) =>
        new(false, kind, error, null, recipients, dryRun);
}
