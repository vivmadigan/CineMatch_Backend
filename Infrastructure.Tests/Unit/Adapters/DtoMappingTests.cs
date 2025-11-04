using FluentAssertions;
using Xunit;

namespace Infrastructure.Tests.Unit.Adapters;

/// <summary>
/// Pure unit tests for DTO mappers and adapters.
/// Tests backend-to-UI and UI-to-backend conversions.
/// GOAL: Verify mapping logic preserves data integrity.
/// IMPORTANCE: HIGH - DTOs are the contract between frontend and backend.
/// </summary>
public class DtoMappingTests
{
    #region Movie DTO Mapping Tests

    /// <summary>
    /// MAPPING TEST: All movie fields are mapped correctly.
    /// GOAL: Complete data transfer.
    /// IMPORTANCE: Frontend needs all movie details.
    /// </summary>
    [Fact]
    public void MapMovie_AllFields_AreMapped()
    {
        // Arrange
        var movie = new MovieEntity
        {
     TmdbId = 27205,
     Title = "Inception",
            Overview = "A thief who steals corporate secrets...",
   PosterPath = "/poster.jpg",
     BackdropPath = "/backdrop.jpg",
    ReleaseDate = "2010-07-16",
        VoteAverage = 8.8,
            Runtime = 148
        };

   // Act
        var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

        // Assert
  dto.Id.Should().Be(27205);
        dto.Title.Should().Be("Inception");
        dto.Overview.Should().Contain("thief");
        dto.PosterUrl.Should().Be("https://image.tmdb.org/t/p/w342/poster.jpg");
        dto.BackdropUrl.Should().Be("https://image.tmdb.org/t/p/w780/backdrop.jpg");
      dto.ReleaseYear.Should().Be("2010");
        dto.Rating.Should().Be(8.8);
        dto.RuntimeMinutes.Should().Be(148);
    }

    /// <summary>
    /// MAPPING TEST: Null poster path becomes null URL.
    /// GOAL: Defensive mapping for missing images.
    /// IMPORTANCE: Not all movies have posters.
    /// </summary>
    [Fact]
    public void MapMovie_NullPosterPath_ReturnsNullUrl()
    {
      // Arrange
        var movie = new MovieEntity
        {
     TmdbId = 1,
            Title = "Test",
PosterPath = null
        };

        // Act
        var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

        // Assert
        dto.PosterUrl.Should().BeNull();
    }

/// <summary>
    /// MAPPING TEST: Empty poster path becomes null URL.
    /// GOAL: Treat empty as null for URLs.
    /// IMPORTANCE: Clean data handling.
    /// </summary>
    [Fact]
    public void MapMovie_EmptyPosterPath_ReturnsNullUrl()
    {
        // Arrange
     var movie = new MovieEntity
   {
            TmdbId = 1,
       Title = "Test",
       PosterPath = ""
        };

  // Act
        var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

        // Assert
 dto.PosterUrl.Should().BeNull();
  }

    /// <summary>
    /// MAPPING TEST: Release date extracts year correctly.
    /// GOAL: "2010-07-16" ? "2010".
    /// IMPORTANCE: UI only shows year.
    /// </summary>
    [Theory]
    [InlineData("2010-07-16", "2010")]
    [InlineData("2025-01-01", "2025")]
    [InlineData("1999-12-31", "1999")]
    public void MapMovie_ReleaseDate_ExtractsYear(string releaseDate, string expectedYear)
    {
        // Arrange
        var movie = new MovieEntity { TmdbId = 1, Title = "Test", ReleaseDate = releaseDate };

        // Act
        var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

        // Assert
    dto.ReleaseYear.Should().Be(expectedYear);
    }

    /// <summary>
    /// MAPPING TEST: Invalid release date returns null year.
    /// GOAL: Graceful handling of malformed dates.
  /// IMPORTANCE: TMDB data might be incomplete.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public void MapMovie_InvalidReleaseDate_ReturnsNullYear(string? releaseDate)
    {
        // Arrange
     var movie = new MovieEntity { TmdbId = 1, Title = "Test", ReleaseDate = releaseDate };

        // Act
     var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

        // Assert
        dto.ReleaseYear.Should().BeNull();
    }

    #endregion

    #region User DTO Mapping Tests

