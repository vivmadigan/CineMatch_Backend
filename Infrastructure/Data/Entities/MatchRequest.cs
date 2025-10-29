using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Data.Entities
{
    // EF ENTITY — a user's request to match with another user for a specific movie.
    // When reciprocal request exists (target?requestor, same tmdbId), a ChatRoom is created.
    //
    // Keys & Indexes:
    // - GUID PK for unique identification
    // - Index on (TargetUserId, RequestorId, TmdbId) for fast reciprocal lookup
    // - Index on RequestorId for "my sent requests"
    // - Index on TargetUserId for "requests I received"

    [Index(nameof(TargetUserId), nameof(RequestorId), nameof(TmdbId))]
    [Index(nameof(RequestorId))]
    [Index(nameof(TargetUserId))]
    public class MatchRequest
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(450)]
        public string RequestorId { get; set; } = "";

        [Required]
        [MaxLength(450)]
        public string TargetUserId { get; set; } = "";

        public int TmdbId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(RequestorId))]
        public UserEntity? Requestor { get; set; }

        [ForeignKey(nameof(TargetUserId))]
        public UserEntity? Target { get; set; }
    }
}
