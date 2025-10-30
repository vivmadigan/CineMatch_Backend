namespace Infrastructure.Models.Matches
{
    /// <summary>
    /// Minimal movie info for displaying shared movies in match cards.
    /// Includes full CDN URLs so frontend doesn't need additional API calls.
    /// </summary>
    public sealed class SharedMovieDto
    {
        /// <summary>
        /// TMDB movie ID (e.g., 550 for Fight Club)
        /// </summary>
        public int TmdbId { get; set; }

        /// <summary>
        /// Movie title (e.g., "Fight Club")
        /// </summary>
     public string Title { get; set; } = "";

   /// <summary>
        /// Full CDN URL to poster image (e.g., "https://image.tmdb.org/t/p/w342/...")
  /// </summary>
        public string PosterUrl { get; set; } = "";

        /// <summary>
        /// Release year (e.g., "1999")
        /// </summary>
        public string? ReleaseYear { get; set; }
    }
}
