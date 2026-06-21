using System.Reflection;
using SmtpForAI.Cli;
using SmtpForAI.Commands;
using SmtpForAI.Configuration;
using SmtpForAI.Mcp;

namespace SmtpForAI;

internal static class Program
{
    private static int Main(string[] args)
    {
        var app = AppConfiguration.Create();
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        var rest = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "send" => SendCommand.Run(rest, app),
                "config" => ConfigCommand.Run(rest, app),
                "mcp" => McpCommand.Run(rest, app),
                "help" or "--help" or "-h" => PrintUsage(ExitCodes.Success),
                "version" or "--version" or "-v" => PrintVersion(),
                "" => StatusThenUsage(app),
                _ => UnknownCommand(command),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return ExitCodes.SendFailure;
        }
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        return PrintUsage(ExitCodes.Usage);
    }

    /// <summary>No command: show config status (so an unconfigured tool guides the user) then usage.</summary>
    private static int StatusThenUsage(AppConfiguration app)
    {
        var settings = app.LoadSettings();
        if (!settings.IsConfigured)
        {
            Console.WriteLine("SmtpForAI is not configured yet.");
            Console.WriteLine($"  Missing: {string.Join(", ", settings.MissingRequiredFields())}");
            Console.WriteLine("  Run 'SmtpForAI config' for interactive setup, or 'config set --host ... --password ...'.");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("SmtpForAI is configured and ready. Use 'send' to send an email.");
            Console.WriteLine();
        }
        return PrintUsage(ExitCodes.Success);
    }

    private static int PrintVersion()
    {
        var asm = typeof(Program).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "unknown";
        Console.WriteLine($"SmtpForAI {info}");
        return ExitCodes.Success;
    }

    private static int PrintUsage(int exitCode)
    {
        Console.WriteLine(
            """
            SmtpForAI — send email over SMTP from the command line.

            Usage:
              SmtpForAI config                 Interactive setup (host, auth, allowlist, password)
              SmtpForAI config set [options]   Non-interactive setup, e.g.:
                  --host --port --use-ssl --username --from --display --password
                  --allow-recipient <addr>   (repeatable)   --allow-domain <domain> (repeatable)
                  --max-recipients --max-attachment-bytes
              SmtpForAI config show            Print current config (password is never shown)

              SmtpForAI send [options]
                  --to <addr>        Recipient (repeatable, or comma-separated)
                  --cc <addr>        Cc recipient (repeatable)
                  --bcc <addr>       Bcc recipient (repeatable)
                  --subject <text>   Subject line
                  --body <text>      Body text (or --body-file <path>, or piped via stdin)
                  --body-file <path> Read the body from a file
                  --html             Treat the body as HTML
                  --from <addr>      Override the configured From address
                  --attach <path>    Attachment (repeatable)
                  --json             Emit a machine-readable result: {"ok":true|false,...}
                  --dry-run          Validate (incl. allowlist/limits) without sending

              SmtpForAI mcp                    Run as an MCP (Model Context Protocol) server over stdio.
                                               Exposes send_email, validate_recipient, get_config_status.
                                               Same allowlist/limits as the CLI; intended to be launched
                                               by an MCP client (e.g. Claude Desktop), not run manually.

              SmtpForAI version                Print the build version (also: --version, -v).

            Exit codes: 0 success, 1 usage error, 2 config/policy error, 3 send failure.
            """);
        return exitCode;
    }
}
