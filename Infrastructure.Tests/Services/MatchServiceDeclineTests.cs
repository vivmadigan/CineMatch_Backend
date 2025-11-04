using FluentAssertions;
using Infrastructure.Services.Matches;
using Infrastructure.Tests.Helpers;
using Infrastructure.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for MatchService decline functionality.
/// Tests declining match requests, idempotency, and edge cases.
/// GOAL: Ensure users can decline match requests reliably without breaking the system.
/// </summary>
public class MatchServiceDeclineTests
{
    #region Basic Decline Tests

    /// <summary>
    /// GOAL: Ensure declining a match request removes it from the database.
    /// IMPORTANCE: Users should be able to reject unwanted match requests cleanly.
    /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_RemovesIncomingRequest()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var decliner = await DbFixture.CreateTestUserAsync(context, displayName: "Decliner");
        var requestor = await DbFixture.CreateTestUserAsync(context, displayName: "Requestor");
        var service = new MatchService(context, new MockNotificationService());

        // Requestor sends match request to Decliner
     await service.RequestAsync(requestor.Id, decliner.Id, 27205, CancellationToken.None);

        var requestsBefore = await context.MatchRequests.CountAsync();
    requestsBefore.Should().Be(1);

        // Act - Decliner declines the request
        await service.DeclineMatchAsync(decliner.Id, requestor.Id, 27205, CancellationToken.None);

     // Assert - Request should be removed
var requestsAfter = await context.MatchRequests.CountAsync();
        requestsAfter.Should().Be(0);
    }

    /// <summary>
    /// GOAL: Ensure decline only removes the specific incoming request (not other requests).
    /// IMPORTANCE: Declining one request shouldn't affect other unrelated match requests.
    /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_OnlyRemovesSpecificRequest()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var decliner = await DbFixture.CreateTestUserAsync(context, displayName: "Decliner");
        var requestor1 = await DbFixture.CreateTestUserAsync(context, displayName: "Requestor1");
    var requestor2 = await DbFixture.CreateTestUserAsync(context, displayName: "Requestor2");
        var service = new MatchService(context, new MockNotificationService());

        // Two users send requests to Decliner for different movies
      await service.RequestAsync(requestor1.Id, decliner.Id, 27205, CancellationToken.None); // Inception
        await service.RequestAsync(requestor2.Id, decliner.Id, 238, CancellationToken.None);   // Godfather

      var requestsBefore = await context.MatchRequests.CountAsync();
        requestsBefore.Should().Be(2);

        // Act - Decline only request from requestor1 for Inception
        await service.DeclineMatchAsync(decliner.Id, requestor1.Id, 27205, CancellationToken.None);

        // Assert - Only one request remains (from requestor2)
   var requestsAfter = await context.MatchRequests.CountAsync();
        requestsAfter.Should().Be(1);

        var remainingRequest = await context.MatchRequests.FirstAsync();
        remainingRequest.RequestorId.Should().Be(requestor2.Id);
        remainingRequest.TmdbId.Should().Be(238);
 }

    /// <summary>
    /// GOAL: Ensure decline only removes incoming requests (not outgoing requests user sent).
    /// IMPORTANCE: Declining an incoming request shouldn't cancel the user's own outgoing requests.
  /// </summary>
