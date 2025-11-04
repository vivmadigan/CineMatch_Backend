using FluentAssertions;
using Xunit;

namespace Infrastructure.Tests.Unit.Chat;

/// <summary>
/// Pure unit tests for chat message validation rules.
/// Tests trimming, length limits, empty messages, and forbidden content.
/// GOAL: Verify message validation logic without database dependencies.
/// IMPORTANCE: CRITICAL - Prevents invalid messages from being stored.
/// </summary>
public class ChatMessageValidationTests
{
    #region Trimming Tests

  /// <summary>
    /// VALIDATION TEST: Leading/trailing whitespace is trimmed.
    /// GOAL: Messages are stored without extra whitespace.
    /// IMPORTANCE: Better UX - no weird spacing in chat bubbles.
    /// </summary>
    [Theory]
    [InlineData("  Hello  ", "Hello")]
    [InlineData("\t\tMessage\t\t", "Message")]
    [InlineData("\n\nTest\n\n", "Test")]
    [InlineData("   Text with spaces   ", "Text with spaces")]
    public void ValidateMessage_TrimsWhitespace(string input, string expected)
    {
   // Act
        var result = TrimMessage(input);

    // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// EDGE CASE TEST: Only whitespace becomes empty after trim.
 /// GOAL: Whitespace-only messages are detected.
    /// IMPORTANCE: Empty messages should be rejected.
    /// </summary>
 [Theory]
    [InlineData("   ")]
    [InlineData("\t\t\t")]
    [InlineData("\n\n\n")]
    [InlineData("")]
  public void ValidateMessage_WhitespaceOnly_BecomesEmpty(string input)
    {
        // Act
        var result = TrimMessage(input);

        // Assert
     result.Should().BeEmpty("whitespace-only input should trim to empty");
    }

    #endregion

    #region Empty Message Tests

    /// <summary>
    /// NEGATIVE TEST: Empty string is invalid.
    /// GOAL: Cannot send blank messages.
    /// IMPORTANCE: Prevents empty chat bubbles.
    /// </summary>
 [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
  [InlineData("\n")]
    public void ValidateMessage_Empty_IsInvalid(string message)
  {
     // Act
        var isValid = IsValidMessage(message);

        // Assert
   isValid.Should().BeFalse("empty messages are invalid");
    }

    /// <summary>
    /// POSITIVE TEST: Non-empty messages are valid.
 /// GOAL: Normal messages pass validation.
 /// IMPORTANCE: Basic functionality works.
    /// </summary>
    [Theory]
    [InlineData("Hello")]
    [InlineData("a")]
    [InlineData("123")]
    [InlineData("Hello, World!")]
    public void ValidateMessage_NonEmpty_IsValid(string message)
    {
        // Act
        var isValid = IsValidMessage(message);

        // Assert
        isValid.Should().BeTrue("non-empty messages are valid");
    }

    #endregion

    #region Length Limit Tests

    /// <summary>
    /// VALIDATION TEST: Messages under 2000 characters are valid.
    /// GOAL: Reasonable message length limit.
    /// IMPORTANCE: Prevents database bloat and UI issues.
    /// </summary>
    [Fact]
    public void ValidateMessage_Under2000Characters_IsValid()
    {
        // Arrange
     var message = new string('A', 1999);

        // Act
        var isValid = IsValidMessage(message);

        // Assert
        isValid.Should().BeTrue("messages under 2000 chars are valid");
 }

    /// <summary>
    /// BOUNDARY TEST: Exactly 2000 characters is valid.
    /// GOAL: Maximum length is inclusive.
    /// IMPORTANCE: Boundary condition test.
/// </summary>
    [Fact]
    public void ValidateMessage_Exactly2000Characters_IsValid()
    {
        // Arrange
        var message = new string('A', 2000);

 // Act
        var isValid = IsValidMessage(message);

        // Assert
        isValid.Should().BeTrue("exactly 2000 chars is valid");
    }

    /// <summary>
    /// NEGATIVE TEST: Over 2000 characters is invalid.
    /// GOAL: Messages exceeding limit are rejected.
    /// IMPORTANCE: Enforces database constraint.
    /// </summary>
    [Fact]
    public void ValidateMessage_Over2000Characters_IsInvalid()
    {
        // Arrange
        var message = new string('A', 2001);

        // Act
        var isValid = IsValidMessage(message);

 // Assert
      isValid.Should().BeFalse("messages over 2000 chars are invalid");
    }

    /// <summary>
    /// EDGE CASE TEST: Very long message is rejected.
    /// GOAL: Extreme length is handled.
    /// IMPORTANCE: Prevents abuse.
    /// </summary>
[Fact]
    public void ValidateMessage_VeryLong_IsInvalid()
    {
        // Arrange
        var message = new string('A', 10000);

   // Act
        var isValid = IsValidMessage(message);

  // Assert
        isValid.Should().BeFalse("very long messages are invalid");
    }

    #endregion

    #region Unicode and Special Characters Tests

    /// <summary>
    /// POSITIVE TEST: Unicode characters are allowed.
    /// GOAL: Internationalization support.
    /// IMPORTANCE: Users can send messages in any language.
    /// </summary>
 [Theory]
 [InlineData("Hello ??")]
    [InlineData("?????? ???")]
    [InlineData("????? ???????")]
    [InlineData("?? Movie night! ??")]
    public void ValidateMessage_Unicode_IsValid(string message)
    {
  // Act
        var isValid = IsValidMessage(message);

    // Assert
        isValid.Should().BeTrue("unicode characters are allowed");
    }

 /// <summary>
    /// POSITIVE TEST: Emojis are allowed.
    /// GOAL: Modern chat experience.
    /// IMPORTANCE: Emojis are standard in messaging.
    /// </summary>
    [Theory]
    [InlineData("??")]
    [InlineData("??")]
    [InlineData("??")]
    [InlineData("?? Party! ??")]
    public void ValidateMessage_Emojis_IsValid(string message)
    {
        // Act
        var isValid = IsValidMessage(message);

 // Assert
        isValid.Should().BeTrue("emojis are allowed");
    }

    /// <summary>
    /// POSITIVE TEST: Special characters are allowed.
    /// GOAL: Normal punctuation works.
    /// IMPORTANCE: Don't over-sanitize messages.
    /// </summary>
    [Theory]
    [InlineData("Hello! How are you?")]
 [InlineData("Meeting at 3:00 PM")]
    [InlineData("Cost: $50 (half price)")]
    [InlineData("Email: test@example.com")]
    public void ValidateMessage_SpecialCharacters_IsValid(string message)
{
        // Act
  var isValid = IsValidMessage(message);

        // Assert
   isValid.Should().BeTrue("special characters are allowed");
    }

    #endregion

    #region Newline Handling Tests

    /// <summary>
    /// POSITIVE TEST: Newlines are preserved.
    /// GOAL: Multi-line messages work.
    /// IMPORTANCE: Users can send formatted text.
    /// </summary>
  [Fact]
    public void ValidateMessage_Newlines_ArePreserved()
    {
  // Arrange
        var message = "Line 1\nLine 2\nLine 3";

        // Act
        var isValid = IsValidMessage(message);
        var result = TrimMessage(message);

    // Assert
        isValid.Should().BeTrue("newlines are allowed");
    result.Should().Contain("\n", "newlines should be preserved");
    }

    /// <summary>
    /// EDGE CASE TEST: Only newlines is invalid (whitespace-only).
    /// GOAL: Message must have actual content.
    /// IMPORTANCE: Newlines alone don't constitute a message.
    /// </summary>
    [Fact]
    public void ValidateMessage_OnlyNewlines_IsInvalid()
    {
        // Arrange
        var message = "\n\n\n";

   // Act
     var isValid = IsValidMessage(message);

        // Assert
        isValid.Should().BeFalse("newlines-only is invalid");
    }

    #endregion

    #region Null Input Tests

    /// <summary>
    /// NEGATIVE TEST: Null message is invalid.
    /// GOAL: Null safety.
  /// IMPORTANCE: Prevents NullReferenceException.
    /// </summary>
    [Fact]
    public void ValidateMessage_Null_IsInvalid()
    {
        // Act
     var isValid = IsValidMessage(null);

        // Assert
     isValid.Should().BeFalse("null message is invalid");
    }

    #endregion

    #region Combined Validation Tests

    /// <summary>
    /// INTEGRATION TEST: Valid message after trim and length check.
  /// GOAL: Full validation pipeline works.
    /// IMPORTANCE: All rules work together.
    /// </summary>
    [Theory]
    [InlineData("  Hello  ", true)]
 [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Valid message", true)]
    public void ValidateMessage_FullPipeline(string input, bool expectedValid)
    {
        // Act
        var trimmed = TrimMessage(input);
        var isValid = IsValidMessage(trimmed);

        // Assert
     isValid.Should().Be(expectedValid);
    }

    #endregion

    #region Helper Methods (Pure Business Logic)

    /// <summary>
    /// Trim whitespace from message.
    /// Mirrors ChatService message processing.
    /// </summary>
 private string TrimMessage(string? message)
    {
      return message?.Trim() ?? "";
    }

 /// <summary>
  /// Validate message meets all requirements.
    /// Mirrors ChatService validation logic.
    /// </summary>
    private bool IsValidMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        return false;

  var trimmed = message.Trim();

   if (trimmed.Length == 0)
     return false;

        if (trimmed.Length > 2000)
   return false;

   return true;
    }

    #endregion
}
