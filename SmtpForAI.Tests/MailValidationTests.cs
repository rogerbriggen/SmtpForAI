using SmtpForAI.Configuration;
using SmtpForAI.Security;

namespace SmtpForAI.Tests;

[TestClass]
public sealed class MailValidationTests
{
    private static SmtpSettings Settings(
        IReadOnlyList<string>? recipients = null,
        IReadOnlyList<string>? domains = null,
        int maxRecipients = 10,
        long maxAttachment = 1000) => new()
    {
        AllowedRecipients = recipients ?? Array.Empty<string>(),
        AllowedDomains = domains ?? Array.Empty<string>(),
        MaxRecipients = maxRecipients,
        MaxAttachmentBytes = maxAttachment,
    };

    [TestMethod]
    public void Exact_recipient_match_is_allowed_case_insensitively()
    {
        var s = Settings(recipients: new[] { "Alice@Example.com" });
        Assert.IsTrue(MailValidation.IsRecipientAllowed("alice@example.com", s));
    }

    [TestMethod]
    public void Domain_match_is_allowed()
    {
        var s = Settings(domains: new[] { "example.com" });
        Assert.IsTrue(MailValidation.IsRecipientAllowed("anyone@example.com", s));
        Assert.IsFalse(MailValidation.IsRecipientAllowed("anyone@other.com", s));
    }

    [TestMethod]
    public void Domain_entry_with_leading_at_is_normalized()
    {
        var s = Settings(domains: new[] { "@example.com" });
        Assert.IsTrue(MailValidation.IsRecipientAllowed("bob@example.com", s));
    }

    [TestMethod]
    public void Empty_allowlist_blocks_everyone()
    {
        var s = Settings();
        Assert.IsTrue(s.HasEmptyAllowlist);
        Assert.IsFalse(MailValidation.IsRecipientAllowed("anyone@example.com", s));
    }

    [TestMethod]
    public void Validate_rejects_no_recipients()
    {
        var s = Settings(domains: new[] { "example.com" });
        Assert.IsNotNull(MailValidation.Validate(Array.Empty<string>(), Array.Empty<long>(), s));
    }

    [TestMethod]
    public void Validate_rejects_too_many_recipients()
    {
        var s = Settings(domains: new[] { "example.com" }, maxRecipients: 1);
        var error = MailValidation.Validate(new[] { "a@example.com", "b@example.com" }, Array.Empty<long>(), s);
        StringAssert.Contains(error, "Too many recipients");
    }

    [TestMethod]
    public void Validate_rejects_oversized_attachment()
    {
        var s = Settings(domains: new[] { "example.com" }, maxAttachment: 100);
        var error = MailValidation.Validate(new[] { "a@example.com" }, new long[] { 101 }, s);
        StringAssert.Contains(error, "exceeds");
    }

    [TestMethod]
    public void Validate_passes_for_allowed_recipient_within_limits()
    {
        var s = Settings(domains: new[] { "example.com" });
        Assert.IsNull(MailValidation.Validate(new[] { "a@example.com" }, new long[] { 50 }, s));
    }

    [TestMethod]
    public void Validate_blocks_recipient_not_on_allowlist()
    {
        var s = Settings(domains: new[] { "example.com" });
        var error = MailValidation.Validate(new[] { "evil@evil.com" }, Array.Empty<long>(), s);
        StringAssert.Contains(error, "allowlist");
    }

    [TestMethod]
    [DataRow("bad@@example.com")]
    [DataRow("no-at-sign")]
    [DataRow("trailing@")]
    [DataRow("two@ats@example.com")]
    [DataRow("with space@example.com")]
    public void Malformed_address_is_not_allowed(string address)
    {
        var s = Settings(domains: new[] { "example.com" });
        Assert.IsFalse(MailValidation.IsRecipientAllowed(address, s));
    }

    [TestMethod]
    public void Validate_reports_invalid_address_before_allowlist()
    {
        // Domain is allowed, but the double-@ makes the address malformed.
        var s = Settings(domains: new[] { "example.com" });
        var error = MailValidation.Validate(new[] { "bad@@example.com" }, Array.Empty<long>(), s);
        StringAssert.Contains(error, "Invalid email address");
    }

    [TestMethod]
    public void TryNormalizeAddress_extracts_canonical_address_and_domain()
    {
        Assert.IsTrue(MailValidation.TryNormalizeAddress("Alice@Example.com", out var normalized, out var domain));
        Assert.AreEqual("Alice@Example.com", normalized);
        Assert.AreEqual("Example.com", domain);
    }
}
