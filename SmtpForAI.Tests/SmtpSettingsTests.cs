using Microsoft.Extensions.Configuration;
using SmtpForAI.Configuration;

namespace SmtpForAI.Tests;

[TestClass]
public sealed class SmtpSettingsTests
{
    private static SmtpSettings Load(Dictionary<string, string?> values) =>
        SmtpSettings.Load(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [TestMethod]
    public void Loads_scalar_values_and_defaults()
    {
        var s = Load(new()
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Port"] = "465",
            ["Smtp:UseSsl"] = "false",
            ["Smtp:Username"] = "bob@example.com",
            ["Smtp:FromAddress"] = "bob@example.com",
            ["Smtp:Password"] = "pw",
            ["Security:AllowedDomains:0"] = "example.com",
        });

        Assert.AreEqual("smtp.example.com", s.Host);
        Assert.AreEqual(465, s.Port);
        Assert.IsFalse(s.UseSsl);
        Assert.IsTrue(s.HasPassword);
        Assert.IsTrue(s.IsConfigured);
        Assert.IsEmpty(s.MissingRequiredFields());
    }

    [TestMethod]
    public void Empty_allowlist_makes_tool_not_send_ready()
    {
        var s = Load(new()
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Username"] = "bob@example.com",
            ["Smtp:FromAddress"] = "bob@example.com",
            ["Smtp:Password"] = "pw",
            // no allowlist
        });

        Assert.IsTrue(s.HasEmptyAllowlist);
        Assert.IsFalse(s.IsConfigured);
        Assert.IsTrue(s.MissingRequiredFields().Any(m => m.Contains("allowlist", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Reads_array_sections_reflection_free()
    {
        var s = Load(new()
        {
            ["Security:AllowedDomains:0"] = "example.com",
            ["Security:AllowedDomains:1"] = "team.example.com",
            ["Security:AllowedRecipients:0"] = "vip@partner.com",
        });

        CollectionAssert.AreEqual(new[] { "example.com", "team.example.com" }, s.AllowedDomains.ToArray());
        CollectionAssert.AreEqual(new[] { "vip@partner.com" }, s.AllowedRecipients.ToArray());
        Assert.IsFalse(s.HasEmptyAllowlist);
    }

    [TestMethod]
    public void Reports_missing_required_fields_when_empty()
    {
        var s = Load(new());
        Assert.IsFalse(s.IsConfigured);
        CollectionAssert.Contains(s.MissingRequiredFields().ToArray(), "Smtp:Host");
        Assert.IsTrue(s.HasEmptyAllowlist);
    }

    [TestMethod]
    public void Bad_numeric_values_fall_back_to_defaults()
    {
        var s = Load(new() { ["Smtp:Port"] = "not-a-number", ["Security:MaxRecipients"] = "" });
        Assert.AreEqual(587, s.Port);
        Assert.AreEqual(10, s.MaxRecipients);
    }
}
