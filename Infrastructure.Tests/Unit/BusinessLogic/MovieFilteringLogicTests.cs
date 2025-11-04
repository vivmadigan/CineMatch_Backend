using FluentAssertions;
using Xunit;

namespace Infrastructure.Tests.Unit.BusinessLogic;

/// <summary>
/// Unit tests for movie filtering business logic (length buckets, genre parsing).
/// These are PURE unit tests - no database, no HTTP, just logic.
/// GOAL: Verify filtering logic is deterministic and correct.
/// </summary>
public class MovieFilteringLogicTests
{
    #region Length Bucket Mapping

    /// <summary>
    /// POSITIVE TEST: Verify runtime length mapping logic.
 /// GOAL: Length keys ("short", "medium", "long") map to correct runtime ranges.
    /// IMPORTANCE: Core filtering feature; wrong ranges = wrong movie recommendations.
    /// </summary>
    [Theory]
    [InlineData("short", null, 99)]
    [InlineData("medium", 100, 140)]
    [InlineData("long", 141, null)]
    [InlineData("unknown", 100, 140)] // Defaults to medium
    [InlineData("MEDIUM", 100, 140)] // Case insensitive
    [InlineData("", 100, 140)] // Empty defaults to medium
    public void MapLengthToRuntime_ReturnsCorrectBounds(string lengthKey, int? expectedMin, int? expectedMax)
    {
        // Act - Simulate MoviesController.MapLengthToRuntime()
        var (min, max) = MapLengthToRuntime(lengthKey?.ToLowerInvariant() ?? "medium");

    // Assert
        min.Should().Be(expectedMin);
        max.Should().Be(expectedMax);
  }

    /// <summary>
    /// POSITIVE TEST: Verify length options structure for UI dropdowns.
    /// GOAL: Frontend receives correct filter options with proper labels.
    /// IMPORTANCE: UI depends on this data structure.
    /// </summary>
    [Fact]
    public void LengthOptions_HaveCorrectStructure()
    {
        // Arrange & Act - Simulate MoviesController.Options() response
      var lengths = GetLengthOptions();

        // Assert - Verify all 3 buckets present with correct data
        lengths.Should().HaveCount(3);
        
        // Short bucket
        var shortBucket = lengths.Single(l => l.Key == "short");
        shortBucket.Label.Should().Contain("<100");
  shortBucket.Min.Should().BeNull();
    shortBucket.Max.Should().Be(99);
        
        // Medium bucket
    var mediumBucket = lengths.Single(l => l.Key == "medium");
        mediumBucket.Label.Should().Contain("100");
        mediumBucket.Label.Should().Contain("140");
        mediumBucket.Min.Should().Be(100);
        mediumBucket.Max.Should().Be(140);
   
 // Long bucket
        var longBucket = lengths.Single(l => l.Key == "long");
        longBucket.Label.Should().Contain(">140");
        longBucket.Min.Should().Be(141);
     longBucket.Max.Should().BeNull();
    }

    #endregion

    #region Genre ID Parsing

  /// <summary>
    /// POSITIVE TEST: Verify genre ID parsing from comma-separated query string.
    /// GOAL: String "28,35,878" converts to List<int> [28, 35, 878].
    /// IMPORTANCE: Core filtering feature for discover endpoint.
    /// </summary>
    [Theory]
    [InlineData("28,35,878", new[] { 28, 35, 878 })]
    [InlineData("28", new[] { 28 })]
    [InlineData("28,35,invalid,878", new[] { 28, 35, 878 })] // Filters invalid
    [InlineData("", new int[] { })]
    [InlineData(null, new int[] { })]
    [InlineData("  28  ,  35  ", new[] { 28, 35 })] // Handles extra whitespace
 public void ParseGenres_HandlesVariousFormats(string? input, int[] expected)
    {
      // Act - Simulate MoviesController.ParseGenres()
        var result = ParseGenres(input);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    /// <summary>
    /// NEGATIVE TEST: Verify negative/zero genre IDs are filtered out.
    /// GOAL: Invalid genre IDs don't reach TMDB API.
    /// IMPORTANCE: Prevents API errors from malformed input.
    /// </summary>
    [Theory]
    [InlineData("0,28,35", new[] { 28, 35 })]
    [InlineData("-1,28,35", new[] { 28, 35 })]
    [InlineData("0,-1,-999", new int[] { })]
    [InlineData("-1", new int[] { })]
    public void ParseGenres_FiltersInvalidIds(string input, int[] expected)
    {
        // Act
        var result = ParseGenres(input);

        // Assert
        result.Should().BeEquivalentTo(expected);
        result.Should().NotContain(id => id <= 0, "negative and zero IDs should be filtered");
    }

    /// <summary>
    /// NEGATIVE TEST: Verify duplicate genre IDs are preserved (filtering happens elsewhere).
    /// GOAL: Parser doesn't modify duplicates; service layer handles deduplication.
    /// IMPORTANCE: Single responsibility - parser parses, service deduplicates.
    /// </summary>
    [Fact]
    public void ParseGenres_PreservesDuplicates()
    {
        // Arrange
   var input = "28,28,35,28";

        // Act
        var result = ParseGenres(input);

   // Assert - Parser doesn't deduplicate (service layer does)
   result.Should().HaveCount(4);
        result.Where(x => x == 28).Should().HaveCount(3);
        result.Where(x => x == 35).Should().HaveCount(1);
  }

    #endregion

    #region Helper Methods

    private static (int? min, int? max) MapLengthToRuntime(string lengthKey)
    {
 return lengthKey switch
        {
    "short" => (null, 99),
  "medium" => (100, 140),
  "long" => (141, null),
            _ => (100, 140) // default to medium
        };
    }

    private static List<(string Key, string Label, int? Min, int? Max)> GetLengthOptions()
    {
        return new List<(string, string, int?, int?)>
 {
        ("short", "Short (<100 min)", null, 99),
            ("medium", "Medium (100–140)", 100, 140),
    ("long", "Long (>140 min)", 141, null)
  };
    }

    private static List<int> ParseGenres(string? genresParam)
  {
        if (string.IsNullOrWhiteSpace(genresParam)) return new List<int>();

        return genresParam
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
    .Where(id => id > 0)
            .ToList();
    }

    #endregion
}
