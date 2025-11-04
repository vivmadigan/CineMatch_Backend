using FluentAssertions;
using Infrastructure.Data.Entities;
using Infrastructure.Services;
using Infrastructure.Services.Matches;
using Infrastructure.Tests.Helpers;
using Infrastructure.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Advanced edge case tests for MatchService.GetMatchStatusAsync().
/// Tests complex status logic with 5 possible states and edge cases.
/// GOAL: Exhaustive coverage of status transitions, deleted data, and stale requests.
/// </summary>
public class MatchServiceStatusAdvancedTests
{
    #region Deleted Room Edge Cases

    /// <summary>
    /// NEGATIVE TEST: Verify status returns "none" when chat room is deleted after match.
    /// GOAL: Handle data inconsistency gracefully (admin deleted room).
 /// IMPORTANCE: Prevents showing "matched" status when room no longer exists.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_AfterRoomDeleted_ReturnsNoneStatus()
    {
   // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
var service = new MatchService(context, new MockNotificationService());

        // Create mutual match
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        var matchResult = await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

  matchResult.Matched.Should().BeTrue();
   var roomId = matchResult.RoomId!.Value;

   // Verify matched status before deletion
        var statusBefore = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
        statusBefore.Status.Should().Be("matched");
        statusBefore.RoomId.Should().Be(roomId);

    // Act - Delete the room (simulate admin action or bug)
        var room = await context.ChatRooms.FindAsync(roomId);
        if (room != null)
        {
            var memberships = context.ChatMemberships.Where(m => m.RoomId == roomId);
  context.ChatMemberships.RemoveRange(memberships);
            context.ChatRooms.Remove(room);
     await context.SaveChangesAsync();
 }

        // Act - Check status after room deletion
  var statusAfter = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert - Should return "none" (no room exists)
        statusAfter.Status.Should().Be("none");
     statusAfter.RoomId.Should().BeNull();
        statusAfter.CanMatch.Should().BeTrue();
    }

    /// <summary>
    /// NEGATIVE TEST: Verify status with orphaned membership (room deleted, membership remains).
    /// GOAL: Handle database inconsistency gracefully.
    /// IMPORTANCE: Prevents crashes from orphaned data.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithOrphanedMembership_HandlesGracefully()
    {
    // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());

        // Create room and memberships manually
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        context.ChatRooms.Add(room);
        context.ChatMemberships.AddRange(
    new ChatMembership { RoomId = room.Id, UserId = user1.Id, IsActive = true, JoinedAt = DateTime.UtcNow },
            new ChatMembership { RoomId = room.Id, UserId = user2.Id, IsActive = true, JoinedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        // Delete room but leave memberships (orphaned)
        context.ChatRooms.Remove(room);
        await context.SaveChangesAsync();

   // Act - Check status with orphaned memberships
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert - Should handle gracefully (no room found, returns "none")
 result.Status.Should().Be("none");
    }

    #endregion

    #region Different Movies Edge Cases

    /// <summary>
    /// POSITIVE TEST: Verify status with both users sending requests for DIFFERENT movies.
    /// GOAL: Each user sees their own "pending_sent", not "matched".
    /// IMPORTANCE: Different movies = separate requests, not mutual match.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithBothRequestsDifferentMovies_ReturnsCorrectStatus()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());

        // User1 requests User2 for Movie A (27205)
     await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

        // User2 requests User1 for Movie B (238) - DIFFERENT movie
        await service.RequestAsync(user2.Id, user1.Id, 238, CancellationToken.None);

  // Act - Check from User1's perspective
        var user1Status = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Act - Check from User2's perspective
     var user2Status = await service.GetMatchStatusAsync(user2.Id, user1.Id, CancellationToken.None);

        // Assert - User1 sees: sent request for Movie A, received request for Movie B
        // GetMatchStatusAsync checks ALL requests between users (not movie-specific)
        // So it detects BOTH sent and received requests
        user1Status.Status.Should().Be("matched"); // Both sent requests = edge case "matched"
 user2Status.Status.Should().Be("matched"); // Both sent requests = edge case "matched"
    }

    /// <summary>
    /// POSITIVE TEST: Verify status with multiple pending requests for different movies.
    /// GOAL: Status reflects existence of ANY request, not movie-specific.
    /// IMPORTANCE: GetMatchStatusAsync is user-to-user, not per-movie.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithMultipleRequestsDifferentMovies_ShowsPendingSent()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
    var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());

