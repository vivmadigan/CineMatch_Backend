using FluentAssertions;
using Infrastructure.External;
using Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Infrastructure.Tests.Unit.External;

/// <summary>
/// Unit tests for TMDB client error handling and network failures.
/// Uses mocked HttpClient to simulate various failure scenarios.
/// GOAL: Verify error handling logic without real network calls.
/// IMPORTANCE: HIGH - External APIs fail in production, we must handle gracefully.
/// </summary>
public class TmdbClientErrorHandlingTests
{
    #region Configuration Error Tests

    /// <summary>
    /// ERROR TEST: Missing API key throws exception immediately.
    /// GOAL: Fail fast if configuration is missing.
    /// IMPORTANCE: CRITICAL - prevents silent failures.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_MissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new TmdbOptions
   {
            BaseUrl = "https://api.themoviedb.org/3/",
      ApiKey = "" // Missing!
});
        
        var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object)
        {
        BaseAddress = new Uri(options.Value.BaseUrl)
        };
        
        var client = new TmdbClient(httpClient, options, NullLogger<TmdbClient>.Instance);
        
        // Act
        Func<Task> act = async () => await client.DiscoverAsync(
Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
        
        // Assert
      await act.Should().ThrowAsync<InvalidOperationException>()
       .WithMessage("*ApiKey is missing*");
    }

    /// <summary>
    /// ERROR TEST: Missing API key in DiscoverTopAsync.
    /// GOAL: All endpoints check for API key.
    /// IMPORTANCE: Consistency across endpoints.
    /// </summary>
    [Fact]
 public async Task DiscoverTopAsync_MissingApiKey_ThrowsInvalidOperationException()
    {
    // Arrange
  var options = Microsoft.Extensions.Options.Options.Create(new TmdbOptions { ApiKey = "" });
        var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object)
      {
  BaseAddress = new Uri("https://api.themoviedb.org/3/")
     };
        
        var client = new TmdbClient(httpClient, options, NullLogger<TmdbClient>.Instance);
        
    // Act
        Func<Task> act = async () => await client.DiscoverTopAsync(1, null, null, CancellationToken.None);
        
        // Assert
     await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// ERROR TEST: Missing API key in GetGenresAsync.
    /// GOAL: All endpoints check for API key.
    /// IMPORTANCE: Consistency across endpoints.
    /// </summary>
    [Fact]
    public async Task GetGenresAsync_MissingApiKey_ThrowsInvalidOperationException()
    {
      // Arrange
  var options = Microsoft.Extensions.Options.Options.Create(new TmdbOptions { ApiKey = "" });
        var mockHandler = new Mock<HttpMessageHandler>();
  var httpClient = new HttpClient(mockHandler.Object)
  {
  BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };
 
        var client = new TmdbClient(httpClient, options, NullLogger<TmdbClient>.Instance);
        
        // Act
        Func<Task> act = async () => await client.GetGenresAsync(null, CancellationToken.None);
        
        // Assert
 await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region HTTP Status Code Error Tests

    /// <summary>
    /// ERROR TEST: 401 Unauthorized (invalid API key).
    /// GOAL: Returns empty response instead of throwing.
    /// IMPORTANCE: Current implementation returns empty on errors.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_Returns401_ReturnsEmptyResponse()
    {
      // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
        mockHandler.Protected()
         .Setup<Task<HttpResponseMessage>>(
    "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
       ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
          {
StatusCode = HttpStatusCode.Unauthorized,
     Content = new StringContent("{\"status_code\":7,\"status_message\":\"Invalid API key\"}")
       });
  
        // Act
      var result = await client.DiscoverAsync(
        Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
    
        // Assert
        result.Should().NotBeNull();
     result.Results.Should().BeEmpty();
    }

    /// <summary>
    /// ERROR TEST: 404 Not Found (invalid endpoint).
  /// GOAL: Returns empty response instead of throwing.
    /// IMPORTANCE: Graceful degradation.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_Returns404_ReturnsEmptyResponse()
    {
        // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
mockHandler.Protected()
       .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
    ItExpr.IsAny<HttpRequestMessage>(),
   ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
          StatusCode = HttpStatusCode.NotFound,
   Content = new StringContent("{\"status_code\":34,\"status_message\":\"The resource you requested could not be found.\"}")
      });
        
        // Act
        var result = await client.DiscoverAsync(
   Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
        
      // Assert
    result.Should().NotBeNull();
        result.Results.Should().BeEmpty();
    }

    /// <summary>
    /// ERROR TEST: 500 Internal Server Error.
    /// GOAL: Returns empty response, doesn't crash app.
    /// IMPORTANCE: TMDB server errors shouldn't take down our app.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_Returns500_ReturnsEmptyResponse()
    {
      // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
        mockHandler.Protected()
    .Setup<Task<HttpResponseMessage>>(
     "SendAsync",
   ItExpr.IsAny<HttpRequestMessage>(),
     ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(new HttpResponseMessage
            {
      StatusCode = HttpStatusCode.InternalServerError,
    Content = new StringContent("Internal Server Error")
            });
        
   // Act
    var result = await client.DiscoverAsync(
            Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
   
        // Assert
        result.Should().NotBeNull();
        result.Results.Should().BeEmpty();
    }

/// <summary>
  /// ERROR TEST: 429 Rate Limit Exceeded.
    /// GOAL: Returns empty response (could be enhanced with retry logic later).
    /// IMPORTANCE: TMDB has rate limits.
    /// </summary>
    [Fact]
 public async Task DiscoverAsync_Returns429_ReturnsEmptyResponse()
    {
    // Arrange
   var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
   
        mockHandler.Protected()
    .Setup<Task<HttpResponseMessage>>(
  "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
    ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = (HttpStatusCode)429,
     Content = new StringContent("{\"status_message\":\"Your request count (41) is over the allowed limit of 40.\"}")
            });
        
   // Act
      var result = await client.DiscoverAsync(
 Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
        
        // Assert
        result.Should().NotBeNull();
  result.Results.Should().BeEmpty();
    }

    #endregion

    #region Network Failure Tests

    /// <summary>
    /// ERROR TEST: Network timeout.
    /// GOAL: Timeout exception propagates (HttpClient handles this).
    /// IMPORTANCE: Long network delays should timeout.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_NetworkTimeout_ThrowsTaskCanceledException()
 {
   // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
 mockHandler.Protected()
   .Setup<Task<HttpResponseMessage>>(
      "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));
        
        // Act
   Func<Task> act = async () => await client.DiscoverAsync(
            Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
        
     // Assert
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    /// <summary>
    /// ERROR TEST: Network unavailable (HttpRequestException).
    /// GOAL: Network errors propagate up.
    /// IMPORTANCE: Offline scenarios.
  /// </summary>
    [Fact]
    public async Task DiscoverAsync_NetworkUnavailable_ThrowsHttpRequestException()
    {
        // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
 "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
      ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("No such host is known"));
        
        // Act
   Func<Task> act = async () => await client.DiscoverAsync(
      Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
        
        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    #endregion

    #region Malformed Response Tests

    /// <summary>
    /// ERROR TEST: Invalid JSON response.
    /// GOAL: JSON parsing errors throw JsonException.
    /// IMPORTANCE: Malformed responses should be caught.
    /// </summary>
  [Fact]
    public async Task DiscoverAsync_InvalidJson_ThrowsJsonException()
    {
        // Arrange
var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
    "SendAsync",
  ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync(new HttpResponseMessage
          {
           StatusCode = HttpStatusCode.OK,
  Content = new StringContent("{invalid json}") // Malformed!
            });

        // Act
        Func<Task> act = async () => await client.DiscoverAsync(
     Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
     
        // Assert
        await act.Should().ThrowAsync<JsonException>();
    }

    /// <summary>
    /// ERROR TEST: Empty response body.
    /// GOAL: Empty response returns empty model.
    /// IMPORTANCE: Edge case - TMDB returns no data.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_EmptyResponseBody_ReturnsEmptyModel()
    {
        // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
   
        mockHandler.Protected()
       .Setup<Task<HttpResponseMessage>>(
    "SendAsync",
       ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
       StatusCode = HttpStatusCode.OK,
         Content = new StringContent("{\"results\":[]}")
     });
  
        // Act
    var result = await client.DiscoverAsync(
            Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
   
        // Assert
        result.Should().NotBeNull();
        result.Results.Should().BeEmpty();
 }

    /// <summary>
  /// ERROR TEST: Null results array in response.
    /// GOAL: Null results don't crash, return empty.
    /// IMPORTANCE: Defensive deserialization.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_NullResultsArray_ReturnsEmptyModel()
    {
        // Arrange
  var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
      mockHandler.Protected()
   .Setup<Task<HttpResponseMessage>>(
    "SendAsync",
           ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
           StatusCode = HttpStatusCode.OK,
  Content = new StringContent("{\"results\":null}")
   });
        
        // Act
var result = await client.DiscoverAsync(
      Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
  
     // Assert - Should not crash
  result.Should().NotBeNull();
    }

    #endregion

    #region Cancellation Token Tests

    /// <summary>
    /// CANCELLATION TEST: Honors cancellation token.
  /// GOAL: Operations can be cancelled mid-flight.
 /// IMPORTANCE: User can cancel slow requests.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
   
    var cts = new CancellationTokenSource();
  cts.Cancel(); // Cancel immediately
     
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
    ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
    .ThrowsAsync(new OperationCanceledException());
        
        // Act
        Func<Task> act = async () => await client.DiscoverAsync(
   Array.Empty<int>(), null, null, 1, null, null, cts.Token);
        
        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region GetGenres Error Tests

    /// <summary>
    /// ERROR TEST: GetGenres with 500 error returns empty.
    /// GOAL: Genre endpoint follows same error handling pattern.
    /// IMPORTANCE: Consistency across endpoints.
    /// </summary>
    [Fact]
    public async Task GetGenresAsync_Returns500_ReturnsEmptyResponse()
    {
      // Arrange
     var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
        mockHandler.Protected()
.Setup<Task<HttpResponseMessage>>(
    "SendAsync",
  ItExpr.IsAny<HttpRequestMessage>(),
          ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync(new HttpResponseMessage
      {
   StatusCode = HttpStatusCode.InternalServerError,
  Content = new StringContent("Server Error")
            });
        
   // Act
        var result = await client.GetGenresAsync(null, CancellationToken.None);
        
        // Assert
     result.Should().NotBeNull();
 result.Genres.Should().BeEmpty();
    }

    #endregion

    #region Additional HTTP Error Scenarios

 /// <summary>
  /// ERROR TEST: 503 Service Unavailable.
    /// GOAL: TMDB maintenance handled gracefully.
    /// IMPORTANCE: TMDB occasionally goes down for maintenance.
    /// </summary>
    [Fact]
  public async Task DiscoverAsync_ServiceUnavailable_ReturnsEmptyResponse()
    {
      // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
   
        mockHandler.Protected()
  .Setup<Task<HttpResponseMessage>>(
    "SendAsync",
   ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync(new HttpResponseMessage
        {
      StatusCode = HttpStatusCode.ServiceUnavailable,
    Content = new StringContent("{\"status_message\":\"Service temporarily unavailable\"}")
     });
  
     // Act
        var result = await client.DiscoverAsync(
  Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
 
        // Assert
   result.Should().NotBeNull();
        result.Results.Should().BeEmpty();
    }

    /// <summary>
 /// ERROR TEST: 403 Forbidden (API key suspended).
    /// GOAL: Account issues handled gracefully.
    /// IMPORTANCE: API key can be suspended for violations.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_Forbidden_ReturnsEmptyResponse()
    {
     // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
  mockHandler.Protected()
       .Setup<Task<HttpResponseMessage>>(
    "SendAsync",
           ItExpr.IsAny<HttpRequestMessage>(),
    ItExpr.IsAny<CancellationToken>())
       .ReturnsAsync(new HttpResponseMessage
          {
            StatusCode = HttpStatusCode.Forbidden,
 Content = new StringContent("{\"status_message\":\"API key suspended\"}")
            });
  
        // Act
        var result = await client.DiscoverAsync(
  Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
        
        // Assert
     result.Should().NotBeNull();
  result.Results.Should().BeEmpty();
    }

    /// <summary>
    /// ERROR TEST: Very large response body (memory handling).
    /// GOAL: Large responses don't cause OOM.
    /// IMPORTANCE: Some discover queries return many results.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_VeryLargeResponse_HandlesGracefully()
    {
      // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
        // Create a large but valid JSON response (1000 movies)
   var largeMovies = Enumerable.Range(1, 1000)
       .Select(i => $"{{\"id\":{i},\"title\":\"Movie {i}\",\"genre_ids\":[28],\"overview\":\"Overview\",\"release_date\":\"2024-01-01\",\"vote_average\":7.5}}")
            .ToList();
   var largeJson = $"{{\"results\":[{string.Join(",", largeMovies)}]}}";
     
      mockHandler.Protected()
      .Setup<Task<HttpResponseMessage>>(
    "SendAsync",
   ItExpr.IsAny<HttpRequestMessage>(),
          ItExpr.IsAny<CancellationToken>())
  .ReturnsAsync(new HttpResponseMessage
       {
          StatusCode = HttpStatusCode.OK,
         Content = new StringContent(largeJson)
 });
        
   // Act
    var result = await client.DiscoverAsync(
     Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
        
   // Assert - Should handle large response
  result.Should().NotBeNull();
   result.Results.Should().NotBeEmpty();
    }

    /// <summary>
    /// ERROR TEST: Response with missing required fields.
    /// GOAL: Partial data doesn't crash deserialization.
    /// IMPORTANCE: TMDB data quality varies.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_MissingRequiredFields_HandlesGracefully()
 {
        // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
   // Missing 'title' field
      var invalidJson = "{\"results\":[{\"id\":1,\"genre_ids\":[28]}]}";
   
   mockHandler.Protected()
    .Setup<Task<HttpResponseMessage>>(
   "SendAsync",
       ItExpr.IsAny<HttpRequestMessage>(),
         ItExpr.IsAny<CancellationToken>())
     .ReturnsAsync(new HttpResponseMessage
            {
    StatusCode = HttpStatusCode.OK,
         Content = new StringContent(invalidJson)
     });
 
 // Act
        var result = await client.DiscoverAsync(
   Array.Empty<int>(), null, null, 1, null, null, CancellationToken.None);
        
   // Assert - Should not crash
   result.Should().NotBeNull();
    }

    #endregion

    #region GetGenres Additional Tests

    /// <summary>
    /// ERROR TEST: GetGenres with network timeout.
    /// GOAL: Genre endpoint handles timeout.
    /// IMPORTANCE: Consistency across endpoints.
 /// </summary>
    [Fact]
    public async Task GetGenresAsync_Timeout_ThrowsTaskCanceledException()
    {
        // Arrange
var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
        
   mockHandler.Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
    ItExpr.IsAny<HttpRequestMessage>(),
    ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout"));
        
 // Act
 Func<Task> act = async () => await client.GetGenresAsync(null, CancellationToken.None);
  
        // Assert
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    /// <summary>
    /// ERROR TEST: GetGenres with malformed JSON.
    /// GOAL: Invalid genre data handled.
    /// IMPORTANCE: Data validation.
    /// </summary>
    [Fact]
    public async Task GetGenresAsync_MalformedJson_ThrowsJsonException()
    {
   // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
  
   mockHandler.Protected()
       .Setup<Task<HttpResponseMessage>>(
     "SendAsync",
   ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
   .ReturnsAsync(new HttpResponseMessage
       {
         StatusCode = HttpStatusCode.OK,
Content = new StringContent("{genres: [invalid]}")
  });
        
      // Act
        Func<Task> act = async () => await client.GetGenresAsync(null, CancellationToken.None);
        
   // Assert
 await act.Should().ThrowAsync<JsonException>();
    }

 /// <summary>
    /// ERROR TEST: GetGenres returns empty genres list.
    /// GOAL: Empty list handled gracefully.
    /// IMPORTANCE: Edge case scenario.
    /// </summary>
    [Fact]
    public async Task GetGenresAsync_EmptyGenresList_ReturnsEmptyModel()
    {
  // Arrange
        var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
   
  mockHandler.Protected()
 .Setup<Task<HttpResponseMessage>>(
 "SendAsync",
         ItExpr.IsAny<HttpRequestMessage>(),
      ItExpr.IsAny<CancellationToken>())
 .ReturnsAsync(new HttpResponseMessage
    {
         StatusCode = HttpStatusCode.OK,
 Content = new StringContent("{\"genres\":[]}")
     });
     
      // Act
   var result = await client.GetGenresAsync(null, CancellationToken.None);
        
// Assert
  result.Should().NotBeNull();
   result.Genres.Should().BeEmpty();
    }

    #endregion

    #region DiscoverTop Additional Tests

    /// <summary>
    /// ERROR TEST: DiscoverTop with all error scenarios.
    /// GOAL: Popular movies endpoint has same error handling.
    /// IMPORTANCE: Consistency across discover methods.
    /// </summary>
    [Fact]
    public async Task DiscoverTopAsync_ServerError_ReturnsEmptyResponse()
    {
 // Arrange
     var (client, mockHandler) = CreateClientWithMockHandler("test-api-key");
   
     mockHandler.Protected()
       .Setup<Task<HttpResponseMessage>>(
    "SendAsync",
    ItExpr.IsAny<HttpRequestMessage>(),
       ItExpr.IsAny<CancellationToken>())
 .ReturnsAsync(new HttpResponseMessage
     {
 StatusCode = HttpStatusCode.InternalServerError
     });
     
        // Act
        var result = await client.DiscoverTopAsync(1, null, null, CancellationToken.None);
        
  // Assert
    result.Should().NotBeNull();
        result.Results.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private (TmdbClient client, Mock<HttpMessageHandler> mockHandler) CreateClientWithMockHandler(string apiKey)
    {
 var options = Microsoft.Extensions.Options.Options.Create(new TmdbOptions
        {
            BaseUrl = "https://api.themoviedb.org/3/",
            ApiKey = apiKey
        });
     
    var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object)
        {
BaseAddress = new Uri(options.Value.BaseUrl),
      Timeout = TimeSpan.FromSeconds(8)
        };
        
        var client = new TmdbClient(httpClient, options, NullLogger<TmdbClient>.Instance);
 
        return (client, mockHandler);
    }

    #endregion
}
