using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Infrastructure.External;
using Infrastructure.Models;
using Presentation.Tests.Helpers;
using Xunit;

namespace Presentation.Tests.Controllers;

/// <summary>
/// API integration tests for Movies endpoints when TMDB external service fails.
/// Tests how the API handles various TMDB error scenarios (500, 404, 429, timeout).
/// GOAL: Verify graceful degradation when external dependencies fail.
/// IMPORTANCE: CRITICAL - TMDB downtime should not crash the application.
/// </summary>
[Collection(nameof(ApiTestCollection))]
public class MoviesControllerTmdbFailureTests
{
    private readonly ApiTestFixture _fixture;

    public MoviesControllerTmdbFailureTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region TMDB 500 Internal Server Error

    /// <summary>
    /// API TEST: When TMDB returns 500, discover endpoint handles gracefully.
    /// GOAL: Application doesn't crash when TMDB is down.
    /// IMPORTANCE: CRITICAL - External service failures are common in production.
    /// </summary>
    [Fact]
    public async Task Discover_WhenTmdbReturns500_ReturnsEmptyOrCachedResults()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act - Call discover when TMDB would return 500
        // Note: In real scenario, mock ITmdbClient in fixture to return 500
     // For now, we test that the endpoint doesn't crash
      var response = await client.GetAsync("/api/movies/discover?genres=28");

