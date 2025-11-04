using FluentAssertions;
using Infrastructure.Services;
using Infrastructure.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Comprehensive tests for UserLikesService concurrency, idempotency, and edge cases.
/// These are integration tests - testing service with real database operations.
/// GOAL: Ensure likes are handled correctly under concurrent access and edge cases.
/// IMPORTANCE: HIGH PRIORITY - Likes are core to match algorithm.
/// </summary>
public class UserLikesServiceAdvancedTests
{
    #region Concurrency Tests

    /// <summary>
    /// CONCURRENCY TEST: Verify multiple concurrent likes for same movie are handled correctly.
    /// GOAL: Only one like record should exist after concurrent operations.
    /// IMPORTANCE: CRITICAL - Race conditions could create duplicate likes.
    /// </summary>
  [Fact]
  public async Task UpsertLike_ConcurrentSameMovie_CreatesOnlyOneLike()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        // Act - Simulate 5 concurrent like requests for same movie
        var tasks = Enumerable.Range(0, 5).Select(_ =>
      service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None)
        );

        await Task.WhenAll(tasks);

        // Assert - Only 1 like should exist
    var likes = await context.UserMovieLikes
            .Where(l => l.UserId == user.Id && l.TmdbId == 27205)
            .ToListAsync();

     likes.Should().ContainSingle("concurrent upserts should result in single like");
    }

    /// <summary>
    /// CONCURRENCY TEST: Verify concurrent likes for different movies work correctly.
    /// GOAL: All likes should be saved independently.
    /// IMPORTANCE: Ensures concurrency doesn't corrupt data.
    /// </summary>
    [Fact]
    public async Task UpsertLike_ConcurrentDifferentMovies_CreatesAllLikes()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        var movies = new[]
        {
            (27205, "Inception"),
          (238, "The Godfather"),
            (278, "The Shawshank Redemption"),
      (550, "Fight Club"),
            (680, "Pulp Fiction")
  };

   // Act - Like all movies concurrently
      var tasks = movies.Select(m =>
 service.UpsertLikeAsync(user.Id, m.Item1, m.Item2, "/poster.jpg", "2010", CancellationToken.None)
        );

await Task.WhenAll(tasks);

        // Assert - All 5 likes should exist
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);
   likes.Should().HaveCount(5, "all concurrent likes should be saved");
        likes.Select(l => l.TmdbId).Should().BeEquivalentTo(movies.Select(m => m.Item1));
    }

    /// <summary>
    /// CONCURRENCY TEST: Verify concurrent like/unlike operations are handled safely.
    /// GOAL: Final state should be consistent (either liked or not liked).
    /// IMPORTANCE: Race conditions between like/unlike could leave invalid state.
    /// </summary>
    [Fact]
    public async Task LikeAndUnlike_Concurrent_ResultsInConsistentState()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        // Pre-seed a like
        await service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);

        // Act - Run 10 concurrent like/unlike operations
        var tasks = new List<Task>();
     for (int i = 0; i < 10; i++)
        {
            if (i % 2 == 0)
  {
     tasks.Add(service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None));
            }
            else
            {
            tasks.Add(service.RemoveLikeAsync(user.Id, 27205, CancellationToken.None));
      }
        }

        await Task.WhenAll(tasks);

        // Assert - Like either exists or doesn't (no partial state)
        var likes = await context.UserMovieLikes
         .Where(l => l.UserId == user.Id && l.TmdbId == 27205)
            .ToListAsync();

    likes.Should().HaveCountLessOrEqualTo(1, "should have 0 or 1 like, never multiple");
    }

    #endregion

    #region Idempotency Tests

    /// <summary>
    /// IDEMPOTENCY TEST: Verify liking same movie multiple times creates only one record.
    /// GOAL: Upsert semantics - subsequent likes update existing record.
    /// IMPORTANCE: Prevents duplicate likes from UI double-clicks or network retries.
    /// </summary>
    [Fact]
    public async Task UpsertLike_SameMovieMultipleTimes_CreatesOnlyOneLike()
    {
        // Arrange
   using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        // Act - Like same movie 3 times
     await service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);
     await service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);
    await service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);

        // Assert
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);
   likes.Should().ContainSingle(l => l.TmdbId == 27205);
    }

    /// <summary>
