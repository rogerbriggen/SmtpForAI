using System.Text.Json;
using SmtpForAI.Configuration;
using SmtpForAI.Mcp;
using SmtpForAI.Services;

namespace SmtpForAI.Tests;

/// <summary>
/// These tests call the MCP tool methods directly (they are plain instance methods
/// decorated with the MCP attribute). The MCP SDK plumbing is exercised by the
/// stdio smoke test in CI/manual verification; here we cover semantics.
/// </summary>
[TestClass]
public sealed class EmailToolTests
{
    private string _tempDir = "";
    private AppConfiguration _app = null!;

    [TestInitialize]
    public void Setup()
    {
        // EmailTool reads settings through AppConfiguration. We build a real one but
        // point its files into a temp dir so tests don't touch the user profile.
        _tempDir = Path.Combine(Path.GetTempPath(), "smtpforai-email-tool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _app = AppConfiguration.ForTesting(
            appSettingsPath: Path.Combine(_tempDir, "appsettings.json"),
            secretsPath: Path.Combine(_tempDir, "secrets.json"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void WriteAppSettings(string json) =>
        File.WriteAllText(_app.AppSettingsPath, json);

    private EmailTool NewTool() => new(_app, new MailSender());

    private static string MinimalConfig => """
        {
          "Smtp": {
            "Host": "smtp.example.com", "Port": 587, "UseSsl": true,
            "Username": "bob@example.com", "FromAddress": "bob@example.com",
            "FromDisplayName": ""
          },
          "Security": {
            "AllowedRecipients": [],
            "AllowedDomains": ["example.com"],
            "MaxRecipients": 10,
            "MaxAttachmentBytes": 10485760
          }
        }
        """;

    // ---- validate_recipient ----

    [TestMethod]
    public void ValidateRecipient_allowed_address_returns_allowed_true()
    {
        WriteAppSettings(MinimalConfig);
        var r = NewTool().ValidateRecipient("alice@example.com");
        Assert.IsTrue(r.WellFormed);
        Assert.IsTrue(r.Allowed);
        Assert.IsNull(r.Reason);
        Assert.AreEqual("alice@example.com", r.NormalizedAddress);
        Assert.AreEqual("example.com", r.Domain);
    }

    [TestMethod]
    public void ValidateRecipient_blocked_address_explains_why()
    {
        WriteAppSettings(MinimalConfig);
        var r = NewTool().ValidateRecipient("evil@evil.com");
        Assert.IsTrue(r.WellFormed);
        Assert.IsFalse(r.Allowed);
        StringAssert.Contains(r.Reason, "allowlist");
    }

    [TestMethod]
    public void ValidateRecipient_malformed_address_is_rejected()
    {
        WriteAppSettings(MinimalConfig);
        var r = NewTool().ValidateRecipient("bad@@example.com");
        Assert.IsFalse(r.WellFormed);
        Assert.IsFalse(r.Allowed);
        StringAssert.Contains(r.Reason, "Invalid email address");
    }

    // ---- get_config_status ----

    [TestMethod]
    public void GetConfigStatus_reports_configured_when_fully_set_up()
    {
        WriteAppSettings(MinimalConfig);
        // password is normally in user secrets; simulate with an in-process override.
        var status = NewToolWithPassword("pw").GetConfigStatus();
        Assert.IsTrue(status.Configured);
        Assert.IsEmpty(status.Missing);
        Assert.IsTrue(status.HasPassword);
        Assert.AreEqual("smtp.example.com", status.Host);
    }

    [TestMethod]
    public void GetConfigStatus_reports_missing_fields_when_unconfigured()
    {
        // empty app settings + no password
        WriteAppSettings("{}");
        var status = NewTool().GetConfigStatus();
        Assert.IsFalse(status.Configured);
        Assert.IsFalse(status.HasPassword);
        Assert.IsTrue(status.Missing.Length > 0);
    }

    [TestMethod]
    public void GetConfigStatus_response_never_contains_the_password()
    {
        WriteAppSettings(MinimalConfig);
        var status = NewToolWithPassword("super-secret-12345").GetConfigStatus();
        var json = JsonSerializer.Serialize(status);
        Assert.IsFalse(json.Contains("super-secret-12345", StringComparison.Ordinal),
            "Password value must never appear in the MCP response payload.");
    }

    private EmailTool NewToolWithPassword(string password)
    {
        // Write the password into the secrets file at the path AppConfiguration is using,
        // so SmtpSettings.Load picks it up via AddUserSecrets.
        UserSecretsStore.WritePasswordTo(_app.SecretsPath, password);
        return new EmailTool(_app, new MailSender());
    }

    // ---- send_email (dry-run path; real send needs an SMTP server) ----

    [TestMethod]
    public void SendEmail_dry_run_returns_ok_for_allowed_recipient()
    {
        WriteAppSettings(MinimalConfig);
        var response = NewToolWithPassword("pw").SendEmail(
            to: new[] { "alice@example.com" },
            subject: "hi",
            body: "yo",
            dryRun: true);
        Assert.IsTrue(response.Ok);
        Assert.IsTrue(response.DryRun);
        Assert.AreEqual(1, response.Recipients);
        Assert.IsNull(response.Error);
    }

    [TestMethod]
    public void SendEmail_dry_run_blocks_recipient_outside_allowlist()
    {
        WriteAppSettings(MinimalConfig);
        var response = NewToolWithPassword("pw").SendEmail(
            to: new[] { "evil@evil.com" },
            subject: "hi",
            body: "yo",
            dryRun: true);
        Assert.IsFalse(response.Ok);
        StringAssert.Contains(response.Error, "allowlist");
    }
}
