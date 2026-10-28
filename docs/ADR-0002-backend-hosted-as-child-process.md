# ADR-0002: Backend Process Lifecycle Management

## Context
The app must provide a seamless one-click user experience without requiring users to manually run terminal commands or manage external services.

## Decision
The WPF application starts and manages the backend executable as a child process on local loopback (127.0.0.1) with a dynamically generated per-launch access token.

## Consequences
- Zero-configuration startup for end-users.
- Full process lifetime management (health polling on startup, automatic termination on exit).
- Secure loopback authorization with Bearer token authentication.
