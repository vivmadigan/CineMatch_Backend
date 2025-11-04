using FluentAssertions;
using Infrastructure.Services;
using Infrastructure.Tests.Helpers;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Advanced validation tests for PreferenceService with TMDB integration.
/// These are integration tests - testing validation logic with mocked external API.
/// GOAL: Ensure preference validation works correctly with TMDB genre data.
/// IMPORTANCE: HIGH PRIORITY - Invalid genres could break match algorithm.
/// </summary>
public class PreferenceServiceValidationTests
{
    #region TMDB Genre Validation Tests

    /// <summary>
    /// POSITIVE TEST: Verify valid TMDB genre IDs are accepted.
 /// GOAL: Service accepts genres that exist in TMDB.
    /// IMPORTANCE: Core functionality - users select from real TMDB genres.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithValidTmdbGenres_Succeeds()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
     var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

        // Act - Save with common TMDB genre IDs (Action=28, Comedy=35, Sci-Fi=878)
 var dto = new Infrastructure.Preferences.SavePreferenceDto
        {
            GenreIds = new List<int> { 28, 35, 878 },
            Length = "medium"
        };

        await service.SaveAsync(user.Id, dto, CancellationToken.None);

        // Assert
        var saved = await service.GetAsync(user.Id, CancellationToken.None);
        saved.GenreIds.Should().BeEquivalentTo(new[] { 28, 35, 878 });
    }

    /// <summary>
    /// NEGATIVE TEST: Verify negative genre IDs are rejected.
    /// GOAL: Invalid genre IDs throw ArgumentException.
    /// IMPORTANCE: Data integrity - prevents garbage data.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithNegativeGenreIds_ThrowsArgumentException()
    {
     // Arrange
     using var context = DbFixture.CreateContext();
  var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

        var dto = new Infrastructure.Preferences.SavePreferenceDto
    {
        GenreIds = new List<int> { -1, 28, 35 },
   Length = "medium"
    };

        // Act & Assert
        var act = async () => await service.SaveAsync(user.Id, dto, CancellationToken.None);
   await act.Should().ThrowAsync<ArgumentException>()
       .WithMessage("*positive integers*");
    }

    /// <summary>
    /// NEGATIVE TEST: Verify zero genre ID is rejected.
    /// GOAL: Zero is not a valid TMDB genre ID.
    /// IMPORTANCE: Data integrity.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithZeroGenreId_ThrowsArgumentException()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
   var service = new PreferenceService(context);

        var dto = new Infrastructure.Preferences.SavePreferenceDto
        {
    GenreIds = new List<int> { 0, 28 },
         Length = "medium"
        };

     // Act & Assert
        var act = async () => await service.SaveAsync(user.Id, dto, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
          .WithMessage("*positive integers*");
    }

    /// <summary>
    /// NEGATIVE TEST: Verify null DTO is rejected.
    /// GOAL: Null safety.
    /// IMPORTANCE: Prevents NullReferenceException.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithNullDto_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

        // Act & Assert
     var act = async () => await service.SaveAsync(user.Id, null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

  /// <summary>
    /// NEGATIVE TEST: Verify maximum genre count is enforced (50 genres).
    /// GOAL: Prevents unreasonably large preference lists.
    /// IMPORTANCE: Performance - too many genres slow down discover queries.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithTooManyGenres_ThrowsArgumentException()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

        // Create 51 genre IDs (over the limit of 50)
        var tooManyGenres = Enumerable.Range(1, 51).ToList();

        var dto = new Infrastructure.Preferences.SavePreferenceDto
        {
GenreIds = tooManyGenres,
            Length = "medium"
        };

        // Act & Assert
      var act = async () => await service.SaveAsync(user.Id, dto, CancellationToken.None);
      await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*50 genres*");
    }

    /// <summary>
    /// BOUNDARY TEST: Verify exactly 50 genres is accepted.
    /// GOAL: Boundary condition - at the limit should work.
    /// IMPORTANCE: Ensures limit is inclusive, not exclusive.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithExactly50Genres_Succeeds()
    {
        // Arrange
     using var context = DbFixture.CreateContext();
   var user = await DbFixture.CreateTestUserAsync(context);
  var service = new PreferenceService(context);

        var exactly50Genres = Enumerable.Range(1, 50).ToList();

        var dto = new Infrastructure.Preferences.SavePreferenceDto
        {
            GenreIds = exactly50Genres,
            Length = "medium"
        };

        // Act - Should not throw
        await service.SaveAsync(user.Id, dto, CancellationToken.None);

     // Assert
     var saved = await service.GetAsync(user.Id, CancellationToken.None);
        saved.GenreIds.Should().HaveCount(50);
    }

    #endregion

    #region Duplicate Genre Handling Tests

    /// <summary>
    /// POSITIVE TEST: Verify duplicate genre IDs are removed.
    /// GOAL: Service deduplicates genres automatically.
    /// IMPORTANCE: Better UX - users might accidentally select same genre twice.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithDuplicateGenres_RemovesDuplicates()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

        var dto = new Infrastructure.Preferences.SavePreferenceDto
        {
  GenreIds = new List<int> { 28, 28, 35, 28, 878, 35 }, // Duplicates
 Length = "medium"
        };

 // Act
        await service.SaveAsync(user.Id, dto, CancellationToken.None);

        // Assert - Only unique genres saved
        var saved = await service.GetAsync(user.Id, CancellationToken.None);
        saved.GenreIds.Should().BeEquivalentTo(new[] { 28, 35, 878 });
        saved.GenreIds.Should().HaveCount(3);
    }

    /// <summary>
 /// POSITIVE TEST: Verify duplicate removal doesn't affect order.
    /// GOAL: First occurrence of duplicate is kept.
    /// IMPORTANCE: Predictable behavior.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithDuplicates_PreservesFirstOccurrence()
    {
   // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

        var dto = new Infrastructure.Preferences.SavePreferenceDto
        {
            GenreIds = new List<int> { 28, 35, 28, 878 },
            Length = "medium"
        };

        // Act
        await service.SaveAsync(user.Id, dto, CancellationToken.None);

      // Assert - Order preserved (28 first, then 35, then 878)
        var saved = await service.GetAsync(user.Id, CancellationToken.None);
        saved.GenreIds.Should().ContainInOrder(28, 35, 878);
    }

  #endregion

    #region Length Validation Tests

    /// <summary>
    /// POSITIVE TEST: Verify all valid length values are accepted.
    /// GOAL: Service accepts "short", "medium", "long".
    /// IMPORTANCE: Core filtering feature.
    /// </summary>
    [Theory]
    [InlineData("short")]
    [InlineData("medium")]
    [InlineData("long")]
    public async Task SaveAsync_WithValidLengthKeys_Succeeds(string lengthKey)
    {
        // Arrange
        using var context = DbFixture.CreateContext();
      var user = await DbFixture.CreateTestUserAsync(context);
      var service = new PreferenceService(context);

        var dto = new Infrastructure.Preferences.SavePreferenceDto
 {
   GenreIds = new List<int> { 28 },
         Length = lengthKey
   };

        // Act
   await service.SaveAsync(user.Id, dto, CancellationToken.None);

        // Assert
        var saved = await service.GetAsync(user.Id, CancellationToken.None);
   saved.Length.Should().Be(lengthKey);
    }

    /// <summary>
    /// NEGATIVE TEST: Verify invalid length values are rejected.
    /// GOAL: Only predefined length keys are allowed.
    /// IMPORTANCE: Data integrity - prevents invalid filter values.
    /// </summary>
    [Theory]
    [InlineData("extra-long")]
    [InlineData("tiny")]
    [InlineData("invalid")]
    [InlineData("")]
    public async Task SaveAsync_WithInvalidLength_ThrowsArgumentOutOfRangeException(string invalidLength)
    {
 // Arrange
        using var context = DbFixture.CreateContext();
      var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

        var dto = new Infrastructure.Preferences.SavePreferenceDto
        {
         GenreIds = new List<int> { 28 },
      Length = invalidLength
        };

  // Act & Assert
        var act = async () => await service.SaveAsync(user.Id, dto, CancellationToken.None);
      await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
 }

    /// <summary>
    /// POSITIVE TEST: Verify case-insensitive length matching.
    /// GOAL: "MEDIUM", "Medium", "medium" all work.
    /// IMPORTANCE: Better UX - case shouldn't matter.
    /// </summary>
    [Theory]
    [InlineData("MEDIUM", "medium")]
    [InlineData("Medium", "medium")]
    [InlineData("MeDiUm", "medium")]
    [InlineData("SHORT", "short")]
    [InlineData("LONG", "long")]
    public async Task SaveAsync_WithMixedCaseLength_NormalizesToLowerCase(string input, string expected)
    {
        // Arrange
        using var context = DbFixture.CreateContext();
    var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

      var dto = new Infrastructure.Preferences.SavePreferenceDto
  {
            GenreIds = new List<int> { 28 },
     Length = input
        };

        // Act
        await service.SaveAsync(user.Id, dto, CancellationToken.None);

    // Assert
var saved = await service.GetAsync(user.Id, CancellationToken.None);
        saved.Length.Should().Be(expected);
    }

    #endregion

    #region Update Behavior Tests

    /// <summary>
    /// POSITIVE TEST: Verify updating preferences replaces old values.
    /// GOAL: SaveAsync is truly an upsert - overwrites existing.
    /// IMPORTANCE: Users should be able to change their preferences.
    /// </summary>
    [Fact]
    public async Task SaveAsync_UpdateExisting_ReplacesOldPreferences()
    {
        // Arrange
      using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
    var service = new PreferenceService(context);

        // Save initial preferences
        var initialDto = new Infrastructure.Preferences.SavePreferenceDto
        {
            GenreIds = new List<int> { 28, 35 },
            Length = "short"
        };
        await service.SaveAsync(user.Id, initialDto, CancellationToken.None);

   // Act - Update with completely different preferences
        var updatedDto = new Infrastructure.Preferences.SavePreferenceDto
        {
     GenreIds = new List<int> { 878, 12 },
            Length = "long"
        };
  await service.SaveAsync(user.Id, updatedDto, CancellationToken.None);

  // Assert - Old preferences gone, new ones saved
        var saved = await service.GetAsync(user.Id, CancellationToken.None);
     saved.GenreIds.Should().BeEquivalentTo(new[] { 878, 12 });
        saved.GenreIds.Should().NotContain(28);
        saved.GenreIds.Should().NotContain(35);
 saved.Length.Should().Be("long");
    }

    /// <summary>
    /// POSITIVE TEST: Verify UpdatedAt timestamp is set on save.
    /// GOAL: Track when preferences were last modified.
    /// IMPORTANCE: Useful for analytics and debugging.
    /// </summary>
    [Fact]
    public async Task SaveAsync_SetsUpdatedAtTimestamp()
 {
 // Arrange
        using var context = DbFixture.CreateContext();
     var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

   var beforeSave = DateTime.UtcNow;

        var dto = new Infrastructure.Preferences.SavePreferenceDto
        {
            GenreIds = new List<int> { 28 },
   Length = "medium"
      };

        // Act
        await service.SaveAsync(user.Id, dto, CancellationToken.None);

        var afterSave = DateTime.UtcNow;

        // Assert - UpdatedAt is between before/after timestamps
        var saved = await context.UserPreferences.FindAsync(user.Id);
        saved.Should().NotBeNull();
  saved!.UpdatedAt.Should().BeOnOrAfter(beforeSave);
        saved.UpdatedAt.Should().BeOnOrBefore(afterSave);
    }

    #endregion

    #region Empty Genre List Tests

    /// <summary>