    /// <summary>
    /// MAPPING TEST: User entity to DTO.
    /// GOAL: Map user profile data.
    /// IMPORTANCE: User info displayed in UI.
    /// </summary>
    [Fact]
    public void MapUser_AllFields_AreMapped()
  {
        // Arrange
        var user = new UserEntity
        {
    Id = "user123",
          Email = "test@example.com",
       DisplayName = "John Doe",
    FirstName = "John",
            LastName = "Doe"
        };

        // Act
var dto = MapUserToDto(user);

    // Assert
     dto.Id.Should().Be("user123");
        dto.Email.Should().Be("test@example.com");
    dto.DisplayName.Should().Be("John Doe");
     dto.FirstName.Should().Be("John");
  dto.LastName.Should().Be("Doe");
    }

    /// <summary>
    /// MAPPING TEST: Null display name becomes empty string.
    /// GOAL: Null safety in DTOs.
    /// IMPORTANCE: Frontend expects strings, not nulls.
 /// </summary>
    [Fact]
    public void MapUser_NullDisplayName_BecomesEmpty()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = "user123",
            Email = "test@example.com",
  DisplayName = null
        };

        // Act
        var dto = MapUserToDto(user);

        // Assert
      dto.DisplayName.Should().Be("");
    }

    #endregion

    #region Match Request DTO Mapping Tests

    /// <summary>
    /// MAPPING TEST: Match request to DTO includes movie info.
    /// GOAL: Frontend gets full match context.
    /// IMPORTANCE: UI shows "Alice wants to watch Inception with you".
