namespace Infrastructure.Models.Chat;

/// <summary>
/// Room metadata returned when user joins a chat room.
/// </summary>
public sealed class RoomMetadataDto
{
    public Guid RoomId { get; set; }
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherDisplayName { get; set; } = string.Empty;
    public string? OtherAvatar { get; set; }
    public int? TmdbId { get; set; }
}
