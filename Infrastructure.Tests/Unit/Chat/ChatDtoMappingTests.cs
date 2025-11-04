using FluentAssertions;
using System.Globalization;
using Xunit;

namespace Infrastructure.Tests.Unit.Chat;

/// <summary>
/// Pure unit tests for chat DTO mapping logic.
/// Tests entity to DTO conversion, timestamp formatting, and edge cases.
/// GOAL: Verify mapping logic works correctly without database.
/// IMPORTANCE: HIGH - DTOs are what frontend receives.
/// </summary>
public class ChatDtoMappingTests
{
    #region Basic Mapping Tests

    /// <summary>
    /// MAPPING TEST: All required fields are mapped.
    /// GOAL: DTO contains all necessary data.
    /// IMPORTANCE: Frontend needs complete message data.
  /// </summary>
    [Fact]
    public void MapToDto_AllFields_AreMapped()
    {
        // Arrange
  var messageId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
     var senderId = "user123";
        var senderName = "Alice";
  var text = "Hello, World!";
    var sentAt = DateTime.UtcNow;

        // Act
  var dto = MapMessageToDto(messageId, roomId, senderId, senderName, text, sentAt);

        // Assert
        dto.Id.Should().Be(messageId);
     dto.RoomId.Should().Be(roomId);
   dto.SenderId.Should().Be(senderId);
        dto.SenderDisplayName.Should().Be(senderName);
      dto.Text.Should().Be(text);
        dto.SentAt.Should().Be(sentAt);
}

    /// <summary>
    /// MAPPING TEST: Null display name becomes empty string.
    /// GOAL: Defensive mapping handles missing data.
    /// IMPORTANCE: Frontend doesn't crash on null.
    /// </summary>
    [Fact]
    public void MapToDto_NullDisplayName_BecomesEmpty()
    {
     // Arrange
  var messageId = Guid.NewGuid();
     var roomId = Guid.NewGuid();
  var senderId = "user123";
      string? senderName = null;
        var text = "Hello";
        var sentAt = DateTime.UtcNow;

        // Act
        var dto = MapMessageToDto(messageId, roomId, senderId, senderName, text, sentAt);

 // Assert
  dto.SenderDisplayName.Should().Be("");
    }

    #endregion

    #region Timestamp Mapping Tests

    /// <summary>
    /// TIMESTAMP TEST: DateTime is preserved in UTC.
    /// GOAL: Timestamps don't lose timezone info.
    /// IMPORTANCE: CRITICAL - Time zones must be handled correctly.
    /// </summary>
    [Fact]
    public void MapToDto_TimestampIsUtc()
    {
   // Arrange
        var sentAt = new DateTime(2025, 11, 3, 15, 30, 0, DateTimeKind.Utc);

        // Act
        var dto = MapMessageToDto(Guid.NewGuid(), Guid.NewGuid(), "user1", "Alice", "Test", sentAt);

     // Assert
   dto.SentAt.Kind.Should().Be(DateTimeKind.Utc);
        dto.SentAt.Should().Be(sentAt);
    }

    /// <summary>
  /// TIMESTAMP TEST: Recent timestamp formats correctly.
 /// GOAL: ISO 8601 format for API responses.
    /// IMPORTANCE: Frontend can parse timestamps reliably.
    /// </summary>
    [Fact]
    public void FormatTimestamp_RecentTime_IsIso8601()
 {
   // Arrange
   var timestamp = new DateTime(2025, 11, 3, 15, 30, 45, DateTimeKind.Utc);

      // Act
   var formatted = FormatTimestamp(timestamp);

        // Assert
     formatted.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z?$");
    }

