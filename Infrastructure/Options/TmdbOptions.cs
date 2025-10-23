using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Purpose: Strongly-typed options bound from configuration section "TMDB" (user-secrets).
// Why: Keeps keys and base URLs out of code, easy to inject anywhere via IOptions<TmdbOptions>.

namespace Infrastructure.Options
{
    // Note on 'sealed':
    // - 'public' controls visibility; 'sealed' controls inheritance.
    // - We mark simple DTOs/options/services as 'sealed' to say "not a base class"
    public sealed class TmdbOptions
    {
        public string BaseUrl { get; set; } = "https://api.themoviedb.org/3"; // TMDB v3 base
        public string ImageBase { get; set; } = "https://image.tmdb.org/t/p/"; // CDN base for posters/backdrops
        public string ApiKey { get; set; } = "";                               // v3 key (kept server-side)
        public string DefaultLanguage { get; set; } = "en-US";                 // or "sv-SE"
        public string DefaultRegion { get; set; } = "US";                      // or "SE"
        public string? ReadAccessToken { get; set; }                           // optional v4 token for later
    }
}
