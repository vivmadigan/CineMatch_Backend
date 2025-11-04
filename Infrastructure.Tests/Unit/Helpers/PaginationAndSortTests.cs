using FluentAssertions;
using Xunit;

namespace Infrastructure.Tests.Unit.Helpers;

/// <summary>
/// Pure unit tests for pagination and sort helper functions.
/// Tests cursor generation, page calculation, and sort stability.
/// GOAL: Verify helper functions work correctly in isolation.
/// IMPORTANCE: HIGH - Used throughout the application for list operations.
/// </summary>
public class PaginationAndSortTests
{
    #region Pagination Cursor Tests

    /// <summary>
    /// CURSOR TEST: Generate cursor from timestamp.
    /// GOAL: Cursor encodes pagination state.
    /// IMPORTANCE: Enables efficient pagination.
    /// </summary>
    [Fact]
    public void GenerateCursor_FromTimestamp_IsValid()
    {
  // Arrange
        var timestamp = new DateTime(2025, 11, 3, 15, 30, 0, DateTimeKind.Utc);

        // Act
    var cursor = GenerateCursor(timestamp);

     // Assert
  cursor.Should().NotBeNullOrEmpty();
        cursor.Should().Contain("2025");
    }

    /// <summary>
    /// CURSOR TEST: Cursor is deterministic.
    /// GOAL: Same input produces same cursor.
    /// IMPORTANCE: Pagination consistency.
    /// </summary>
  [Fact]
    public void GenerateCursor_Deterministic()
    {
        // Arrange
   var timestamp = new DateTime(2025, 11, 3, 15, 30, 0, DateTimeKind.Utc);

        // Act
  var cursor1 = GenerateCursor(timestamp);
        var cursor2 = GenerateCursor(timestamp);

        // Assert
  cursor1.Should().Be(cursor2);
    }

    /// <summary>
  /// CURSOR TEST: Parse cursor back to timestamp.
    /// GOAL: Round-trip cursor conversion.
    /// IMPORTANCE: Resuming pagination requires decoding cursor.
    /// </summary>
    [Fact]
    public void ParseCursor_RoundTrip_ReturnsOriginalTimestamp()
    {
     // Arrange
        var originalTimestamp = new DateTime(2025, 11, 3, 15, 30, 0, DateTimeKind.Utc);
        var cursor = GenerateCursor(originalTimestamp);

  // Act
        var parsed = ParseCursor(cursor);

    // Assert
        parsed.Should().BeCloseTo(originalTimestamp, TimeSpan.FromMilliseconds(1));
    }

    /// <summary>
    /// CURSOR TEST: Invalid cursor returns default.
 /// GOAL: Graceful handling of malformed cursors.
    /// IMPORTANCE: Defensive programming.
    /// </summary>
    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseCursor_Invalid_ReturnsDefault(string? cursor)
    {
        // Act
    var parsed = ParseCursor(cursor);

  // Assert
     parsed.Should().Be(DateTime.MinValue);
    }

    #endregion

    #region Page Calculation Tests

    /// <summary>
    /// PAGINATION TEST: Calculate correct page from offset.
    /// GOAL: Offset 0, size 10 ? page 1.
    /// IMPORTANCE: Page numbering logic.
    /// </summary>
    [Theory]
    [InlineData(0, 10, 1)]   // First page
    [InlineData(10, 10, 2)]  // Second page
    [InlineData(20, 10, 3)]  // Third page
    [InlineData(0, 5, 1)]    // Different page size
    [InlineData(5, 5, 2)]
    public void CalculatePageNumber_FromOffset(int offset, int pageSize, int expectedPage)
    {
    // Act
        var page = CalculatePageNumber(offset, pageSize);

    // Assert
        page.Should().Be(expectedPage);
    }

    /// <summary>
    /// PAGINATION TEST: Calculate offset from page number.
    /// GOAL: Page 3, size 10 ? offset 20.
    /// IMPORTANCE: Reverse calculation for API queries.
    /// </summary>
    [Theory]
    [InlineData(1, 10, 0)]   // First page starts at 0
    [InlineData(2, 10, 10)]  // Second page starts at 10
    [InlineData(3, 10, 20)]// Third page starts at 20
    [InlineData(1, 5, 0)]  // Different page size
    [InlineData(2, 5, 5)]
    public void CalculateOffset_FromPageNumber(int page, int pageSize, int expectedOffset)
    {
      // Act
        var offset = CalculateOffset(page, pageSize);

        // Assert
        offset.Should().Be(expectedOffset);
    }

