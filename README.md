# VocalEye Desktop — Phase 1

Phase 1 includes a WPF desktop app, dependency injection, backend process launching (optional), health checking, optional Bearer token support, and optional `/api/info` display.

## Requirements
- Visual Studio 2022 with .NET 10 SDK and the .NET desktop development workload.
- Your separate VocalEye ASP.NET Core API project if you want the desktop app to launch the backend.

## Run
1. Extract the ZIP and open `VocalEye.Desktop.sln`.
2. Set `VocalEye.Desktop.App` as Startup Project.
3. Edit `src/VocalEye.Desktop.App/appsettings.json`.
4. Build and press F5.

Set `LaunchBackend` to true and `ProjectPath` to your API `.csproj` to let the app launch it. The backend must expose `/health`. `/api/info` is optional. Set `LaunchBackend` false to connect to a backend started separately.
