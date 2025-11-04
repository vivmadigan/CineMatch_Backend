using FluentAssertions;
using Infrastructure.Services;
using Infrastructure.Services.Matches;
using Infrastructure.Tests.Helpers;
using Infrastructure.Tests.Mocks;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for MatchService.GetMatchStatusAsync() - NEW method with 0 prior tests.
/// Tests all status states (none, pending_sent, pending_received, matched) with positive and negative scenarios.
/// GOAL: Ensure frontend gets accurate match status for UI state management.
/// </summary>
public class MatchServiceStatusTests
{
    #region Positive Tests - Valid Status States

    /// <summary>
    /// POSITIVE TEST: Verify status="none" when no match requests exist between users.
    /// GOAL: Frontend shows "Send Match Request" button (enabled state).
    /// IMPORTANCE: Default state; most common scenario for new potential matches.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithNoRequests_ReturnsNoneStatus()
    {
     // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
      var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var likesService = new UserLikesService(context);
        var service = new MatchService(context, new MockNotificationService());

   // Both like same movie (candidates, but no requests)
      await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", null, null, CancellationToken.None);

        // Act
      var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

      // Assert
        result.Status.Should().Be("none");
        result.CanMatch.Should().BeTrue();
        result.CanDecline.Should().BeFalse();
 result.RequestSentAt.Should().BeNull();
        result.RoomId.Should().BeNull();
        result.SharedMovies.Should().ContainSingle(m => m.TmdbId == 27205);
  }

    /// <summary>
  /// POSITIVE TEST: Verify status="pending_sent" when current user sent a match request.
  /// GOAL: Frontend shows "Request Sent" (disabled button, pending state).
  /// IMPORTANCE: Prevents duplicate requests; user knows they're waiting for response.
  /// </summary>
  [Fact]
    public async Task GetMatchStatusAsync_WithSentRequest_ReturnsPendingSentStatus()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());

// User1 sends request to User2
        var beforeRequest = DateTime.UtcNow;
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
 var afterRequest = DateTime.UtcNow;

