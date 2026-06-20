using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace SmtpForAI.Configuration;

/// <summary>
/// Strongly typed view over <c>appsettings.json</c> (the <c>Smtp</c> and
/// <c>Security</c> sections) plus the password loaded from user secrets.
/// Loaded reflection-free to stay trim/AOT friendly.
/// </summary>
internal sealed class SmtpSettings
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string Username { get; init; } = "";
    public string FromAddress { get; init; } = "";
    public string FromDisplayName { get; init; } = "";

    public IReadOnlyList<string> AllowedRecipients { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedDomains { get; init; } = Array.Empty<string>();
    public int MaxRecipients { get; init; } = 10;
    public long MaxAttachmentBytes { get; init; } = 10L * 1024 * 1024;

    /// <summary>SMTP password — sourced from user secrets, never from appsettings.json.</summary>
    public string? Password { get; init; }

    public bool HasPassword => !string.IsNullOrEmpty(Password);

    public static SmtpSettings Load(IConfiguration config) => new()
    {
        Host = config["Smtp:Host"] ?? "",
        Port = ParseInt(config["Smtp:Port"], 587),
        UseSsl = ParseBool(config["Smtp:UseSsl"], true),
        Username = config["Smtp:Username"] ?? "",
        FromAddress = config["Smtp:FromAddress"] ?? "",
        FromDisplayName = config["Smtp:FromDisplayName"] ?? "",
        AllowedRecipients = ReadArray(config, "Security:AllowedRecipients"),
        AllowedDomains = ReadArray(config, "Security:AllowedDomains"),
        MaxRecipients = ParseInt(config["Security:MaxRecipients"], 10),
        MaxAttachmentBytes = ParseLong(config["Security:MaxAttachmentBytes"], 10L * 1024 * 1024),
        Password = config["Smtp:Password"],
    };

    /// <summary>Required fields that are still empty; empty result means "ready to send".</summary>
    public IReadOnlyList<string> MissingRequiredFields()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(Host)) missing.Add("Smtp:Host");
        if (string.IsNullOrWhiteSpace(Username)) missing.Add("Smtp:Username");
        if (string.IsNullOrWhiteSpace(FromAddress)) missing.Add("Smtp:FromAddress");
        if (!HasPassword) missing.Add("Smtp:Password (user secrets)");
        // Fail-closed: with an empty allowlist every send is blocked, so the tool is not send-ready.
        if (HasEmptyAllowlist) missing.Add("Security:AllowedRecipients/AllowedDomains (allowlist is empty)");
        return missing;
    }

    public bool IsConfigured => MissingRequiredFields().Count == 0;

    /// <summary>True when no recipient could ever pass the allowlist (fail-closed).</summary>
    public bool HasEmptyAllowlist => AllowedRecipients.Count == 0 && AllowedDomains.Count == 0;

    private static IReadOnlyList<string> ReadArray(IConfiguration config, string key)
    {
        var list = new List<string>();
        foreach (var child in config.GetSection(key).GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
                list.Add(child.Value!.Trim());
        }
        return list;
    }

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static long ParseLong(string? value, long fallback) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static bool ParseBool(string? value, bool fallback) =>
        bool.TryParse(value, out var v) ? v : fallback;
}
