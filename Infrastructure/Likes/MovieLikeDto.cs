using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Likes
{
    public sealed class MovieLikeDto
    {
        public int TmdbId { get; set; }
        public string Title { get; set; } = "";
        public string? PosterUrl { get; set; }     // full URL, built from ImageBase + PosterPath
        public string? ReleaseYear { get; set; }
        public DateTime LikedAt { get; set; }
    }
}