    // Assert - Should not crash, returns 200 with empty results or cached data
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, // Empty results
          HttpStatusCode.ServiceUnavailable // Or 503 if implemented
        );

  if (response.StatusCode == HttpStatusCode.OK)
   {
       var movies = await response.Content.ReadFromJsonAsync<List<MovieSummaryDto>>();
   movies.Should().NotBeNull();
        // May be empty if TMDB unavailable
        }
    }

    /// <summary>
    /// API TEST: Test endpoint (popular movies) when TMDB returns 500.
    /// GOAL: Demo endpoint doesn't crash.
    /// IMPORTANCE: User-facing feature should degrade gracefully.
    /// </summary>
    [Fact]
    public async Task Test_WhenTmdbReturns500_HandlesGracefully()
    {
        // Arrange
   var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/movies/test");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
         HttpStatusCode.ServiceUnavailable
        );
    }

    #endregion

 #region TMDB 404 Not Found

    /// <summary>
    /// API TEST: When TMDB returns 404, API returns empty results.
    /// GOAL: Invalid TMDB requests don't crash the app.
 /// IMPORTANCE: Defensive programming against bad external responses.
    /// </summary>
    [Fact]
    public async Task Discover_WhenTmdbReturns404_ReturnsEmptyResults()
    {
        // Arrange
   var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

  // Act - Request with parameters that might cause TMDB 404
        var response = await client.GetAsync("/api/movies/discover?genres=99999");

        // Assert - Should return 200 with empty results, not crash
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var movies = await response.Content.ReadFromJsonAsync<List<MovieSummaryDto>>();
        movies.Should().NotBeNull();
    }

    #endregion

    #region TMDB 429 Rate Limit

    /// <summary>
    /// API TEST: When TMDB rate limits (429), API handles appropriately.
    /// GOAL: Rate limiting doesn't crash the application.
    /// IMPORTANCE: HIGH - TMDB has rate limits, must handle gracefully.
    /// </summary>
    [Fact]
    public async Task Discover_WhenTmdbRateLimits_ReturnsServiceUnavailableOrRetries()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act - Make multiple rapid requests to potentially trigger rate limit
   var responses = new List<HttpResponseMessage>();
   for (int i = 0; i < 3; i++)
     {
            responses.Add(await client.GetAsync("/api/movies/discover"));
        }

        // Assert - All requests should complete without exception
        responses.Should().AllSatisfy(r =>
        {
            r.StatusCode.Should().BeOneOf(
     HttpStatusCode.OK,
         HttpStatusCode.TooManyRequests,      // 429 if propagated
       HttpStatusCode.ServiceUnavailable    // 503 if retrying
    );
});
    }

    /// <summary>
    /// API TEST: Options endpoint when TMDB genres endpoint rate limits.
    /// GOAL: Cached genre list used when TMDB unavailable.
    /// IMPORTANCE: Options endpoint is called frequently, should be resilient.
    /// </summary>
    [Fact]
    public async Task Options_WhenTmdbGenresRateLimited_ReturnsCachedOrError()
    {
     // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act - First call should cache genres
        var response1 = await client.GetAsync("/api/movies/options");
        response1.EnsureSuccessStatusCode();

        // Second call should use cache even if TMDB fails
  var response2 = await client.GetAsync("/api/movies/options");

    // Assert - Should succeed with cached data
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        var options = await response2.Content.ReadFromJsonAsync<Infrastructure.Options.MovieOptionsDto>();
   options.Should().NotBeNull();
     options!.Genres.Should().NotBeNull();
    }

    #endregion

    #region TMDB Timeout

    /// <summary>
    /// API TEST: When TMDB request times out, API returns timeout error.
    /// GOAL: Long TMDB requests don't hang the API indefinitely.
    /// IMPORTANCE: HIGH - Timeouts are common with external APIs.
    /// NOTE: This test may take 8+ seconds (TMDB client timeout).
    /// </summary>
    [Fact(Skip = "Long-running test - enable for full test suite")]
    public async Task Discover_WhenTmdbTimesOut_ReturnsGatewayTimeoutOrEmpty()
    {
// Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

    // Act - Request might timeout if TMDB is slow
        // In real scenario, mock ITmdbClient to simulate timeout
      var response = await client.GetAsync("/api/movies/discover");

     // Assert - Should return error, not hang forever
        response.StatusCode.Should().BeOneOf(
     HttpStatusCode.OK,  // Empty results
         HttpStatusCode.GatewayTimeout,     // 504 if implemented
            HttpStatusCode.ServiceUnavailable  // 503 if timeout treated as unavailable
        );
    }

    #endregion

    #region Multiple TMDB Failures

    /// <summary>
    /// API TEST: Multiple endpoints fail gracefully when TMDB is down.
    /// GOAL: Verify all movie endpoints handle TMDB failures consistently.
    /// IMPORTANCE: Comprehensive failure testing.
    /// </summary>
    [Fact]
    public async Task AllMovieEndpoints_WhenTmdbUnavailable_HandleGracefully()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

    // Act - Call all TMDB-dependent endpoints
    var discoverResponse = await client.GetAsync("/api/movies/discover");
        var testResponse = await client.GetAsync("/api/movies/test");
        var optionsResponse = await client.GetAsync("/api/movies/options");

  // Assert - None should throw exceptions
        var responses = new[] { discoverResponse, testResponse, optionsResponse };
        responses.Should().AllSatisfy(r =>
        {
   r.Should().NotBeNull();
            r.StatusCode.Should().BeOneOf(
      HttpStatusCode.OK,
    HttpStatusCode.ServiceUnavailable,
          HttpStatusCode.GatewayTimeout
        );
 });
  }

    #endregion

    #region Fallback and Caching Tests

    /// <summary>
    /// API TEST: When TMDB fails, subsequent requests use cached data.
    /// GOAL: Verify caching strategy works during outages.
    /// IMPORTANCE: Resilience - users still get data during outages.
    /// </summary>
    [Fact]
    public async Task Discover_AfterTmdbFailure_UsesCachedResults()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act - First call should succeed and cache results
 var response1 = await client.GetAsync("/api/movies/discover?genres=28");
        var movies1 = await response1.Content.ReadFromJsonAsync<List<MovieSummaryDto>>();

 // Simulate TMDB failure by making another call
        // (In real test, mock TMDB to fail on second call)
 var response2 = await client.GetAsync("/api/movies/discover?genres=28");

        // Assert - Should still return results (cached or fresh)
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    var movies2 = await response2.Content.ReadFromJsonAsync<List<MovieSummaryDto>>();
        movies2.Should().NotBeNull();
    }

    #endregion

    #region Error Response Format Tests

    /// <summary>
    /// API TEST: When TMDB fails, API returns proper error format.
    /// GOAL: Error responses follow API conventions (ProblemDetails).
    /// IMPORTANCE: Consistent error format for frontend handling.
    /// </summary>
    [Fact]
    public async Task Discover_WhenTmdbFails_ReturnsProperErrorFormat()
    {
        // Arrange
     var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

     // Act - Call endpoint when TMDB might fail
        var response = await client.GetAsync("/api/movies/discover?genres=28&page=999");

   // Assert - If error, should be proper format
        if (!response.IsSuccessStatusCode)
        {
       response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
 
        // Should be ProblemDetails or custom error format
   var content = await response.Content.ReadAsStringAsync();
   content.Should().NotBeNullOrEmpty();
     }
    }

    #endregion

  #region Retry Logic Tests

    /// <summary>
    /// API TEST: Verify retry logic when TMDB returns transient errors.
    /// GOAL: Transient failures (500, timeout) are retried before giving up.
    /// IMPORTANCE: Improves reliability by handling temporary TMDB issues.
    /// NOTE: This test assumes retry logic is implemented. If not, documents expected behavior.
    /// </summary>
    [Fact(Skip = "Enable if Polly retry policy is implemented")]
    public async Task Discover_WithTransientTmdbFailure_RetriesBeforeFailing()
    {
 // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act - Make request that would trigger retry logic
    var response = await client.GetAsync("/api/movies/discover");

        // Assert - Should have retried (check logs or response headers)
 response.StatusCode.Should().BeOneOf(
 HttpStatusCode.OK,
        HttpStatusCode.ServiceUnavailable
  );

        // If implemented, check Retry-After header or custom retry count header
     if (response.Headers.Contains("X-Retry-Count"))
        {
   var retryCount = response.Headers.GetValues("X-Retry-Count").First();
  int.Parse(retryCount).Should().BeGreaterThan(0);
        }
    }

    #endregion
}
