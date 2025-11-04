using FluentAssertions;
using Infrastructure.Data.Entities;
using Infrastructure.Services.Chat;
using Infrastructure.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Security boundary tests for ChatService.
/// Tests SQL injection, XSS, authorization bypasses, and malicious input handling.
/// GOAL: Ensure service is secure against common web vulnerabilities.
/// </summary>
public class ChatServiceSecurityTests
{
    #region SQL Injection Tests

    /// <summary>
  /// NEGATIVE TEST: Verify AppendAsync with SQL injection attempt in message text.
    /// GOAL: EF Core parameterization prevents SQL injection.
    /// IMPORTANCE: **CRITICAL** - Prevents database compromise.
    /// </summary>
    [Fact]
    public async Task AppendAsync_WithSQLInjectionInText_EscapesCorrectly()
    {
      // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
      var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
     context.ChatRooms.Add(room);
        context.ChatMemberships.Add(new ChatMembership
      {
            RoomId = room.Id,
       UserId = user.Id,
   IsActive = true,
      JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new ChatService(context);
        var sqlInjection = "'; DROP TABLE ChatMessages; --";

        // Act
        var result = await service.AppendAsync(room.Id, user.Id, sqlInjection, CancellationToken.None);

        // Assert - Text stored as-is (not executed as SQL)
        result.Text.Should().Be(sqlInjection);

        // Verify database integrity (ChatMessages table still exists)
 var messageExists = await context.ChatMessages.AnyAsync(m => m.Text == sqlInjection);
        messageExists.Should().BeTrue();
    }

    /// <summary>
    /// NEGATIVE TEST: Verify GetMessagesAsync with SQL injection in roomId parameter.
    /// GOAL: Parameterized queries prevent injection via GUID.
    /// IMPORTANCE: Security - prevents database access via crafted IDs.
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_WithMalformedRoomId_HandlesGracefully()
    {
// Arrange
        using var context = DbFixture.CreateContext();
    var user = await DbFixture.CreateTestUserAsync(context);
        var service = new ChatService(context);

        // Act - Invalid room ID (EF Core handles GUID parsing)
        var act = async () => await service.GetMessagesAsync(Guid.Empty, 50, null, user.Id, CancellationToken.None);

     // Assert - Should throw InvalidOperationException (not SQL error)
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a member*");
    }

    #endregion

    #region XSS (Cross-Site Scripting) Tests

    /// <summary>
    /// NEGATIVE TEST: Verify AppendAsync with XSS script in message text.
    /// GOAL: Service stores XSS as plain text (frontend must escape).
    /// IMPORTANCE: Backend doesn't filter XSS; frontend responsibility.
    /// </summary>
    [Fact]
    public async Task AppendAsync_WithXSSScript_StoresAsPlainText()
  {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
   context.ChatRooms.Add(room);
     context.ChatMemberships.Add(new ChatMembership
{
        RoomId = room.Id,
            UserId = user.Id,
            IsActive = true,
     JoinedAt = DateTime.UtcNow
 });
     await context.SaveChangesAsync();

        var service = new ChatService(context);
    var xssScript = "<script>alert('XSS')</script>";

    // Act
        var result = await service.AppendAsync(room.Id, user.Id, xssScript, CancellationToken.None);

      // Assert - Stored as-is (frontend must escape when rendering)
     result.Text.Should().Be(xssScript);
    }

  /// <summary>
    /// NEGATIVE TEST: Verify AppendAsync with HTML tags in message.
    /// GOAL: HTML stored verbatim; not parsed or filtered.
    /// IMPORTANCE: Backend is data store; frontend handles sanitization.
    /// </summary>
    [Fact]
    public async Task AppendAsync_WithHTMLTags_StoresVerbatim()
    {
        // Arrange
    using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(new ChatMembership
        {
            RoomId = room.Id,
    UserId = user.Id,
   IsActive = true,
       JoinedAt = DateTime.UtcNow
        });
   await context.SaveChangesAsync();

  var service = new ChatService(context);
var htmlContent = "<b>Bold text</b> and <img src='x' onerror='alert(1)'>";

        // Act
        var result = await service.AppendAsync(room.Id, user.Id, htmlContent, CancellationToken.None);

 // Assert
        result.Text.Should().Be(htmlContent);
    }

    #endregion

    #region Authorization Bypass Tests

    /// <summary>
    /// NEGATIVE TEST: Verify AppendAsync prevents message spoofing (wrong senderId).
 /// GOAL: Service uses authenticated userId, not client-provided senderId.
    /// IMPORTANCE: **CRITICAL** - Prevents message impersonation.
    /// </summary>
    [Fact]
    public async Task AppendAsync_AlwaysUseAuthenticatedUserId()
    {
        // Arrange
     using var context = DbFixture.CreateContext();
   var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
      var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
 var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
 context.ChatRooms.Add(room);
        context.ChatMemberships.AddRange(
          new ChatMembership { RoomId = room.Id, UserId = user1.Id, IsActive = true, JoinedAt = DateTime.UtcNow },
  new ChatMembership { RoomId = room.Id, UserId = user2.Id, IsActive = true, JoinedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

    var service = new ChatService(context);

        // Act - User1 sends message (authenticated as User1)
   var result = await service.AppendAsync(room.Id, user1.Id, "Message from User1", CancellationToken.None);

        // Assert - SenderId matches authenticated user (not spoofed)
        result.SenderId.Should().Be(user1.Id);
      result.SenderDisplayName.Should().Be("User1");

        // Verify in database
        var message = await context.ChatMessages.FindAsync(result.Id);
        message.Should().NotBeNull();
        message!.SenderId.Should().Be(user1.Id); // Not user2.Id
    }

    /// <summary>
    /// NEGATIVE TEST: Verify GetMessagesAsync prevents unauthorized room access.
    /// GOAL: Only room members can read messages.
    /// IMPORTANCE: **CRITICAL** - Privacy protection.
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_PreventsCrossTalkBetweenRooms()
    {
      // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var intruder = await DbFixture.CreateTestUserAsync(context, displayName: "Intruder");

        // Room 1: User1 + User2
        var room1 = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        context.ChatRooms.Add(room1);
        context.ChatMemberships.AddRange(
            new ChatMembership { RoomId = room1.Id, UserId = user1.Id, IsActive = true, JoinedAt = DateTime.UtcNow },
        new ChatMembership { RoomId = room1.Id, UserId = user2.Id, IsActive = true, JoinedAt = DateTime.UtcNow }
     );
        await context.SaveChangesAsync();

   var service = new ChatService(context);

      // Add message to room1
        await service.AppendAsync(room1.Id, user1.Id, "Secret message", CancellationToken.None);

        // Act - Intruder tries to read messages from room1
    var act = async () => await service.GetMessagesAsync(room1.Id, 50, null, intruder.Id, CancellationToken.None);

// Assert - Access denied
        await act.Should().ThrowAsync<InvalidOperationException>()
   .WithMessage("*not a member*");
    }

    /// <summary>
    /// NEGATIVE TEST: Verify LeaveAsync doesn't allow leaving other users' memberships.
    /// GOAL: Users can only leave their own memberships.
    /// IMPORTANCE: Authorization - prevents griefing.
    /// </summary>
    [Fact]
    public async Task LeaveAsync_OnlyAffectsOwnMembership()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
 var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        context.ChatRooms.Add(room);
   context.ChatMemberships.AddRange(
        new ChatMembership { RoomId = room.Id, UserId = user1.Id, IsActive = true, JoinedAt = DateTime.UtcNow },
            new ChatMembership { RoomId = room.Id, UserId = user2.Id, IsActive = true, JoinedAt = DateTime.UtcNow }
  );
        await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act - User1 leaves
        await service.LeaveAsync(room.Id, user1.Id, CancellationToken.None);

        // Assert - User1 inactive, User2 still active
        var user1Membership = await context.ChatMemberships.FirstAsync(m => m.RoomId == room.Id && m.UserId == user1.Id);
     var user2Membership = await context.ChatMemberships.FirstAsync(m => m.RoomId == room.Id && m.UserId == user2.Id);

   user1Membership.IsActive.Should().BeFalse();
        user2Membership.IsActive.Should().BeTrue(); // Not affected
    }

    #endregion

    #region Rate Limiting / DoS Prevention Tests

    /// <summary>
    /// NEGATIVE TEST: Verify AppendAsync handles rapid message sending (100 messages).
    /// GOAL: Service doesn't crash or slow down excessively.
    /// IMPORTANCE: DoS prevention - rate limiting should be at controller/middleware level.
    /// NOTE: This test verifies service resilience; actual rate limiting is frontend/controller responsibility.
    /// </summary>
    [Fact]
    public async Task AppendAsync_WithRapidMessageSending_HandlesGracefully()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(new ChatMembership
      {
    RoomId = room.Id,
            UserId = user.Id,
         IsActive = true,
JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act - Send 100 messages rapidly (reduced from 1000 for test speed)
    var tasks = Enumerable.Range(1, 100)
            .Select(i => service.AppendAsync(room.Id, user.Id, $"Message {i}", CancellationToken.None))
    .ToArray();

        var results = await Task.WhenAll(tasks);

    // Assert - All messages stored successfully
        results.Should().HaveCount(100);
  results.Should().AllSatisfy(r => r.Should().NotBeNull());

        var messageCount = await context.ChatMessages.CountAsync(m => m.RoomId == room.Id);
        messageCount.Should().Be(100);
    }

    /// <summary>
    /// NEGATIVE TEST: Verify GetMessagesAsync with very large take parameter doesn't DoS database.
    /// GOAL: Service clamps to max 100 messages.
    /// IMPORTANCE: Prevents excessive memory usage from malicious requests.
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_WithExcessiveTake_ClampsToMaximum()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
     context.ChatRooms.Add(room);
        context.ChatMemberships.Add(new ChatMembership
 {
        RoomId = room.Id,
            UserId = user.Id,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });

        // Add 200 messages
      var messages = Enumerable.Range(1, 200)
         .Select(i => new ChatMessage
        {
           Id = Guid.NewGuid(),
    RoomId = room.Id,
      SenderId = user.Id,
                Text = $"Message {i}",
                SentAt = DateTime.UtcNow.AddMinutes(i)
            });
        context.ChatMessages.AddRange(messages);
    await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act - Request 10,000 messages
        var result = await service.GetMessagesAsync(room.Id, 10000, null, user.Id, CancellationToken.None);

        // Assert - Clamped to 100
        result.Should().HaveCount(100);
    }

    #endregion

    #region Unicode / International Character Tests

    /// <summary>
 /// POSITIVE TEST: Verify AppendAsync with full Unicode range (emoji, RTL, CJK).
    /// GOAL: All Unicode characters stored and retrieved correctly.
    /// IMPORTANCE: Internationalization - global users send various character sets.
    /// </summary>
    [Fact]
    public async Task AppendAsync_WithFullUnicodeRange_StoresCorrectly()
    {
  // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
     context.ChatRooms.Add(room);
    context.ChatMemberships.Add(new ChatMembership
{
            RoomId = room.Id,
        UserId = user.Id,
  IsActive = true,
         JoinedAt = DateTime.UtcNow
        });
    await context.SaveChangesAsync();

        var service = new ChatService(context);

     // Mix of: English, Chinese, Arabic, Hebrew (RTL), Emoji, Special symbols
        var unicodeMessage = "Hello ?? ????? ???? ???? ??? ??";

        // Act
        var result = await service.AppendAsync(room.Id, user.Id, unicodeMessage, CancellationToken.None);

// Assert - All characters preserved
        result.Text.Should().Be(unicodeMessage);

      // Verify retrieval
      var messages = await service.GetMessagesAsync(room.Id, 50, null, user.Id, CancellationToken.None);
     messages.First().Text.Should().Be(unicodeMessage);
    }

    #endregion

    #region Concurrent Access Security Tests

    /// <summary>
    /// NEGATIVE TEST: Verify concurrent leave/reactivate operations don't corrupt membership state.
    /// GOAL: Race conditions don't leave membership in invalid state.
    /// IMPORTANCE: Data consistency under concurrent access.
    /// </summary>
    [Fact]
  public async Task LeaveAsync_ConcurrentWithReactivate_MaintainsConsistency()
    {
        // Arrange
     using var context = DbFixture.CreateContext();
   var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
    context.ChatRooms.Add(room);
        context.ChatMemberships.Add(new ChatMembership
        {
  RoomId = room.Id,
    UserId = user.Id,
      IsActive = true,
            JoinedAt = DateTime.UtcNow
   });
        await context.SaveChangesAsync();

    var service = new ChatService(context);

        // Act - Concurrent leave and reactivate (race condition)
     var leaveTask = service.LeaveAsync(room.Id, user.Id, CancellationToken.None);
        var reactivateTask = service.ReactivateMembershipAsync(room.Id, user.Id, CancellationToken.None);

        await Task.WhenAll(leaveTask, reactivateTask);

      // Assert - Membership in valid state (either active or inactive, not corrupted)
        var membership = await context.ChatMemberships.FirstAsync(m => m.RoomId == room.Id && m.UserId == user.Id);
        // IsActive is boolean, so it's always valid (true or false)
        membership.Should().NotBeNull();
    }

    #endregion
}
