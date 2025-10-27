using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// API CONTRACT (request) — payload the frontend sends to save preferences.
namespace Infrastructure.Preferences
{
    /// <summary>
    /// Request DTO for saving user preferences.
    /// Validates input at the API boundary before processing.
    /// </summary>
    public sealed class SavePreferenceDto
    {
        /// <summary>
        /// List of TMDB genre IDs the user is interested in.
        /// Must contain valid positive integers.
        /// Maximum 50 genres can be selected.
        /// </summary>
        [Required]
        [MaxLength(50, ErrorMessage = "Cannot select more than 50 genres")]
        public List<int> GenreIds { get; set; } = new();

        /// <summary>
        /// Preferred movie length: "short", "medium", or "long".
        /// Case-insensitive validation.
        /// </summary>
        [Required]
        [RegularExpression("^(short|medium|long)$", ErrorMessage = "Length must be 'short', 'medium', or 'long'")]
        public string Length { get; set; } = "medium";
    }
}
