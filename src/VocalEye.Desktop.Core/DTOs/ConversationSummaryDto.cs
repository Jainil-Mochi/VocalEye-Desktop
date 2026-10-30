using System.Text.Json.Serialization;

namespace VocalEye.Desktop.Core.DTOs;

/// <summary>Item returned by GET /api/conversations.</summary>
public sealed class ConversationSummaryDto
{
    public Guid ConversationId { get; init; }

    /// <summary>Null until backend slice B4 (titles) is implemented.</summary>
    public string? Title { get; init; }
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Model { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public int MessageCount { get; init; }
}
