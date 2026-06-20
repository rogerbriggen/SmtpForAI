# SmtpForAI

[![CI](https://github.com/rogerbriggen/SmtpForAI/actions/workflows/ci.yml/badge.svg)](https://github.com/rogerbriggen/SmtpForAI/actions/workflows/ci.yml)

A small, secure command-line tool that lets AI assistants (via "skills") send email over SMTP.
Configuration lives in `appsettings.json`; the SMTP password is kept in the .NET secret manager
(out of source control). A recipient **allowlist** and per-message limits prevent abuse.

Built on .NET 10. The only non-Microsoft dependency is [MailKit](https://github.com/jstedfast/MailKit)
(the library Microsoft recommends in place of the legacy `System.Net.Mail.SmtpClient`).

---

## For end users

### 1. Build or publish

```bash
dotnet build -c Release
# or a self-contained executable you can copy anywhere:
dotnet publish SmtpForAI/SmtpForAI.csproj -c Release -r win-x64   # or linux-x64, osx-arm64, ...
```

> The published `SmtpForAI` executable reads `appsettings.json` from **next to the executable**.
> The SMTP password is stored separately in the OS user-secrets store, never in `appsettings.json`.

### 2. Configure

Interactive setup (prompts for host, port, SSL, username, From address, allowlist, and a masked
password):

```bash
SmtpForAI config
```

Or non-interactively (handy for scripts/CI/AI):

```bash
SmtpForAI config set \
  --host smtp.example.com --port 587 --use-ssl true \
  --username you@example.com --from you@example.com --display "Your Name" \
  --allow-domain example.com --allow-recipient vip@partner.com \
  --max-recipients 10 --max-attachment-bytes 10485760 \
  --password "your-app-password"
```

Check status at any time (the password is **never** printed):

```bash
SmtpForAI config show
```

Where things are stored:
- **`appsettings.json`** — next to the executable. Holds the `Smtp` (host/port/auth/from) and
  `Security` (allowlist/limits) sections.
- **`secrets.json`** — in your user profile (e.g. on Windows
  `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`), holding only `Smtp:Password`.
  This is plaintext but kept out of the repository — treat your profile accordingly.

### 3. Send

```bash
# Validate against the allowlist/limits without sending:
SmtpForAI send --to alice@example.com --subject "Hello" --body "Hi there" --dry-run --json

# Actually send:
SmtpForAI send --to alice@example.com --subject "Hello" --body "Hi there" --json
```

**`send` options**

| Option | Description |
| --- | --- |
| `--to` / `--cc` / `--bcc` | Recipients. Repeatable, or comma-separated in one value. |
| `--subject` | Subject line. |
| `--body` | Body text. |
| `--body-file <path>` | Read the body from a file (instead of `--body`). |
| *(stdin)* | If neither `--body` nor `--body-file` is given, the body is read from stdin. |
| `--html` | Treat the body as HTML. |
| `--from <addr>` | Override the configured From address. |
| `--attach <path>` | Attach a file. Repeatable. |
| `--json` | Emit a machine-readable result: `{"ok":true,...}` / `{"ok":false,"error":"..."}`. |
| `--dry-run` | Validate (allowlist + limits) without sending. |

### Security model

Every recipient (To/Cc/Bcc) must either match an entry in `Security:AllowedRecipients`
(exact, case-insensitive) **or** have its domain listed in `Security:AllowedDomains`. The total
recipient count must be within `MaxRecipients`, and each attachment within `MaxAttachmentBytes`.

> **Fail-closed:** if both allowlists are empty, **every** send is blocked. Add at least one
> allowed domain or recipient via `config`.

### Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success |
| `1` | Usage error (unknown/incomplete command line) |
| `2` | Config or policy error (not configured, recipient not on allowlist, attachment too large…) |
| `3` | SMTP send failure |

### Troubleshooting

- **`2` "not configured"** — run `SmtpForAI config show` to see which fields are missing.
- **`2` "not on the allowlist"** — add the recipient/domain via `config set --allow-domain …`.
- **`3` authentication failed** — many providers require an *app password*, not your login
  password. Check the username and that the account allows SMTP.
- **`3` TLS/connection errors** — verify host/port and `--use-ssl`. Port `465` uses implicit
  TLS; port `587` uses STARTTLS; `--use-ssl false` disables both.

### Use from an AI assistant via MCP

`SmtpForAI mcp` starts a [Model Context Protocol](https://modelcontextprotocol.io) server over
stdio so MCP-aware clients (Claude Desktop, Cursor, …) can call the tool directly. Configure
SmtpForAI normally first (`SmtpForAI config`), then point the client at the published exe.

**Claude Desktop** — add to `claude_desktop_config.json` (Settings → Developer → Edit Config):

```json
{
  "mcpServers": {
    "smtpforai": {
      "command": "C:\\path\\to\\SmtpForAI.exe",
      "args": ["mcp"]
    }
  }
}
```

Restart Claude Desktop. Three tools become available:

| Tool | What it does |
| --- | --- |
| `send_email` | Sends an email. Supports `to`/`cc`/`bcc`, `subject`, `body`, `isHtml`, `from`, `attachments` (absolute paths), and `dryRun` (validate without sending). |
| `validate_recipient` | Pure check: is an address well-formed *and* on the allowlist? No SMTP traffic. |
| `get_config_status` | Read-only status: `configured`, `missing[]`, `hasPassword` (the password value is never returned), plus host/port and allowlist counts. |

The MCP path uses the **same** allowlist, per-message limits, and fail-closed behavior as the
CLI — there is no MCP-only "trusted" mode. There is intentionally no `set_config` tool, so an
AI prompt cannot relax the policy or change credentials.

---

## For developers

### Layout

```
SmtpForAI/                 # the CLI application
  Cli/                     # ArgParser, ExitCodes
  Commands/                # ConfigCommand, SendCommand
  Configuration/           # SmtpSettings, AppConfiguration, AppSettingsWriter, UserSecretsStore
  Mcp/                     # McpCommand (stdio server), EmailTool ([McpServerTool] methods)
  Security/                # MailValidation (allowlist + limits)
  Services/                # MailRequest, SendResult, MailSender (shared CLI + MCP core)
  appsettings.json         # committed template (no secrets)
SmtpForAI.Tests/           # MSTest unit tests + MCP stdio smoke tests
skill/                     # SKILL.md (AI skill manifest) + catalog.json
.github/workflows/ci.yml   # CI
```

### Prerequisites

- .NET 10 SDK (pinned via `global.json`).

### Build / run / test

```bash
dotnet build SmtpForAI.slnx -c Release
dotnet test  SmtpForAI.slnx -c Release
dotnet run --project SmtpForAI -- config show
```

### Design notes

- **Microsoft-only, with two exceptions.** The project otherwise uses only Microsoft/BCL
  packages. The two deliberate exceptions are **MailKit/MimeKit** (the library Microsoft
  recommends in place of `System.Net.Mail.SmtpClient`) and **ModelContextProtocol** (the
  Microsoft-+-Anthropic-maintained C# SDK for MCP).
- **AOT/trim.** MailKit/MimeKit and the MCP SDK are both reflection-heavy and not
  trim/AOT safe, so `PublishAot` is **not** enabled. Distribute via a self-contained
  `dotnet publish` instead.
- **CLI + MCP share one core.** Both `send` (CLI) and `send_email` (MCP) build a
  `MailRequest`, hand it to `Services/MailSender`, which runs `MailValidation` (allowlist +
  limits + address syntax) before any SMTP traffic. The security policy cannot diverge between
  the two surfaces.
- **MCP stdio gotcha.** `Mcp/McpCommand` routes all logging to **stderr** because stdout is
  the MCP JSON-RPC channel. Any stray `Console.Write` there would corrupt the protocol.
- **Reflection-free config.** `SmtpSettings.Load` reads values via the `IConfiguration` indexer
  and `GetChildren()` (no binder), keeping the door open to trimming.
- **Secrets.** `UserSecretsStore` reads/writes `secrets.json` with `JsonNode`, keyed
  `"Smtp:Password"` exactly as the .NET config system stores it — so the file stays compatible
  with the `dotnet user-secrets` CLI.

### CI

`.github/workflows/ci.yml` builds and tests the solution:
- **Linux** (`ubuntu-latest`) on every push and pull request — the cheap default.
- **Windows** (`windows-latest`) only on `main`, to exercise Windows without paying the higher
  Windows-minute cost on every PR.

### Skill packaging

The `skill/` folder contains `SKILL.md` (the AI skill manifest describing how to invoke the
`SmtpForAI` executable) and `catalog.json` (a catalog manifest registering the skill for
discovery/install). An assistant configured with this skill calls the CLI with `--json` and
honors the exit codes above.

---

## Roadmap

- HTTP / SSE MCP transport (currently stdio only)
- OAuth2 / XOAUTH2 SMTP authentication
- Optional DPAPI-encrypted secret storage on Windows