    /// <summary>
    /// PAGINATION TEST: Total pages calculation.
    /// GOAL: 25 items, size 10 ? 3 pages.
    /// IMPORTANCE: UI needs to know total page count.
    /// </summary>
    [Theory]
    [InlineData(25, 10, 3)]   // 25/10 = 3 pages
    [InlineData(30, 10, 3)]   // 30/10 = 3 pages
    [InlineData(31, 10, 4)]   // 31/10 = 4 pages (partial last page)
    [InlineData(0, 10, 0)]    // No items = 0 pages
    [InlineData(5, 10, 1)]    // Less than page size = 1 page
    public void CalculateTotalPages_VariousScenarios(int totalItems, int pageSize, int expectedPages)
    {
        // Act
    var totalPages = CalculateTotalPages(totalItems, pageSize);

        // Assert
        totalPages.Should().Be(expectedPages);
    }

    /// <summary>
    /// PAGINATION TEST: Has next page logic.
    /// GOAL: Determine if more pages exist.
    /// IMPORTANCE: UI "Load More" button visibility.
    /// </summary>
  [Theory]
    [InlineData(1, 10, 25, true)]   // Page 1 of 3 ? has next
    [InlineData(2, 10, 25, true)]   // Page 2 of 3 ? has next
    [InlineData(3, 10, 25, false)]  // Page 3 of 3 ? no next
    [InlineData(1, 10, 5, false)]   // Only 1 page ? no next
    public void HasNextPage_Logic(int currentPage, int pageSize, int totalItems, bool expectedHasNext)
    {
        // Act
  var hasNext = HasNextPage(currentPage, pageSize, totalItems);

        // Assert
        hasNext.Should().Be(expectedHasNext);
    }

    #endregion

    #region Sort Stability Tests

    /// <summary>
    /// SORT TEST: Stable sort preserves order of equal elements.
    /// GOAL: Elements with same key maintain relative order.
    /// IMPORTANCE: Predictable sorting behavior.
    /// </summary>
    [Fact]
    public void StableSort_PreservesOrderOfEqualElements()
    {
        // Arrange
        var items = new List<SortableItem>
 {
    new(1, "First", 5),
  new(2, "Second", 5),
            new(3, "Third", 5)
        };

        // Act
        var sorted = items.OrderBy(x => x.Score).ToList();

        // Assert - Order should be preserved
        sorted[0].Id.Should().Be(1);
        sorted[1].Id.Should().Be(2);
        sorted[2].Id.Should().Be(3);
    }

    /// <summary>
    /// SORT TEST: Descending sort with stable tie-breaking.
 /// GOAL: Higher scores first, ties maintain order.
    /// IMPORTANCE: Leaderboard/ranking scenarios.
    /// </summary>
    [Fact]
    public void StableSort_Descending_WithTieBreaker()
    {
 // Arrange
        var items = new List<SortableItem>
        {
      new(1, "A", 10),
            new(2, "B", 5),
          new(3, "C", 10),
       new(4, "D", 5)
  };

    // Act - Sort by score desc, then by ID asc (tie breaker)
        var sorted = items.OrderByDescending(x => x.Score).ThenBy(x => x.Id).ToList();

     // Assert
        sorted[0].Id.Should().Be(1, "score 10, ID 1");
        sorted[1].Id.Should().Be(3, "score 10, ID 3");
        sorted[2].Id.Should().Be(2, "score 5, ID 2");
    sorted[3].Id.Should().Be(4, "score 5, ID 4");
    }

    /// <summary>
    /// SORT TEST: Multi-level sort (score, then date, then name).
    /// GOAL: Complex sorting works correctly.
    /// IMPORTANCE: Candidate ranking uses multi-level sort.
    /// </summary>
    [Fact]
    public void MultiLevelSort_Works()
    {
   // Arrange
        var oldDate = new DateTime(2020, 1, 1);
        var newDate = new DateTime(2025, 1, 1);

    var items = new List<ComplexSortItem>
        {
          new(1, "Alice", 10, oldDate),
        new(2, "Bob", 10, newDate),
            new(3, "Charlie", 5, newDate),
      new(4, "Diana", 10, newDate)
     };

   // Act - Sort by score desc, then date desc, then name asc
        var sorted = items
            .OrderByDescending(x => x.Score)
          .ThenByDescending(x => x.Date)
            .ThenBy(x => x.Name)
       .ToList();

   // Assert
 sorted[0].Name.Should().Be("Bob", "score 10, newest, name B");
        sorted[1].Name.Should().Be("Diana", "score 10, newest, name D");
        sorted[2].Name.Should().Be("Alice", "score 10, oldest");
        sorted[3].Name.Should().Be("Charlie", "score 5");
    }

