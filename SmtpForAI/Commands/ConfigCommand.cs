using System.Globalization;
using System.Text;
using SmtpForAI.Cli;
using SmtpForAI.Configuration;

namespace SmtpForAI.Commands;

/// <summary>
/// <c>config</c> (interactive setup), <c>config set …</c> (non-interactive),
/// and <c>config show</c>. The password is stored in user secrets; everything
/// else goes to <c>appsettings.json</c>.
/// </summary>
internal static class ConfigCommand
{
    private static readonly HashSet<string> BooleanFlags = new(StringComparer.OrdinalIgnoreCase);

    public static int Run(string[] args, AppConfiguration app)
    {
        var sub = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
            ? args[0].ToLowerInvariant()
            : "";
        var rest = sub.Length > 0 ? args.Skip(1).ToArray() : args;

        return sub switch
        {
            "show" => Show(app),
            "set" => Set(rest, app),
            "" => Interactive(app),
            _ => UnknownSub(sub),
        };
    }

    private static int UnknownSub(string sub)
    {
        Console.Error.WriteLine($"Unknown config subcommand: {sub}");
        Console.Error.WriteLine("Usage: config [show|set] ...   (no subcommand = interactive setup)");
        return ExitCodes.Usage;
    }

    private static int Show(AppConfiguration app)
    {
        var s = app.LoadSettings();
        Console.WriteLine("SmtpForAI configuration");
        Console.WriteLine($"  appsettings.json : {app.AppSettingsPath}");
        Console.WriteLine($"  secrets.json     : {app.SecretsPath}");
        Console.WriteLine();
        Console.WriteLine("[Smtp]");
        Console.WriteLine($"  Host            : {Display(s.Host)}");
        Console.WriteLine($"  Port            : {s.Port}");
        Console.WriteLine($"  UseSsl          : {s.UseSsl}");
        Console.WriteLine($"  Username        : {Display(s.Username)}");
        Console.WriteLine($"  FromAddress     : {Display(s.FromAddress)}");
        Console.WriteLine($"  FromDisplayName : {Display(s.FromDisplayName)}");
        Console.WriteLine($"  Password        : {(s.HasPassword ? "(set)" : "(not set)")}");
        Console.WriteLine();
        Console.WriteLine("[Security]");
        Console.WriteLine($"  AllowedRecipients : {DisplayList(s.AllowedRecipients)}");
        Console.WriteLine($"  AllowedDomains    : {DisplayList(s.AllowedDomains)}");
        Console.WriteLine($"  MaxRecipients     : {s.MaxRecipients}");
        Console.WriteLine($"  MaxAttachmentBytes: {s.MaxAttachmentBytes}");
        Console.WriteLine();

        var missing = s.MissingRequiredFields();
        if (missing.Count == 0)
            Console.WriteLine("Status: configured and ready to send.");
        else
            Console.WriteLine($"Status: NOT configured. Missing: {string.Join(", ", missing)}");

        if (s.HasEmptyAllowlist)
            Console.WriteLine("Warning: the allowlist is empty, so every send will be blocked. " +
                              "Add Security:AllowedRecipients or Security:AllowedDomains.");

        return ExitCodes.Success;
    }