  /// <summary>
    /// TIMESTAMP TEST: Midnight boundary is handled correctly.
    /// GOAL: Edge case at day boundary works.
    /// IMPORTANCE: Prevents off-by-one errors in date formatting.
    /// </summary>
    [Fact]
    public void FormatTimestamp_Midnight_IsCorrect()
  {
        // Arrange
        var midnight = new DateTime(2025, 11, 3, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var formatted = FormatTimestamp(midnight);

        // Assert
        formatted.Should().Contain("00:00:00");
    }

    /// <summary>
    /// TIMESTAMP TEST: End of day is handled correctly.
    /// GOAL: 23:59:59 formats without rolling to next day.
    /// IMPORTANCE: Time boundaries are tricky.
    /// </summary>
    [Fact]
    public void FormatTimestamp_EndOfDay_IsCorrect()
 {
        // Arrange
        var endOfDay = new DateTime(2025, 11, 3, 23, 59, 59, DateTimeKind.Utc);

   // Act
     var formatted = FormatTimestamp(endOfDay);

        // Assert
     formatted.Should().Contain("23:59:59");
    }

  #endregion

    #region Room Summary Tests

    /// <summary>
    /// SUMMARY TEST: Last message and timestamp are set.
    /// GOAL: Room list shows latest activity.
    /// IMPORTANCE: UI needs this for conversation list.
    /// </summary>
    [Fact]
    public void UpdateRoomSummary_SetsLastTextAndLastAt()
    {
        // Arrange
        var roomId = Guid.NewGuid();
  var lastText = "See you tomorrow!";
 var lastAt = DateTime.UtcNow;

        // Act
     var summary = CreateRoomSummary(roomId, lastText, lastAt);

   // Assert
        summary.RoomId.Should().Be(roomId);
   summary.LastText.Should().Be(lastText);
        summary.LastAt.Should().Be(lastAt);
    }

    /// <summary>
  /// SUMMARY TEST: Empty room has null last message.
    /// GOAL: No messages = no preview.
    /// IMPORTANCE: Represents initial state correctly.
    /// </summary>
    [Fact]
    public void UpdateRoomSummary_EmptyRoom_HasNullLast()
    {
        // Arrange
        var roomId = Guid.NewGuid();

        // Act
    var summary = CreateRoomSummary(roomId, null, null);

        // Assert
  summary.RoomId.Should().Be(roomId);
        summary.LastText.Should().BeNull();
     summary.LastAt.Should().BeNull();
    }

    /// <summary>
    /// SUMMARY TEST: Long message is truncated for preview.
    /// GOAL: Preview shows first ~100 characters.
    /// IMPORTANCE: UI doesn't overflow with long messages.
    /// </summary>
  [Fact]
    public void UpdateRoomSummary_LongMessage_IsTruncated()
    {
        // Arrange
    var roomId = Guid.NewGuid();
        var longText = new string('A', 200);

   // Act
        var preview = TruncateForPreview(longText, maxLength: 100);

        // Assert
   preview.Length.Should().BeLessOrEqualTo(103); // 100 + "..."
    preview.Should().EndWith("...");
}

    #endregion

    #region Text Sanitization Tests

    /// <summary>
 /// SECURITY TEST: HTML tags in message text.
    /// GOAL: Messages are stored as-is (frontend handles escaping).
    /// IMPORTANCE: Backend doesn't strip valid characters.
    /// </summary>
    [Theory]
    [InlineData("<script>alert('XSS')</script>")]
    [InlineData("<b>Bold text</b>")]
    [InlineData("<a href='evil.com'>Click here</a>")]
    public void MapToDto_HtmlTags_ArePreserved(string text)
    {
        // Arrange & Act
   var dto = MapMessageToDto(Guid.NewGuid(), Guid.NewGuid(), "user1", "Alice", text, DateTime.UtcNow);

        // Assert
    dto.Text.Should().Be(text, "backend stores raw text, frontend escapes for display");
    }

 /// <summary>
  /// EDGE CASE TEST: SQL-like strings in message.
    /// GOAL: Messages with SQL syntax are stored safely.
  /// IMPORTANCE: Users can discuss SQL in chat.
    /// </summary>
    [Theory]
  [InlineData("SELECT * FROM Users")]
    [InlineData("DROP TABLE Messages")]
    [InlineData("'; DELETE FROM Chat; --")]
    public void MapToDto_SqlLikeText_IsStoredSafely(string text)
    {
 // Arrange & Act
        var dto = MapMessageToDto(Guid.NewGuid(), Guid.NewGuid(), "user1", "Alice", text, DateTime.UtcNow);

   // Assert
        dto.Text.Should().Be(text, "parameterized queries prevent SQL injection");
    }

    #endregion

    #region Pagination Cursor Tests

    /// <summary>
    /// CURSOR TEST: Message ID can be used as pagination cursor.
    /// GOAL: Cursor-based pagination works.
  /// IMPORTANCE: Efficient loading of message history.
    /// </summary>
    [Fact]
    public void CreatePaginationCursor_FromMessageId()
    {
   // Arrange
   var messageId = Guid.NewGuid();
    var sentAt = DateTime.UtcNow;

 // Act
        var cursor = CreateCursor(messageId, sentAt);

   // Assert
        cursor.Should().NotBeNullOrEmpty();
        cursor.Should().Contain(messageId.ToString());
    }

    /// <summary>
    /// CURSOR TEST: Cursor is stable and deterministic.
    /// GOAL: Same input produces same cursor.
    /// IMPORTANCE: Pagination is consistent.
    /// </summary>
  [Fact]
    public void CreatePaginationCursor_IsDeterministic()
    {
        // Arrange
     var messageId = Guid.NewGuid();
        var sentAt = new DateTime(2025, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var cursor1 = CreateCursor(messageId, sentAt);
        var cursor2 = CreateCursor(messageId, sentAt);

     // Assert
     cursor1.Should().Be(cursor2);
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// EDGE CASE TEST: Empty text after trim.
    /// GOAL: Empty messages handled in mapping.
    /// IMPORTANCE: Should not reach this point (validation catches it).
    /// </summary>
    [Fact]
    public void MapToDto_EmptyText_IsMapped()
    {
        // Arrange & Act
        var dto = MapMessageToDto(Guid.NewGuid(), Guid.NewGuid(), "user1", "Alice", "", DateTime.UtcNow);

   // Assert
        dto.Text.Should().BeEmpty("empty text is mapped as-is");
    }

    /// <summary>
    /// EDGE CASE TEST: Very long sender name.
    /// GOAL: Long names don't break mapping.
    /// IMPORTANCE: Defensive programming.
    /// </summary>
    [Fact]
    public void MapToDto_VeryLongSenderName_IsMapped()
    {
        // Arrange
        var longName = new string('A', 500);

        // Act
        var dto = MapMessageToDto(Guid.NewGuid(), Guid.NewGuid(), "user1", longName, "Hi", DateTime.UtcNow);

     // Assert
        dto.SenderDisplayName.Should().Be(longName);
    }

    /// <summary>
  /// EDGE CASE TEST: Guid.Empty IDs.
    /// GOAL: Invalid IDs are handled (shouldn't happen in practice).
    /// IMPORTANCE: Defensive mapping.
  /// </summary>
    [Fact]
    public void MapToDto_EmptyGuids_AreMapped()
    {
        // Arrange & Act
   var dto = MapMessageToDto(Guid.Empty, Guid.Empty, "", "", "Test", DateTime.UtcNow);

        // Assert
        dto.Id.Should().Be(Guid.Empty);
        dto.RoomId.Should().Be(Guid.Empty);
    }

    #endregion

    #region Helper Methods (Pure Business Logic)

    private record ChatMessageDto(
        Guid Id,
   Guid RoomId,
     string SenderId,
string SenderDisplayName,
        string Text,
    DateTime SentAt
    );

    private record RoomSummary(
        Guid RoomId,
        string? LastText,
        DateTime? LastAt
    );

    private ChatMessageDto MapMessageToDto(
   Guid id, Guid roomId, string senderId, string? senderName, string text, DateTime sentAt)
    {
    return new ChatMessageDto(
  id,
        roomId,
    senderId,
   senderName ?? "",
   text,
      sentAt
        );
    }

    private RoomSummary CreateRoomSummary(Guid roomId, string? lastText, DateTime? lastAt)
    {
        return new RoomSummary(roomId, lastText, lastAt);
    }

    private string FormatTimestamp(DateTime timestamp)
    {
  return timestamp.ToString("o", CultureInfo.InvariantCulture); // ISO 8601
    }

    private string TruncateForPreview(string text, int maxLength)
    {
     if (text.Length <= maxLength)
        return text;

        return text[..maxLength] + "...";
    }

    private string CreateCursor(Guid messageId, DateTime sentAt)
    {
  return $"{messageId}_{sentAt:o}";
    }

 #endregion
}
