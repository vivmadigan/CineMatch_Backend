using System.Net;
using FluentAssertions;
using Infrastructure.External;
using Infrastructure.Options;
using Infrastructure.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Infrastructure.Tests.External;

/// <summary>
/// Unit tests for TmdbClient.
/// Tests query building, error handling, and response parsing with mocked HTTP.
/// </summary>
public class TmdbClientTests
{
    private static ILogger<TmdbClient> CreateMockLogger()
    {
      return new Mock<ILogger<TmdbClient>>().Object;
    }

  [Fact]
    public async Task DiscoverAsync_WithMissingApiKey_ThrowsInvalidOperationException()
    {
   // Arrange
     var mockHandler = new MockTmdbHttpHandler();
     mockHandler.SetupFailure("/discover/movie", HttpStatusCode.Unauthorized);

  var httpClient = new HttpClient(mockHandler)
  {
      BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

var options = Microsoft.Extensions.Options.Options.Create(new TmdbOptions
     {
     BaseUrl = "https://api.themoviedb.org/3",
  ApiKey = "", // Missing API key
   ImageBase = "https://image.tmdb.org/t/p/",
   DefaultLanguage = "en-US",
          DefaultRegion = "US"
  });

var logger = CreateMockLogger();
        var client = new TmdbClient(httpClient, options, logger);

        // Act & Assert
   await Assert.ThrowsAsync<InvalidOperationException>(() =>
      client.DiscoverAsync([], null, null, 1, null, null, CancellationToken.None));
    }

 [Fact]
    public async Task DiscoverAsync_WithGenresAndRuntime_BuildsCorrectQuery()
    {
     // Arrange
   var mockHandler = new MockTmdbHttpHandler();
    mockHandler.SetupResponse("/discover/movie", new TmdbDiscoverResponse
  {
            Page = 1,
 Results =
     [
      new TmdbMovie
        {
    Id = 27205,
  Title = "Inception",
      Overview = "A thief who enters dreams...",
     GenreIds = [28, 878],
           PosterPath = "/poster.jpg",
    BackdropPath = "/backdrop.jpg",
          ReleaseDate = "2010-07-16",
  VoteAverage = 8.4
     }
       ],
    TotalPages = 1,
   TotalResults = 1
        });

        var httpClient = new HttpClient(mockHandler)
        {
         BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

  var options = Microsoft.Extensions.Options.Options.Create(new TmdbOptions
     {
      BaseUrl = "https://api.themoviedb.org/3",
       ApiKey = "test-api-key",
   ImageBase = "https://image.tmdb.org/t/p/",
        DefaultLanguage = "en-US",
  DefaultRegion = "US"
        });

 var logger = CreateMockLogger();
        var client = new TmdbClient(httpClient, options, logger);

    // Act
        var result = await client.DiscoverAsync(
         [28, 35],
 100,
      140,
    1,
 "en-US",
      "US",
 CancellationToken.None);

  // Assert
        result.Should().NotBeNull();
        result.Results.Should().ContainSingle();
    result.Results.First().Title.Should().Be("Inception");

  // Verify request was made
    mockHandler.Requests.Should().ContainSingle();
        var request = mockHandler.Requests.First();
      request.RequestUri.Should().NotBeNull();
        
   var query = request.RequestUri!.Query;
      query.Should().Contain("with_genres=28,35");
query.Should().Contain("with_runtime.gte=100");
 query.Should().Contain("with_runtime.lte=140");
    }

    [Fact]
    public async Task GetGenresAsync_ReturnsGenres()
    {
   // Arrange
   var mockHandler = new MockTmdbHttpHandler();
    mockHandler.SetupResponse("/genre/movie/list", new TmdbGenreResponse
   {
         Genres =
       [
new TmdbGenre { Id = 28, Name = "Action" },
    new TmdbGenre { Id = 35, Name = "Comedy" },
    new TmdbGenre { Id = 18, Name = "Drama" }
            ]
   });

     var httpClient = new HttpClient(mockHandler)
  {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

 var options = Microsoft.Extensions.Options.Options.Create(new TmdbOptions
 {
   BaseUrl = "https://api.themoviedb.org/3",
 ApiKey = "test-api-key",
     ImageBase = "https://image.tmdb.org/t/p/",
     DefaultLanguage = "en-US",
    DefaultRegion = "US"
  });

  var logger = CreateMockLogger();
        var client = new TmdbClient(httpClient, options, logger);

   // Act
        var result = await client.GetGenresAsync("en-US", CancellationToken.None);

   // Assert
    result.Should().NotBeNull();
        result.Genres.Should().HaveCount(3);
        result.Genres.Should().Contain(g => g.Name == "Action" && g.Id == 28);
 }
}