/// IDEMPOTENCY TEST: Verify unliking non-existent like doesn't throw error.
    /// GOAL: Unlike is idempotent - safe to call multiple times.
    /// IMPORTANCE: UI shouldn't crash if user clicks unlike twice.
    /// </summary>
    [Fact]
    public async Task RemoveLike_NonExistentLike_DoesNotThrow()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        // Act - Unlike movie that was never liked
        var act = async () => await service.RemoveLikeAsync(user.Id, 27205, CancellationToken.None);

        // Assert - Should not throw
        await act.Should().NotThrowAsync();

        // Verify no likes exist
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);
     likes.Should().BeEmpty();
    }

    /// <summary>
    /// IDEMPOTENCY TEST: Verify like ? unlike ? like sequence works correctly.
    /// GOAL: User can change their mind multiple times.
    /// IMPORTANCE: Common user workflow.
    /// </summary>
    [Fact]
    public async Task LikeUnlikeLike_Sequence_WorksCorrectly()
    {
     // Arrange
      using var context = DbFixture.CreateContext();
     var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        // Act - Like, unlike, like again
        await service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);
        var likesAfterFirst = await service.GetLikesAsync(user.Id, CancellationToken.None);

        await service.RemoveLikeAsync(user.Id, 27205, CancellationToken.None);
        var likesAfterUnlike = await service.GetLikesAsync(user.Id, CancellationToken.None);

        await service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);
        var likesAfterSecond = await service.GetLikesAsync(user.Id, CancellationToken.None);

        // Assert
        likesAfterFirst.Should().ContainSingle();
        likesAfterUnlike.Should().BeEmpty();
        likesAfterSecond.Should().ContainSingle();
likesAfterSecond.First().TmdbId.Should().Be(27205);
    }

    #endregion

    #region Performance Tests

    /// <summary>
    /// PERFORMANCE TEST: Verify service handles large number of likes efficiently.
    /// GOAL: User with 1000 likes shouldn't experience slow performance.
    /// IMPORTANCE: Scalability - some users might like many movies.
    /// </summary>
    [Fact]
    public async Task GetLikes_With1000Likes_CompletesQuickly()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        // Create 1000 likes
  for (int i = 1; i <= 1000; i++)
        {
     await service.UpsertLikeAsync(user.Id, i, $"Movie {i}", $"/poster{i}.jpg", "2020", CancellationToken.None);
        }

        // Act - Measure time to retrieve all likes
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        likes.Should().HaveCount(1000);
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "retrieving 1000 likes should take < 1 second");
    }

    /// <summary>
    /// PERFORMANCE TEST: Verify batch unlike operations are efficient.
    /// GOAL: Unliking multiple movies shouldn't cause performance issues.
    /// IMPORTANCE: User might want to clear their entire like history.
    /// </summary>
    [Fact]
    public async Task RemoveLike_100Movies_CompletesQuickly()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        // Create 100 likes
        for (int i = 1; i <= 100; i++)
    {
    await service.UpsertLikeAsync(user.Id, i, $"Movie {i}", $"/poster{i}.jpg", "2020", CancellationToken.None);
        }

        // Act - Unlike all movies
   var stopwatch = System.Diagnostics.Stopwatch.StartNew();
  for (int i = 1; i <= 100; i++)
        {
            await service.RemoveLikeAsync(user.Id, i, CancellationToken.None);
        }
    stopwatch.Stop();

        // Assert
    var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);
        likes.Should().BeEmpty();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "unliking 100 movies should take < 2 seconds");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// EDGE CASE TEST: Verify user with no likes returns empty list.
    /// GOAL: GetLikes handles empty state gracefully.
    /// IMPORTANCE: Common initial state for new users.
    /// </summary>
    [Fact]
    public async Task GetLikes_NewUser_ReturnsEmptyList()
 {
// Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        // Act
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);

     // Assert
        likes.Should().BeEmpty();
        likes.Should().NotBeNull();
    }

    /// <summary>
    /// EDGE CASE TEST: Verify likes are ordered by creation time (newest first).
    /// GOAL: User sees their most recent likes first.
    /// IMPORTANCE: Better UX - recent activity is more relevant.
    /// </summary>
    [Fact]
    public async Task GetLikes_ReturnsNewestFirst()
    {
  // Arrange
   using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

 // Like movies in specific order
        await service.UpsertLikeAsync(user.Id, 1, "Movie 1", "/poster1.jpg", "2020", CancellationToken.None);
   await Task.Delay(100); // Ensure different timestamps
        await service.UpsertLikeAsync(user.Id, 2, "Movie 2", "/poster2.jpg", "2020", CancellationToken.None);
        await Task.Delay(100);
        await service.UpsertLikeAsync(user.Id, 3, "Movie 3", "/poster3.jpg", "2020", CancellationToken.None);

        // Act
  var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);

        // Assert - Most recent (Movie 3) should be first
        likes.Should().HaveCount(3);
        likes.First().TmdbId.Should().Be(3, "newest like should be first");
      likes.Last().TmdbId.Should().Be(1, "oldest like should be last");
    }

    /// <summary>
    /// EDGE CASE TEST: Verify empty title is handled gracefully.
    /// GOAL: Service doesn't crash with missing movie data.
  /// IMPORTANCE: TMDB API might return incomplete data.
    /// </summary>
    [Fact]
    public async Task UpsertLike_WithEmptyTitle_Succeeds()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
  var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

   // Act - Like with empty title
        await service.UpsertLikeAsync(user.Id, 27205, "", "/poster.jpg", "2010", CancellationToken.None);

        // Assert
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);
        likes.Should().ContainSingle();
        likes.First().Title.Should().BeEmpty();
    }

    /// <summary>
    /// EDGE CASE TEST: Verify null poster path is handled gracefully.
    /// GOAL: Missing posters don't break functionality.
    /// IMPORTANCE: Not all movies have posters.
