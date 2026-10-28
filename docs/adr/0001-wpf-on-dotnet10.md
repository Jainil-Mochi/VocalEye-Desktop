# ADR-0001: WPF on .NET 10 with MVVM

Status: Accepted (confirm after the Phase 0 spike)

## Context
VocalEye Desktop v1 is a Windows-only chat app that talks to the backend over HTTP.
The team already uses C#, Visual Studio and .NET for the backend.

## Decision
Use WPF on .NET 10 (LTS) with MVVM (CommunityToolkit.Mvvm) and Microsoft.Extensions.Hosting for DI.

## Consequences
- Same language, tooling and runtime as the backend; low learning cost.
- Windows only. If macOS or Linux matters, the UI layer can move to Avalonia
  because the app only talks to the backend over HTTP.
- Default WPF look is dated; a modern theme is chosen in the spike (ADR-0003).

## Alternatives considered
WinUI 3, Avalonia, .NET MAUI, Electron, Tauri (see project plan, technology section).
