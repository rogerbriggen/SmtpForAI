namespace SmtpForAI.Cli;

/// <summary>Process exit codes shared across commands.</summary>
internal static class ExitCodes
{
    public const int Success = 0;
    public const int Usage = 1;        // unknown/incomplete command-line
    public const int Config = 2;       // not configured, or a validation/policy failure
    public const int SendFailure = 3;  // the SMTP send itself failed
}
