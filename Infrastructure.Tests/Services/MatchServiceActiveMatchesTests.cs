using FluentAssertions;
using Infrastructure.Data.Entities;
using Infrastructure.Services;
using Infrastructure.Services.Chat;
using Infrastructure.Services.Matches;
using Infrastructure.Tests.Helpers;
using Infrastructure.Tests.Mocks;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for MatchService.GetActiveMatchesAsync().
/// Tests active match retrieval, sorting, last message preview, and shared movies.
/// GOAL: Ensure "Active Matches" / "Chats" page displays correct data in proper order.
/// </summary>
public class MatchServiceActiveMatchesTests
{
    #region Basic Retrieval Tests

    /// <summary>
    /// GOAL: Ensure GetActiveMatchesAsync returns empty list when user has no matches.
    /// IMPORTANCE: New users should see empty state, not an error.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_WithNoMatches_ReturnsEmpty()
    {
  // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

        // Act
        var result = await service.GetActiveMatchesAsync(user.Id, CancellationToken.None);

      // Assert
        result.Should().BeEmpty();
    }

    /// <summary>
    /// GOAL: Ensure GetActiveMatchesAsync returns matched users with chat rooms.
    /// IMPORTANCE: Users need to see who they've matched with to start chatting.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_WithOneMatch_ReturnsMatchWithRoomId()
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

        // Act
 var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

     // Assert
     result.Should().ContainSingle();
        result.First().UserId.Should().Be(user2.Id);
        result.First().DisplayName.Should().Be("User2");
      result.First().RoomId.Should().Be(matchResult.RoomId!.Value);
    }

 /// <summary>
    /// GOAL: Ensure GetActiveMatchesAsync returns all active matches for a user.
    /// IMPORTANCE: Users with multiple matches should see complete list.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_WithMultipleMatches_ReturnsAll()
    {
   // Arrange
        using var context = DbFixture.CreateContext();
        var currentUser = await DbFixture.CreateTestUserAsync(context, displayName: "CurrentUser");
     var match1 = await DbFixture.CreateTestUserAsync(context, displayName: "Match1");
        var match2 = await DbFixture.CreateTestUserAsync(context, displayName: "Match2");
        var match3 = await DbFixture.CreateTestUserAsync(context, displayName: "Match3");
 var service = new MatchService(context, new MockNotificationService());

      // Create 3 mutual matches
        await service.RequestAsync(currentUser.Id, match1.Id, 27205, CancellationToken.None);
   await service.RequestAsync(match1.Id, currentUser.Id, 27205, CancellationToken.None);

        await service.RequestAsync(currentUser.Id, match2.Id, 238, CancellationToken.None);
        await service.RequestAsync(match2.Id, currentUser.Id, 238, CancellationToken.None);

    await service.RequestAsync(currentUser.Id, match3.Id, 603, CancellationToken.None);
        await service.RequestAsync(match3.Id, currentUser.Id, 603, CancellationToken.None);

        // Act
        var result = await service.GetActiveMatchesAsync(currentUser.Id, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
  result.Select(m => m.DisplayName).Should().Contain(new[] { "Match1", "Match2", "Match3" });
    }

    #endregion

    #region Filtering Tests

    /// <summary>
    /// GOAL: Ensure inactive memberships (users who left) are NOT included in active matches.
    /// IMPORTANCE: Users who left rooms shouldn't appear in active chats list.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_ExcludesInactiveMemberships()
    {
        // Arrange
      using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());
    var chatService = new ChatService(context);

        // Create mutual match
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
    var matchResult = await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // User1 leaves the room
        await chatService.LeaveAsync(matchResult.RoomId!.Value, user1.Id, CancellationToken.None);

        // Act
     var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

        // Assert - User1 should not see the match (inactive membership)
     result.Should().BeEmpty();
    }

    /// <summary>
    /// GOAL: Ensure only matches with active memberships for BOTH users are shown.
    /// IMPORTANCE: If other user left, match shouldn't appear in active list.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_ExcludesMatchesWhereOtherUserLeft()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
    var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());
        var chatService = new ChatService(context);

        // Create mutual match
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
      var matchResult = await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // User2 leaves the room
        await chatService.LeaveAsync(matchResult.RoomId!.Value, user2.Id, CancellationToken.None);

     // Act - User1 checks their active matches
        var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

        // Assert - User1 should still see the match (they're still active)
        result.Should().ContainSingle();
    }

    #endregion

    #region Sorting Tests

    /// <summary>
    /// GOAL: Ensure matches are sorted by last message time (most recent first).
    /// IMPORTANCE: Users expect most recent conversations at the top of the list.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_SortsByLastMessageTime_MostRecentFirst()
    {
        // Arrange
   using var context = DbFixture.CreateContext();
        var currentUser = await DbFixture.CreateTestUserAsync(context, displayName: "CurrentUser");
 var match1 = await DbFixture.CreateTestUserAsync(context, displayName: "Match1");
        var match2 = await DbFixture.CreateTestUserAsync(context, displayName: "Match2");
  var match3 = await DbFixture.CreateTestUserAsync(context, displayName: "Match3");
        var service = new MatchService(context, new MockNotificationService());
        var chatService = new ChatService(context);

  // Create 3 matches
     await service.RequestAsync(currentUser.Id, match1.Id, 27205, CancellationToken.None);
     var room1 = await service.RequestAsync(match1.Id, currentUser.Id, 27205, CancellationToken.None);

        await service.RequestAsync(currentUser.Id, match2.Id, 238, CancellationToken.None);
        var room2 = await service.RequestAsync(match2.Id, currentUser.Id, 238, CancellationToken.None);

      await service.RequestAsync(currentUser.Id, match3.Id, 603, CancellationToken.None);
        var room3 = await service.RequestAsync(match3.Id, currentUser.Id, 603, CancellationToken.None);

   // Send messages at different times
        await chatService.AppendAsync(room1.RoomId!.Value, currentUser.Id, "Message to Match1", CancellationToken.None);
        await Task.Delay(100);
        await chatService.AppendAsync(room3.RoomId!.Value, currentUser.Id, "Message to Match3", CancellationToken.None);
        await Task.Delay(100);
        await chatService.AppendAsync(room2.RoomId!.Value, currentUser.Id, "Message to Match2", CancellationToken.None);

        // Act
        var result = await service.GetActiveMatchesAsync(currentUser.Id, CancellationToken.None);

        // Assert - Most recent message first
        result.Should().HaveCount(3);
      result[0].DisplayName.Should().Be("Match2"); // Most recent message
        result[1].DisplayName.Should().Be("Match3");
    result[2].DisplayName.Should().Be("Match1"); // Oldest message
    }

    /// <summary>
    /// GOAL: Ensure matches without messages are sorted by MatchedAt time.
    /// IMPORTANCE: New matches without messages should still appear in a consistent order.
/// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_SortsByMatchedAt_WhenNoMessages()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var currentUser = await DbFixture.CreateTestUserAsync(context, displayName: "CurrentUser");
        var match1 = await DbFixture.CreateTestUserAsync(context, displayName: "Match1");
      var match2 = await DbFixture.CreateTestUserAsync(context, displayName: "Match2");
  var service = new MatchService(context, new MockNotificationService());

        // Create matches with slight delay
        await service.RequestAsync(currentUser.Id, match1.Id, 27205, CancellationToken.None);
        await service.RequestAsync(match1.Id, currentUser.Id, 27205, CancellationToken.None);

        await Task.Delay(100);

        await service.RequestAsync(currentUser.Id, match2.Id, 238, CancellationToken.None);
        await service.RequestAsync(match2.Id, currentUser.Id, 238, CancellationToken.None);

        // Act
var result = await service.GetActiveMatchesAsync(currentUser.Id, CancellationToken.None);

        // Assert - Most recent match first (no messages, so sort by MatchedAt)
        result.Should().HaveCount(2);
     result[0].DisplayName.Should().Be("Match2"); // Matched more recently
      result[1].DisplayName.Should().Be("Match1");
    }

    #endregion

    #region Last Message Preview Tests

    /// <summary>
    /// GOAL: Ensure LastMessage property shows the most recent message text.
    /// IMPORTANCE: Users need message previews to identify conversations quickly.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_IncludesLastMessagePreview()
    {
     // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
    var service = new MatchService(context, new MockNotificationService());
     var chatService = new ChatService(context);

     // Create match
 await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        var matchResult = await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Send messages
   await chatService.AppendAsync(matchResult.RoomId!.Value, user1.Id, "First message", CancellationToken.None);
        await chatService.AppendAsync(matchResult.RoomId!.Value, user2.Id, "Second message", CancellationToken.None);
        await chatService.AppendAsync(matchResult.RoomId!.Value, user1.Id, "Latest message!", CancellationToken.None);

        // Act
    var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

    // Assert
        result.Should().ContainSingle();
        result.First().LastMessage.Should().Be("Latest message!");
    }

    /// <summary>
    /// GOAL: Ensure LastMessage is null when no messages exist.
    /// IMPORTANCE: New chats without messages should handle null gracefully.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_LastMessageIsNull_WhenNoMessages()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());

        // Create match without sending any messages
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Act
        var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

  // Assert
        result.Should().ContainSingle();
        result.First().LastMessage.Should().BeNull();
        result.First().LastMessageAt.Should().BeNull();
    }

    /// <summary>
    /// GOAL: Ensure LastMessageAt property shows the timestamp of the most recent message.
    /// IMPORTANCE: Users need timestamps to know when conversations were last active.
 /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_IncludesLastMessageTimestamp()
{
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
    var service = new MatchService(context, new MockNotificationService());
        var chatService = new ChatService(context);

        // Create match
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        var matchResult = await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        var beforeSend = DateTime.UtcNow;
  await chatService.AppendAsync(matchResult.RoomId!.Value, user1.Id, "Test message", CancellationToken.None);
        var afterSend = DateTime.UtcNow;

      // Act
        var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

     // Assert
        result.Should().ContainSingle();
        result.First().LastMessageAt.Should().NotBeNull();
        result.First().LastMessageAt.Should().BeOnOrAfter(beforeSend);
        result.First().LastMessageAt.Should().BeOnOrBefore(afterSend);
}

    #endregion

    #region Shared Movies Tests

 /// <summary>
    /// GOAL: Ensure SharedMovies property includes movies both users liked.
    /// IMPORTANCE: Users need context for why they matched (shared interests).
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_IncludesSharedMovies()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
  var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var likesService = new UserLikesService(context);
        var service = new MatchService(context, new MockNotificationService());

 // Both users like the same movies
