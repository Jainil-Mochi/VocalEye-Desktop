# ADR-0002: Backend hosted as a child process

Status: Accepted

## Context
The user should double-click one app: no Swagger, Postgres or terminal.

## Decision
The desktop app starts the backend (self-contained publish, bundled in the installer) as a hidden
child process on 127.0.0.1 and a free port, passes a random per-launch bearer token (slice B2),
waits for GET /health, and stops it on exit. An advanced setting can point to a remote backend.
Secrets are held by the app (DPAPI / Credential Manager) and passed as environment variables.

## Consequences
- One install, one icon; nothing else on the PC can call the backend without the token.
- The app must handle start-up failures (port, antivirus, missing file) with a clear error and log viewer.

## Alternatives considered
A separate always-on Windows service: harder to install and update.
