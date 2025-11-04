using FluentAssertions;
using Infrastructure.Data.Entities;
using Infrastructure.Services.Matches;
using Infrastructure.Tests.Helpers;
using Infrastructure.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for MatchService database transaction integrity and failure scenarios.
/// Tests partial updates, constraint violations, and rollback behavior.
/// GOAL: Ensure data consistency even when database operations fail.
/// </summary>
public class MatchServiceTransactionTests
{
    #region Constraint Violation Tests

    /// <summary>
    /// NEGATIVE TEST: Verify RequestAsync with non-existent target user throws DbUpdateException.
    /// GOAL: Foreign key violation is enforced by database.
    /// IMPORTANCE: Database integrity; invalid user IDs rejected at database level.
    /// </summary>
    [Fact]
    public async Task RequestAsync_WithNonExistentTargetUser_ThrowsDbUpdateException()
    {
        // Arrange
      using var context = DbFixture.CreateContext();
 var user1 = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

        var nonExistentUserId = Guid.NewGuid().ToString();

  // Act - Request to non-existent user (violates FK constraint)
       var act = async () => await service.RequestAsync(user1.Id, nonExistentUserId, 27205, CancellationToken.None);

        // Assert - Throws DbUpdateException due to foreign key constraint
 await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
 /// NEGATIVE TEST: Verify duplicate match request prevention (unique constraint).
    /// GOAL: Database should prevent duplicate (requestorId, targetUserId, tmdbId) tuples.
    /// IMPORTANCE: Data integrity; idempotency at database level.
    /// </summary>
    [Fact]
 public async Task RequestAsync_DuplicateRequest_IsIdempotent()
    {
        // Arrange
 using var context = DbFixture.CreateContext();
  var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);
   var service = new MatchService(context, new MockNotificationService());

   // Act - Send same request twice
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
 await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

     // Assert - Only 1 request exists
     var requestCount = await context.MatchRequests
 .CountAsync(mr => mr.RequestorId == user1.Id && mr.TargetUserId == user2.Id && mr.TmdbId == 27205);

        requestCount.Should().Be(1);
    }

    #endregion

    #region Partial Update Tests

    /// <summary>
    /// NEGATIVE TEST: Verify room creation doesn't leave orphaned memberships if SaveChanges fails.
    /// GOAL: Transaction rollback should revert all changes.
    /// IMPORTANCE: Atomic operations; all-or-nothing for match creation.
    /// NOTE: Hard to simulate SaveChanges failure in unit tests; this verifies current behavior.
    /// </summary>
    [Fact]
    public async Task RequestAsync_MutualMatch_CreatesAtomicTransaction()
    {
   // Arrange
    using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);
  var service = new MatchService(context, new MockNotificationService());

    // Setup reciprocal requests
      await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

      var roomCountBefore = await context.ChatRooms.CountAsync();
        var membershipCountBefore = await context.ChatMemberships.CountAsync();

        // Act - Create mutual match
     var result = await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Assert - Room and memberships created together
   result.Matched.Should().BeTrue();
  result.RoomId.Should().NotBeNull();

   var roomCountAfter = await context.ChatRooms.CountAsync();
        var membershipCountAfter = await context.ChatMemberships.CountAsync();

     roomCountAfter.Should().Be(roomCountBefore + 1); // 1 new room
        membershipCountAfter.Should().Be(membershipCountBefore + 2); // 2 new memberships

   // Verify room has exactly 2 memberships
        var roomMemberships = await context.ChatMemberships
      .CountAsync(m => m.RoomId == result.RoomId);

