namespace Infrastructure.Models.Matches
{
    /// <summary>
    /// Represents an active match (users who have matched and have a chat room).
    /// Used for displaying the "Active Matches" or "Chats" list.
    /// </summary>
    public sealed class ActiveMatchDto
    {
        /// <summary>
     /// User ID of the matched user
        /// </summary>
     public string UserId { get; set; } = "";

        /// <summary>
 /// Display name of the matched user
  /// </summary>
        public string DisplayName { get; set; } = "";

      /// <summary>
  /// Chat room ID for this match
/// </summary>
        public Guid RoomId { get; set; }

        /// <summary>
        /// When the mutual match was created (chat room created date)
        /// </summary>
        public DateTime MatchedAt { get; set; }

        /// <summary>
        /// Timestamp of the last message in this chat (null if no messages yet)
   /// </summary>
        public DateTime? LastMessageAt { get; set; }

        /// <summary>
      /// Preview of the last message sent in this chat (null if no messages)
     /// </summary>
        public string? LastMessage { get; set; }

        /// <summary>
        /// Number of unread messages in this chat for the current user
        /// </summary>
      public int UnreadCount { get; set; }

        /// <summary>
        /// Movies that both users liked (context for why they matched)
    /// </summary>
        public List<SharedMovieDto> SharedMovies { get; set; } = [];
  }
}
