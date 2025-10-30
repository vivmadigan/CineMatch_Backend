namespace Infrastructure.Models.Chat;

/// <summary>
/// Represents a conversation in the user's chat list.
/// Includes the other user's info and last message preview.
/// </summary>
public sealed class ConversationDto
{
    public Guid RoomId { get; set; }
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherDisplayName { get; set; } = string.Empty;
    public string? OtherAvatar { get; set; }
    public int? TmdbId { get; set; }
    public string? LastText { get; set; }
    public DateTime? LastAt { get; set; }
    public int UnreadCount { get; set; }
}
