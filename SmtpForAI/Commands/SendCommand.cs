using System.Text.Json;
using System.Text.Json.Nodes;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SmtpForAI.Cli;
using SmtpForAI.Configuration;
using SmtpForAI.Security;

namespace SmtpForAI.Commands;

/// <summary>
/// <c>send</c> — builds a message, enforces the security policy, and sends it
/// via MailKit. Supports structured <c>--json</c> output and <c>--dry-run</c>.
/// </summary>
internal static class SendCommand
{
    private static readonly HashSet<string> BooleanFlags =
        new(StringComparer.OrdinalIgnoreCase) { "html", "json", "dry-run" };

    public static int Run(string[] args, AppConfiguration app)
    {
        var parser = ArgParser.Parse(args, BooleanFlags);
        var json = parser.Has("json");
        var dryRun = parser.Has("dry-run");
        var settings = app.LoadSettings();

        var to = SplitAddresses(parser.GetAll("to"));
        var cc = SplitAddresses(parser.GetAll("cc"));
        var bcc = SplitAddresses(parser.GetAll("bcc"));
        var subject = parser.Get("subject") ?? "";
        var from = parser.Get("from") ?? settings.FromAddress;

        // --- gather attachments + sizes ---
        var attachments = parser.GetAll("attach");
        var attachmentSizes = new List<long>();
        foreach (var path in attachments)
        {
            if (!File.Exists(path))
                return Fail(json, ExitCodes.Config, $"Attachment not found: {path}");
            attachmentSizes.Add(new FileInfo(path).Length);
        }

        var allRecipients = to.Concat(cc).Concat(bcc).ToList();

        // --- security policy (always enforced, even on dry-run) ---
        var policyError = MailValidation.Validate(allRecipients, attachmentSizes, settings);
        if (policyError is not null)
            return Fail(json, ExitCodes.Config, policyError);

        // --- resolve body: --body, then --body-file, then stdin ---
        var body = ResolveBody(parser);

        if (dryRun)
        {
            if (json)
                WriteJson(new JsonObject { ["ok"] = true, ["dryRun"] = true, ["recipients"] = allRecipients.Count });
            else
                Console.WriteLine($"Dry run OK: would send '{subject}' to {allRecipients.Count} recipient(s).");
            return ExitCodes.Success;
        }

        if (!settings.IsConfigured)
            return Fail(json, ExitCodes.Config,
                $"Not configured. Missing: {string.Join(", ", settings.MissingRequiredFields())}. Run 'config' first.");

        if (string.IsNullOrEmpty(from))
            return Fail(json, ExitCodes.Config, "No From address (set Smtp:FromAddress or pass --from).");

        if (!MailValidation.TryNormalizeAddress(from, out _, out _))
            return Fail(json, ExitCodes.Config, $"Invalid From address: '{from}'.");

        // --- build the message (any address parse error here is an input/policy error) ---
        MimeMessage message;
        try
        {
            message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromDisplayName ?? string.Empty, from));
            AddAll(message.To, to);
            AddAll(message.Cc, cc);
            AddAll(message.Bcc, bcc);
            message.Subject = subject;

            var builder = new BodyBuilder();
            if (parser.Has("html"))
                builder.HtmlBody = body;
            else
                builder.TextBody = body;
            foreach (var path in attachments)
                builder.Attachments.Add(path);
            message.Body = builder.ToMessageBody();
        }
        catch (ParseException ex)
        {
            return Fail(json, ExitCodes.Config, $"Could not build the message: {ex.Message}");
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
            return Fail(json, ExitCodes.SendFailure, $"SMTP send failed: {ex.Message}");
        }

        if (json)
            WriteJson(new JsonObject { ["ok"] = true, ["messageId"] = message.MessageId });
        else
            Console.WriteLine($"Sent '{subject}' to {allRecipients.Count} recipient(s).");
        return ExitCodes.Success;
    }

    private static SecureSocketOptions ResolveSocketOptions(SmtpSettings s)
    {
        if (!s.UseSsl)
            return SecureSocketOptions.None;
        return s.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
    }

    private static string ResolveBody(ArgParser parser)
    {
        var inline = parser.Get("body");
        if (inline is not null)
            return inline;

        var file = parser.Get("body-file");
        if (file is not null)
            return File.Exists(file) ? File.ReadAllText(file) : "";

        if (Console.IsInputRedirected)
            return Console.In.ReadToEnd();

        return "";
    }

    private static List<string> SplitAddresses(IReadOnlyList<string> raw)
    {
        var result = new List<string>();
        foreach (var entry in raw)
            result.AddRange(entry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return result;
    }

    private static void AddAll(InternetAddressList list, IEnumerable<string> addresses)
    {
        foreach (var address in addresses)
            list.Add(MailboxAddress.Parse(address));
    }

    private static int Fail(bool json, int exitCode, string message)
    {
        if (json)
            WriteJson(new JsonObject { ["ok"] = false, ["error"] = message });
        else
            Console.Error.WriteLine($"Error: {message}");
        return exitCode;
    }

    private static void WriteJson(JsonObject payload) =>
        Console.WriteLine(payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}
