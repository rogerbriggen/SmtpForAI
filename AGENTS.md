# AGENTS.md

This file provides instructions for Claude Code, Github Copilot, Codex (and other AI assistants) working with this repository.

The goal is to create a simple, secure SMTP server that can be used by AI assistants to send emails. The server will be designed to be easy to set up and use, while also providing basic security features to prevent abuse.

The server will use dotnet 10.

There will be a version which can be used with skills, meaning it will run on the command line and accept commands to send emails. This version will be designed to be easy to integrate with AI assistants, allowing them to send emails without needing to interact with the server directly.
The configuration is stored in appsettings.json, the password will be stored securely in the dotnet secret manager. You can configure the needed infos with /config command or if it is not setup, it will show also without the command.

The server might later also provide a MCP server interface so you can integrate it with our AI tools.

We want this tool easy and secure, so try to not use any third party libraries besides the Microsoft ones except MailKit.