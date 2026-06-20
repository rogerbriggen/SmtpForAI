---
name: send-email
description: Send an email over SMTP via the SmtpForAI command-line tool. Use when the user asks to send an email, mail a report, or notify someone by email. Requires the tool to be configured first (host, credentials, and a recipient allowlist).
---

# Send email (SmtpForAI)

This skill sends email by invoking the `SmtpForAI` command-line executable. It does **not**
talk SMTP directly — it shells out to the tool, which holds the SMTP host, credentials, and a
security allowlist.

## Prerequisites

- The `SmtpForAI` executable is built/published and on `PATH` (or you know its full path).
- It has been configured once: `SmtpForAI config` (interactive) or
  `SmtpForAI config set --host ... --port ... --username ... --from ... --password ... --allow-domain ...`.
- Check readiness anytime with `SmtpForAI config show` (the password is never printed).

## How to send

Always pass `--json` so you can parse the result, and prefer `--dry-run` first to confirm the
recipients pass the allowlist before actually sending.

1. **Validate (no send):**
   ```
   SmtpForAI send --to alice@example.com --subject "Status" --body "All green." --dry-run --json
   ```
   Expect `{"ok":true,"dryRun":true,...}`. If `ok` is false, report the `error` to the user and stop.

2. **Send:**
   ```
   SmtpForAI send --to alice@example.com --subject "Status" --body "All green." --json
   ```
   Expect `{"ok":true,"messageId":"..."}`.

### Options

- `--to` / `--cc` / `--bcc` — recipients; repeatable, or comma-separated in one value.
- `--subject` — subject line.
- `--body` — body text. Alternatives: `--body-file <path>`, or pipe the body via stdin.
- `--html` — treat the body as HTML.
- `--attach <path>` — attach a file; repeatable.
- `--from <addr>` — override the configured From address.
- `--json` — machine-readable result `{"ok":bool,...}`.
- `--dry-run` — validate (allowlist + limits) without sending.

### Result contract & exit codes

- stdout JSON: `{"ok":true,...}` on success, `{"ok":false,"error":"..."}` on failure.
- Exit codes: `0` success · `1` usage error · `2` config/policy error (e.g. recipient not on
  allowlist, not configured) · `3` SMTP send failure.

## Safety notes

- The tool enforces a recipient **allowlist** and per-message limits. A `2` exit with an
  "allowlist" error means the recipient is not permitted — do **not** retry blindly; tell the
  user the recipient must be added via `config`.
- Never put the SMTP password on the command line in shared/logged contexts beyond initial
  `config set`; it is stored in the OS user-secrets store thereafter.
