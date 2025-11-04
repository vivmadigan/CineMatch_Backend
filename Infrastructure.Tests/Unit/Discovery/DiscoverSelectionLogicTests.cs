using FluentAssertions;
using Xunit;

namespace Infrastructure.Tests.Unit.Discovery;

/// <summary>
/// Pure unit tests for movie discovery selection logic.
/// Tests filtering, ordering, deduplication, and edge cases.
/// GOAL: Verify discovery algorithm works correctly without external dependencies.
/// IMPORTANCE: CRITICAL - Core feature for movie recommendations.
/// </summary>
public class DiscoverSelectionLogicTests
{
    #region Movie Record for Testing
    private record Movie(int Id, string Title, List<int> GenreIds, int Runtime, double Rating, DateTime ReleaseDate);
    #endregion

    #region Filtering Tests

    /// <summary>
    /// FILTER TEST: Genre filter includes movies with matching genres.
    /// GOAL: Movies matching ANY selected genre are included.
 /// IMPORTANCE: Core filtering logic.
    /// </summary>
    [Fact]
    public void FilterByGenres_MatchingGenres_AreIncluded()
    {
  // Arrange
        var movies = new List<Movie>
     {
new(1, "Action Movie", new List<int> { 28 }, 120, 7.5, DateTime.UtcNow),
         new(2, "Comedy Movie", new List<int> { 35 }, 100, 7.0, DateTime.UtcNow),
       new(3, "Drama Movie", new List<int> { 18 }, 110, 8.0, DateTime.UtcNow)
        };
  var selectedGenres = new List<int> { 28, 35 }; // Action + Comedy

        // Act
        var filtered = FilterByGenres(movies, selectedGenres);

        // Assert
        filtered.Should().HaveCount(2);
    filtered.Should().Contain(m => m.Id == 1);
        filtered.Should().Contain(m => m.Id == 2);
  }

    /// <summary>
    /// FILTER TEST: Empty genre filter returns all movies.
    /// GOAL: No genre preference = show everything.
    /// IMPORTANCE: Default behavior.
    /// </summary>
    [Fact]
    public void FilterByGenres_EmptyFilter_ReturnsAll()
{
        // Arrange
        var movies = new List<Movie>
     {
   new(1, "Movie 1", new List<int> { 28 }, 120, 7.5, DateTime.UtcNow),
  new(2, "Movie 2", new List<int> { 35 }, 100, 7.0, DateTime.UtcNow),
   new(3, "Movie 3", new List<int> { 18 }, 110, 8.0, DateTime.UtcNow)
  };
   var selectedGenres = new List<int>();

 // Act
        var filtered = FilterByGenres(movies, selectedGenres);

   // Assert
        filtered.Should().HaveCount(3);
    }

    /// <summary>
    /// FILTER TEST: Movie with multiple genres matches if any match.
    /// GOAL: OR logic for genres.
    /// IMPORTANCE: Movies can belong to multiple genres.
    /// </summary>
    [Fact]
    public void FilterByGenres_MultiGenreMovie_MatchesIfAnyGenreMatches()
    {
   // Arrange
      var movies = new List<Movie>
        {
  new(1, "Action Comedy", new List<int> { 28, 35 }, 120, 7.5, DateTime.UtcNow)
  };
        var selectedGenres = new List<int> { 28 }; // Only Action

     // Act
        var filtered = FilterByGenres(movies, selectedGenres);

        // Assert
     filtered.Should().ContainSingle("movie has action genre");
}

    /// <summary>
    /// FILTER TEST: Runtime filter (short).
    /// GOAL: Only movies under 100 minutes.
    /// IMPORTANCE: Length preference filtering.
    /// </summary>
    [Fact]
    public void FilterByRuntime_Short_ReturnsUnder100Minutes()
    {
        // Arrange
        var movies = new List<Movie>
     {
    new(1, "Short Movie", new List<int> { 28 }, 90, 7.5, DateTime.UtcNow),
         new(2, "Medium Movie", new List<int> { 28 }, 120, 7.0, DateTime.UtcNow),
   new(3, "Long Movie", new List<int> { 28 }, 180, 8.0, DateTime.UtcNow)
        };

        // Act
        var filtered = FilterByRuntime(movies, min: null, max: 99);

 // Assert
        filtered.Should().ContainSingle();
      filtered.First().Runtime.Should().Be(90);
    }

