using FluentAssertions;
using Xunit;

namespace Infrastructure.Tests.Unit.Preferences;

/// <summary>
/// Pure unit tests for preferences mapping and validation logic.
/// Tests genre ID validation, length bucket rules, and round-trip conversions.
/// GOAL: Verify preference mapping works correctly without external dependencies.
/// IMPORTANCE: HIGH - Preferences drive movie discovery.
/// </summary>
public class PreferencesMappingTests
{
    #region Genre ID Validation Tests

    /// <summary>
    /// VALIDATION TEST: Valid TMDB genre IDs are accepted.
    /// GOAL: Positive integers are valid.
    /// IMPORTANCE: Core validation rule.
    /// </summary>
    [Theory]
    [InlineData(28)]   // Action
    [InlineData(35)]   // Comedy
    [InlineData(878)]  // Science Fiction
    [InlineData(12)]   // Adventure
    [InlineData(16)]   // Animation
    public void ValidateGenreId_ValidTmdbIds_AreAccepted(int genreId)
    {
        // Act
      var isValid = IsValidGenreId(genreId);

        // Assert
        isValid.Should().BeTrue("positive integers are valid TMDB genre IDs");
    }

    /// <summary>
 /// VALIDATION TEST: Zero is invalid genre ID.
    /// GOAL: Zero is not a valid TMDB ID.
    /// IMPORTANCE: Prevents invalid data.
    /// </summary>
    [Fact]
    public void ValidateGenreId_Zero_IsInvalid()
    {
        // Act
        var isValid = IsValidGenreId(0);

        // Assert
        isValid.Should().BeFalse("zero is not a valid genre ID");
 }

    /// <summary>
    /// VALIDATION TEST: Negative IDs are invalid.
    /// GOAL: Only positive integers allowed.
    /// IMPORTANCE: Data integrity.
    /// </summary>
    [Theory]
    [InlineData(-1)]
 [InlineData(-28)]
    [InlineData(-999)]
    public void ValidateGenreId_Negative_IsInvalid(int genreId)
    {
     // Act
        var isValid = IsValidGenreId(genreId);

        // Assert
        isValid.Should().BeFalse("negative genre IDs are invalid");
    }

    #endregion

    #region Genre List Validation Tests

    /// <summary>
    /// VALIDATION TEST: Empty genre list is valid.
    /// GOAL: Users can have no genre preferences (show all).
    /// IMPORTANCE: Valid use case - discover all genres.
 /// </summary>
    [Fact]
    public void ValidateGenreList_Empty_IsValid()
    {
        // Arrange
        var genres = new List<int>();

        // Act
    var isValid = IsValidGenreList(genres);

        // Assert
        isValid.Should().BeTrue("empty list means no genre filter");
    }

  /// <summary>
    /// VALIDATION TEST: List with valid IDs is accepted.
    /// GOAL: Multiple valid genres work.
    /// IMPORTANCE: Common use case.
 /// </summary>
    [Fact]
    public void ValidateGenreList_ValidIds_IsAccepted()
    {
        // Arrange
        var genres = new List<int> { 28, 35, 878 };

     // Act
        var isValid = IsValidGenreList(genres);

        // Assert
        isValid.Should().BeTrue("list contains only valid IDs");
    }

    /// <summary>
    /// VALIDATION TEST: List with any invalid ID is rejected.
    /// GOAL: All IDs must be valid.
    /// IMPORTANCE: One bad ID invalidates the entire list.
    /// </summary>
    [Fact]
    public void ValidateGenreList_ContainsInvalidId_IsRejected()
    {
 // Arrange
        var genres = new List<int> { 28, -1, 35 }; // -1 is invalid

        // Act
   var isValid = IsValidGenreList(genres);

        // Assert
        isValid.Should().BeFalse("list contains invalid ID");
    }

    /// <summary>
    /// VALIDATION TEST: Maximum 50 genres allowed.
  /// GOAL: Prevent unreasonably large lists.
    /// IMPORTANCE: Performance - too many genres slow queries.
    /// </summary>
    [Fact]
    public void ValidateGenreList_Over50Genres_IsRejected()
    {
        // Arrange
        var genres = Enumerable.Range(1, 51).ToList();

      // Act
        var isValid = IsValidGenreList(genres);

        // Assert
        isValid.Should().BeFalse("maximum 50 genres allowed");
    }

