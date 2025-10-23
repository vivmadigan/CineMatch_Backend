using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Likes
{
    // API CONTRACT (request) — optional snapshot the UI can send when liking a movie.
    public sealed class LikeMovieRequestDto
    {
        public string? Title { get; set; }
        public string? PosterPath { get; set; }   // raw TMDB path (e.g. /abc.jpg)
        public string? ReleaseYear { get; set; }  // e.g. "2024"
    }
}
