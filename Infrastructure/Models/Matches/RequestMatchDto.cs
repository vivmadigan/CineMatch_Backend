using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Models.Matches
{
    // API CONTRACT (request) — initiate a match request with another user for a specific movie.
    public sealed class RequestMatchDto
    {
        [Required]
        public string TargetUserId { get; set; } = "";

     [Required]
   [Range(1, int.MaxValue, ErrorMessage = "TmdbId must be a positive integer")]
        public int TmdbId { get; set; }
    }
}