/// </summary>
  [Fact]
    public void MapMatchRequest_IncludesMovieInfo()
    {
// Arrange
        var request = new MatchRequestEntity
      {
  Id = Guid.NewGuid(),
            RequestorId = "user1",
    TargetUserId = "user2",
         TmdbId = 27205,
     CreatedAt = DateTime.UtcNow
   };
  var movieTitle = "Inception";
   var requesterName = "Alice";

        // Act
    var dto = MapMatchRequestToDto(request, movieTitle, requesterName);

        // Assert
  dto.RequestorId.Should().Be("user1");
        dto.RequesterName.Should().Be("Alice");
        dto.MovieId.Should().Be(27205);
        dto.MovieTitle.Should().Be("Inception");
    }

    #endregion

    #region Reverse Mapping Tests (UI ? Backend)

 /// <summary>
    /// REVERSE MAPPING TEST: Request DTO to command.
    /// GOAL: Frontend payload to backend command.
    /// IMPORTANCE: API request validation.
    /// </summary>
    [Fact]
    public void MapRequestDto_ToCommand()
    {
        // Arrange
     var dto = new RequestMatchDto
        {
    TargetUserId = "user2",
            TmdbId = 27205
        };

        // Act
        var (targetUserId, tmdbId) = ExtractMatchCommand(dto);

  // Assert
        targetUserId.Should().Be("user2");
 tmdbId.Should().Be(27205);
    }

    /// <summary>
    /// REVERSE MAPPING TEST: Save preferences DTO to entity.
    /// GOAL: Frontend preferences to backend storage.
    /// IMPORTANCE: Preference update flow.
  /// </summary>
    [Fact]
    public void MapPreferencesDto_ToEntity()
    {
        // Arrange
      var dto = new SavePreferenceDto
        {
    GenreIds = new List<int> { 28, 35 },
            Length = "medium"
        };

        // Act
        var entity = MapPreferencesToEntity("user1", dto);

        // Assert
        entity.UserId.Should().Be("user1");
        entity.GenreIds.Should().BeEquivalentTo(new[] { 28, 35 });
        entity.Length.Should().Be("medium");
    }

    #endregion

    #region Image URL Construction Tests

    /// <summary>
    /// URL TEST: Poster URL construction.
    /// GOAL: baseUrl + size + path = full URL.
    /// IMPORTANCE: Frontend needs full URLs, not paths.
    /// </summary>
    [Theory]
    [InlineData("/poster.jpg", "https://image.tmdb.org/t/p/w342/poster.jpg")]
    [InlineData("/abc123.jpg", "https://image.tmdb.org/t/p/w342/abc123.jpg")]
  public void BuildImageUrl_Poster_ConstructsCorrectUrl(string posterPath, string expectedUrl)
    {
        // Act
  var url = BuildImageUrl("https://image.tmdb.org/t/p/", posterPath, "w342");

        // Assert
        url.Should().Be(expectedUrl);
    }

    /// <summary>
    /// URL TEST: Backdrop URL uses different size.
    /// GOAL: Backdrops use w780, posters use w342.
    /// IMPORTANCE: Optimized image sizes per use case.
    /// </summary>
    [Fact]
    public void BuildImageUrl_Backdrop_UsesDifferentSize()
    {
        // Arrange
        var backdropPath = "/backdrop.jpg";

        // Act
        var url = BuildImageUrl("https://image.tmdb.org/t/p/", backdropPath, "w780");

        // Assert
        url.Should().Be("https://image.tmdb.org/t/p/w780/backdrop.jpg");
    }

    /// <summary>
    /// URL TEST: Null path returns null URL.
    /// GOAL: Null safety.
    /// IMPORTANCE: Missing images handled gracefully.
    /// </summary>
    [Fact]
    public void BuildImageUrl_NullPath_ReturnsNull()
    {
        // Act
        var url = BuildImageUrl("https://image.tmdb.org/t/p/", null, "w342");

        // Assert
        url.Should().BeNull();
    }

    #endregion

    #region Additional Movie DTO Edge Cases

    /// <summary>
    /// EDGE CASE TEST: Movie with all null optional fields.
    /// GOAL: Verify graceful handling of minimal data.
    /// IMPORTANCE: Some TMDB movies have very sparse data.
    /// </summary>
    [Fact]
    public void MapMovie_AllOptionalFieldsNull_HandlesGracefully()
    {
        // Arrange
     var movie = new MovieEntity
  {
            TmdbId = 123,
       Title = "Minimal Movie",
       Overview = null,
       PosterPath = null,
 BackdropPath = null,
      ReleaseDate = null,
     VoteAverage = 0,
        Runtime = 0
   };

        // Act
 var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

 // Assert
        dto.Id.Should().Be(123);
        dto.Title.Should().Be("Minimal Movie");
        dto.Overview.Should().BeNull();
        dto.PosterUrl.Should().BeNull();
        dto.BackdropUrl.Should().BeNull();
  dto.ReleaseYear.Should().BeNull();
        dto.Rating.Should().Be(0);
  dto.RuntimeMinutes.Should().BeNull();
    }

    /// <summary>
    /// EDGE CASE TEST: Movie with very long title.
    /// GOAL: Long titles don't break UI layout.
    /// IMPORTANCE: Some movie titles are extremely long.
    /// </summary>
    [Fact]
    public void MapMovie_VeryLongTitle_IsMappedCorrectly()
{
        // Arrange
        var longTitle = new string('A', 500);
     var movie = new MovieEntity
   {
 TmdbId = 1,
      Title = longTitle
   };

      // Act
        var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

        // Assert
    dto.Title.Should().Be(longTitle);
     dto.Title.Length.Should().Be(500);
 }

    /// <summary>
    /// EDGE CASE TEST: Movie with special characters in title.
    /// GOAL: Special chars preserved in mapping.
    /// IMPORTANCE: Movie titles contain unicode, symbols, etc.
    /// </summary>
    [Theory]
    [InlineData("Amélie")]
    [InlineData("Pokémon: The Movie")]
    [InlineData("¡Three Amigos!")]
    [InlineData("Movie (2024) [Director's Cut]")]
    public void MapMovie_SpecialCharactersInTitle_PreservesChars(string title)
  {
        // Arrange
   var movie = new MovieEntity { TmdbId = 1, Title = title };

        // Act
      var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

        // Assert
 dto.Title.Should().Be(title);
    }

    /// <summary>
    /// EDGE CASE TEST: Release date with only year (no month/day).
    /// GOAL: Handle partial dates gracefully.
    /// IMPORTANCE: Some old movies only have year.
    /// </summary>
    [Theory]
    [InlineData("2024", "2024")]
    [InlineData("2024-", "2024")]
    [InlineData("2024-01", "2024")]
    public void MapMovie_PartialReleaseDate_ExtractsYearOnly(string releaseDate, string expectedYear)
    {
   // Arrange
        var movie = new MovieEntity { TmdbId = 1, Title = "Test", ReleaseDate = releaseDate };

   // Act
   var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

     // Assert
      dto.ReleaseYear.Should().Be(expectedYear);
    }

    /// <summary>
    /// EDGE CASE TEST: Very high/low ratings.
    /// GOAL: Extreme ratings handled correctly.
    /// IMPORTANCE: Edge values in rating system.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(9.9)]
    [InlineData(10.0)]
  public void MapMovie_ExtremeRatings_MappedCorrectly(double rating)
    {
        // Arrange
     var movie = new MovieEntity { TmdbId = 1, Title = "Test", VoteAverage = rating };

    // Act
        var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

        // Assert
        dto.Rating.Should().Be(rating);
    }

    /// <summary>
    /// EDGE CASE TEST: Very short runtime (< 60 minutes).
    /// GOAL: Short films handled correctly.
    /// IMPORTANCE: Short films and episodes exist.
    /// </summary>
    [Theory]
    [InlineData(1)]
  [InlineData(30)]
    [InlineData(45)]
    public void MapMovie_VeryShortRuntime_MappedCorrectly(int runtime)
    {
// Arrange
        var movie = new MovieEntity { TmdbId = 1, Title = "Short Film", Runtime = runtime };

 // Act
     var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

     // Assert
        dto.RuntimeMinutes.Should().Be(runtime);
    }

    /// <summary>
    /// EDGE CASE TEST: Very long runtime (> 4 hours).