/// </summary>
    [Fact]
    public async Task UpsertLike_WithNullPosterPath_Succeeds()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

    // Act - Like with null poster
        await service.UpsertLikeAsync(user.Id, 27205, "Inception", null, "2010", CancellationToken.None);

     // Assert
    var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);
        likes.Should().ContainSingle();
        likes.First().PosterPath.Should().BeNull();
    }

    /// <summary>
    /// EDGE CASE TEST: Verify null release year is handled gracefully.
    /// GOAL: Movies without release dates still work.
    /// IMPORTANCE: Older or unreleased movies might lack this data.
    /// </summary>
    [Fact]
    public async Task UpsertLike_WithNullReleaseYear_Succeeds()
    {
      // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
   var service = new UserLikesService(context);

  // Act - Like with null year
        await service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", null, CancellationToken.None);

        // Assert
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);
        likes.Should().ContainSingle();
        likes.First().ReleaseYear.Should().BeNull();
}

    /// <summary>
    /// EDGE CASE TEST: Verify very long title (500 chars) is handled.
    /// GOAL: Titles are truncated or stored correctly.
    /// IMPORTANCE: Defensive programming against malformed TMDB data.
    /// </summary>
[Fact]
    public async Task UpsertLike_WithVeryLongTitle_Succeeds()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
   var service = new UserLikesService(context);

        var longTitle = new string('A', 256); // Max length per schema

        // Act
        await service.UpsertLikeAsync(user.Id, 27205, longTitle, "/poster.jpg", "2010", CancellationToken.None);

        // Assert
    var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);
        likes.Should().ContainSingle();
        likes.First().Title.Length.Should().BeLessOrEqualTo(256);
    }

#endregion

    #region Data Integrity Tests

    /// <summary>
    /// DATA INTEGRITY TEST: Verify likes are isolated between users.
    /// GOAL: User A's likes don't appear in User B's list.
    /// IMPORTANCE: CRITICAL SECURITY - Data leakage prevention.
    /// </summary>
    [Fact]
    public async Task GetLikes_IsolatedBetweenUsers()
    {
 // Arrange
        using var context = DbFixture.CreateContext();
   var user1 = await DbFixture.CreateTestUserAsync(context, email: "user1@test.com");
  var user2 = await DbFixture.CreateTestUserAsync(context, email: "user2@test.com");
        var service = new UserLikesService(context);

        // User 1 likes Movie 1
    await service.UpsertLikeAsync(user1.Id, 1, "Movie 1", "/poster1.jpg", "2020", CancellationToken.None);

        // User 2 likes Movie 2
  await service.UpsertLikeAsync(user2.Id, 2, "Movie 2", "/poster2.jpg", "2020", CancellationToken.None);

  // Act
        var user1Likes = await service.GetLikesAsync(user1.Id, CancellationToken.None);
        var user2Likes = await service.GetLikesAsync(user2.Id, CancellationToken.None);

        // Assert
        user1Likes.Should().ContainSingle(l => l.TmdbId == 1);
        user1Likes.Should().NotContain(l => l.TmdbId == 2);

        user2Likes.Should().ContainSingle(l => l.TmdbId == 2);
  user2Likes.Should().NotContain(l => l.TmdbId == 1);
    }

    /// <summary>
    /// DATA INTEGRITY TEST: Verify unlike doesn't affect other users' likes.
    /// GOAL: User A unliking Movie X doesn't remove User B's like for Movie X.
    /// IMPORTANCE: CRITICAL - Ensure data integrity in multi-user scenarios.
    /// </summary>
    [Fact]
    public async Task RemoveLike_DoesNotAffectOtherUsers()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
      var user1 = await DbFixture.CreateTestUserAsync(context, email: "user1@test.com");
        var user2 = await DbFixture.CreateTestUserAsync(context, email: "user2@test.com");
        var service = new UserLikesService(context);

      // Both users like same movie
        await service.UpsertLikeAsync(user1.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);
        await service.UpsertLikeAsync(user2.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);

      // Act - User 1 unlikes
        await service.RemoveLikeAsync(user1.Id, 27205, CancellationToken.None);

        // Assert - User 2 still has like
        var user1Likes = await service.GetLikesAsync(user1.Id, CancellationToken.None);
     var user2Likes = await service.GetLikesAsync(user2.Id, CancellationToken.None);

  user1Likes.Should().BeEmpty();
     user2Likes.Should().ContainSingle(l => l.TmdbId == 27205);
    }

    #endregion
}
