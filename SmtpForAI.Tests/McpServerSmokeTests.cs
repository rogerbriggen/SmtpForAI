using ModelContextProtocol.Client;

namespace SmtpForAI.Tests;

/// <summary>
/// Drives the real <c>SmtpForAI mcp</c> exe over stdio using the official MCP client
/// SDK. This is the contract test for the MCP surface: if these pass, the same exe
/// will work in Claude Desktop / Cursor / any other stdio MCP client.
/// </summary>
[TestClass]
public sealed class McpServerSmokeTests
{
    private static string FindExe()
    {
        // SMTPFORAI_TEST_EXE lets a developer point the smoke tests at a
        // published (e.g. trimmed self-contained) exe for verification.
        var envOverride = Environment.GetEnvironmentVariable("SMTPFORAI_TEST_EXE");
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            Assert.IsTrue(File.Exists(envOverride), $"SMTPFORAI_TEST_EXE points at non-existent path: {envOverride}");
            return envOverride;
        }
        // Tests run from .../SmtpForAI.Tests/bin/<config>/net10.0/. The main exe is
        // built into the sibling project's bin tree under the same config.
        var assemblyDir = Path.GetDirectoryName(typeof(McpServerSmokeTests).Assembly.Location)!;
        var config = new DirectoryInfo(assemblyDir).Parent!.Name;       // Debug or Release
        var repoRoot = new DirectoryInfo(assemblyDir).Parent!.Parent!.Parent!.Parent!.FullName;
        var exeName = OperatingSystem.IsWindows() ? "SmtpForAI.exe" : "SmtpForAI";
        var path = Path.Combine(repoRoot, "SmtpForAI", "bin", config, "net10.0", exeName);
        Assert.IsTrue(File.Exists(path), $"SmtpForAI exe not found at {path} — build the main project first.");
        return path;
    }

    private static async Task<McpClient> ConnectAsync()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "SmtpForAI",
            Command = FindExe(),
            Arguments = ["mcp"],
        });
        return await McpClient.CreateAsync(transport);
    }

    [TestMethod]
    public async Task tools_list_exposes_the_three_email_tools()
    {
        await using var client = await ConnectAsync();

        var tools = await client.ListToolsAsync();
        var names = tools.Select(t => t.Name).ToHashSet();

        CollectionAssert.Contains(names.ToArray(), "send_email");
        CollectionAssert.Contains(names.ToArray(), "validate_recipient");
        CollectionAssert.Contains(names.ToArray(), "get_config_status");

        // Every tool should have a description so MCP clients can surface it.
        foreach (var tool in tools)
            Assert.IsFalse(string.IsNullOrWhiteSpace(tool.Description), $"Tool '{tool.Name}' is missing a description.");
    }

    [TestMethod]
    public async Task get_config_status_returns_a_response_without_leaking_secrets()
    {
        // If a real secrets.json exists for this assembly's UserSecretsId, read the actual
        // password value and confirm it does not appear in the MCP response.
        var settings = SmtpForAI.Configuration.AppConfiguration.Create().LoadSettings();
        var liveSecret = settings.Password;

        await using var client = await ConnectAsync();
        var result = await client.CallToolAsync("get_config_status");

        Assert.IsFalse(result.IsError ?? false);
        var text = string.Join("\n", result.Content
            .OfType<ModelContextProtocol.Protocol.TextContentBlock>()
            .Select(c => c.Text));

        StringAssert.Contains(text, "configured", StringComparison.OrdinalIgnoreCase);
        // No JSON property *named* "password" (we expose only the boolean "hasPassword").
        Assert.IsFalse(text.Contains("\"password\":", StringComparison.OrdinalIgnoreCase),
            "MCP get_config_status response must not include a 'password' field.");
        // The actual secret value, if one is configured, must never appear in the response.
        if (!string.IsNullOrEmpty(liveSecret))
        {
            Assert.IsFalse(text.Contains(liveSecret, StringComparison.Ordinal),
                "MCP get_config_status response leaked the live SMTP password value.");
        }
    }

    [TestMethod]
    public async Task validate_recipient_rejects_malformed_address()
    {
        await using var client = await ConnectAsync();
        var result = await client.CallToolAsync(
            "validate_recipient",
            new Dictionary<string, object?> { ["address"] = "bad@@example.com" });

        Assert.IsFalse(result.IsError ?? false);
        var text = string.Join("\n", result.Content
            .OfType<ModelContextProtocol.Protocol.TextContentBlock>()
            .Select(c => c.Text));
        StringAssert.Contains(text, "Invalid email address");
    }
}
