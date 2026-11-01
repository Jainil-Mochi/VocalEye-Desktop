# VocalEye Desktop — Phase 3 (Tools and approvals) patch

Copy `src/` and `tests/` over your repo. **Not compiled** (no .NET SDK where this was written) —
expect small fixes on first build. Phase 2 must already be in place.

## Files that REPLACE Phase 2 files (overwrite them)
* Core/DTOs: `ChatCompletedDto.cs` (ModelUsedDto moved to its own file), `ChatRequestDto.cs`,
  `ConversationMessageDto.cs`, `ConversationSummaryDto.cs` (keeps the flexible `model` converter)
* ApiClient: `ChatService.cs` (adds GET /api/tools)
* App: `ViewModels/ChatViewModel.cs`, `ViewModels/ChatMessageViewModel.cs`, `Views/ChatView.xaml`,
  `ChatFeatureRegistration.cs`

Everything else in the zip is new. `ChatView.xaml.cs`, `ChatApiServiceCollectionExtensions.cs`,
`MainViewModel` and `MainWindow.xaml` need no change. Existing Phase 2 tests still compile.

## What it does
* A reply with `pendingApprovals` shows one card per tool call, inside that turn's message:
  risk badge (from GET /api/tools), plain-language headline, key fields, collapsible previews
  (file content, email body, each project file with size) and a "Show raw arguments" toggle.
  Cards are built from the real `argumentsJson`, never from the model's description.
* Approve / Deny per card. Decisions are sent **together**, only once the last card is decided, as
  `{conversationId, approvals:[{approvalId, approved}]}` (no message, no edited arguments).
  "Deny all" is the only bulk action; there is deliberately no "Approve all".
* While approvals are pending the composer is blocked and a banner shows how many are left.
* Tool results appear in the thread; a failed tool shows its reason inline (not an error banner).
* Generated files/folders/links get **Open** and **Show in folder** buttons. Nothing opens automatically.
  Blocked: executables/scripts/shortcuts, network (UNC) paths, `..` paths, non-http(s) links.
* Second rounds of approvals work; a denied tool leaves the chat usable.
* Failure submitting decisions → cards stay open and **Retry** resends the same decisions.
  Stop during an approval turn → cards close, no auto-retry (a tool may already have run), and a
  warning says so.
* Resumable approvals (B6): if `GET .../messages` returns `pendingApprovals` on the newest message,
  cards reappear after restart.

## Assumptions to verify against the backend OpenAPI
* B6 shapes: `toolCalls[].artifacts[{kind,path}]`, and optional `toolCalls` / `pendingApprovals` on
  each message from the messages endpoint. Until B6 lands, file/folder buttons use a best-effort
  path found in the tool's output text (file_generator and code_builder only).
* Approvals are sent to `POST /api/chat/stream` (same body as /api/chat), answered via the usual
  SSE `completed` event.
* B7 (tool-loop limit) is backend-only; if it ends a turn with an error it shows through the normal
  error banner.

## Manual check (clean machine demo)
1. "Make a PDF about AI basics" → card shows file name/type/content → Approve → result + Open works.
2. Ask for an email → card shows To/Subject/full body → Deny → chat still works.
3. Ask for two tools in one turn → nothing is sent until both are decided.
4. Ask to open a URL and an app → card shows the exact target.
5. Kill the backend mid-submit → error + Retry; restart backend → Retry works.

## Not in this patch
FlaUI UI smoke test and fake-backend scripts (plan Phase 3 exit gate), model picker (Phase 4),
editable email before sending (later option), toast notifications for pending approvals (Phase 5).
