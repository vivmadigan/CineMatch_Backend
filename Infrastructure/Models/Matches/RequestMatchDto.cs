using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Infrastructure.Models.Matches
{
    // API CONTRACT (request) — initiate a match request with another user for a specific movie.
    public sealed class RequestMatchDto
    {
        [Required]
        [JsonPropertyName("targetUserId")]  // ?? Explicit camelCase mapping
        public string TargetUserId { get; set; } = "";

        [Required]
      [Range(1, int.MaxValue, ErrorMessage = "TmdbId must be a positive integer")]
        [JsonPropertyName("tmdbId")]  // ?? Explicit camelCase mapping
        public int TmdbId { get; set; }
    }
}
