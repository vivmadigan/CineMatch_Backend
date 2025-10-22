using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Infrastructure.External
{
    // Purpose: Minimal TMDB response models used by our client.
    // Why: Don't mirror the whole TMDB schema; include only what your UI needs.
    public sealed class TmdbDiscoverResponse
    {
        public int Page { get; set; }

        [JsonPropertyName("total_pages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("total_results")]
        public int TotalResults { get; set; }

        public List<TmdbMovie> Results { get; set; } = [];
    }
}