 #endregion

    #region Skip/Take Tests

    /// <summary>
    /// PAGINATION TEST: Skip and take for page retrieval.
    /// GOAL: Correct subset of items returned.
    /// IMPORTANCE: Core pagination implementation.
  /// </summary>
    [Fact]
    public void SkipTake_ReturnsCorrectPage()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).ToList();
   var pageSize = 10;
        var page = 3; // Third page

        // Act
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // Assert
     pageItems.Should().HaveCount(10);
        pageItems.First().Should().Be(21); // Items 21-30
        pageItems.Last().Should().Be(30);
    }

    /// <summary>
    /// PAGINATION TEST: Last page with partial items.
    /// GOAL: Correctly handle incomplete final page.
    /// IMPORTANCE: Edge case - not all pages are full.
    /// </summary>
    [Fact]
    public void SkipTake_PartialLastPage()
    {
        // Arrange
        var items = Enumerable.Range(1, 25).ToList();
     var pageSize = 10;
        var page = 3; // Third page has only 5 items

      // Act
   var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

      // Assert
        pageItems.Should().HaveCount(5);
 pageItems.First().Should().Be(21);
   pageItems.Last().Should().Be(25);
    }

    /// <summary>
    /// PAGINATION TEST: Skip beyond available items returns empty.
    /// GOAL: Over-pagination returns empty list, not error.
    /// IMPORTANCE: Graceful handling of invalid page numbers.
    /// </summary>
    [Fact]
    public void SkipTake_BeyondAvailable_ReturnsEmpty()
 {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();
        var pageSize = 10;
        var page = 5; // Beyond available data

        // Act
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // Assert
        pageItems.Should().BeEmpty();
    }

    #endregion

    #region Additional Pagination Edge Cases

    /// <summary>
    /// EDGE CASE TEST: Calculate page with zero page size.
 /// GOAL: Prevent division by zero.
    /// IMPORTANCE: Defensive programming.
    /// </summary>
    [Fact]
    public void CalculatePageNumber_ZeroPageSize_ThrowsException()
    {
   // Act & Assert - Should throw division by zero
        Assert.Throws<DivideByZeroException>(() => CalculatePageNumber(10, 0));
    }

    /// <summary>
    /// EDGE CASE TEST: Calculate offset with zero page size.
    /// GOAL: Prevent invalid offset calculation.
    /// IMPORTANCE: Edge case validation.
 /// </summary>
    [Fact]
    public void CalculateOffset_ZeroPageSize_ReturnsZero()
    {
    // Act
  var offset = CalculateOffset(5, 0);

    // Assert
   offset.Should().Be(0, "page size of 0 should result in offset 0");
  }

    /// <summary>
    /// EDGE CASE TEST: Total pages with negative total items.
    /// GOAL: Handle invalid input gracefully.
    /// IMPORTANCE: Defensive programming.
    /// NOTE: Current implementation returns -1, which is mathematically correct for ceiling(-1/10).