    /// <summary>
    /// FILTER TEST: Runtime filter (medium).
    /// GOAL: Movies between 100-140 minutes.
    /// IMPORTANCE: Default length preference.
    /// </summary>
    [Fact]
    public void FilterByRuntime_Medium_Returns100To140Minutes()
    {
     // Arrange
        var movies = new List<Movie>
  {
            new(1, "Short Movie", new List<int> { 28 }, 90, 7.5, DateTime.UtcNow),
new(2, "Medium Movie", new List<int> { 28 }, 120, 7.0, DateTime.UtcNow),
    new(3, "Long Movie", new List<int> { 28 }, 180, 8.0, DateTime.UtcNow)
        };

   // Act
   var filtered = FilterByRuntime(movies, min: 100, max: 140);

   // Assert
  filtered.Should().ContainSingle();
     filtered.First().Runtime.Should().Be(120);
    }

    /// <summary>
    /// FILTER TEST: Runtime filter (long).
    /// GOAL: Movies over 140 minutes.
    /// IMPORTANCE: Epic film preference.
  /// </summary>
    [Fact]
    public void FilterByRuntime_Long_ReturnsOver140Minutes()
    {
        // Arrange
    var movies = new List<Movie>
        {
            new(1, "Short Movie", new List<int> { 28 }, 90, 7.5, DateTime.UtcNow),
  new(2, "Medium Movie", new List<int> { 28 }, 120, 7.0, DateTime.UtcNow),
 new(3, "Long Movie", new List<int> { 28 }, 180, 8.0, DateTime.UtcNow)
        };

 // Act
   var filtered = FilterByRuntime(movies, min: 141, max: null);

     // Assert
     filtered.Should().ContainSingle();
        filtered.First().Runtime.Should().Be(180);
    }

    #endregion

    #region Ordering Tests

    /// <summary>
    /// ORDER TEST: Movies ordered by rating (highest first).
    /// GOAL: Best movies shown first.
    /// IMPORTANCE: Quality-first ordering.
    /// </summary>
    [Fact]
    public void OrderMovies_ByRating_HighestFirst()
    {
      // Arrange
        var movies = new List<Movie>
        {
    new(1, "Movie 1", new List<int> { 28 }, 120, 6.5, DateTime.UtcNow),
            new(2, "Movie 2", new List<int> { 28 }, 120, 8.5, DateTime.UtcNow),
   new(3, "Movie 3", new List<int> { 28 }, 120, 7.0, DateTime.UtcNow)
  };

        // Act
        var ordered = OrderByRating(movies);

     // Assert
   ordered.First().Id.Should().Be(2, "highest rated first");
  ordered.Last().Id.Should().Be(1, "lowest rated last");
 }

    /// <summary>
    /// ORDER TEST: Movies with same rating ordered by release date (newest first).
    /// GOAL: Tie-breaking logic.
    /// IMPORTANCE: Deterministic ordering.
    /// </summary>
    [Fact]
    public void OrderMovies_SameRating_NewestFirst()
    {
        // Arrange
        var oldDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
   var newDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var movies = new List<Movie>
  {
  new(1, "Old Movie", new List<int> { 28 }, 120, 7.5, oldDate),
     new(2, "New Movie", new List<int> { 28 }, 120, 7.5, newDate)
        };

     // Act
        var ordered = OrderByRatingThenDate(movies);

        // Assert
   ordered.First().Id.Should().Be(2, "newer movie breaks tie");
        ordered.Last().Id.Should().Be(1);
    }