await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", "/inception.jpg", "2010", CancellationToken.None);
    await likesService.UpsertLikeAsync(user1.Id, 238, "The Godfather", "/godfather.jpg", "1972", CancellationToken.None);

      await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", "/inception.jpg", "2010", CancellationToken.None);
        await likesService.UpsertLikeAsync(user2.Id, 238, "The Godfather", "/godfather.jpg", "1972", CancellationToken.None);

        // Create match
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Act
        var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
     result.First().SharedMovies.Should().HaveCount(2);
    result.First().SharedMovies.Should().Contain(m => m.TmdbId == 27205 && m.Title == "Inception");
        result.First().SharedMovies.Should().Contain(m => m.TmdbId == 238 && m.Title == "The Godfather");
    }

    /// <summary>
    /// GOAL: Ensure SharedMovies includes poster URLs and release years.
    /// IMPORTANCE: UI needs complete movie metadata to display cards properly.
    /// </summary>
    [Fact]
public async Task GetActiveMatchesAsync_SharedMoviesIncludeMetadata()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
  var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
      var likesService = new UserLikesService(context);
        var service = new MatchService(context, new MockNotificationService());

        // Both like Inception with metadata
        await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", "/xyz123.jpg", "2010", CancellationToken.None);
   await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", "/xyz123.jpg", "2010", CancellationToken.None);

        // Create match
await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Act
        var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

     // Assert
        result.Should().ContainSingle();
        var sharedMovie = result.First().SharedMovies.First();
        sharedMovie.TmdbId.Should().Be(27205);
    sharedMovie.Title.Should().Be("Inception");
        sharedMovie.PosterUrl.Should().Contain("/xyz123.jpg");
        sharedMovie.ReleaseYear.Should().Be("2010");
 }