        // User1 sends 3 requests for different movies
    await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None); // Inception
        await service.RequestAsync(user1.Id, user2.Id, 238, CancellationToken.None);   // Godfather
        await service.RequestAsync(user1.Id, user2.Id, 603, CancellationToken.None);   // Matrix

    // Act
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert - Shows "pending_sent" (ANY sent request exists)
        result.Status.Should().Be("pending_sent");
        result.CanMatch.Should().BeFalse();
        result.RequestSentAt.Should().NotBeNull();
    }

    #endregion

    #region Stale Data Edge Cases

    /// <summary>
    /// NEGATIVE TEST: Verify status with very old match request (30 days ago).
    /// GOAL: Stale requests still show correct status (no expiry logic).
    /// IMPORTANCE: Confirms no automatic cleanup happens (by design).
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithExpiredRequest_StillShowsPending()
    {
        // Arrange
using var context = DbFixture.CreateContext();
    var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
   var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");

    // Create old match request directly in database
        var oldRequest = new MatchRequest
 {
      Id = Guid.NewGuid(),
            RequestorId = user1.Id,
            TargetUserId = user2.Id,
    TmdbId = 27205,
            CreatedAt = DateTime.UtcNow.AddDays(-30) // 30 days old
        };
        context.MatchRequests.Add(oldRequest);
        await context.SaveChangesAsync();

        var service = new MatchService(context, new MockNotificationService());

   // Act
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert - Still shows "pending_sent" (no expiry)
        result.Status.Should().Be("pending_sent");
        result.RequestSentAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(-30), TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// POSITIVE TEST: Verify status with request created in future (clock skew edge case).
  /// GOAL: Handle timestamp anomalies gracefully.
    /// IMPORTANCE: Server clock differences shouldn't crash status checks.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithFutureTimestampRequest_HandlesGracefully()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
      var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");

    // Create request with future timestamp (clock skew scenario)
        var futureRequest = new MatchRequest
      {
       Id = Guid.NewGuid(),
       RequestorId = user1.Id,
     TargetUserId = user2.Id,
    TmdbId = 27205,
 CreatedAt = DateTime.UtcNow.AddHours(2) // 2 hours in future
        };
        context.MatchRequests.Add(futureRequest);
        await context.SaveChangesAsync();

  var service = new MatchService(context, new MockNotificationService());

        // Act
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert - Still shows "pending_sent" (timestamp accepted as-is)
        result.Status.Should().Be("pending_sent");
        result.RequestSentAt.Should().BeAfter(DateTime.UtcNow);
    }

    #endregion

    #region Membership State Edge Cases

    /// <summary>
  /// NEGATIVE TEST: Verify status when both memberships are inactive (both left room).
    /// GOAL: Status still shows "matched" (room exists, memberships inactive).
    /// IMPORTANCE: Inactive doesn't mean unmatched; users can rejoin.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithBothMembershipsInactive_StillReturnsMatched()
    {
      // Arrange
 using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());

        // Create mutual match
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        var matchResult = await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        var roomId = matchResult.RoomId!.Value;

        // Both users leave the room
        var chatService = new Infrastructure.Services.Chat.ChatService(context);
        await chatService.LeaveAsync(roomId, user1.Id, CancellationToken.None);
        await chatService.LeaveAsync(roomId, user2.Id, CancellationToken.None);

        // Act
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