[Fact]
    public async Task DeclineMatchAsync_DoesNotAffectOutgoingRequests()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
    var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
   var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());

        // User1 sends request to User2
   await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

      // User2 sends request to User1 (different movie)
        await service.RequestAsync(user2.Id, user1.Id, 238, CancellationToken.None);

 var requestsBefore = await context.MatchRequests.CountAsync();
        requestsBefore.Should().Be(2);

        // Act - User1 declines User2's request for Godfather
        await service.DeclineMatchAsync(user1.Id, user2.Id, 238, CancellationToken.None);

        // Assert - User1's outgoing request should still exist
 var requestsAfter = await context.MatchRequests.CountAsync();
        requestsAfter.Should().Be(1);

        var remainingRequest = await context.MatchRequests.FirstAsync();
        remainingRequest.RequestorId.Should().Be(user1.Id);
      remainingRequest.TargetUserId.Should().Be(user2.Id);
        remainingRequest.TmdbId.Should().Be(27205);
    }

    #endregion

    #region Idempotency Tests

    /// <summary>
    /// GOAL: Ensure declining a request twice doesn't throw an error.
    /// IMPORTANCE: Users may click "decline" button multiple times; system should handle gracefully.
 /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_CalledTwice_IsIdempotent()
    {
  // Arrange
        using var context = DbFixture.CreateContext();
        var decliner = await DbFixture.CreateTestUserAsync(context, displayName: "Decliner");
        var requestor = await DbFixture.CreateTestUserAsync(context, displayName: "Requestor");
        var service = new MatchService(context, new MockNotificationService());

        await service.RequestAsync(requestor.Id, decliner.Id, 27205, CancellationToken.None);

        // Act - Decline twice
    await service.DeclineMatchAsync(decliner.Id, requestor.Id, 27205, CancellationToken.None);
        await service.DeclineMatchAsync(decliner.Id, requestor.Id, 27205, CancellationToken.None); // Should not throw

        // Assert - No requests remain
 var requestsAfter = await context.MatchRequests.CountAsync();
        requestsAfter.Should().Be(0);
    }

    /// <summary>
    /// GOAL: Ensure declining a non-existent request doesn't throw an error.
    /// IMPORTANCE: Frontend may send decline for already-expired requests; backend should handle gracefully.
    /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_NonExistentRequest_DoesNotThrow()
    {
        // Arrange
  using var context = DbFixture.CreateContext();
   var decliner = await DbFixture.CreateTestUserAsync(context, displayName: "Decliner");
        var requestor = await DbFixture.CreateTestUserAsync(context, displayName: "Requestor");
        var service = new MatchService(context, new MockNotificationService());

        // Act - Decline a request that never existed
        var act = async () => await service.DeclineMatchAsync(decliner.Id, requestor.Id, 27205, CancellationToken.None);

        // Assert - Should not throw
        await act.Should().NotThrowAsync();

        var requests = await context.MatchRequests.CountAsync();
        requests.Should().Be(0);
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// GOAL: Ensure declining with swapped user IDs doesn't remove the request.
    /// IMPORTANCE: Security - users should only be able to decline requests sent TO them, not FROM them.
    /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_WithSwappedUserIds_DoesNotRemoveRequest()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
     var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());

        // User1 sends request to User2
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

      // Act - User1 tries to "decline" their own outgoing request (wrong direction)
   await service.DeclineMatchAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

        // Assert - Request should still exist (wrong direction, so nothing removed)
        var requestsAfter = await context.MatchRequests.CountAsync();
     requestsAfter.Should().Be(1);
    }

  /// <summary>
    /// GOAL: Ensure declining requires exact movie ID match.
    /// IMPORTANCE: Users may have multiple requests from same person for different movies.
    /// </summary>
    [Fact]
 public async Task DeclineMatchAsync_WithWrongTmdbId_DoesNotRemoveRequest()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
  var decliner = await DbFixture.CreateTestUserAsync(context, displayName: "Decliner");
        var requestor = await DbFixture.CreateTestUserAsync(context, displayName: "Requestor");
        var service = new MatchService(context, new MockNotificationService());

      // Requestor sends request for Inception (27205)
        await service.RequestAsync(requestor.Id, decliner.Id, 27205, CancellationToken.None);

      // Act - Decliner tries to decline for Godfather (238) - wrong movie
        await service.DeclineMatchAsync(decliner.Id, requestor.Id, 238, CancellationToken.None);

        // Assert - Request for Inception should still exist
        var requestsAfter = await context.MatchRequests.CountAsync();
  requestsAfter.Should().Be(1);

   var remainingRequest = await context.MatchRequests.FirstAsync();
        remainingRequest.TmdbId.Should().Be(27205); // Inception still there
    }

