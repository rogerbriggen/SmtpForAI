using Microsoft.Extensions.Configuration;
using SmtpForAI.Configuration;
using SmtpForAI.Services;

namespace SmtpForAI.Tests;

[TestClass]
public sealed class MailSenderTests
{
    private static SmtpSettings Configured(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Port"] = "587",
            ["Smtp:UseSsl"] = "true",
            ["Smtp:Username"] = "bob@example.com",
            ["Smtp:FromAddress"] = "bob@example.com",
            ["Smtp:Password"] = "pw",
            ["Security:AllowedDomains:0"] = "example.com",
        };
        if (overrides is not null)
            foreach (var (k, v) in overrides)
                values[k] = v;
        return SmtpSettings.Load(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }

    [TestMethod]
    public void DryRun_succeeds_for_allowed_recipient()
    {
        var result = new MailSender().DryRun(
            MailRequest.Create(to: new[] { "alice@example.com" }, subject: "hi", body: "yo"),
            Configured());
        Assert.IsTrue(result.Ok);
        Assert.IsTrue(result.DryRun);
        Assert.AreEqual(SendErrorKind.None, result.ErrorKind);
        Assert.AreEqual(1, result.Recipients);
    }

    [TestMethod]
    public void DryRun_blocks_recipient_outside_allowlist()
    {
        var result = new MailSender().DryRun(
            MailRequest.Create(to: new[] { "evil@evil.com" }, subject: "hi", body: "yo"),
            Configured());
        Assert.IsFalse(result.Ok);
        Assert.AreEqual(SendErrorKind.Config, result.ErrorKind);
        StringAssert.Contains(result.Error, "allowlist");
    }

    [TestMethod]
    public void Send_against_unconfigured_settings_fails_with_Config_without_contacting_smtp()
    {
        var settings = SmtpSettings.Load(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Security:AllowedDomains:0"] = "example.com" }).Build());

        var result = new MailSender().Send(
            MailRequest.Create(to: new[] { "alice@example.com" }, subject: "hi", body: "yo"),
            settings);

        Assert.IsFalse(result.Ok);
        Assert.AreEqual(SendErrorKind.Config, result.ErrorKind);
        StringAssert.Contains(result.Error, "Not configured");
    }

    [TestMethod]
    public void Send_rejects_invalid_from_address()
    {
        var result = new MailSender().Send(
            MailRequest.Create(
                to: new[] { "alice@example.com" },
                subject: "hi",
                body: "yo",
                from: "bad@@example.com"),
            Configured());

        Assert.IsFalse(result.Ok);
        Assert.AreEqual(SendErrorKind.Config, result.ErrorKind);
        StringAssert.Contains(result.Error, "Invalid From");
    }

    [TestMethod]
    public void DryRun_rejects_malformed_recipient()
    {
        var result = new MailSender().DryRun(
            MailRequest.Create(to: new[] { "bad@@example.com" }, subject: "hi", body: "yo"),
            Configured());
        Assert.IsFalse(result.Ok);
        Assert.AreEqual(SendErrorKind.Config, result.ErrorKind);
        StringAssert.Contains(result.Error, "Invalid email address");
    }

    [TestMethod]
    public void DryRun_rejects_missing_attachment_file()
    {
        var result = new MailSender().DryRun(
            MailRequest.Create(
                to: new[] { "alice@example.com" },
                subject: "hi",
                body: "yo",
                attachments: new[] { Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.bin") }),
            Configured());
        Assert.IsFalse(result.Ok);
        Assert.AreEqual(SendErrorKind.Config, result.ErrorKind);
        StringAssert.Contains(result.Error, "Attachment not found");
    }
}