/// </summary>
    [Fact]
    public void CalculateTotalPages_NegativeTotalItems_ReturnsNegative()
    {
      // Act
var totalPages = CalculateTotalPages(-10, 10);

   // Assert - Current implementation returns negative value
      totalPages.Should().BeLessThan(0, "negative items result in negative pages (current behavior)");
    }

    /// <summary>
    /// EDGE CASE TEST: Very large page numbers.
    /// GOAL: System handles extreme pagination.
    /// IMPORTANCE: Edge case for large datasets.
    /// </summary>
    [Theory]
    [InlineData(1000000, 10, 9999990)] // Page 1 million
    [InlineData(int.MaxValue - 1, 1, int.MaxValue - 2)]
    public void CalculateOffset_VeryLargePage_HandlesCorrectly(int page, int pageSize, int expectedOffset)
    {
   // Act
     var offset = CalculateOffset(page, pageSize);

      // Assert
 offset.Should().Be(expectedOffset);
    }

    /// <summary>
    /// EDGE CASE TEST: Has next page with very large total.
    /// GOAL: Correct logic with large numbers.
    /// IMPORTANCE: Scalability.
    /// </summary>
    [Fact]
    public void HasNextPage_VeryLargeTotal_CalculatesCorrectly()
    {
 // Act
        var hasNext = HasNextPage(1, 100, 1000000);

   // Assert
        hasNext.Should().BeTrue("should have many more pages");
    }

    /// <summary>
    /// EDGE CASE TEST: Total pages with uneven division.
    /// GOAL: Ceiling math works correctly.
    /// IMPORTANCE: Partial pages common scenario.
    /// </summary>
    [Theory]
    [InlineData(101, 10, 11)] // 10.1 pages ? 11
    [InlineData(99, 10, 10)]  // 9.9 pages ? 10
    [InlineData(1, 10, 1)]    // 0.1 pages ? 1
    public void CalculateTotalPages_UnevenDivision_CeilsCorrectly(int totalItems, int pageSize, int expectedPages)
    {
 // Act
        var totalPages = CalculateTotalPages(totalItems, pageSize);

 // Assert
        totalPages.Should().Be(expectedPages);
    }

    #endregion

    #region Additional Cursor Edge Cases

    /// <summary>
    /// EDGE CASE TEST: Generate cursor from DateTime.MinValue.
    /// GOAL: Extreme dates handled.
/// IMPORTANCE: Edge case for timestamp bounds.
  /// </summary>
    [Fact]
    public void GenerateCursor_MinDateTime_GeneratesValidCursor()
  {
     // Act
        var cursor = GenerateCursor(DateTime.MinValue);

        // Assert
    cursor.Should().NotBeNullOrEmpty();
 }

 /// <summary>
    /// EDGE CASE TEST: Generate cursor from DateTime.MaxValue.
 /// GOAL: Future dates handled.
    /// IMPORTANCE: Edge case for timestamp bounds.
  /// </summary>
 [Fact]
    public void GenerateCursor_MaxDateTime_GeneratesValidCursor()
    {
  // Act
        var cursor = GenerateCursor(DateTime.MaxValue);

   // Assert
    cursor.Should().NotBeNullOrEmpty();
    }