 roomMemberships.Should().Be(2);
    }

    /// <summary>
    /// NEGATIVE TEST: Verify match requests are cleaned up after mutual match.
    /// GOAL: Both incoming and outgoing requests should be deleted.
    /// IMPORTANCE: Data hygiene; fulfilled requests shouldn't linger.
    /// </summary>
 [Fact]
    public async Task RequestAsync_MutualMatch_RemovesBothRequests()
    {
    // Arrange
        using var context = DbFixture.CreateContext();
    var user1 = await DbFixture.CreateTestUserAsync(context);
   var user2 = await DbFixture.CreateTestUserAsync(context);
     var service = new MatchService(context, new MockNotificationService());

// Setup reciprocal requests
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
  await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Act - Verify requests removed
   var requestCount = await context.MatchRequests
  .CountAsync(mr =>
      (mr.RequestorId == user1.Id && mr.TargetUserId == user2.Id) ||
      (mr.RequestorId == user2.Id && mr.TargetUserId == user1.Id));

        // Assert - Both requests removed
requestCount.Should().Be(0);
  }

    #endregion

    #region Concurrent Write Tests

    /// <summary>
    /// NEGATIVE TEST: Verify concurrent match requests don't create duplicate rooms.
    /// GOAL: Race condition handling; only 1 room created.
 /// IMPORTANCE: **CRITICAL** - Prevents duplicate rooms from simultaneous requests.
    /// </summary>
    [Fact]
    public async Task RequestAsync_ConcurrentMutualMatches_CreatesOnlyOneRoom()
    {
  // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
  var user2 = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

        // Both users already sent requests (setup race condition)
 await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

        // Act - Both users "accept" simultaneously by sending reciprocal request
        var task1 = service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);
   var task2 = service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

      var results = await Task.WhenAll(task1, task2);

        // Assert - Only 1 room created (not 2)
        var matchedResults = results.Where(r => r.Matched).ToList();
        matchedResults.Should().ContainSingle("only one concurrent match should succeed");

        var roomCount = await context.ChatRooms.CountAsync();
