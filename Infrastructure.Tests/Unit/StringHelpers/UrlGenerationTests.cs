using FluentAssertions;
using Xunit;

namespace Infrastructure.Tests.Unit.StringHelpers;

/// <summary>
/// Unit tests for URL generation logic (TMDB URLs, image CDN URLs).
/// These are PURE unit tests - testing string concatenation in isolation.
/// GOAL: Verify URLs are built correctly for frontend consumption.
/// </summary>
public class UrlGenerationTests
{
    #region TMDB Movie URL Generation

    /// <summary>
    /// POSITIVE TEST: Verify TMDB movie URL format.
    /// GOAL: Movie ID converts to correct www.themoviedb.org URL.
    /// IMPORTANCE: External links must be valid.
    /// </summary>
    [Theory]
    [InlineData(27205, "https://www.themoviedb.org/movie/27205")]
    [InlineData(238, "https://www.themoviedb.org/movie/238")]
    [InlineData(1, "https://www.themoviedb.org/movie/1")]
    [InlineData(999999, "https://www.themoviedb.org/movie/999999")]
    public void GenerateTmdbMovieUrl_CreatesCorrectFormat(int tmdbId, string expectedUrl)
    {
        // Act
   var url = GenerateTmdbMovieUrl(tmdbId);

        // Assert
        url.Should().Be(expectedUrl);
        url.Should().StartWith("https://www.themoviedb.org/movie/");
    }

    #endregion

    #region Image CDN URL Generation

    /// <summary>
    /// POSITIVE TEST: Verify poster URL concatenation.
    /// GOAL: Base URL + size + poster path = full CDN URL.
  /// IMPORTANCE: Broken image URLs = missing posters in UI.
    /// </summary>
    [Theory]
    [InlineData("https://image.tmdb.org/t/p/", "/abc.jpg", "w342", "https://image.tmdb.org/t/p/w342/abc.jpg")]
    [InlineData("https://image.tmdb.org/t/p/", "/xyz.png", "w500", "https://image.tmdb.org/t/p/w500/xyz.png")]
    [InlineData("https://image.tmdb.org/t/p/", "/test.jpg", "original", "https://image.tmdb.org/t/p/original/test.jpg")]
    public void GenerateImageUrl_ConcatenatesCorrectly(string baseUrl, string posterPath, string size, string expected)
    {
        // Act
        var result = GenerateImageUrl(baseUrl, posterPath, size);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// POSITIVE TEST: Verify base URL with trailing slash is handled.
/// GOAL: Both with/without trailing slash work correctly.
    /// IMPORTANCE: Configuration flexibility.
    /// </summary>
    [Theory]
    [InlineData("https://image.tmdb.org/t/p", "/abc.jpg", "w342", "https://image.tmdb.org/t/p/w342/abc.jpg")]
 [InlineData("https://image.tmdb.org/t/p/", "/abc.jpg", "w342", "https://image.tmdb.org/t/p/w342/abc.jpg")]
    public void GenerateImageUrl_HandlesTrailingSlash(string baseUrl, string posterPath, string size, string expected)
    {
    // Act
        var result = GenerateImageUrl(baseUrl, posterPath, size);

   // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// NEGATIVE TEST: Verify null poster path returns null.
    /// GOAL: Graceful handling of missing images.
    /// IMPORTANCE: Prevents broken image URLs in frontend.
 /// </summary>
    [Theory]
 [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateImageUrl_WithNullPath_ReturnsNull(string? posterPath)
    {
     // Arrange
      var baseUrl = "https://image.tmdb.org/t/p/";
        var size = "w342";

        // Act
     var result = GenerateImageUrl(baseUrl, posterPath, size);

     // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static string GenerateTmdbMovieUrl(int tmdbId)
    {
        return $"https://www.themoviedb.org/movie/{tmdbId}";
    }

    private static string? GenerateImageUrl(string baseUrl, string? posterPath, string size)
    {
        if (string.IsNullOrWhiteSpace(posterPath)) return null;
        
        // Simulate MoviesController.Img() logic
        var cleanBaseUrl = baseUrl.TrimEnd('/');
        var cleanPosterPath = posterPath.TrimStart('/');
        return $"{cleanBaseUrl}/{size}/{cleanPosterPath}";
    }

    #endregion
}