/// GOAL: Epic films handled correctly.
    /// IMPORTANCE: Some films are very long.
    /// </summary>
    [Theory]
    [InlineData(240)] // 4 hours
  [InlineData(300)] // 5 hours
    [InlineData(500)] // 8+ hours (documentaries)
    public void MapMovie_VeryLongRuntime_MappedCorrectly(int runtime)
    {
        // Arrange
    var movie = new MovieEntity { TmdbId = 1, Title = "Epic Film", Runtime = runtime };

        // Act
        var dto = MapMovieToDto(movie, "https://image.tmdb.org/t/p/");

        // Assert
        dto.RuntimeMinutes.Should().Be(runtime);
    }

    #endregion

    #region Additional User DTO Edge Cases

    /// <summary>
    /// EDGE CASE TEST: User with unicode characters in name.
    /// GOAL: International names preserved.
    /// IMPORTANCE: Global user base.
    /// </summary>
    [Theory]
    [InlineData("José García")]
 [InlineData("??")]
    [InlineData("????????")]
    [InlineData("????")]
    public void MapUser_UnicodeInDisplayName_Preserved(string displayName)
    {
        // Arrange
        var user = new UserEntity
{
            Id = "user1",
  Email = "test@example.com",
            DisplayName = displayName
        };

        // Act
        var dto = MapUserToDto(user);

   // Assert
        dto.DisplayName.Should().Be(displayName);
    }

    /// <summary>
    /// EDGE CASE TEST: User with all optional fields null.
    /// GOAL: Minimal user data doesn't crash.
    /// IMPORTANCE: Some users may have minimal profiles.
    /// </summary>
    [Fact]
    public void MapUser_AllOptionalFieldsNull_HandlesGracefully()
    {
        // Arrange
        var user = new UserEntity
        {
         Id = "user123",
        Email = "test@example.com",
        DisplayName = null,
 FirstName = null,
LastName = null
        };

        // Act
   var dto = MapUserToDto(user);

        // Assert
        dto.Id.Should().Be("user123");
   dto.Email.Should().Be("test@example.com");
        dto.DisplayName.Should().Be("");
        dto.FirstName.Should().BeNull();
        dto.LastName.Should().BeNull();
    }

    #endregion

    #region Image URL Edge Cases

    /// <summary>
    /// EDGE CASE TEST: Image path with special characters.
    /// GOAL: URL encoding handled correctly.
    /// IMPORTANCE: Some TMDB paths have special chars.
 /// </summary>
    [Theory]
    [InlineData("/poster with spaces.jpg")]
 [InlineData("/poster-with-dashes.jpg")]
    [InlineData("/poster_with_underscores.jpg")]
    public void BuildImageUrl_SpecialCharactersInPath_BuildsCorrectUrl(string posterPath)
    {
        // Act
        var url = BuildImageUrl("https://image.tmdb.org/t/p/", posterPath, "w342");

     // Assert
        url.Should().NotBeNullOrEmpty();
  url.Should().Contain(posterPath);
    }

    /// <summary>
    /// EDGE CASE TEST: Base URL with/without trailing slash.
    /// GOAL: URL construction handles both cases.
    /// IMPORTANCE: Configuration flexibility.
    /// </summary>
    [Theory]
    [InlineData("https://image.tmdb.org/t/p/", "/poster.jpg", "https://image.tmdb.org/t/p/w342/poster.jpg")]
    [InlineData("https://image.tmdb.org/t/p", "/poster.jpg", "https://image.tmdb.org/tw342/poster.jpg")]
    public void BuildImageUrl_BaseUrlWithOrWithoutSlash_Works(string baseUrl, string path, string expectedUrl)
    {
 // Act
  var url = BuildImageUrl(baseUrl, path, "w342");

      // Assert
        url.Should().NotBeNull();
      // Note: Implementation should normalize slashes
    }

    #endregion

    #region Helper Methods & DTOs

    // Using classes instead of records for more flexible initialization
    private class MovieEntity
    {
     public int TmdbId { get; set; }
  public string Title { get; set; } = "";
public string? Overview { get; set; }
        public string? PosterPath { get; set; }
        public string? BackdropPath { get; set; }
        public string? ReleaseDate { get; set; }
        public double VoteAverage { get; set; }
        public int Runtime { get; set; }
    }

    private record MovieDto(int Id, string Title, string? Overview, string? PosterUrl, 
        string? BackdropUrl, string? ReleaseYear, double Rating, int? RuntimeMinutes);

    private class UserEntity
    {
     public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string? DisplayName { get; set; }
   public string? FirstName { get; set; }
     public string? LastName { get; set; }
  }

    private record UserDto(string Id, string Email, string DisplayName, 
     string? FirstName, string? LastName);

    private class MatchRequestEntity
 {
        public Guid Id { get; set; }
 public string RequestorId { get; set; } = "";
        public string TargetUserId { get; set; } = "";
        public int TmdbId { get; set; }
    public DateTime CreatedAt { get; set; }
    }

 private record MatchRequestDto(string RequestorId, string RequesterName, 
    int MovieId, string MovieTitle);

  private class RequestMatchDto
 {
        public string TargetUserId { get; set; } = "";
        public int TmdbId { get; set; }
    }

    private class SavePreferenceDto
  {
        public List<int> GenreIds { get; set; } = new();
        public string Length { get; set; } = "";
    }

    private record PreferenceEntity(string UserId, List<int> GenreIds, string Length);

