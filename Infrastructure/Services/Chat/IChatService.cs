using Infrastructure.Models.Chat;

namespace Infrastructure.Services.Chat
{
    // Purpose: Chat operations (send, retrieve, leave rooms).
    // Why: Encapsulates chat logic; keeps SignalR hub and controllers thin.
    public interface IChatService
    {
/// <summary>
    /// List all chat rooms for the current user with last message preview.
   /// </summary>
 /// <param name="userId">Current user's ID</param>
    /// <param name="ct">Cancellation token</param>
     /// <returns>List of rooms with other user info and last message</returns>
   Task<IReadOnlyList<ChatRoomListItemDto>> ListMyRoomsAsync(string userId, CancellationToken ct);

    /// <summary>
     /// Get paginated message history for a room.
        /// </summary>
        /// <param name="roomId">Chat room ID</param>
        /// <param name="take">Number of messages to return (default 50)</param>
        /// <param name="beforeUtc">Get messages before this timestamp (for pagination)</param>
        /// <param name="userId">Current user's ID (for membership validation)</param>
        /// <param name="ct">Cancellation token</param>
   /// <returns>List of messages, newest first</returns>
     Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid roomId, int take, DateTime? beforeUtc, string userId, CancellationToken ct);

        /// <summary>
     /// Append a new message to a room and return the saved DTO.
        /// </summary>
        /// <param name="roomId">Chat room ID</param>
        /// <param name="userId">Sender's user ID</param>
     /// <param name="text">Message text (max 2000 chars)</param>
        /// <param name="ct">Cancellation token</param>
     /// <returns>Saved message DTO with sender info</returns>
        /// <exception cref="InvalidOperationException">User not an active member</exception>
   /// <exception cref="ArgumentException">Text empty or too long</exception>
  Task<ChatMessageDto> AppendAsync(Guid roomId, string userId, string text, CancellationToken ct);

        /// <summary>
        /// Mark user's membership as inactive (leave room).
   /// </summary>
        /// <param name="roomId">Chat room ID</param>
        /// <param name="userId">User leaving the room</param>
        /// <param name="ct">Cancellation token</param>
        /// <exception cref="InvalidOperationException">User not a member</exception>
        Task LeaveAsync(Guid roomId, string userId, CancellationToken ct);

    /// <summary>
    /// Reactivate membership when user rejoins a room (or validate if already active).
    /// </summary>
    /// <param name="roomId">Chat room ID</param>
    /// <param name="userId">User rejoining the room</param>
    /// <param name="ct">Cancellation token</param>
    /// <exception cref="InvalidOperationException">User not a member</exception>
    Task ReactivateMembershipAsync(Guid roomId, string userId, CancellationToken ct);
    }
}
