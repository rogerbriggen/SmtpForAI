using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace SmtpForAI.Configuration;

/// <summary>
/// Locates the config files and loads merged settings:
/// <c>appsettings.json</c> (next to the executable) overlaid with the
/// password from user secrets.
/// </summary>
internal sealed class AppConfiguration
{
    public string AppSettingsPath { get; }
    public string UserSecretsId { get; }
    public string SecretsPath { get; }

    private AppConfiguration(string appSettingsPath, string userSecretsId, string secretsPath)
    {
        AppSettingsPath = appSettingsPath;
        UserSecretsId = userSecretsId;
        SecretsPath = secretsPath;
    }

    public static AppConfiguration Create()
    {
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var id = UserSecretsStore.GetUserSecretsId(Assembly.GetExecutingAssembly());
        return new AppConfiguration(appSettingsPath, id, UserSecretsStore.ResolvePath(id));
    }

    public SmtpSettings LoadSettings()
    {
        // SecretsPath is the same file AddUserSecrets(UserSecretsId) would resolve to,
        // but loading it directly as a JSON source keeps the lookup explicit and
        // makes the class testable against a temp-dir secrets.json.
        var config = new ConfigurationBuilder()
            .AddJsonFile(AppSettingsPath, optional: true, reloadOnChange: false)
            .AddJsonFile(SecretsPath, optional: true, reloadOnChange: false)
            .Build();
        return SmtpSettings.Load(config);
    }

    /// <summary>Test seam: build an instance with explicit file paths.</summary>
    internal static AppConfiguration ForTesting(string appSettingsPath, string secretsPath, string userSecretsId = "test") =>
        new(appSettingsPath, userSecretsId, secretsPath);
}
