namespace Infrastructure.Models.Chat;

/// <summary>
/// Request body for sending a message.
/// </summary>
public sealed class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
}
