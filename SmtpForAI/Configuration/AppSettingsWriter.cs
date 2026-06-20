using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmtpForAI.Configuration;

/// <summary>
/// Writes the non-secret configuration (the <c>Smtp</c> and <c>Security</c>
/// sections) to <c>appsettings.json</c>. The password is never written here —
/// it lives in user secrets (see <see cref="UserSecretsStore"/>).
/// </summary>
internal static class AppSettingsWriter
{
    public static void Write(string path, SmtpSettings settings)
    {
        var root = new JsonObject
        {
            ["Smtp"] = new JsonObject
            {
                ["Host"] = settings.Host,
                ["Port"] = settings.Port,
                ["UseSsl"] = settings.UseSsl,
                ["Username"] = settings.Username,
                ["FromAddress"] = settings.FromAddress,
                ["FromDisplayName"] = settings.FromDisplayName,
            },
            ["Security"] = new JsonObject
            {
                ["AllowedRecipients"] = ToArray(settings.AllowedRecipients),
                ["AllowedDomains"] = ToArray(settings.AllowedDomains),
                ["MaxRecipients"] = settings.MaxRecipients,
                ["MaxAttachmentBytes"] = settings.MaxAttachmentBytes,
            },
        };

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static JsonArray ToArray(IReadOnlyList<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(value);
        return array;
    }
}
