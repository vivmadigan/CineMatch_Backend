namespace Infrastructure.Models.Matches
{
    /// <summary>
    /// Represents a potential match candidate with shared movie interests.
    /// </summary>
    public sealed class CandidateDto
    {
        /// <summary>
        /// User ID of the candidate
        /// </summary>
        public string UserId { get; set; } = "";

        /// <summary>
        /// Display name of the candidate
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Number of movies both users have liked
        /// </summary>
        public int OverlapCount { get; set; }

        /// <summary>
        /// List of TMDB movie IDs that both users liked (for backward compatibility)
        /// </summary>
        public List<int> SharedMovieIds { get; set; } = [];

        /// <summary>
        /// Full movie details for shared movies (title, poster, year).
        /// Eliminates need for frontend to make additional API calls.
        /// </summary>
        public List<SharedMovieDto> SharedMovies { get; set; } = [];

        /// <summary>
        /// Match request status between current user and this candidate.
        /// Possible values:
        /// - "none": No match requests exist
        /// - "pending_sent": Current user sent request to candidate (waiting for response)
        /// - "pending_received": Candidate sent request to current user (candidate is waiting)
        /// - "matched": Mutual match exists (chat room created)
        /// </summary>
        public string MatchStatus { get; set; } = "none";

        /// <summary>
        /// Timestamp when current user sent match request to this candidate.
        /// Null if no request was sent or if candidate sent request to current user.
        /// Used to display "Sent X days ago" in UI.
        /// </summary>
        public DateTime? RequestSentAt { get; set; }
    }
}