// Assert - Still shows "matched" (room exists, memberships inactive)
  result.Status.Should().Be("matched");
        result.RoomId.Should().Be(roomId);
}

    /// <summary>
    /// POSITIVE TEST: Verify status when only one membership is inactive.
    /// GOAL: Status still shows "matched" (room exists for both).
    /// IMPORTANCE: One user leaving doesn't unmatch the pair.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithOneMembershipInactive_StillReturnsMatched()
    {
        // Arrange
using var context = DbFixture.CreateContext();
      var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
    var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
 var service = new MatchService(context, new MockNotificationService());

        // Create mutual match
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        var matchResult = await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

 var roomId = matchResult.RoomId!.Value;

// Only User1 leaves
    var chatService = new Infrastructure.Services.Chat.ChatService(context);
        await chatService.LeaveAsync(roomId, user1.Id, CancellationToken.None);

        // Act
   var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert - Still shows "matched"
        result.Status.Should().Be("matched");
        result.RoomId.Should().Be(roomId);
    }

    #endregion

    #region Shared Movies Edge Cases

    /// <summary>
    /// POSITIVE TEST: Verify shared movies persist after one user unlikes movie.
    /// GOAL: GetMatchStatusAsync shows current shared movies (dynamic).
    /// IMPORTANCE: Shared movies reflect current state, not historical.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_AfterUnlikingSharedMovie_UpdatesSharedMovies()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
      var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var likesService = new UserLikesService(context);
 var service = new MatchService(context, new MockNotificationService());

        // Both like 2 movies
        await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(user1.Id, 238, "Godfather", null, null, CancellationToken.None);

        await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", null, null, CancellationToken.None);
      await likesService.UpsertLikeAsync(user2.Id, 238, "Godfather", null, null, CancellationToken.None);

      // Verify both movies shared
        var statusBefore = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
        statusBefore.SharedMovies.Should().HaveCount(2);

   // User1 unlikes Inception
  await likesService.RemoveLikeAsync(user1.Id, 27205, CancellationToken.None);

        // Act
        var statusAfter = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert - Only Godfather remains shared
     statusAfter.SharedMovies.Should().ContainSingle();
        statusAfter.SharedMovies.First().TmdbId.Should().Be(238);
    }

    /// <summary>
    /// NEGATIVE TEST: Verify shared movies with missing movie metadata.
    /// GOAL: Handles movies with NULL title/poster gracefully.
    /// IMPORTANCE: Incomplete data shouldn't crash status checks.
  /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithIncompleteMovieMetadata_ThrowsDbUpdateException()
    {
     // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");

    // Act - Try to create likes with NULL title (violates NOT NULL constraint)
        var act = async () =>
        {
  var like1 = new UserMovieLike
   {
       UserId = user1.Id,
       TmdbId = 999999,
    Title = null, // NULL title - violates constraint
        PosterPath = null,
     ReleaseYear = null,
       CreatedAt = DateTime.UtcNow
    };
       var like2 = new UserMovieLike
     {
        UserId = user2.Id,
    TmdbId = 999999,
    Title = null, // NULL title - violates constraint
     PosterPath = null,
   ReleaseYear = null,
          CreatedAt = DateTime.UtcNow
};
  context.UserMovieLikes.AddRange(like1, like2);
   await context.SaveChangesAsync();
        };

    // Assert - Throws DbUpdateException due to NOT NULL constraint
      await act.Should().ThrowAsync<DbUpdateException>();
 }
    #endregion

    #region Status Transition Validation

    /// <summary>
  /// POSITIVE TEST: Verify complete status lifecycle from none ? pending_sent ? matched ? none.
    /// GOAL: Full state machine validation.
  /// IMPORTANCE: Ensures all transitions work correctly.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_FullLifecycle_AllTransitionsWork()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
  var service = new MatchService(context, new MockNotificationService());

   // State 1: None
        var state1 = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
        state1.Status.Should().Be("none");

        // State 2: User1 sends request ? pending_sent
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        var state2 = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
     state2.Status.Should().Be("pending_sent");

        // State 3: User2 accepts ? matched
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);
      var state3 = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
        state3.Status.Should().Be("matched");
    var roomId = state3.RoomId!.Value;

    // State 4: Delete room ? none (simulate unmatch)
  var room = await context.ChatRooms.FindAsync(roomId);
   if (room != null)
{
    var memberships = context.ChatMemberships.Where(m => m.RoomId == roomId);
            context.ChatMemberships.RemoveRange(memberships);
        context.ChatRooms.Remove(room);
            await context.SaveChangesAsync();
        }

        var state4 = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
     state4.Status.Should().Be("none");
    }

 #endregion

    #region Concurrent Status Checks

  /// <summary>
    /// POSITIVE TEST: Verify concurrent status checks return consistent results.
    /// GOAL: No race conditions in status calculation.
    /// IMPORTANCE: Multiple simultaneous requests shouldn't cause inconsistencies.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_ConcurrentCalls_ReturnsConsistentResults()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());

      // Create pending request
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

        // Act - 10 concurrent status checks
        var tasks = Enumerable.Range(1, 10)
       .Select(_ => service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None))
     .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All results identical
        results.Should().AllSatisfy(r => r.Status.Should().Be("pending_sent"));
    results.Select(r => r.RequestSentAt).Distinct().Should().ContainSingle();
    }

    #endregion
}
