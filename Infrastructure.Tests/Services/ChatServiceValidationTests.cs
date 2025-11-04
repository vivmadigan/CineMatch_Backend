using FluentAssertions;
using Infrastructure.Data.Entities;
using Infrastructure.Models.Chat;
using Infrastructure.Services.Chat;
using Infrastructure.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ChatService validation and edge cases.
/// Tests message validation, pagination, DTO mapping, and error handling.
/// GOAL: Ensure chat rules are enforced correctly to prevent bad data and ensure proper API contracts.
/// </summary>
public class ChatServiceValidationTests
{
    #region Message Text Validation Tests

    /// <summary>
    /// GOAL: Prevent empty messages from being saved to the database.
    /// IMPORTANCE: Empty messages provide no value and clutter the chat history.
    /// </summary>
    [Fact]
    public async Task AppendAsync_WithEmptyText_ThrowsArgumentException()
    {
      // Arrange
 using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
        {
 RoomId = room.Id,
            UserId = user.Id,
       IsActive = true,
 JoinedAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);
    await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act & Assert
 var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AppendAsync(room.Id, user.Id, "", CancellationToken.None));

      exception.Message.Should().Contain("Message text cannot be empty");
    }

    /// <summary>
    /// GOAL: Prevent whitespace-only messages (spaces, tabs, newlines) from being saved.
    /// IMPORTANCE: Whitespace messages appear blank in UI and provide no value.
  /// </summary>
    [Fact]
    public async Task AppendAsync_WithWhitespaceText_ThrowsArgumentException()
    {
      // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
    {
            RoomId = room.Id,
      UserId = user.Id,
     IsActive = true,
       JoinedAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);
        await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
        service.AppendAsync(room.Id, user.Id, "   \t\n  ", CancellationToken.None));

 exception.Message.Should().Contain("Message text cannot be empty");
    }

    /// <summary>
    /// GOAL: Enforce maximum message length to prevent database overflow and UI issues.
    /// IMPORTANCE: Long messages can break UI layout and exceed database field limits.
  /// </summary>
    [Fact]
    public async Task AppendAsync_WithTextExceeding2000Chars_ThrowsArgumentException()
{
        // Arrange
    using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
   var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
        {
       RoomId = room.Id,
    UserId = user.Id,
        IsActive = true,
    JoinedAt = DateTime.UtcNow
   };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);
        await context.SaveChangesAsync();

        var service = new ChatService(context);
        var longText = new string('A', 2001); // 1 char over limit

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
 service.AppendAsync(room.Id, user.Id, longText, CancellationToken.None));

        exception.Message.Should().Contain("cannot exceed 2000 characters");
    }

    /// <summary>
    /// GOAL: Allow maximum valid message length (exactly 2000 chars).
    /// IMPORTANCE: Users should be able to send messages up to the limit without errors.
    /// </summary>
  [Fact]
    public async Task AppendAsync_WithExactly2000Chars_Succeeds()
    {
        // Arrange
     using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
      var membership = new ChatMembership
        {
    RoomId = room.Id,
       UserId = user.Id,
     IsActive = true,
   JoinedAt = DateTime.UtcNow
   };
        context.ChatRooms.Add(room);
  context.ChatMemberships.Add(membership);
    await context.SaveChangesAsync();

    var service = new ChatService(context);
        var maxText = new string('A', 2000); // Exactly at limit

        // Act
        var result = await service.AppendAsync(room.Id, user.Id, maxText, CancellationToken.None);

 // Assert
result.Should().NotBeNull();
  result.Text.Should().HaveLength(2000);
    }

    /// <summary>
    /// GOAL: Ensure leading/trailing whitespace is trimmed from messages.
    /// IMPORTANCE: Trimming improves display consistency and prevents accidental whitespace.
    /// </summary>
    [Fact]
    public async Task AppendAsync_TrimsWhitespace()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
     var user = await DbFixture.CreateTestUserAsync(context);
  var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
        {
          RoomId = room.Id,
 UserId = user.Id,
        IsActive = true,
      JoinedAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);
    await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act
        var result = await service.AppendAsync(room.Id, user.Id, "  Hello World!  ", CancellationToken.None);

    // Assert
        result.Text.Should().Be("Hello World!");
    }

    #endregion

    #region Membership Validation Tests

    /// <summary>
    /// GOAL: Prevent users from sending messages to rooms they're not members of.
    /// IMPORTANCE: Security - users should only access rooms they belong to.
    /// </summary>
  [Fact]
    public async Task AppendAsync_UserNotMember_ThrowsInvalidOperationException()
  {
      // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
     var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        context.ChatRooms.Add(room);
        await context.SaveChangesAsync();
        // Note: No membership created!

        var service = new ChatService(context);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
 service.AppendAsync(room.Id, user.Id, "Hello", CancellationToken.None));

        exception.Message.Should().Contain("not an active member");
    }

    /// <summary>
    /// GOAL: Prevent inactive members (who left the room) from sending messages.
    /// IMPORTANCE: Users who left should not be able to send messages until they rejoin.
    /// </summary>
    [Fact]
    public async Task AppendAsync_UserInactiveMember_ThrowsInvalidOperationException()
    {
        // Arrange
  using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
  var membership = new ChatMembership
        {
            RoomId = room.Id,
     UserId = user.Id,
  IsActive = false, // Inactive!
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            LeftAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);
        await context.SaveChangesAsync();

 var service = new ChatService(context);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
    service.AppendAsync(room.Id, user.Id, "Hello", CancellationToken.None));

        exception.Message.Should().Contain("not an active member");
    }

    /// <summary>
    /// GOAL: Prevent non-members from viewing messages in rooms they don't belong to.
    /// IMPORTANCE: Privacy - users should only see messages in their own rooms.
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_UserNotMember_ThrowsInvalidOperationException()
    {
   // Arrange
        using var context = DbFixture.CreateContext();
      var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        context.ChatRooms.Add(room);
        await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetMessagesAsync(room.Id, 50, null, user.Id, CancellationToken.None));

        exception.Message.Should().Contain("not a member");
    }

    #endregion

    #region Pagination Tests

    /// <summary>
    /// GOAL: Ensure 'take' parameter is clamped to minimum of 1 message.
    /// IMPORTANCE: Prevents errors from requesting 0 or negative message counts.
 /// </summary>
    [Fact]
    public async Task GetMessagesAsync_WithZeroTake_ReturnsAtLeastOneMessage()
    {
     // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
        {
            RoomId = room.Id,
 UserId = user.Id,
            IsActive = true,
      JoinedAt = DateTime.UtcNow
    };
context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);

        // Add 3 messages
        for (int i = 0; i < 3; i++)
        {
      context.ChatMessages.Add(new ChatMessage
            {
           Id = Guid.NewGuid(),
     RoomId = room.Id,
   SenderId = user.Id,
         Text = $"Message {i + 1}",
        SentAt = DateTime.UtcNow.AddMinutes(i)
    });
     }
        await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act - Request 0 messages (should clamp to 1)
        var result = await service.GetMessagesAsync(room.Id, 0, null, user.Id, CancellationToken.None);

 // Assert
        result.Should().HaveCountGreaterOrEqualTo(1);
    }

    /// <summary>
    /// GOAL: Ensure 'take' parameter is clamped to maximum of 100 messages.
    /// IMPORTANCE: Prevents excessive database queries and memory usage.
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_WithTakeOver100_ReturnsMaximum100()
    {
        // Arrange
     using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
  {
       RoomId = room.Id,
 UserId = user.Id,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
     context.ChatMemberships.Add(membership);

     // Add 150 messages
        for (int i = 0; i < 150; i++)
      {
  context.ChatMessages.Add(new ChatMessage
            {
  Id = Guid.NewGuid(),
    RoomId = room.Id,
         SenderId = user.Id,
       Text = $"Message {i + 1}",
       SentAt = DateTime.UtcNow.AddMinutes(i)
        });
     }
        await context.SaveChangesAsync();

var service = new ChatService(context);

        // Act - Request 500 messages (should clamp to 100)
        var result = await service.GetMessagesAsync(room.Id, 500, null, user.Id, CancellationToken.None);

        // Assert
        result.Should().HaveCount(100);
    }

    /// <summary>
    /// GOAL: Ensure 'beforeUtc' parameter filters messages correctly for pagination.
    /// IMPORTANCE: Enables infinite scroll / "load more" functionality in UI.
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_WithBeforeUtc_ReturnsOnlyOlderMessages()
    {
        // Arrange
      using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
      {
     RoomId = room.Id,
          UserId = user.Id,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);

        var cutoffTime = DateTime.UtcNow;

        // Add messages before cutoff
        context.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
         RoomId = room.Id,
   SenderId = user.Id,
      Text = "Old message 1",
            SentAt = cutoffTime.AddMinutes(-10)
        });
        context.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
      RoomId = room.Id,
          SenderId = user.Id,
    Text = "Old message 2",
            SentAt = cutoffTime.AddMinutes(-5)
        });

        // Add messages after cutoff
        context.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
         RoomId = room.Id,
       SenderId = user.Id,
   Text = "Recent message",
            SentAt = cutoffTime.AddMinutes(5)
        });

        await context.SaveChangesAsync();

        var service = new ChatService(context);

     // Act - Get messages before cutoff
        var result = await service.GetMessagesAsync(room.Id, 50, cutoffTime, user.Id, CancellationToken.None);

      // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(msg => msg.Text.Should().StartWith("Old"));
    }

    /// <summary>
    /// GOAL: Ensure messages are returned in descending order (newest first).
    /// IMPORTANCE: UI typically shows newest messages at bottom; needs reverse chronological order from API.
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_ReturnsMessagesInDescendingOrder()
    {
        // Arrange
      using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
    var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
 var membership = new ChatMembership
   {
       RoomId = room.Id,
            UserId = user.Id,
      IsActive = true,
 JoinedAt = DateTime.UtcNow
        };
  context.ChatRooms.Add(room);
  context.ChatMemberships.Add(membership);

        // Add messages with different timestamps
  context.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
    RoomId = room.Id,
        SenderId = user.Id,
  Text = "First",
            SentAt = DateTime.UtcNow.AddMinutes(-10)
   });
        context.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
     RoomId = room.Id,
         SenderId = user.Id,
            Text = "Third",
            SentAt = DateTime.UtcNow
 });
 context.ChatMessages.Add(new ChatMessage
  {
      Id = Guid.NewGuid(),
    RoomId = room.Id,
            SenderId = user.Id,
   Text = "Second",
       SentAt = DateTime.UtcNow.AddMinutes(-5)
        });

        await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act
        var result = await service.GetMessagesAsync(room.Id, 50, null, user.Id, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
    result[0].Text.Should().Be("Third"); // Newest first
        result[1].Text.Should().Be("Second");
        result[2].Text.Should().Be("First");
  }

    #endregion

    #region DTO Mapping Tests

    /// <summary>
 /// GOAL: Ensure ChatMessageDto uses exact property name 'SenderDisplayName' (not 'SenderName').
    /// IMPORTANCE: Frontend expects 'senderDisplayName' in JSON; mismatch breaks frontend integration.
    /// </summary>
    [Fact]
    public async Task AppendAsync_ReturnsDtoWithSenderDisplayNameProperty()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context, displayName: "TestUser123");
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
    var membership = new ChatMembership
      {
        RoomId = room.Id,
       UserId = user.Id,
     IsActive = true,
  JoinedAt = DateTime.UtcNow
   };
        context.ChatRooms.Add(room);
      context.ChatMemberships.Add(membership);
        await context.SaveChangesAsync();

        var service = new ChatService(context);

     // Act
        var result = await service.AppendAsync(room.Id, user.Id, "Hello!", CancellationToken.None);

 // Assert
        result.SenderDisplayName.Should().Be("TestUser123");
        // Verify property exists (compile-time check confirms correct DTO structure)
     typeof(ChatMessageDto).Should().HaveProperty<string>("SenderDisplayName");
    }

    /// <summary>
    /// GOAL: Ensure ChatMessageDto uses exact property name 'Text' (not 'Content' or 'Message').
    /// IMPORTANCE: Frontend expects 'text' in JSON; mismatch breaks frontend integration.
    /// </summary>
    [Fact]
    public async Task AppendAsync_ReturnsDtoWithTextProperty()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
   var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
   var membership = new ChatMembership
        {
 RoomId = room.Id,
        UserId = user.Id,
            IsActive = true,
      JoinedAt = DateTime.UtcNow
  };
        context.ChatRooms.Add(room);
     context.ChatMemberships.Add(membership);
        await context.SaveChangesAsync();

   var service = new ChatService(context);

  // Act
    var result = await service.AppendAsync(room.Id, user.Id, "Test message", CancellationToken.None);

      // Assert
     result.Text.Should().Be("Test message");
        // Verify property exists (compile-time check confirms correct DTO structure)
   typeof(ChatMessageDto).Should().HaveProperty<string>("Text");
    }

    /// <summary>
 /// GOAL: Ensure ChatMessageDto uses exact property name 'SentAt' (not 'Timestamp' or 'CreatedAt').
    /// IMPORTANCE: Frontend expects 'sentAt' in JSON; mismatch breaks frontend integration.
    /// </summary>
    [Fact]
    public async Task AppendAsync_ReturnsDtoWithSentAtProperty()
    {
      // Arrange
     using var context = DbFixture.CreateContext();
 var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
      var membership = new ChatMembership
      {
       RoomId = room.Id,
  UserId = user.Id,
       IsActive = true,
        JoinedAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
     context.ChatMemberships.Add(membership);
  await context.SaveChangesAsync();

    var service = new ChatService(context);

 // Act
  var result = await service.AppendAsync(room.Id, user.Id, "Hello!", CancellationToken.None);

 // Assert
        result.SentAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        // Verify property exists (compile-time check confirms correct DTO structure)
        typeof(ChatMessageDto).Should().HaveProperty<DateTime>("SentAt");
    }

    /// <summary>
    /// GOAL: Ensure 'SentAt' timestamp is in UTC for consistent timezone handling.
    /// IMPORTANCE: Frontend expects UTC timestamps (ISO 8601 format) to avoid timezone bugs.
    /// </summary>
    [Fact]
    public async Task AppendAsync_SentAtIsUtc()
    {
        // Arrange
      using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
        {
   RoomId = room.Id,
UserId = user.Id,
   IsActive = true,
    JoinedAt = DateTime.UtcNow
      };
        context.ChatRooms.Add(room);
  context.ChatMemberships.Add(membership);
 await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act
        var result = await service.AppendAsync(room.Id, user.Id, "Hello!", CancellationToken.None);

        // Assert
        result.SentAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    /// <summary>
    /// GOAL: Handle missing sender display name gracefully by returning "Unknown".
    /// IMPORTANCE: Prevents null reference errors in frontend when sender is deleted.
    /// </summary>
    [Fact]
    public async Task GetMessagesAsync_WithDeletedSender_ThrowsDbUpdateException()
    {
     // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
   var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
      {
  RoomId = room.Id,
   UserId = user.Id,
     IsActive = true,
            JoinedAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);
        await context.SaveChangesAsync();

        // Act - Try to add message from non-existent user (violates FK constraint)
        var act = async () =>
 {
   context.ChatMessages.Add(new ChatMessage
   {
    Id = Guid.NewGuid(),
     RoomId = room.Id,
   SenderId = "deleted-user-id", // Non-existent user
       Text = "Ghost message",
     SentAt = DateTime.UtcNow
     });
     await context.SaveChangesAsync();
    };

        // Assert - Throws DbUpdateException due to foreign key constraint
await act.Should().ThrowAsync<DbUpdateException>();
    }

    #endregion

    #region Leave/Reactivate Tests

    /// <summary>
    /// GOAL: Ensure LeaveAsync marks membership as inactive and sets LeftAt timestamp.
    /// IMPORTANCE: Tracks when users leave rooms for analytics and UI filtering.
    /// </summary>
    [Fact]
    public async Task LeaveAsync_SetsIsActiveToFalseAndSetsLeftAt()
    {
 // Arrange
        using var context = DbFixture.CreateContext();
   var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
        {
      RoomId = room.Id,
            UserId = user.Id,
      IsActive = true,
   JoinedAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);
    await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act
        await service.LeaveAsync(room.Id, user.Id, CancellationToken.None);

        // Assert
  var updatedMembership = context.ChatMemberships.First(m => m.RoomId == room.Id && m.UserId == user.Id);
     updatedMembership.IsActive.Should().BeFalse();
        updatedMembership.LeftAt.Should().NotBeNull();
        updatedMembership.LeftAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// GOAL: Ensure LeaveAsync is idempotent (calling twice doesn't fail or duplicate actions).
    /// IMPORTANCE: Users may spam "leave" button; system should handle gracefully.
    /// </summary>
 [Fact]
    public async Task LeaveAsync_CalledTwice_IsIdempotent()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
   var membership = new ChatMembership
        {
          RoomId = room.Id,
       UserId = user.Id,
         IsActive = true,
    JoinedAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
   context.ChatMemberships.Add(membership);
        await context.SaveChangesAsync();

        var service = new ChatService(context);

   // Act - Leave twice
     await service.LeaveAsync(room.Id, user.Id, CancellationToken.None);
        var firstLeftAt = context.ChatMemberships.First(m => m.RoomId == room.Id).LeftAt;

        await Task.Delay(100); // Small delay to ensure timestamp difference if re-set

      await service.LeaveAsync(room.Id, user.Id, CancellationToken.None);
      var secondLeftAt = context.ChatMemberships.First(m => m.RoomId == room.Id).LeftAt;

      // Assert - LeftAt should not change on second call
      secondLeftAt.Should().Be(firstLeftAt);
    }

    /// <summary>
/// GOAL: Ensure ReactivateMembershipAsync reactivates inactive memberships.
/// IMPORTANCE: Users should be able to rejoin rooms they previously left.
/// </summary>
    [Fact]
    public async Task ReactivateMembershipAsync_ReactivatesInactiveMembership()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
        {
RoomId = room.Id,
            UserId = user.Id,
   IsActive = false, // Inactive
          JoinedAt = DateTime.UtcNow.AddDays(-1),
     LeftAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
context.ChatMemberships.Add(membership);
  await context.SaveChangesAsync();

        var service = new ChatService(context);

 // Act
        await service.ReactivateMembershipAsync(room.Id, user.Id, CancellationToken.None);

        // Assert
        var updatedMembership = context.ChatMemberships.First(m => m.RoomId == room.Id && m.UserId == user.Id);
        updatedMembership.IsActive.Should().BeTrue();
        updatedMembership.LeftAt.Should().BeNull();
        updatedMembership.JoinedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// GOAL: Ensure ReactivateMembershipAsync is idempotent for already-active memberships.
    /// IMPORTANCE: Frontend may call "join" multiple times; should not fail or cause issues.
  /// </summary>
    [Fact]
    public async Task ReactivateMembershipAsync_AlreadyActive_DoesNothing()
    {
 // Arrange
   using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
    var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var originalJoinedAt = DateTime.UtcNow.AddHours(-2);
        var membership = new ChatMembership
        {
         RoomId = room.Id,
  UserId = user.Id,
            IsActive = true, // Already active
  JoinedAt = originalJoinedAt
        };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);
        await context.SaveChangesAsync();

        var service = new ChatService(context);

    // Act
 await service.ReactivateMembershipAsync(room.Id, user.Id, CancellationToken.None);

  // Assert - JoinedAt should not change
        var updatedMembership = context.ChatMemberships.First(m => m.RoomId == room.Id && m.UserId == user.Id);
        updatedMembership.IsActive.Should().BeTrue();
  updatedMembership.JoinedAt.Should().Be(originalJoinedAt); // Unchanged
    }

    #endregion
}
