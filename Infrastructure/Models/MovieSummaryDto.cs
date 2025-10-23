using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Models
{
    // Purpose: The shape your API returns to the frontend.
    // Why: Normalize once in backend; the UI stays simple and stable.
    public sealed class MovieSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string OneLiner { get; set; } = ""; // 1-line synopsis for cards
        public int? RuntimeMinutes { get; set; }    // null for MVP (we don't call the details endpoint yet)
        public string? PosterUrl { get; set; }      // full CDN URL with size baked in
        public string? BackdropUrl { get; set; }
        public List<int> GenreIds { get; set; } = [];
        public string? ReleaseYear { get; set; }
        public double Rating { get; set; }          // 0..10, 1 decimal
        public string TmdbUrl { get; set; } = "";   // handy link while developing
    }
}
