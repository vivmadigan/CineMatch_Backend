using Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Purpose: Concrete TMDB client (typed HttpClient) used via ITmdbClient.
// Why: Centralizes BaseAddress/Timeout/headers and keeps API keys on the server.
//
// Why 'sealed'?
// - This class is not intended to be a base type; we mock ITmdbClient in tests.
// - Sealing communicates intent ("don't inherit me") and enables tiny JIT wins.
// - Visibility is still 'public'; 'sealed' only prevents subclassing.
//
// Why inject IOptions<TmdbOptions> instead of TmdbOptions directly?
// - Options pattern binds config (appsettings + user-secrets + env) into a typed class.
// - IOptions<T> is the DI wrapper that provides the current value via .Value.
// - Keeps config out of code and lets you change keys/URLs without recompiling.
//
// Why use a *typed* HttpClient (configured in Program.cs)?
// - Centralizes BaseAddress/Timeout/headers per external API.
// - IHttpClientFactory manages sockets/DNS lifetimes safely.
// - You inject ITmdbClient in controllers/services; cleaner & easy to mock.
namespace Infrastructure.External
{
    public sealed class TmdbClient : ITmdbClient
    {
        private readonly HttpClient _http;      // Provided by IHttpClientFactory with BaseAddress/Timeout from Program.cs
        private readonly TmdbOptions _opt;      // The actual options object bound from config/user-secrets
        private readonly ILogger<TmdbClient> _logger;

        public TmdbClient(
            HttpClient http,
            IOptions<TmdbOptions> opt,
            ILogger<TmdbClient> logger)
        {
            _http = http;
            _opt = opt.Value;
            _logger = logger;

            // Optional: if you decide to use v4 later, uncomment:
            // if (!string.IsNullOrWhiteSpace(_opt.ReadAccessToken))
            //     _http.DefaultRequestHeaders.Authorization =
            //         new AuthenticationHeaderValue("Bearer", _opt.ReadAccessToken);

            // Prefer JSON responses explicitly (some APIs vary behavior by Accept header).
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
        // Return either the caller's language or the default from options if blank.
        private string Lang(string? language) =>
            string.IsNullOrWhiteSpace(language) ? _opt.DefaultLanguage : language!;

        // Same for region.
        private string Reg(string? region) =>
            string.IsNullOrWhiteSpace(region) ? _opt.DefaultRegion : region!;

        public async Task<TmdbDiscoverResponse> DiscoverTopAsync(
            int page, string? language, string? region, CancellationToken ct)
        {
            // Fail fast if we forgot to set the secret.
            if (string.IsNullOrWhiteSpace(_opt.ApiKey))
                throw new InvalidOperationException("TMDB ApiKey is missing. Set TMDB:ApiKey in user-secrets.");

            // Build query string parts (language, region, filters, paging, api_key).
            var qs = new List<string>
            {
                $"language={Uri.EscapeDataString(Lang(language))}",
                $"region={Uri.EscapeDataString(Reg(region))}",
                "include_adult=false",
                "include_video=false",
                "sort_by=popularity.desc",
                "vote_count.gte=100",          // filter out super-obscure titles for nicer demos
                $"page={Math.Max(page, 1)}",
                $"api_key={_opt.ApiKey}"       // v3 API key passed as query param (simple MVP choice)
            };

            // Compose the relative URL; BaseAddress is already set in Program.cs.
            // IMPORTANT: NO leading slash.
            // HttpClient combines BaseAddress + relative path. If you start with '/', it resets to domain root
            var url = $"discover/movie?{string.Join('&', qs)}"; // no leading slash

            // Make the HTTP call (honors the CancellationToken).
            using var res = await _http.GetAsync(url, ct);
            _logger.LogInformation("TMDB request: {Uri}", res.RequestMessage?.RequestUri);

            // If TMDB returns an error, log and return an empty payload (controller can still return 200 with []).
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("TMDB discover failed: {Status} {Body}", (int)res.StatusCode, body);
                return new TmdbDiscoverResponse();
            }

            // Read the response stream.
            await using var stream = await res.Content.ReadAsStreamAsync(ct);

            // Deserialize JSON into our minimal model; case-insensitive helps with minor casing differences.
            var data = await JsonSerializer.DeserializeAsync<TmdbDiscoverResponse>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                ct);

            // If deserialization returned null for any reason, return an empty model.
            return data ?? new TmdbDiscoverResponse();
        }

        public async Task<TmdbGenreResponse> GetGenresAsync(string? language, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_opt.ApiKey))
                throw new InvalidOperationException("TMDB ApiKey is missing. Set TMDB:ApiKey in user-secrets.");

            // No leading slash so BaseAddress (/3/) stays intact
            var url = $"genre/movie/list?language={Uri.EscapeDataString(Lang(language))}&api_key={_opt.ApiKey}";

            using var res = await _http.GetAsync(url, ct);
            _logger.LogInformation("TMDB request: {Uri}", res.RequestMessage?.RequestUri);

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("TMDB genres failed: {Status} {Body}", (int)res.StatusCode, body);
                return new TmdbGenreResponse(); // safe empty
            }

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            var data = await JsonSerializer.DeserializeAsync<TmdbGenreResponse>(
                stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

            return data ?? new TmdbGenreResponse();
        }

        public async Task<TmdbDiscoverResponse> DiscoverAsync(
            IEnumerable<int> genreIds, int? runtimeMin, int? runtimeMax,
            int page, string? language, string? region, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_opt.ApiKey))
                throw new InvalidOperationException("TMDB ApiKey is missing. Set TMDB:ApiKey in user-secrets.");

            var qs = new List<string>
    {
        $"language={Uri.EscapeDataString(Lang(language))}",
    $"region={Uri.EscapeDataString(Reg(region))}",
        "include_adult=false",
        "include_video=false",
    "sort_by=popularity.desc",
      "vote_count.gte=50",   // keep reasonably-known titles
            $"page={Math.Max(page, 1)}",
        $"api_key={_opt.ApiKey}"
      };

            // Add genre filter if any genres specified (TMDB uses comma-separated with AND logic)
            var genreList = genreIds?.Where(g => g > 0).ToList();
            if (genreList?.Count > 0)
                qs.Add($"with_genres={string.Join(',', genreList)}");

            // Add runtime filters (TMDB uses with_runtime.gte and with_runtime.lte)
            if (runtimeMin.HasValue)
                qs.Add($"with_runtime.gte={runtimeMin.Value}");
            if (runtimeMax.HasValue)
                qs.Add($"with_runtime.lte={runtimeMax.Value}");

            var url = $"discover/movie?{string.Join('&', qs)}"; // no leading slash

            using var res = await _http.GetAsync(url, ct);
            _logger.LogInformation("TMDB request: {Uri}", res.RequestMessage?.RequestUri);

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("TMDB discover (filtered) failed: {Status} {Body}", (int)res.StatusCode, body);
                return new TmdbDiscoverResponse();
            }

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            var data = await JsonSerializer.DeserializeAsync<TmdbDiscoverResponse>(
     stream,
   new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
       ct);

            return data ?? new TmdbDiscoverResponse();
        }
    }
}
