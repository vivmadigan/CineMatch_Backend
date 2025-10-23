using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data.Entities
{
    // EF ENTITY — persisted user settings for Discover (genre IDs + length bucket).
    // Why: store *user* decisions only; movie catalog still comes from TMDB.
    public class UserPreference
    {
        [Key]                                              // PK
        [ForeignKey(nameof(User))]                         // also the FK to the User nav
        public string UserId { get; set; } = "";

        // Will be stored as JSON nvarchar(max) via a ValueConverter in OnModelCreating
        public List<int> GenreIds { get; set; } = new();

        [MaxLength(15)]
        public string LengthKey { get; set; } = "medium";  // "short" | "medium" | "long"

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public UserEntity? User { get; set; }
    }
}