    private static int Set(string[] args, AppConfiguration app)
    {
        var parser = ArgParser.Parse(args, BooleanFlags);
        var current = app.LoadSettings();

        var updated = new SmtpSettings
        {
            Host = parser.Get("host") ?? current.Host,
            Port = ParseInt(parser.Get("port"), current.Port),
            UseSsl = ParseBool(parser.Get("use-ssl") ?? parser.Get("ssl"), current.UseSsl),
            Username = parser.Get("username") ?? current.Username,
            FromAddress = parser.Get("from") ?? current.FromAddress,
            FromDisplayName = parser.Get("display") ?? parser.Get("display-name") ?? current.FromDisplayName,
            AllowedRecipients = parser.Has("allow-recipient") ? parser.GetAll("allow-recipient") : current.AllowedRecipients,
            AllowedDomains = parser.Has("allow-domain") ? parser.GetAll("allow-domain") : current.AllowedDomains,
            MaxRecipients = ParseInt(parser.Get("max-recipients"), current.MaxRecipients),
            MaxAttachmentBytes = ParseLong(parser.Get("max-attachment-bytes"), current.MaxAttachmentBytes),
        };

        AppSettingsWriter.Write(app.AppSettingsPath, updated);

        var password = parser.Get("password");
        if (!string.IsNullOrEmpty(password))
            UserSecretsStore.WritePassword(app.UserSecretsId, password);

        Console.WriteLine($"Saved configuration to {app.AppSettingsPath}");
        if (!string.IsNullOrEmpty(password))
            Console.WriteLine($"Saved password to user secrets ({app.SecretsPath}).");

        return Show(app);
    }

    private static int Interactive(AppConfiguration app)
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Interactive config needs a terminal. Use 'config set --host ... --password ...' instead.");
            return ExitCodes.Usage;
        }

        var current = app.LoadSettings();
        Console.WriteLine("SmtpForAI interactive setup. Press Enter to keep the current value.");
        Console.WriteLine();

        var host = Prompt("SMTP host", current.Host);
        var port = ParseInt(Prompt("SMTP port", current.Port.ToString(CultureInfo.InvariantCulture)), current.Port);
        var useSsl = ParseBool(Prompt("Use SSL/TLS (true/false)", current.UseSsl.ToString()), current.UseSsl);
        var username = Prompt("Username", current.Username);
        var from = Prompt("From address", string.IsNullOrEmpty(current.FromAddress) ? username : current.FromAddress);
        var display = Prompt("From display name", current.FromDisplayName);
        var allowRecipients = PromptList("Allowed recipient addresses (comma-separated)", current.AllowedRecipients);
        var allowDomains = PromptList("Allowed recipient domains (comma-separated)", current.AllowedDomains);
        var maxRecipients = ParseInt(Prompt("Max recipients per message", current.MaxRecipients.ToString(CultureInfo.InvariantCulture)), current.MaxRecipients);

        var updated = new SmtpSettings
        {
            Host = host,
            Port = port,
            UseSsl = useSsl,
            Username = username,
            FromAddress = from,
            FromDisplayName = display,
            AllowedRecipients = allowRecipients,
            AllowedDomains = allowDomains,
            MaxRecipients = maxRecipients,
            MaxAttachmentBytes = current.MaxAttachmentBytes,
        };
        AppSettingsWriter.Write(app.AppSettingsPath, updated);

        var password = ReadSecret($"SMTP password ({(current.HasPassword ? "Enter to keep current" : "required")}): ");
        if (!string.IsNullOrEmpty(password))
            UserSecretsStore.WritePassword(app.UserSecretsId, password);

        Console.WriteLine();
        Console.WriteLine("Saved.");
        return Show(app);
    }

    // ---- prompt helpers ----

    private static string Prompt(string label, string current)
    {
        Console.Write(string.IsNullOrEmpty(current) ? $"{label}: " : $"{label} [{current}]: ");
        var input = Console.ReadLine();
        return string.IsNullOrEmpty(input) ? current : input.Trim();
    }

    private static IReadOnlyList<string> PromptList(string label, IReadOnlyList<string> current)
    {
        var input = Prompt(label, string.Join(", ", current));
        return input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        var sb = new StringBuilder();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0)
                {
                    sb.Length--;
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                sb.Append(key.KeyChar);
                Console.Write('*');
            }
        }
        Console.WriteLine();
        return sb.ToString();
    }

    private static string Display(string value) => string.IsNullOrEmpty(value) ? "(empty)" : value;

    private static string DisplayList(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(empty)" : string.Join(", ", values);

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static long ParseLong(string? value, long fallback) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static bool ParseBool(string? value, bool fallback) =>
        bool.TryParse(value, out var v) ? v : fallback;
}
