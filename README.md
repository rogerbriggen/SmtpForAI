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

---

## For developers

### Layout

```
SmtpForAI/                 # the CLI application
  Cli/                     # ArgParser, ExitCodes
  Commands/                # ConfigCommand, SendCommand
  Configuration/           # SmtpSettings, AppConfiguration, AppSettingsWriter, UserSecretsStore
  Security/                # MailValidation (allowlist + limits)
  appsettings.json         # committed template (no secrets)
SmtpForAI.Tests/           # MSTest unit tests
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

- **MailKit vs. Microsoft-only.** The project otherwise uses only Microsoft/BCL packages
  (`Microsoft.Extensions.Configuration.*`, `System.Text.Json`). MailKit/MimeKit is the single
  deliberate exception, chosen for proper STARTTLS, modern SASL auth, and OAuth2 headroom.
- **AOT/trim.** MailKit/MimeKit are not fully trim/AOT safe, so `PublishAot` is **not** enabled.
  Distribute via a (trimmed) self-contained `dotnet publish` instead.
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

- MCP interface
- OAuth2 / XOAUTH2 authentication
- Optional DPAPI-encrypted secret storage on Windows