/// POSITIVE TEST: Verify empty genre list is accepted.
    /// GOAL: Users can have no genre preferences (show all genres).
    /// IMPORTANCE: Valid use case - user wants to see everything.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithEmptyGenreList_Succeeds()
  {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
    var service = new PreferenceService(context);

        var dto = new Infrastructure.Preferences.SavePreferenceDto
      {
      GenreIds = new List<int>(),
            Length = "medium"
     };

        // Act
        await service.SaveAsync(user.Id, dto, CancellationToken.None);

// Assert
        var saved = await service.GetAsync(user.Id, CancellationToken.None);
        saved.GenreIds.Should().BeEmpty();
    }

    #endregion

    #region Default Preferences Tests

    /// <summary>
    /// POSITIVE TEST: Verify GetAsync returns defaults for new user.
    /// GOAL: Users who haven't set preferences get sensible defaults.
    /// IMPORTANCE: Better UX - app works immediately without setup.
    /// </summary>
    [Fact]
    public async Task GetAsync_NewUser_ReturnsDefaults()
    {
        // Arrange
    using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
      var service = new PreferenceService(context);

     // Act - Get preferences without saving any
        var prefs = await service.GetAsync(user.Id, CancellationToken.None);

        // Assert - Returns default values
        prefs.Should().NotBeNull();
        prefs.GenreIds.Should().BeEmpty();
        prefs.Length.Should().Be("medium");
    }

    #endregion

    #region Concurrency Tests

    /// <summary>
    /// CONCURRENCY TEST: Verify concurrent saves don't corrupt data.
    /// GOAL: Last write wins, no data corruption.
    /// IMPORTANCE: Multiple browser tabs might save simultaneously.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ConcurrentSaves_LastWriteWins()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
  var user = await DbFixture.CreateTestUserAsync(context);
 var service = new PreferenceService(context);

        // Act - Save 3 different preferences concurrently
        var tasks = new[]
        {
            service.SaveAsync(user.Id, new Infrastructure.Preferences.SavePreferenceDto
            {
          GenreIds = new List<int> { 28 },
                Length = "short"
     }, CancellationToken.None),

            service.SaveAsync(user.Id, new Infrastructure.Preferences.SavePreferenceDto
            {
         GenreIds = new List<int> { 35 },
       Length = "medium"
            }, CancellationToken.None),

     service.SaveAsync(user.Id, new Infrastructure.Preferences.SavePreferenceDto
     {
GenreIds = new List<int> { 878 },
      Length = "long"
  }, CancellationToken.None)
        };

        await Task.WhenAll(tasks);

        // Assert - One of them won (no corruption)
        var saved = await service.GetAsync(user.Id, CancellationToken.None);
        saved.GenreIds.Should().HaveCountLessOrEqualTo(1);
        new[] { "short", "medium", "long" }.Should().Contain(saved.Length);
}

    #endregion

    #region Data Isolation Tests

    /// <summary>
    /// DATA INTEGRITY TEST: Verify preferences are isolated between users.
    /// GOAL: User A's preferences don't affect User B.
    /// IMPORTANCE: CRITICAL SECURITY - Data leakage prevention.
    /// </summary>
    [Fact]
    public async Task SaveAndGet_IsolatedBetweenUsers()
    {
        // Arrange
    using var context = DbFixture.CreateContext();
      var user1 = await DbFixture.CreateTestUserAsync(context, email: "user1@test.com");
        var user2 = await DbFixture.CreateTestUserAsync(context, email: "user2@test.com");
        var service = new PreferenceService(context);

   // User 1 saves preferences
     await service.SaveAsync(user1.Id, new Infrastructure.Preferences.SavePreferenceDto
        {
  GenreIds = new List<int> { 28 },
        Length = "short"
    }, CancellationToken.None);

        // User 2 saves different preferences
        await service.SaveAsync(user2.Id, new Infrastructure.Preferences.SavePreferenceDto
        {
            GenreIds = new List<int> { 35, 878 },
         Length = "long"
        }, CancellationToken.None);

        // Act
        var user1Prefs = await service.GetAsync(user1.Id, CancellationToken.None);
    var user2Prefs = await service.GetAsync(user2.Id, CancellationToken.None);

      // Assert - Completely isolated
 user1Prefs.GenreIds.Should().BeEquivalentTo(new[] { 28 });
        user1Prefs.Length.Should().Be("short");

     user2Prefs.GenreIds.Should().BeEquivalentTo(new[] { 35, 878 });
        user2Prefs.Length.Should().Be("long");
    }

    #endregion

    #region Performance Tests

    /// <summary>
    /// PERFORMANCE TEST: Verify saving large genre list is fast.
    /// GOAL: 50 genres should save in < 100ms.
    /// IMPORTANCE: Ensures scalability.
    /// </summary>
  [Fact]
    public async Task SaveAsync_With50Genres_CompletesQuickly()
  {
// Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

        var dto = new Infrastructure.Preferences.SavePreferenceDto
        {
  GenreIds = Enumerable.Range(1, 50).ToList(),
         Length = "medium"
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await service.SaveAsync(user.Id, dto, CancellationToken.None);
      stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "saving 50 genres should be fast");
  }

    #endregion
}
