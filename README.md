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

### Check the version

```bash
SmtpForAI --version    # or: SmtpForAI -v, SmtpForAI version
```

Prints something like `SmtpForAI 1.0.12-pre+abc1234` (the suffix is the git commit; on a
release build it is omitted, e.g. `SmtpForAI 1.0.12`).

### Troubleshooting

- **`2` "not configured"** — run `SmtpForAI config show` to see which fields are missing.
- **`2` "not on the allowlist"** — add the recipient/domain via `config set --allow-domain …`.
- **`3` authentication failed** — many providers require an *app password*, not your login
  password. Check the username and that the account allows SMTP.
- **`3` TLS/connection errors** — verify host/port and `--use-ssl`. Port `465` uses implicit
  TLS; port `587` uses STARTTLS; `--use-ssl false` disables both.

### Use from an AI assistant via MCP

`SmtpForAI mcp` starts a [Model Context Protocol](https://modelcontextprotocol.io) server over
stdio so MCP-aware clients (Claude Desktop, Cursor, …) can call the tool directly.

#### Configure Claude Desktop

**Prerequisites**

1. Publish a self-contained executable (see [Build or publish](#1-build-or-publish) above) and
   place it somewhere stable, e.g. `C:\Tools\SmtpForAI\SmtpForAI.exe`. Claude Desktop will
   launch this exe on every start, so don't put it in a temp/build directory.
2. Configure SmtpForAI **before** wiring it into Claude Desktop. The MCP server reads the same
   `appsettings.json` + user-secrets as the CLI, and refuses to send if any required field is
   missing:

   ```bash
   SmtpForAI config           # interactive setup
   SmtpForAI config show      # confirm "Configured: True"
   ```

3. Install [Claude Desktop](https://claude.ai/download) (the desktop app — the web version at
   claude.ai does not support local MCP servers).

**Step 1 — locate `claude_desktop_config.json`**

The easiest way is from inside Claude Desktop: open **Settings → Developer → Edit Config**.
That creates the file if it doesn't exist and opens its folder. By default it lives at:

| OS | Path |
| --- | --- |
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |
| macOS | `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Linux | `~/.config/Claude/claude_desktop_config.json` |

**Step 2 — add the `smtpforai` server**

Open the file in any text editor and add an `mcpServers` entry. If the file is empty, paste
this whole snippet; if it already has other servers, just add the `"smtpforai"` block inside
the existing `mcpServers` object.

Windows (note the doubled backslashes — JSON requires `\\`):

```json
{
  "mcpServers": {
    "smtpforai": {
      "command": "C:\\Tools\\SmtpForAI\\SmtpForAI.exe",
      "args": ["mcp"]
    }
  }
}
```

macOS / Linux:

```json
{
  "mcpServers": {
    "smtpforai": {
      "command": "/usr/local/bin/SmtpForAI",
      "args": ["mcp"]
    }
  }
}
```

The `command` must be the **absolute path** to the published executable — Claude Desktop does
not inherit your shell's `PATH`. No `env`, `cwd`, or extra args are needed; SmtpForAI reads
`appsettings.json` from next to the exe and the password from your user profile's secrets
store.

**Step 3 — restart Claude Desktop**

Fully quit and relaunch Claude Desktop (on Windows, also check the system tray; on macOS, use
⌘Q — closing the window only). Connections are established at startup, so a reload isn't
enough.

**Step 4 — verify it's connected**

In a new chat, open **Settings → Developer** and confirm `smtpforai` shows as **running**.
Then ask Claude something like:

> Use the `get_config_status` tool from smtpforai and show me the result.

It should return JSON with `"configured": true` and your configured host/port. If you instead
see "I don't have access to that tool" or the server is marked **failed**, see Troubleshooting
below.

#### Tools exposed over MCP

| Tool | What it does |
| --- | --- |
| `send_email` | Sends an email. Supports `to`/`cc`/`bcc`, `subject`, `body`, `isHtml`, `from`, `attachments` (absolute paths), and `dryRun` (validate without sending). |
| `validate_recipient` | Pure check: is an address well-formed *and* on the allowlist? No SMTP traffic. |
| `get_config_status` | Read-only status: `configured`, `missing[]`, `hasPassword` (the password value is never returned), plus host/port and allowlist counts. |

The MCP path uses the **same** allowlist, per-message limits, and fail-closed behavior as the
CLI — there is no MCP-only "trusted" mode. There is intentionally no `set_config` tool, so an
AI prompt cannot relax the policy or change credentials.

#### Troubleshooting Claude Desktop

- **Server status shows "failed"** — open Claude Desktop's MCP log. On Windows it is
  `%APPDATA%\Claude\logs\mcp-server-smtpforai.log`; on macOS
  `~/Library/Logs/Claude/mcp-server-smtpforai.log`. SmtpForAI routes all its own logging to
  stderr, so any startup error (missing file, bad JSON, .NET runtime missing) appears there.
- **`send_email` returns "not configured"** — Claude Desktop launched the exe, but
  `appsettings.json` / user-secrets are missing values. Run `SmtpForAI config show` from a
  terminal **as the same OS user** that runs Claude Desktop and fix any reported gaps.
- **`send_email` returns "not on the allowlist"** — expected: add the recipient or domain via
  `SmtpForAI config set --allow-domain example.com` (or `--allow-recipient`). The policy is
  intentionally CLI-only.
- **Tool list is empty after restart** — confirm the JSON is valid (a trailing comma or
  single backslash on Windows is the usual cause) and that the `command` path actually exists.
  `claude_desktop_config.json` errors are silent on the UI; they only show up in the log.

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

- .NET 10 SDK (pinned via `global.json`) — **or** use the dev container below, which brings
  its own SDK.

### Dev container

The repo ships a [dev container](https://containers.dev) (`.devcontainer/devcontainer.json`)
so you can develop in an isolated Docker container instead of installing anything on the host.

**Host requirements:** Docker (e.g. Docker Desktop) and VS Code with the
[Dev Containers](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
extension. Open the repo folder and run **“Dev Containers: Reopen in Container”**.

The container is based on the official .NET 10 SDK dev-container image and adds:

- **Tooling:** git, GitHub CLI (`gh`), Node.js LTS, PowerShell (used by the CI release check).
- **AI CLIs:** Claude Code (`claude`), GitHub Copilot CLI (`copilot`), OpenAI Codex (`codex`).
- **VS Code extensions** (installed automatically *inside* the container): C# Dev Kit, C#,
  EditorConfig, Claude Code, Copilot + Copilot Chat, GitHub Pull Requests, GitHub Actions.

On first create it also runs `dotnet restore` so the NuGet cache is warm.

**One-time logins.** CLI credentials are persisted in fixed-name Docker volumes
(`claude-config`, `gh-config`, `copilot-config`, `codex-config`) mounted into the container,
so you authenticate once and the logins survive rebuilds — and are shared with any other dev
container on the machine that mounts the same volumes:

```bash
claude            # Claude Pro/Max OAuth flow
gh auth login     # GitHub CLI
copilot           # prompts for GitHub auth on first run
codex login       # ChatGPT-account OAuth
```

Git credentials need no setup: VS Code forwards the host's git credential helper (and SSH
agent) into the container automatically.

**Notes**

- The volumes only vanish via an explicit `docker volume rm` / `docker volume prune` —
  rebuilding or deleting the container keeps them.
- Because the auth volumes are shared, any code you run in a container that mounts them can
  read those tokens. For untrusted third-party code, use a container without these mounts.
- Nerdbank.GitVersioning needs full git history — don't use a shallow clone.

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
- **`verify-release-version`** runs on `release/*` branches and PRs targeting them, and fails
  the build if `version.json` still has a `-pre` suffix. The check is a `pwsh` step so the
  same script is portable across Windows, macOS, and Linux runners.

### Versioning

The repo uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (NBGV),
driven by `version.json` at the repo root. NBGV stamps `AssemblyVersion`, `FileVersion`, and
`AssemblyInformationalVersion` on every build — there are no version literals in any `.csproj`.

- **Day-to-day** `version.json` reads `"version": "1.0-pre"`, so feature-branch builds produce
  e.g. `1.0.12-pre+abc1234` (height + short commit hash).
- **Releasing** Cut a `release/<x.y>` branch, edit `version.json` to drop the `-pre` suffix
  (e.g. `"version": "1.0"`), and push. The `verify-release-version` CI job will fail the
  branch if you forget. Merge the PR, then tag the merge commit.
- The `publicReleaseRefSpec` in `version.json` lists `main` and `release/*`, so builds on those
  refs omit the `+gitHash` build-metadata suffix.

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