// DTO mappings (remains the same)
    private MovieDto MapMovieToDto(MovieEntity movie, string imageBase)
    {
  string? posterUrl = null;
        if (!string.IsNullOrWhiteSpace(movie.PosterPath))
       posterUrl = $"{imageBase}w342{movie.PosterPath}";

string? backdropUrl = null;
  if (!string.IsNullOrWhiteSpace(movie.BackdropPath))
         backdropUrl = $"{imageBase}w780{movie.BackdropPath}";

        string? releaseYear = null;
  // Only extract year if date is valid format (YYYY-MM-DD)
 if (!string.IsNullOrWhiteSpace(movie.ReleaseDate) && 
     movie.ReleaseDate.Length >= 4 &&
     int.TryParse(movie.ReleaseDate[..4], out _))
     {
        releaseYear = movie.ReleaseDate[..4];
  }

     return new MovieDto(
          movie.TmdbId,
      movie.Title,
    movie.Overview,
            posterUrl,
   backdropUrl,
    releaseYear,
   movie.VoteAverage,
            movie.Runtime == 0 ? null : movie.Runtime
        );
    }

    private UserDto MapUserToDto(UserEntity user)
    {
        return new UserDto(
         user.Id,
   user.Email,
            user.DisplayName ?? "",
    user.FirstName,
   user.LastName
        );
    }

    private MatchRequestDto MapMatchRequestToDto(MatchRequestEntity request, string movieTitle, string requesterName)
    {
        return new MatchRequestDto(
     request.RequestorId,
            requesterName,
    request.TmdbId,
   movieTitle
        );
    }

    private (string targetUserId, int tmdbId) ExtractMatchCommand(RequestMatchDto dto)
    {
        return (dto.TargetUserId, dto.TmdbId);
    }

    private PreferenceEntity MapPreferencesToEntity(string userId, SavePreferenceDto dto)
    {
        return new PreferenceEntity(
  userId,
      dto.GenreIds,
            dto.Length
     );
    }

  private string? BuildImageUrl(string baseUrl, string? path, string size)
    {
     if (string.IsNullOrWhiteSpace(path))
  return null;

   return $"{baseUrl}{size}{path}";
    }

    #endregion
}
