# VocalEye Desktop — Phase 0 starter

This ZIP contains the initial Phase 0 WPF/.NET solution scaffold discussed in chat. It is a starter foundation, not the complete finished VocalEye application.

## Requirements
- Windows
- .NET 10 SDK
- Visual Studio 2026 (or another editor supporting .NET 10 and WPF)

## Open and run
1. Extract the ZIP.
2. Open `VocalEye.Desktop.sln` in Visual Studio.
3. Allow NuGet restore to finish.
4. Build the solution.
5. Run `src/VocalEye.Desktop.App`.

Or from PowerShell in this folder:

```powershell
dotnet restore
dotnet build VocalEye.Desktop.sln
dotnet test tests/VocalEye.Desktop.UnitTests/VocalEye.Desktop.UnitTests.csproj
dotnet run --project src/VocalEye.Desktop.App/VocalEye.Desktop.App.csproj
```

## Projects
- `VocalEye.Desktop.App`: WPF application and initial MVVM shell
- `VocalEye.Desktop.Core`: domain/core project
- `VocalEye.Desktop.ApiClient`: backend HTTP integration placeholder
- `VocalEye.Desktop.Hosting`: hosting/process integration placeholder
- `VocalEye.Desktop.Infrastructure`: infrastructure placeholder
- `VocalEye.Desktop.UnitTests`: starter unit test project

## Notes
- The backend projects intentionally contain placeholders; backend API routes/contracts still need to be confirmed against your existing backend.
- No secrets or backend credentials are included.
- Future increments can be delivered as ZIP files containing only changed/new files. Extract those ZIPs into this solution folder and choose **Replace files** if prompted.