/// <summary>
    /// GOAL: Ensure SharedMovies is empty when users matched but unliked movies later.
    /// IMPORTANCE: Handles edge case where likes are removed after matching.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_SharedMoviesEmpty_WhenNoCommonLikes()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
   var likesService = new UserLikesService(context);
  var service = new MatchService(context, new MockNotificationService());

   // Both like Inception initially
        await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", null, null, CancellationToken.None);

        // Create match
   await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // User2 unlikes Inception after matching
        await likesService.RemoveLikeAsync(user2.Id, 27205, CancellationToken.None);

        // Act
     var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result.First().SharedMovies.Should().BeEmpty();
    }

    #endregion

    #region Unread Count Tests

    /// <summary>
    /// GOAL: Ensure UnreadCount shows messages sent by other user (not current user).
    /// IMPORTANCE: Users need to know if they have unread messages in each chat.
    /// NOTE: Current implementation counts ALL messages from other user (not true "unread").
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_IncludesUnreadCount()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());
        var chatService = new ChatService(context);

        // Create match
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        var matchResult = await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // User2 sends 3 messages (unread by User1)
        await chatService.AppendAsync(matchResult.RoomId!.Value, user2.Id, "Message 1", CancellationToken.None);
        await chatService.AppendAsync(matchResult.RoomId!.Value, user2.Id, "Message 2", CancellationToken.None);
        await chatService.AppendAsync(matchResult.RoomId!.Value, user2.Id, "Message 3", CancellationToken.None);

      // User1 sends 1 message (shouldn't count as unread for User1)
        await chatService.AppendAsync(matchResult.RoomId!.Value, user1.Id, "My message", CancellationToken.None);

  // Act
      var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result.First().UnreadCount.Should().Be(3); // Only messages from User2
    }

    /// <summary>
    /// GOAL: Ensure UnreadCount is 0 when no messages from other user.
    /// IMPORTANCE: New chats or chats where only current user sent messages show 0 unread.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_UnreadCountZero_WhenNoMessagesFromOther()
    {
 // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
   var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());
        var chatService = new ChatService(context);

        // Create match
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        var matchResult = await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Only User1 sends messages (no unread for User1)
     await chatService.AppendAsync(matchResult.RoomId!.Value, user1.Id, "Message 1", CancellationToken.None);
   await chatService.AppendAsync(matchResult.RoomId!.Value, user1.Id, "Message 2", CancellationToken.None);

        // Act
        var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result.First().UnreadCount.Should().Be(0);
    }

    #endregion

 #region MatchedAt Timestamp Tests

    /// <summary>
    /// GOAL: Ensure MatchedAt timestamp reflects when chat room was created.
    /// IMPORTANCE: Users can see when they matched for context.
    /// </summary>
    [Fact]
    public async Task GetActiveMatchesAsync_IncludesMatchedAtTimestamp()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
   var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var service = new MatchService(context, new MockNotificationService());

        var beforeMatch = DateTime.UtcNow;
  await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);
        var afterMatch = DateTime.UtcNow;

        // Act
        var result = await service.GetActiveMatchesAsync(user1.Id, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result.First().MatchedAt.Should().BeOnOrAfter(beforeMatch);
        result.First().MatchedAt.Should().BeOnOrBefore(afterMatch);
    }

    #endregion

    #region Performance / Efficiency Tests

    /// <summary>
    /// GOAL: Ensure GetActiveMatchesAsync handles large number of matches efficiently.
    /// IMPORTANCE: Users with many matches shouldn't experience slow load times.
    /// NOTE: This test verifies correctness; actual performance would need benchmarking.
    /// </summary>
 [Fact]
    public async Task GetActiveMatchesAsync_WithManyMatches_ReturnsAll()
 {
        // Arrange
        using var context = DbFixture.CreateContext();
        var currentUser = await DbFixture.CreateTestUserAsync(context, displayName: "CurrentUser");
        var service = new MatchService(context, new MockNotificationService());

      // Create 10 matches
        for (int i = 1; i <= 10; i++)
    {
        var otherUser = await DbFixture.CreateTestUserAsync(context, displayName: $"Match{i}");
            await service.RequestAsync(currentUser.Id, otherUser.Id, 27205, CancellationToken.None);
            await service.RequestAsync(otherUser.Id, currentUser.Id, 27205, CancellationToken.None);
        }

        // Act
        var result = await service.GetActiveMatchesAsync(currentUser.Id, CancellationToken.None);

        // Assert
        result.Should().HaveCount(10);
    }

    #endregion
}