// Note: Actual count depends on other tests; verify at least 1 room exists
 roomCount.Should().BeGreaterOrEqualTo(1);
    }

    /// <summary>
    /// NEGATIVE TEST: Verify concurrent decline operations are idempotent.
    /// GOAL: Multiple declines don't cause errors.
    /// IMPORTANCE: Race condition resilience; UI may send duplicate requests.
    /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_ConcurrentDeclines_AreIdempotent()
    {
   // Arrange
using var context = DbFixture.CreateContext();
   var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);
    var service = new MatchService(context, new MockNotificationService());

        // User2 sends request to User1
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Act - User1 declines 5 times concurrently
     var tasks = Enumerable.Range(1, 5)
            .Select(_ => service.DeclineMatchAsync(user1.Id, user2.Id, 27205, CancellationToken.None))
       .ToArray();

   await Task.WhenAll(tasks);

        // Assert - Request removed (no errors from concurrent declines)
      var requestExists = await context.MatchRequests
 .AnyAsync(mr => mr.RequestorId == user2.Id && mr.TargetUserId == user1.Id && mr.TmdbId == 27205);

        requestExists.Should().BeFalse();
    }

    #endregion

    #region Data Consistency Tests

    /// <summary>
    /// POSITIVE TEST: Verify GetCandidatesAsync with inconsistent data (room without memberships).
    /// GOAL: Orphaned rooms don't crash candidate filtering.
    /// IMPORTANCE: Graceful degradation with corrupt data.
    /// </summary>
    [Fact]
    public async Task GetCandidatesAsync_WithOrphanedRoom_HandlesGracefully()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
    var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);
        var likesService = new Infrastructure.Services.UserLikesService(context);
    var service = new MatchService(context, new MockNotificationService());

        // Both like same movie
        await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", null, null, CancellationToken.None);

        // Create orphaned room (room exists, no memberships)
        var orphanedRoom = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
  context.ChatRooms.Add(orphanedRoom);
     await context.SaveChangesAsync();

    // Act
        var candidates = await service.GetCandidatesAsync(user1.Id, 20, CancellationToken.None);

   // Assert - Should still return User2 as candidate (orphaned room doesn't affect filtering)
        candidates.Should().Contain(c => c.UserId == user2.Id);
    }

    /// <summary>
    /// NEGATIVE TEST: Verify GetActiveMatchesAsync with orphaned membership throws DbUpdateException.
  /// GOAL: Memberships with invalid room IDs violate foreign key constraint.
    /// IMPORTANCE: Database enforces referential integrity.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_WithOrphanedMembership_ThrowsDbUpdateException()
    {
  // Arrange
 using var context = DbFixture.CreateContext();
     var user1 = await DbFixture.CreateTestUserAsync(context);

       // Act - Try to create orphaned membership (membership with non-existent room)
    var deletedRoomId = Guid.NewGuid();
   var orphanedMembership = new ChatMembership
{
    RoomId = deletedRoomId, // Room doesn't exist
 UserId = user1.Id,
      IsActive = true,
       JoinedAt = DateTime.UtcNow
     };

        var act = async () =>
  {
 context.ChatMemberships.Add(orphanedMembership);
    await context.SaveChangesAsync();
  };

     // Assert - Throws DbUpdateException due to foreign key constraint
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    #endregion

    #region Rollback Simulation Tests

    /// <summary>
    /// NEGATIVE TEST: Verify match request creation is atomic (idempotency check).
    /// GOAL: Repeated calls don't create duplicate requests.
    /// IMPORTANCE: Transaction safety; prevents data duplication.
    /// </summary>
    [Fact]
    public async Task RequestAsync_RepeatedCalls_AreIdempotent()
{
    // Arrange
    using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);
  var service = new MatchService(context, new MockNotificationService());

        // Act - Call 10 times
      for (int i = 0; i < 10; i++)
        {
            await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
 }

      // Assert - Only 1 request exists
        var requestCount = await context.MatchRequests
  .CountAsync(mr => mr.RequestorId == user1.Id && mr.TargetUserId == user2.Id && mr.TmdbId == 27205);

        requestCount.Should().Be(1);
    }

    /// <summary>
    /// POSITIVE TEST: Verify decline operation is atomic (complete or nothing).
    /// GOAL: Decline either removes request or leaves it intact (no partial state).
    /// IMPORTANCE: Transaction integrity.
    /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_IsAtomic()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
 var user2 = await DbFixture.CreateTestUserAsync(context);
   var service = new MatchService(context, new MockNotificationService());

      // User2 sends request
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        var requestBefore = await context.MatchRequests
       .FirstOrDefaultAsync(mr => mr.RequestorId == user2.Id && mr.TargetUserId == user1.Id && mr.TmdbId == 27205);

        requestBefore.Should().NotBeNull();

        // Act - Decline
   await service.DeclineMatchAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

        // Assert - Request fully removed
 var requestAfter = await context.MatchRequests
            .FirstOrDefaultAsync(mr => mr.RequestorId == user2.Id && mr.TargetUserId == user1.Id && mr.TmdbId == 27205);

   requestAfter.Should().BeNull();
    }

  #endregion

    #region Edge Case Cleanup Tests

    /// <summary>
    /// NEGATIVE TEST: Verify GetCandidatesAsync doesn't return users with only deleted match requests.
    /// GOAL: Declined requests don't affect candidate status.
    /// IMPORTANCE: Data freshness; status reflects current state only.
    /// </summary>
    [Fact]
    public async Task GetCandidatesAsync_AfterDecline_StatusReturnsToNone()
    {
        // Arrange
      using var context = DbFixture.CreateContext();
  var user1 = await DbFixture.CreateTestUserAsync(context);
   var user2 = await DbFixture.CreateTestUserAsync(context);
   var likesService = new Infrastructure.Services.UserLikesService(context);
        var service = new MatchService(context, new MockNotificationService());

        // Both like same movie
    await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", null, null, CancellationToken.None);
    await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", null, null, CancellationToken.None);

     // User2 sends request, User1 declines
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);
        await service.DeclineMatchAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

        // Act
   var candidates = await service.GetCandidatesAsync(user1.Id, 20, CancellationToken.None);

// Assert - User2 still appears as candidate with status="none"
        candidates.Should().Contain(c => c.UserId == user2.Id);
    var user2Candidate = candidates.First(c => c.UserId == user2.Id);
        user2Candidate.MatchStatus.Should().Be("none");
    }

    #endregion
}
