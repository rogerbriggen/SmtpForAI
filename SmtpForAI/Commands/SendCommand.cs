using System.Text.Json;
using System.Text.Json.Nodes;
using SmtpForAI.Cli;
using SmtpForAI.Configuration;
using SmtpForAI.Services;

namespace SmtpForAI.Commands;

/// <summary>
/// CLI <c>send</c>: parse args → build a <see cref="MailRequest"/> → call
/// <see cref="MailSender"/> → format the result as text or <c>--json</c>
/// and map to an exit code. All policy/validation/SMTP work lives in
/// <see cref="MailSender"/> so the CLI and the MCP server share the same path.
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

        var request = MailRequest.Create(
            to: SplitAddresses(parser.GetAll("to")),
            cc: SplitAddresses(parser.GetAll("cc")),
            bcc: SplitAddresses(parser.GetAll("bcc")),
            subject: parser.Get("subject") ?? "",
            body: ResolveBody(parser),
            isHtml: parser.Has("html"),
            from: parser.Get("from"),
            attachments: parser.GetAll("attach"));

        var sender = new MailSender();
        var result = dryRun ? sender.DryRun(request, settings) : sender.Send(request, settings);
        return Report(result, json, parser.Get("subject") ?? "");
    }

    private static int Report(SendResult result, bool json, string subject)
    {
        if (!result.Ok)
        {
            var exit = result.ErrorKind == SendErrorKind.SendFailure ? ExitCodes.SendFailure : ExitCodes.Config;
            if (json)
                WriteJson(new JsonObject { ["ok"] = false, ["error"] = result.Error });
            else
                Console.Error.WriteLine($"Error: {result.Error}");
            return exit;
        }

        if (result.DryRun)
        {
            if (json)
                WriteJson(new JsonObject { ["ok"] = true, ["dryRun"] = true, ["recipients"] = result.Recipients });
            else
                Console.WriteLine($"Dry run OK: would send '{subject}' to {result.Recipients} recipient(s).");
        }
        else
        {
            if (json)
                WriteJson(new JsonObject { ["ok"] = true, ["messageId"] = result.MessageId });
            else
                Console.WriteLine($"Sent '{subject}' to {result.Recipients} recipient(s).");
        }
        return ExitCodes.Success;
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

    private static void WriteJson(JsonObject payload) =>
        Console.WriteLine(payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}
