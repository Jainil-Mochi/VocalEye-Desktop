# ADR-0001: Technology Choice - WPF on .NET 10

## Context
VocalEye Desktop requires a mature, robust Windows client stack to serve as the main interface for local AI orchestration, real-time SSE streaming, tool approvals, and local process management.

## Decision
We choose WPF on .NET 10 LTS with MVVM architecture.

## Consequences
- Same runtime and language (.NET C#) as backend.
- High performance for UI virtualization and fast text rendering.
- Modern visual presentation using WPF-UI (Fluent design).
- Windows v1 focus while keeping clean API boundary for multi-platform future.
