using VocalEye.Desktop.Core.DTOs;

namespace VocalEye.Desktop.Core.Interfaces;

/// <summary>
/// Chat backend operations. Implementations throw <see cref="Exceptions.ChatApiException"/>
/// for API/network failures and <see cref="OperationCanceledException"/> on cancellation.
/// </summary>
public interface IChatService
{
    Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationMessageDto>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a message and streams the reply. <paramref name="onTextChunk"/> is invoked for every
    /// "text" event, in order, on an arbitrary thread. Returns the "completed" payload.
    /// </summary>
    Task<ChatCompletedDto> StreamMessageAsync(ChatRequestDto request, Action<string> onTextChunk, CancellationToken cancellationToken);

    /// <summary>GET /api/conversations/{id}/pending-approvals: tool calls still waiting for the user (empty if none).</summary>
    Task<IReadOnlyList<PendingApprovalDto>> GetPendingApprovalsAsync(Guid conversationId, CancellationToken cancellationToken);

    Task RenameConversationAsync(Guid conversationId, string title, CancellationToken cancellationToken);

    Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken);
}
