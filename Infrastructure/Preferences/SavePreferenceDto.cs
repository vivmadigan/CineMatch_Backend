using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// API CONTRACT (request) — payload the frontend sends to save preferences.
namespace Infrastructure.Preferences
{
    public sealed class SavePreferenceDto
    {
        // TMDB genre IDs the user chose (e.g., [28, 35])
        public List<int> GenreIds { get; set; } = new();

        // "short" | "medium" | "long"
        public string Length { get; set; } = "medium";

    }
}