    /// <summary>
    /// ORDER TEST: Stable sort maintains relative order.
    /// GOAL: Movies with identical rating and date keep their order.
    /// IMPORTANCE: Predictable results.
    /// </summary>
    [Fact]
    public void OrderMovies_IdenticalProperties_MaintainsOrder()
    {
    // Arrange
   var date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var movies = new List<Movie>
        {
         new(1, "Movie A", new List<int> { 28 }, 120, 7.5, date),
      new(2, "Movie B", new List<int> { 28 }, 120, 7.5, date),
       new(3, "Movie C", new List<int> { 28 }, 120, 7.5, date)
    };

     // Act
        var ordered = OrderByRatingThenDate(movies);

        // Assert - Order should be preserved when all properties equal
        ordered.Select(m => m.Id).Should().ContainInOrder(1, 2, 3);
    }

    #endregion

    #region Deduplication Tests

    /// <summary>
    /// DEDUP TEST: Duplicate movie IDs are removed.
    /// GOAL: Each movie appears once.
    /// IMPORTANCE: Multiple sources might propose same movie.
    /// </summary>
    [Fact]
    public void Deduplicate_RemovesDuplicateIds()
    {
        // Arrange
        var movies = new List<Movie>
    {
      new(1, "Movie 1", new List<int> { 28 }, 120, 7.5, DateTime.UtcNow),
  new(1, "Movie 1 (duplicate)", new List<int> { 28 }, 120, 7.5, DateTime.UtcNow),
      new(2, "Movie 2", new List<int> { 35 }, 100, 7.0, DateTime.UtcNow)
        };

        // Act
   var deduplicated = DeduplicateById(movies);

        // Assert
        deduplicated.Should().HaveCount(2);
        deduplicated.Select(m => m.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    /// <summary>
    /// DEDUP TEST: First occurrence is kept.
 /// GOAL: Predictable deduplication.
    /// IMPORTANCE: Consistent behavior.
    /// </summary>
    [Fact]
    public void Deduplicate_KeepsFirstOccurrence()
    {
   // Arrange
   var movies = new List<Movie>
        {
       new(1, "First", new List<int> { 28 }, 120, 7.5, DateTime.UtcNow),
            new(1, "Second", new List<int> { 28 }, 120, 7.5, DateTime.UtcNow)
   };

        // Act
  var deduplicated = DeduplicateById(movies);

   // Assert
        deduplicated.Should().ContainSingle();
 deduplicated.First().Title.Should().Be("First");
    }

    #endregion

    #region Empty Catalogue Tests

  /// <summary>
    /// EDGE CASE TEST: Empty movie list returns empty.
    /// GOAL: No results gracefully handled.
    /// IMPORTANCE: Initial state or no matches scenario.
    /// </summary>
    [Fact]
    public void FilterMovies_EmptyCatalogue_ReturnsEmpty()
    {
        // Arrange
   var movies = new List<Movie>();
   var selectedGenres = new List<int> { 28 };

  // Act
        var filtered = FilterByGenres(movies, selectedGenres);

 // Assert
        filtered.Should().BeEmpty();
    }

    /// <summary>
    /// EDGE CASE TEST: No matches returns empty.
  /// GOAL: Filter with no results handled.
    /// IMPORTANCE: User might select very specific filters.
    /// </summary>
    [Fact]
    public void FilterMovies_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var movies = new List<Movie>
     {
     new(1, "Action Movie", new List<int> { 28 }, 120, 7.5, DateTime.UtcNow)
      };
   var selectedGenres = new List<int> { 35 }; // Comedy - not in list

  // Act
        var filtered = FilterByGenres(movies, selectedGenres);

   // Assert
  filtered.Should().BeEmpty();
    }

#endregion

    #region Pagination Tests

    /// <summary>
    /// PAGINATION TEST: Take first N movies.
    /// GOAL: Limit results to requested batch size.
    /// IMPORTANCE: Performance and UX.
    /// </summary>
  [Fact]
    public void PaginateMovies_TakeFirst5()
    {
        // Arrange
        var movies = Enumerable.Range(1, 20)
  .Select(i => new Movie(i, $"Movie {i}", new List<int> { 28 }, 120, 7.5, DateTime.UtcNow))
  .ToList();

        // Act
   var page = movies.Take(5).ToList();

     // Assert
     page.Should().HaveCount(5);
        page.First().Id.Should().Be(1);
page.Last().Id.Should().Be(5);
    }

    /// <summary>
    /// PAGINATION TEST: Requesting more than available returns all.
    /// GOAL: Take doesn't throw on over-request.
    /// IMPORTANCE: Edge case handling.
    /// </summary>
    [Fact]
    public void PaginateMovies_RequestMoreThanAvailable_ReturnsAll()
    {
        // Arrange
        var movies = Enumerable.Range(1, 3)
      .Select(i => new Movie(i, $"Movie {i}", new List<int> { 28 }, 120, 7.5, DateTime.UtcNow))
  .ToList();

        // Act
        var page = movies.Take(10).ToList();

 // Assert
        page.Should().HaveCount(3, "only 3 movies available");
    }

    #endregion

    #region Combined Pipeline Tests

  /// <summary>
    /// INTEGRATION TEST: Full discovery pipeline.
/// GOAL: Filter ? Order ? Deduplicate ? Paginate.
    /// IMPORTANCE: End-to-end logic works together.
    /// </summary>
    [Fact]
    public void DiscoveryPipeline_FullFlow_Works()
    {
   // Arrange
      var movies = new List<Movie>
        {
   new(1, "Low Rated Action", new List<int> { 28 }, 120, 6.0, DateTime.UtcNow),
            new(2, "High Rated Action", new List<int> { 28 }, 120, 8.5, DateTime.UtcNow),
  new(3, "Comedy", new List<int> { 35 }, 100, 7.0, DateTime.UtcNow),
            new(2, "Duplicate", new List<int> { 28 }, 120, 8.5, DateTime.UtcNow), // Duplicate ID
new(4, "Drama", new List<int> { 18 }, 110, 7.5, DateTime.UtcNow)
        };
var selectedGenres = new List<int> { 28 }; // Action only

   // Act - Full pipeline
  var filtered = FilterByGenres(movies, selectedGenres);
     var ordered = OrderByRating(filtered);
      var deduplicated = DeduplicateById(ordered);
  var paginated = deduplicated.Take(5).ToList();

    // Assert
     paginated.Should().HaveCount(2, "2 unique action movies");
        paginated.First().Id.Should().Be(2, "highest rated first");
   paginated.Last().Id.Should().Be(1, "lowest rated last");
  }

    #endregion

    #region Helper Methods (Pure Business Logic)

    private List<Movie> FilterByGenres(List<Movie> movies, List<int> selectedGenres)
 {
        if (selectedGenres.Count == 0)
            return movies;

   return movies.Where(m => m.GenreIds.Any(g => selectedGenres.Contains(g))).ToList();
    }

    private List<Movie> FilterByRuntime(List<Movie> movies, int? min, int? max)
    {
     var filtered = movies.AsEnumerable();

        if (min.HasValue)
         filtered = filtered.Where(m => m.Runtime >= min.Value);

        if (max.HasValue)
       filtered = filtered.Where(m => m.Runtime <= max.Value);

        return filtered.ToList();
  }

    private List<Movie> OrderByRating(List<Movie> movies)
    {
        return movies.OrderByDescending(m => m.Rating).ToList();
    }

 private List<Movie> OrderByRatingThenDate(List<Movie> movies)
    {
     return movies.OrderByDescending(m => m.Rating)
      .ThenByDescending(m => m.ReleaseDate)
         .ToList();
    }

    private List<Movie> DeduplicateById(List<Movie> movies)
    {
     return movies.GroupBy(m => m.Id).Select(g => g.First()).ToList();
    }

    #endregion
}