/// <summary>
    /// GOAL: Ensure decline works with multiple pending requests from same user.
    /// IMPORTANCE: Users may send multiple match requests; decline should work for each.
    /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_WithMultipleRequestsFromSameUser_OnlyDeclinesSpecified()
    {
  // Arrange
        using var context = DbFixture.CreateContext();
        var decliner = await DbFixture.CreateTestUserAsync(context, displayName: "Decliner");
        var requestor = await DbFixture.CreateTestUserAsync(context, displayName: "Requestor");
        var service = new MatchService(context, new MockNotificationService());

      // Requestor sends 3 requests for different movies
        await service.RequestAsync(requestor.Id, decliner.Id, 27205, CancellationToken.None); // Inception
      await service.RequestAsync(requestor.Id, decliner.Id, 238, CancellationToken.None);   // Godfather
   await service.RequestAsync(requestor.Id, decliner.Id, 603, CancellationToken.None);   // Matrix

        var requestsBefore = await context.MatchRequests.CountAsync();
        requestsBefore.Should().Be(3);

        // Act - Decline only the Godfather request
        await service.DeclineMatchAsync(decliner.Id, requestor.Id, 238, CancellationToken.None);

    // Assert - 2 requests remain
      var requestsAfter = await context.MatchRequests.CountAsync();
        requestsAfter.Should().Be(2);

        var remainingRequests = await context.MatchRequests.ToListAsync();
   remainingRequests.Should().Contain(r => r.TmdbId == 27205); // Inception
        remainingRequests.Should().Contain(r => r.TmdbId == 603);   // Matrix
        remainingRequests.Should().NotContain(r => r.TmdbId == 238); // Godfather declined
    }

    #endregion

    #region Integration with GetCandidatesAsync Tests

    /// <summary>
    /// GOAL: Ensure declined user no longer shows as "pending_received" in candidates list.
    /// IMPORTANCE: After declining, candidate status should update to allow new request.
    /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_UpdatesCandidateStatus()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var decliner = await DbFixture.CreateTestUserAsync(context, displayName: "Decliner");
      var requestor = await DbFixture.CreateTestUserAsync(context, displayName: "Requestor");
        var likesService = new Infrastructure.Services.UserLikesService(context);
      var service = new MatchService(context, new MockNotificationService());

        // Both users like the same movie
        await likesService.UpsertLikeAsync(decliner.Id, 27205, "Inception", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(requestor.Id, 27205, "Inception", null, null, CancellationToken.None);

        // Requestor sends match request to Decliner
   await service.RequestAsync(requestor.Id, decliner.Id, 27205, CancellationToken.None);

        // Verify status is pending_received BEFORE decline
  var candidatesBefore = await service.GetCandidatesAsync(decliner.Id, 20, CancellationToken.None);
      candidatesBefore.Should().ContainSingle();
        candidatesBefore.First().MatchStatus.Should().Be("pending_received");

        // Act - Decline the request
      await service.DeclineMatchAsync(decliner.Id, requestor.Id, 27205, CancellationToken.None);

        // Assert - Status should change to "none" (can send new request)
      var candidatesAfter = await service.GetCandidatesAsync(decliner.Id, 20, CancellationToken.None);
        candidatesAfter.Should().ContainSingle();
  candidatesAfter.First().MatchStatus.Should().Be("none");
    }

    #endregion

    #region Concurrent Decline Tests

    /// <summary>
    /// GOAL: Ensure concurrent decline requests don't cause database errors.
    /// IMPORTANCE: In high-traffic scenarios, users may trigger multiple decline requests simultaneously.
    /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_ConcurrentCalls_HandlesGracefully()
    {
  // Arrange
      using var context = DbFixture.CreateContext();
     var decliner = await DbFixture.CreateTestUserAsync(context, displayName: "Decliner");
        var requestor = await DbFixture.CreateTestUserAsync(context, displayName: "Requestor");
        var service = new MatchService(context, new MockNotificationService());

   await service.RequestAsync(requestor.Id, decliner.Id, 27205, CancellationToken.None);

        // Act - Simulate concurrent decline calls
        var task1 = service.DeclineMatchAsync(decliner.Id, requestor.Id, 27205, CancellationToken.None);
     var task2 = service.DeclineMatchAsync(decliner.Id, requestor.Id, 27205, CancellationToken.None);
   var task3 = service.DeclineMatchAsync(decliner.Id, requestor.Id, 27205, CancellationToken.None);

    // Assert - All should complete without throwing
        await Task.WhenAll(task1, task2, task3);

        var requestsAfter = await context.MatchRequests.CountAsync();
        requestsAfter.Should().Be(0);
    }

    #endregion

    #region Decline After Match Tests

    /// <summary>
    /// GOAL: Ensure declining after mutual match doesn't affect chat room.
    /// IMPORTANCE: Once matched, decline should have no effect (room already created).
 /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_AfterMutualMatch_DoesNotAffectRoom()
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

        var roomsBefore = await context.ChatRooms.CountAsync();
        roomsBefore.Should().Be(1);

        // Act - Try to decline after match (no requests exist anymore)
        await service.DeclineMatchAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

     // Assert - Room should still exist
        var roomsAfter = await context.ChatRooms.CountAsync();
roomsAfter.Should().Be(1);

      var room = await context.ChatRooms.FirstAsync();
        room.Id.Should().Be(roomId);
    }

    #endregion

    #region Logging/Debugging Tests

    /// <summary>
    /// GOAL: Ensure DeclineMatchAsync doesn't log sensitive user information.
/// IMPORTANCE: Privacy - decline actions should be logged safely for debugging.
    /// NOTE: This test verifies method completes; actual logging validation would require log capture.
    /// </summary>
    [Fact]
    public async Task DeclineMatchAsync_CompletesWithoutErrors()
    {
        // Arrange
  using var context = DbFixture.CreateContext();
        var decliner = await DbFixture.CreateTestUserAsync(context, displayName: "Decliner");
        var requestor = await DbFixture.CreateTestUserAsync(context, displayName: "Requestor");
        var service = new MatchService(context, new MockNotificationService());

    await service.RequestAsync(requestor.Id, decliner.Id, 27205, CancellationToken.None);

     // Act
        var act = async () => await service.DeclineMatchAsync(decliner.Id, requestor.Id, 27205, CancellationToken.None);

// Assert - Should complete without exceptions
        await act.Should().NotThrowAsync();
    }

  #endregion
}
