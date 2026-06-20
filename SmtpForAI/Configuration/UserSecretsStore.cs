using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace SmtpForAI.Configuration;

/// <summary>
/// Reads and writes the SMTP password in the .NET secret manager
/// (<c>secrets.json</c> under the user profile), keyed exactly as the .NET
/// configuration system stores it (<c>"Smtp:Password"</c>), so the file stays
/// compatible with the <c>dotnet user-secrets</c> CLI.
/// </summary>
internal static class UserSecretsStore
{
    public const string PasswordKey = "Smtp:Password";

    /// <summary>The <c>UserSecretsId</c> baked into the assembly by the SDK.</summary>
    public static string GetUserSecretsId(Assembly assembly) =>
        assembly.GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId
        ?? throw new InvalidOperationException(
            "No UserSecretsId found on the assembly. Ensure <UserSecretsId> is set in the .csproj.");

    /// <summary>Absolute path to this app's <c>secrets.json</c>.</summary>
    public static string ResolvePath(string userSecretsId) =>
        PathHelper.GetSecretsPathFromSecretsId(userSecretsId);

    // ---- id-based convenience overloads ----

    public static string? ReadPassword(string userSecretsId) =>
        ReadPasswordFrom(ResolvePath(userSecretsId));

    public static void WritePassword(string userSecretsId, string password) =>
        WritePasswordTo(ResolvePath(userSecretsId), password);

    // ---- path-based core (unit-testable against a temp file) ----

    public static string? ReadPasswordFrom(string secretsFilePath)
    {
        if (!File.Exists(secretsFilePath))
            return null;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(secretsFilePath)) is JsonObject obj &&
                obj.TryGetPropertyValue(PasswordKey, out var value) &&
                value is not null)
            {
                return value.GetValue<string>();
            }
        }
        catch (JsonException)
        {
            // Corrupt secrets file — treat as "no password".
        }

        return null;
    }

    public static void WritePasswordTo(string secretsFilePath, string password)
    {
        var directory = Path.GetDirectoryName(secretsFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        JsonObject root;
        if (File.Exists(secretsFilePath) &&
            JsonNode.Parse(File.ReadAllText(secretsFilePath)) is JsonObject existing)
        {
            root = existing;
        }
        else
        {
            root = new JsonObject();
        }

        root[PasswordKey] = password;
        File.WriteAllText(
            secretsFilePath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