// Act
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be("pending_sent");
  result.CanMatch.Should().BeFalse(); // Can't send again
   result.CanDecline.Should().BeFalse(); // Can't decline own request
      result.RequestSentAt.Should().NotBeNull();
        result.RequestSentAt.Should().BeOnOrAfter(beforeRequest);
        result.RequestSentAt.Should().BeOnOrBefore(afterRequest);
        result.RoomId.Should().BeNull(); // No room yet (not matched)
    }

    /// <summary>
    /// POSITIVE TEST: Verify status="pending_received" when target user sent a request to current user.
    /// GOAL: Frontend shows "Accept / Decline" buttons (actionable state).
    /// IMPORTANCE: User can respond to incoming requests.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithReceivedRequest_ReturnsPendingReceivedStatus()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
   var service = new MatchService(context, new MockNotificationService());

    // User2 sends request to User1
   var beforeRequest = DateTime.UtcNow;
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);
        var afterRequest = DateTime.UtcNow;

        // Act - User1 checks status with User2
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert
      result.Status.Should().Be("pending_received");
    result.CanMatch.Should().BeTrue(); // Can accept request
        result.CanDecline.Should().BeTrue(); // Can decline request
        result.RequestSentAt.Should().NotBeNull();
   result.RequestSentAt.Should().BeOnOrAfter(beforeRequest);
    result.RequestSentAt.Should().BeOnOrBefore(afterRequest);
   result.RoomId.Should().BeNull(); // No room yet
    }

  /// <summary>
    /// POSITIVE TEST: Verify status="matched" with roomId when mutual match exists (chat room created).
    /// GOAL: Frontend navigates to chat room using roomId.
    /// IMPORTANCE: Critical for chat integration; roomId must be present.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithMutualMatch_ReturnsMatchedStatusAndRoomId()
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
        matchResult.RoomId.Should().NotBeNull();

        // Act
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert
   result.Status.Should().Be("matched");
        result.CanMatch.Should().BeFalse(); // Already matched
        result.CanDecline.Should().BeFalse(); // Can't decline after match
        result.RequestSentAt.Should().BeNull(); // Requests consumed/deleted
        result.RoomId.Should().Be(matchResult.RoomId!.Value); // Room ID present
    }

    /// <summary>
    /// POSITIVE TEST: Verify status="matched" when both users sent requests simultaneously (edge case).
    /// GOAL: Race condition handling - both requests detected as mutual match.
    /// IMPORTANCE: Rare but valid scenario; GetMatchStatusAsync detects bidirectional interest.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithBothSentRequests_ReturnsMatchedStatus()
{
        // Arrange
 using var context = DbFixture.CreateContext();
     var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
   var service = new MatchService(context, new MockNotificationService());

     // Both users send requests (but for different movies)
     await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        await service.RequestAsync(user2.Id, user1.Id, 238, CancellationToken.None); // Different movie

 // Act - Check status (GetMatchStatusAsync checks ANY requests between users)
var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

    // Assert - Should recognize bidirectional interest as "matched" (edge case handling)
      // GetMatchStatusAsync detects both sent and received requests = effectively matched
     result.Status.Should().Be("matched");
        result.CanMatch.Should().BeFalse();
        result.CanDecline.Should().BeFalse();
 }
    #endregion

    #region Negative Tests - Invalid Inputs & Edge Cases

    /// <summary>
    /// NEGATIVE TEST: Verify graceful handling when checking status with non-existent user.
    /// GOAL: Returns "none" status (no crash).
  /// IMPORTANCE: Robust error handling; deleted/invalid users shouldn't break status checks.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithNonExistentTargetUser_ReturnsNoneStatus()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var service = new MatchService(context, new MockNotificationService());

        // Act - Check status with non-existent user
        var result = await service.GetMatchStatusAsync(user1.Id, "non-existent-user-id", CancellationToken.None);

        // Assert - Should return "none" (no data, no error)
    result.Status.Should().Be("none");
        result.CanMatch.Should().BeTrue();
        result.SharedMovies.Should().BeEmpty();
    }

    /// <summary>
    /// NEGATIVE TEST: Verify status check with same user ID (self-match attempt).
    /// GOAL: Returns "none" (users can't match with themselves).
    /// IMPORTANCE: Data integrity; self-matches are invalid.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithSameUserId_ReturnsNoneStatus()
    {
        // Arrange
   using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context, displayName: "User");
        var service = new MatchService(context, new MockNotificationService());

        // Act - Check status with self
        var result = await service.GetMatchStatusAsync(user.Id, user.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be("none");
        result.CanMatch.Should().BeTrue(); // API doesn't block, but RequestAsync would handle
    }

    /// <summary>
    /// NEGATIVE TEST: Verify status after declining a received request returns to "none".
    /// GOAL: After decline, status resets (can send new request).
    /// IMPORTANCE: State transition correctness; decline clears pending state.
    /// </summary>
  [Fact]
  public async Task GetMatchStatusAsync_AfterDeclining_ReturnsNoneStatus()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());

        // User2 sends request to User1
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Verify pending_received status
        var beforeDecline = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
    beforeDecline.Status.Should().Be("pending_received");

     // User1 declines
    await service.DeclineMatchAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

   // Act - Check status after decline
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert - Should return to "none"
        result.Status.Should().Be("none");
        result.CanMatch.Should().BeTrue();
        result.CanDecline.Should().BeFalse();
        result.RequestSentAt.Should().BeNull();
}

    #endregion

    #region Shared Movies Tests

    /// <summary>
    /// POSITIVE TEST: Verify shared movies are included in status response.
    /// GOAL: Frontend displays shared movies as match context.
    /// IMPORTANCE: Users need to see why they match (shared interests).
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_IncludesSharedMovies()
    {
     // Arrange
      using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var likesService = new UserLikesService(context);
      var service = new MatchService(context, new MockNotificationService());

 // Both users like same movies
        await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", "/inception.jpg", "2010", CancellationToken.None);
        await likesService.UpsertLikeAsync(user1.Id, 238, "The Godfather", "/godfather.jpg", "1972", CancellationToken.None);

        await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", "/inception.jpg", "2010", CancellationToken.None);
        await likesService.UpsertLikeAsync(user2.Id, 238, "The Godfather", "/godfather.jpg", "1972", CancellationToken.None);

        // Act
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

      // Assert
     result.SharedMovies.Should().HaveCount(2);
result.SharedMovies.Should().Contain(m => m.TmdbId == 27205 && m.Title == "Inception");
  result.SharedMovies.Should().Contain(m => m.TmdbId == 238 && m.Title == "The Godfather");
        result.SharedMovies.Should().AllSatisfy(m =>
        {
    m.PosterUrl.Should().NotBeNullOrEmpty();
          m.ReleaseYear.Should().NotBeNullOrEmpty();
        });
    }

    /// <summary>
    /// NEGATIVE TEST: Verify empty shared movies when users have no common likes.
    /// GOAL: Returns empty list (no crash).
    /// IMPORTANCE: Handles mismatch scenarios gracefully.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithNoSharedMovies_ReturnsEmptyList()
    {
   // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var likesService = new UserLikesService(context);
        var service = new MatchService(context, new MockNotificationService());

 // Users like different movies
        await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(user2.Id, 238, "The Godfather", null, null, CancellationToken.None);

 // Act
      var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

        // Assert
        result.SharedMovies.Should().BeEmpty();
    }

    /// <summary>
    /// POSITIVE TEST: Verify shared movies persist even after match is created.
    /// GOAL: Matched users can still see what movies brought them together.
    /// IMPORTANCE: Context preservation; users remember why they matched.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_AfterMatch_StillIncludesSharedMovies()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
   var likesService = new UserLikesService(context);
        var service = new MatchService(context, new MockNotificationService());

        // Both like Inception
        await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", "/inception.jpg", "2010", CancellationToken.None);
 await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", "/inception.jpg", "2010", CancellationToken.None);

   // Create mutual match
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Act
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);

  // Assert
        result.Status.Should().Be("matched");
      result.SharedMovies.Should().ContainSingle(m => m.TmdbId == 27205);
    }

    #endregion

    #region Status Transition Tests

    /// <summary>
    /// POSITIVE TEST: Verify status transitions from "none" ? "pending_sent" ? "matched".
    /// GOAL: Full lifecycle test; validates state machine correctness.
    /// IMPORTANCE: Ensures status reflects actual match state at each step.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_StatusTransitions_FromNoneToPendingSentToMatched()
    {
        // Arrange
    using var context = DbFixture.CreateContext();
  var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
     var service = new MatchService(context, new MockNotificationService());

        // Act & Assert - State 1: "none"
        var state1 = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
        state1.Status.Should().Be("none");

        // Act & Assert - State 2: User1 sends request ? "pending_sent"
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
 var state2 = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
        state2.Status.Should().Be("pending_sent");

        // Act & Assert - State 3: User2 accepts ? "matched"
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);
        var state3 = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
        state3.Status.Should().Be("matched");
        state3.RoomId.Should().NotBeNull();
    }

    /// <summary>
    /// NEGATIVE TEST: Verify status from User2's perspective when User1 sends request.
    /// GOAL: Reciprocal view - User2 sees "pending_received" when User1 sent request.
    /// IMPORTANCE: Symmetry check; both users see correct status from their perspective.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_ReciprocalView_PendingReceivedForTargetUser()
    {
        // Arrange
      using var context = DbFixture.CreateContext();
 var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
    var service = new MatchService(context, new MockNotificationService());

        // User1 sends request to User2
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

        // Act - Check from both perspectives
      var user1View = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
   var user2View = await service.GetMatchStatusAsync(user2.Id, user1.Id, CancellationToken.None);

        // Assert - Reciprocal statuses
 user1View.Status.Should().Be("pending_sent");
        user1View.CanMatch.Should().BeFalse();
        user1View.CanDecline.Should().BeFalse();

        user2View.Status.Should().Be("pending_received");
user2View.CanMatch.Should().BeTrue();
    user2View.CanDecline.Should().BeTrue();
  }

    #endregion

    #region Performance & Efficiency Tests

    /// <summary>
  /// POSITIVE TEST: Verify GetMatchStatusAsync completes efficiently with no matches.
    /// GOAL: Fast response time (no expensive queries).
    /// IMPORTANCE: Performance baseline; status checks happen frequently.
 /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithNoData_CompletesQuickly()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
var service = new MatchService(context, new MockNotificationService());

        // Act - Time the query
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await service.GetMatchStatusAsync(user1.Id, user2.Id, CancellationToken.None);
     stopwatch.Stop();

 // Assert
  result.Status.Should().Be("none");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100); // Fast query
    }

    #endregion
}
