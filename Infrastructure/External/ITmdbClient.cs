using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Purpose: Contract for TMDB calls the app needs now.
// Why: Improves testability (mock ITmdbClient) and separation of concerns.
namespace Infrastructure.External
{
    public interface ITmdbClient
    {
        // Fetch a page of popular movies for MVP demos.
        // Good signal for "show 5 movies" without obscure titles.
        Task<TmdbDiscoverResponse> DiscoverTopAsync(int page, string? language, string? region, CancellationToken ct);

        // Official TMDB genre list (localized by language)
        Task<TmdbGenreResponse> GetGenresAsync(string? language, CancellationToken ct);
    }
}