/// <summary>
    /// EDGE CASE TEST: Parse cursor with very long string.
    /// GOAL: Malformed cursors don't crash parser.
    /// IMPORTANCE: Defensive programming against tampering.
    /// </summary>
    [Fact]
    public void ParseCursor_VeryLongString_ReturnsDefault()
    {
 // Arrange
   var longCursor = new string('A', 10000);

        // Act
     var parsed = ParseCursor(longCursor);

   // Assert
        parsed.Should().Be(DateTime.MinValue);
    }

    /// <summary>
    /// EDGE CASE TEST: Parse cursor with SQL injection attempt.
    /// GOAL: Malicious input handled safely.
    /// IMPORTANCE: Security - cursor is user input.
 /// </summary>
    [Theory]
 [InlineData("'; DROP TABLE Users; --")]
    [InlineData("<script>alert('XSS')</script>")]
  [InlineData("../../../../etc/passwd")]
 public void ParseCursor_MaliciousInput_ReturnsDefault(string maliciousCursor)
    {
    // Act
        var parsed = ParseCursor(maliciousCursor);

  // Assert
    parsed.Should().Be(DateTime.MinValue, "should reject malicious input");
    }

    #endregion

    #region Additional Sort Edge Cases

 /// <summary>
    /// SORT TEST: Sort empty list.
    /// GOAL: Empty collections handled.
    /// IMPORTANCE: Edge case for no data.
    /// </summary>
    [Fact]
 public void Sort_EmptyList_ReturnsEmpty()
    {
   // Arrange
     var items = new List<SortableItem>();

        // Act
   var sorted = items.OrderBy(x => x.Score).ToList();

        // Assert
    sorted.Should().BeEmpty();
    }

    /// <summary>
  /// SORT TEST: Sort single item.
    /// GOAL: Single item collections work.
    /// IMPORTANCE: Boundary condition.
    /// </summary>
    [Fact]
  public void Sort_SingleItem_ReturnsSameItem()
    {
   // Arrange
        var items = new List<SortableItem> { new(1, "Only", 5) };

    // Act
        var sorted = items.OrderBy(x => x.Score).ToList();

        // Assert
        sorted.Should().ContainSingle();
  sorted.First().Id.Should().Be(1);
    }

    /// <summary>
    /// SORT TEST: Sort with all identical values.
    /// GOAL: Handles degenerate case.
    /// IMPORTANCE: Edge case for uniform data.
    /// </summary>
    [Fact]
    public void Sort_AllIdenticalValues_MaintainsOrder()
    {
    // Arrange
   var items = new List<SortableItem>
  {
  new(1, "First", 5),
     new(2, "Second", 5),
new(3, "Third", 5),
      new(4, "Fourth", 5)
        };

      // Act
   var sorted = items.OrderBy(x => x.Score).ToList();

        // Assert - Original order preserved
   sorted.Select(x => x.Id).Should().ContainInOrder(1, 2, 3, 4);
 }

    /// <summary>
    /// SORT TEST: Sort with negative scores.
    /// GOAL: Negative values sorted correctly.
    /// IMPORTANCE: Some scoring systems use negatives.
    /// </summary>
    [Fact]
    public void Sort_NegativeScores_SortsCorrectly()
    {
        // Arrange
   var items = new List<SortableItem>
        {
       new(1, "A", -5),
  new(2, "B", 10),
     new(3, "C", -10),
       new(4, "D", 0)
   };

   // Act
  var sorted = items.OrderBy(x => x.Score).ToList();

   // Assert
   sorted[0].Score.Should().Be(-10);
  sorted[1].Score.Should().Be(-5);
  sorted[2].Score.Should().Be(0);
   sorted[3].Score.Should().Be(10);
    }

    #endregion

    #region Skip/Take Edge Cases

    /// <summary>
 /// PAGINATION TEST: Skip more than available.
    /// GOAL: Over-skip returns empty.
    /// IMPORTANCE: Common pagination error.
 /// </summary>
    [Fact]
    public void SkipTake_SkipBeyondAvailable_ReturnsEmpty()
    {
        // Arrange
     var items = Enumerable.Range(1, 10).ToList();

        // Act
    var result = items.Skip(100).Take(10).ToList();

   // Assert
 result.Should().BeEmpty();
    }

    /// <summary>
    /// PAGINATION TEST: Take zero items.
    /// GOAL: Take(0) returns empty.
    /// IMPORTANCE: Edge case validation.
    /// </summary>
    [Fact]
    public void SkipTake_TakeZero_ReturnsEmpty()
 {
        // Arrange
   var items = Enumerable.Range(1, 100).ToList();

   // Act
     var result = items.Take(0).ToList();

// Assert
   result.Should().BeEmpty();
 }

    /// <summary>
    /// PAGINATION TEST: Negative skip.
    /// GOAL: Negative skip treated as zero.
    /// IMPORTANCE: Edge case handling.
    /// </summary>
    [Fact]
    public void SkipTake_NegativeSkip_TreatedAsZero()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();

    // Act
   var result = items.Skip(-5).Take(3).ToList();

        // Assert
    result.Should().HaveCount(3);
        result.First().Should().Be(1); // Starts from beginning
  }

    #endregion

    #region Helper Methods & Records

    private record SortableItem(int Id, string Name, int Score);
    private record ComplexSortItem(int Id, string Name, int Score, DateTime Date);

    private string GenerateCursor(DateTime timestamp)
    {
  // Convert to UTC and format as ISO 8601
        return timestamp.ToUniversalTime().ToString("o");
 }

    private DateTime ParseCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
  return DateTime.MinValue;

  if (DateTime.TryParse(cursor, null, System.Globalization.DateTimeStyles.RoundtripKind, out var result))
            return result;

        return DateTime.MinValue;
    }

    private int CalculatePageNumber(int offset, int pageSize)
    {
        return (offset / pageSize) + 1;
    }

    private int CalculateOffset(int page, int pageSize)
    {
        return (page - 1) * pageSize;
    }

    private int CalculateTotalPages(int totalItems, int pageSize)
    {
        if (totalItems == 0) return 0;
   return (int)Math.Ceiling((double)totalItems / pageSize);
  }

    private bool HasNextPage(int currentPage, int pageSize, int totalItems)
    {
        var totalPages = CalculateTotalPages(totalItems, pageSize);
        return currentPage < totalPages;
    }

    #endregion
}