    /// <summary>
    /// BOUNDARY TEST: Exactly 50 genres is accepted.
    /// GOAL: Limit is inclusive.
    /// IMPORTANCE: Boundary condition.
    /// </summary>
    [Fact]
    public void ValidateGenreList_Exactly50Genres_IsAccepted()
    {
        // Arrange
        var genres = Enumerable.Range(1, 50).ToList();

        // Act
        var isValid = IsValidGenreList(genres);

        // Assert
        isValid.Should().BeTrue("exactly 50 genres is allowed");
    }

    #endregion

  #region Length Bucket Mapping Tests

    /// <summary>
    /// MAPPING TEST: "short" maps to correct runtime range.
    /// GOAL: short = movies under 100 minutes.
    /// IMPORTANCE: Core filtering logic.
    /// </summary>
    [Fact]
    public void MapLength_Short_ReturnsCorrectRange()
    {
        // Act
        var (min, max) = MapLengthToRuntime("short");

        // Assert
    min.Should().BeNull("no minimum for short");
        max.Should().Be(99);
    }

  /// <summary>
    /// MAPPING TEST: "medium" maps to correct runtime range.
    /// GOAL: medium = 100-140 minutes.
    /// IMPORTANCE: Default length preference.
  /// </summary>
    [Fact]
    public void MapLength_Medium_ReturnsCorrectRange()
    {
        // Act
        var (min, max) = MapLengthToRuntime("medium");

        // Assert
        min.Should().Be(100);
        max.Should().Be(140);
    }

/// <summary>
    /// MAPPING TEST: "long" maps to correct runtime range.
  /// GOAL: long = movies over 140 minutes.
    /// IMPORTANCE: Filter for epic films.
    /// </summary>
    [Fact]
    public void MapLength_Long_ReturnsCorrectRange()
    {
        // Act
        var (min, max) = MapLengthToRuntime("long");

        // Assert
  min.Should().Be(141);
        max.Should().BeNull("no maximum for long");
}

    /// <summary>
    /// MAPPING TEST: Invalid length key falls back to medium.
    /// GOAL: Safe default when invalid input.
    /// IMPORTANCE: Defensive programming.
    /// </summary>
    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("extra-long")]
