# VocalEye Desktop — Phase 2 (Chat MVP) patch

This zip contains **new files only**, laid out to match your solution. Copy the `src/` and `tests/`
folders over your repo (nothing of your Phase 1 files is overwritten). It was written without
access to your Phase 1 code and **has not been compiled** — expect small namespace/name tweaks.

## 1. Packages / project references
* `Directory.Packages.props`: add `<PackageVersion Include="Markdig" Version="<current stable>" />`
* `VocalEye.Desktop.App.csproj`: `<PackageReference Include="Markdig" />` (CommunityToolkit.Mvvm already present)
* `VocalEye.Desktop.ApiClient.csproj`: needs `Microsoft.Extensions.Http` (probably there from Phase 1)
  and a reference to Core.
* `VocalEye.Desktop.UnitTests.csproj`: references Core, ApiClient and App (App is WPF, so the test
  project must target `net10.0-windows`); packages xunit, Moq, FluentAssertions.
* `ApiClient` uses your existing `AuthenticationHandler` (namespace `VocalEye.Desktop.ApiClient`).
  Adjust the `using`/namespace in `ChatApiServiceCollectionExtensions.cs` if it lives elsewhere.

## 2. Wire it up
1. Registration (same `IServiceCollection` as Phase 1):
   `services.AddChatFeature(new Uri(configuration["Backend:BaseUrl"]!));`
   (Resolve `ChatViewModel` on the UI thread — it captures the UI SynchronizationContext.)
2. Expose it from your existing `MainViewModel`:
   `public ChatViewModel Chat { get; }` (inject it) and forward your Phase 1 status:
   `Chat.ConnectionStatus = "Connected"; Chat.IsConnected = true;`
   Call `await Chat.InitializeAsync();` once the backend is reported healthy.
3. In `MainWindow.xaml` replace the main content with:
   `<views:ChatView DataContext="{Binding Chat}"/>` (`xmlns:views="clr-namespace:VocalEye.Desktop.App.Views"`).

## 3. Behaviour decisions (documented UX policy)
* Switching chats or pressing New Chat **cancels** an in-progress reply.
* New chats are local drafts (client GUID) until the first send; no undocumented create endpoint is called.
* Rename updates the UI only after the server confirms; failures keep the editor open.
* Titles: uses server `title` when present, else the first user message (local fallback).
* Streaming text is batched every 50 ms; Markdown is re-rendered per batch.
* Link policy: only http/https/mailto, only on explicit click. Images are never fetched.
* Markdown subset: headings, paragraphs, bold/italic/strike, lists, inline + fenced code, quotes, links.
  Tables are not rendered (add Markdig `UsePipeTables` + a table converter later).
* `completed` with no text + pending approvals shows an info message (approvals = Phase 3).

## 4. Verify against the backend OpenAPI before closing the phase
* List/messages responses are accepted as a bare JSON array (or an object wrapping it).
* Rename/delete success codes and the `title` field depend on backend slice **B4**; until it exists the app
  shows a "needs B4" message on 404/405/501. Track B4 as open backend work.
