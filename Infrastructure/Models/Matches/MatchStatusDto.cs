namespace Infrastructure.Models.Matches
{
    /// <summary>
    /// Represents the current match status between two users.
    /// Used for profile pages and quick status checks.
    /// </summary>
public sealed class MatchStatusDto
    {
        /// <summary>
   /// Current match status with the target user.
     /// Values: "none", "pending_sent", "pending_received", "matched"
        /// </summary>
        public string Status { get; set; } = "none";

        /// <summary>
        /// Whether current user can send a match request to this user.
        /// False if request already sent or users are already matched.
    /// </summary>
  public bool CanMatch { get; set; }

        /// <summary>
        /// Whether current user can decline a match request from this user.
        /// True only if status is "pending_received".
        /// </summary>
        public bool CanDecline { get; set; }

        /// <summary>
        /// Timestamp when match request was sent (null if no request exists).
        /// If status is "pending_sent", this is when current user sent the request.
   /// If status is "pending_received", this is when target user sent the request.
   /// </summary>
        public DateTime? RequestSentAt { get; set; }

        /// <summary>
        /// Chat room ID if users are matched (null otherwise)
  /// </summary>
   public Guid? RoomId { get; set; }

   /// <summary>
        /// Movies that both users liked (shared interests)
 /// </summary>
        public List<SharedMovieDto> SharedMovies { get; set; } = [];
    }
}
