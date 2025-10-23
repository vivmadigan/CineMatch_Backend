using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data.Entities
{
    // EF ENTITY — a user's "want to watch" selection for a TMDB movie.
    // We keep a tiny snapshot (Title, PosterPath, Year) so UI can render even if TMDB is slow.
    //
    // Keys & Indexes:
    // - Composite PK (UserId, TmdbId) prevents duplicates per user.
    // - Index on TmdbId: fast "who liked movie X?"
    // - Index on (UserId, CreatedAt): fast "recent likes for this user".

    [PrimaryKey(nameof(UserId), nameof(TmdbId))]
    [Index(nameof(TmdbId))]
    [Index(nameof(UserId), nameof(CreatedAt))]
    public class UserMovieLike
    {
        public string UserId { get; set; } = "";

        public int TmdbId { get; set; }

        public bool Liked { get; set; } = true;          // MVP: presence of row means "liked"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(256)]
        public string Title { get; set; } = "";

        [MaxLength(256)]
        public string? PosterPath { get; set; }          // store raw TMDB path (e.g. /abc.jpg)

        [MaxLength(4)]
        public string? ReleaseYear { get; set; }

        [ForeignKey(nameof(UserId))]
        public UserEntity? User { get; set; }
    }
}
