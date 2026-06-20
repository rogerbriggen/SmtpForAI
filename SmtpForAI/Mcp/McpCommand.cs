using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmtpForAI.Cli;
using SmtpForAI.Configuration;
using SmtpForAI.Services;

namespace SmtpForAI.Mcp;

/// <summary>
/// <c>mcp</c> subcommand. Starts an MCP (Model Context Protocol) server over
/// stdio so a desktop client (Claude Desktop, Cursor, …) can call the
/// <see cref="EmailTool"/> tools as a child process. Blocks until the parent
/// closes the pipe.
/// </summary>
internal static class McpCommand
{
    public static int Run(string[] args, AppConfiguration app)
    {
        // IMPORTANT: stdout is the MCP JSON-RPC channel. Any stray Console.Write
        // (including console logging) would corrupt the protocol stream — route
        // all logging to stderr.
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.Services.AddSingleton(app);
        builder.Services.AddSingleton<MailSender>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        builder.Build().Run();
        return ExitCodes.Success;
    }
}