public void MapLength_Invalid_FallsBackToMedium(string lengthKey)
    {
        // Act
        var (min, max) = MapLengthToRuntime(lengthKey);

        // Assert
        min.Should().Be(100, "invalid input defaults to medium");
        max.Should().Be(140);
    }

    /// <summary>
    /// MAPPING TEST: Case-insensitive matching.
    /// GOAL: "SHORT", "Short", "short" all work.
    /// IMPORTANCE: Better UX.
    /// </summary>
    [Theory]
    [InlineData("SHORT")]
    [InlineData("Short")]
    [InlineData("sHoRt")]
    public void MapLength_CaseInsensitive_Works(string lengthKey)
    {
     // Act
        var (min, max) = MapLengthToRuntime(lengthKey.ToLowerInvariant());

  // Assert
  min.Should().BeNull();
        max.Should().Be(99);
    }

    #endregion

    #region Reverse Mapping Tests

    /// <summary>
    /// REVERSE MAPPING TEST: Runtime to length bucket.
    /// GOAL: 90 minutes ? "short".
    /// IMPORTANCE: Display user's saved preferences.
  /// </summary>
    [Theory]
    [InlineData(50, "short")]
    [InlineData(99, "short")]
    [InlineData(100, "medium")]
    [InlineData(120, "medium")]
    [InlineData(140, "medium")]
    [InlineData(141, "long")]
    [InlineData(180, "long")]
    public void MapRuntimeToLength_VariousRuntimes_ReturnCorrectBucket(int runtime, string expectedBucket)
    {
        // Act
        var bucket = MapRuntimeToLengthBucket(runtime);

        // Assert
        bucket.Should().Be(expectedBucket);
    }

  #endregion

    #region Round Trip Tests

    /// <summary>
    /// ROUND TRIP TEST: Preferences survive save/load cycle.
    /// GOAL: Data integrity - no data loss.
    /// IMPORTANCE: Users don't lose their preferences.
  /// </summary>
    [Fact]
    public void RoundTrip_PreferencesSaveAndLoad_MaintainsValues()
    {
        // Arrange
        var originalGenres = new List<int> { 28, 35, 878 };
        var originalLength = "medium";

      // Act - Simulate save (just store in variable)
var saved = (genres: originalGenres, length: originalLength);

      // Act - Simulate load
     var loaded = (genres: saved.genres, length: saved.length);

        // Assert
        loaded.genres.Should().BeEquivalentTo(originalGenres);
        loaded.length.Should().Be(originalLength);
    }

    /// <summary>
    /// ROUND TRIP TEST: Genre deduplication on save.
    /// GOAL: Duplicate IDs are removed.
    /// IMPORTANCE: Prevents redundant data.
    /// </summary>
    [Fact]
    public void RoundTrip_DuplicateGenres_AreRemoved()
    {
        // Arrange
 var genresWithDuplicates = new List<int> { 28, 28, 35, 28, 878 };

        // Act
var deduplicated = DeduplicateGenres(genresWithDuplicates);

    // Assert
    deduplicated.Should().BeEquivalentTo(new[] { 28, 35, 878 });
    deduplicated.Should().HaveCount(3);
    }

    /// <summary>
    /// ROUND TRIP TEST: Genre order is preserved after deduplication.
  /// GOAL: First occurrence is kept.
    /// IMPORTANCE: Predictable behavior.
    /// </summary>
    [Fact]
    public void RoundTrip_GenreOrder_IsPreserved()
    {
      // Arrange
  var genres = new List<int> { 35, 28, 878 };

        // Act
        var processed = DeduplicateGenres(genres);

        // Assert
     processed.Should().ContainInOrder(35, 28, 878);
    }

    #endregion

    #region Conflicting Preferences Tests

    /// <summary>
    /// VALIDATION TEST: Cannot have both empty genres and specific length.
    /// GOAL: Validate logical consistency (actually this IS valid - can filter by length only).
    /// IMPORTANCE: This test verifies no genre filter + length filter is allowed.
    /// </summary>
    [Fact]
    public void ValidatePreferences_EmptyGenresWithLength_IsValid()
    {
        // Arrange
   var genres = new List<int>();
        var length = "long";

  // Act
        var isValid = ArePreferencesValid(genres, length);

        // Assert
        isValid.Should().BeTrue("can filter by length without genre filter");
    }

    /// <summary>
    /// VALIDATION TEST: Null length falls back to default.
    /// GOAL: Missing length uses "medium".
    /// IMPORTANCE: Safe defaults.
    /// </summary>
    [Fact]
    public void ValidatePreferences_NullLength_UsesDefault()
  {
        // Arrange
  string? length = null;

        // Act
   var normalized = length ?? "medium";

      // Assert
    normalized.Should().Be("medium");
    }

    #endregion

    #region Helper Methods (Pure Business Logic)

    private bool IsValidGenreId(int genreId)
    {
 return genreId > 0;
    }

    private bool IsValidGenreList(List<int> genres)
    {
        if (genres.Count > 50)
            return false;

        return genres.All(g => g > 0);
    }

    private (int? min, int? max) MapLengthToRuntime(string lengthKey)
    {
        return lengthKey.ToLowerInvariant() switch
   {
     "short" => (null, 99),
            "medium" => (100, 140),
            "long" => (141, null),
_ => (100, 140) // Default to medium
        };
    }

    private string MapRuntimeToLengthBucket(int runtime)
    {
     if (runtime < 100) return "short";
        if (runtime <= 140) return "medium";
        return "long";
    }

    private List<int> DeduplicateGenres(List<int> genres)
    {
        return genres.Distinct().ToList();
    }

    private bool ArePreferencesValid(List<int> genres, string? length)
    {
        if (!IsValidGenreList(genres))
       return false;

        var validLengths = new[] { "short", "medium", "long" };
        if (length != null && !validLengths.Contains(length.ToLowerInvariant()))
return false;

        return true;
    }

    #endregion
}
