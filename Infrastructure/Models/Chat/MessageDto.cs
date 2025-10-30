namespace Infrastructure.Models.Chat;

/// <summary>
/// Represents a single chat message.
/// </summary>
public sealed class MessageDto
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
