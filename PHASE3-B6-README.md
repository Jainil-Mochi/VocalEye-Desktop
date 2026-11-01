# Phase 3 update for your finished backend slice B6

Overwrite these files (they replace the earlier Phase 3 versions, including any hand edits made for
the "No path was given" fix). Nothing else changes.

What changed to match your contract
* Artifacts are `{ kind, value }` (`file`, `folder`, `url`). `desktop_command` URLs get an "Open link" button.
  A legacy `path` field is still accepted; artifacts with no usable value are ignored and the old
  output-text path search is only a fallback.
* History: tool messages carry `toolResults` (name, argumentsJson, success, output, artifacts), so old tool
  cards are redrawn when you reopen a chat. An assistant row that only says "Used tools: x" is hidden when the
  next row has the real results.
* Resuming approvals: opening a conversation calls `GET /api/conversations/{id}/pending-approvals`; any waiting
  calls show as approval cards on the last message and the composer stays blocked until they are answered.
  A 404 or an older backend is ignored quietly.
* `IChatService` gained `GetPendingApprovalsAsync` (update any hand-written fake of the interface).
* The empty-reply message now explains the likely cause (a tool call too large to finish) and offers Retry.

Assumed property names (check in Swagger): `toolResults[].name` (or `toolName`), `argumentsJson`, `success`,
`output`, `artifacts`; pending-approvals items shaped like `pendingApprovals` in a chat response.
