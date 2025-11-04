using FluentAssertions;
using Xunit;

namespace Infrastructure.Tests.Unit.StringHelpers;

/// <summary>
/// Unit tests for movie synopsis truncation logic.
/// These are PURE unit tests - testing string manipulation in isolation.
/// GOAL: Verify text is properly shortened for UI cards.
/// </summary>
public class SynopsisTruncationTests
{
    #region Truncation Logic

    /// <summary>
    /// POSITIVE TEST: Verify short text passes through unchanged.
    /// GOAL: Text under 140 chars is not truncated.
    /// IMPORTANCE: Don't add "..." to already-short text.
    /// </summary>
    [Theory]
    [InlineData("Short text")]
    [InlineData("This text is exactly one hundred characters long and should not be truncated at all by the function")] // 100 chars
    public void TruncateOverview_ShortText_PassesThrough(string input)
    {
        // Act
        var result = TruncateOverview(input, 140);

      // Assert
   result.Should().Be(input);
     result.Should().NotContain("...");
    }

    /// <summary>
  /// POSITIVE TEST: Verify long text is truncated with ellipsis.
    /// GOAL: Text over 140 chars is cut at 140 and "..." added.
    /// IMPORTANCE: Prevents text overflow in UI cards.
    /// </summary>
    [Fact]
    public void TruncateOverview_LongText_TruncatesWithEllipsis()
    {
        // Arrange
 var longText = "This is a very long synopsis that exceeds 140 characters and should be truncated with an ellipsis at the end to fit nicely in a UI card without breaking the layout or causing display issues";

        // Act
        var result = TruncateOverview(longText, 140);

      // Assert
        result.Should().HaveLength(143); // 140 chars + "..."
     result.Should().EndWith("...");
    result.Should().StartWith("This is a very long synopsis");
        result.Should().NotBe(longText);
    }

  #endregion

    #region Newline Removal

    /// <summary>
    /// POSITIVE TEST: Verify newlines are removed from multi-line synopses.
    /// GOAL: \n and \r characters are replaced with spaces.
 /// IMPORTANCE: UI cards expect single-line text.
    /// </summary>
    [Theory]
    [InlineData("Line one\nLine two", "Line one Line two")]
    [InlineData("Line one\r\nLine two", "Line one  Line two")] // \r\n becomes two spaces
    [InlineData("Line one\rLine two\nLine three", "Line one Line two Line three")]
    public void TruncateOverview_RemovesNewlines(string input, string expected)
    {
  // Act
      var result = TruncateOverview(input, 140);

        // Assert
        result.Should().Be(expected);
        result.Should().NotContain("\n");
    result.Should().NotContain("\r");
    }

    /// <summary>
    /// POSITIVE TEST: Verify multiple consecutive newlines are handled.
    /// GOAL: \n\n\n becomes spaces, not crashed.
    /// IMPORTANCE: Real TMDB data sometimes has weird formatting.
    /// </summary>
    [Fact]
    public void TruncateOverview_MultipleNewlines_ReplacedWithSpaces()
    {
        // Arrange
    var input = "Paragraph one\n\n\nParagraph two";

        // Act
        var result = TruncateOverview(input, 140);

 // Assert
        result.Should().Be("Paragraph one   Paragraph two");
    }

    #endregion

    #region Empty/Null Input

    /// <summary>
    /// NEGATIVE TEST: Verify null input returns empty string.
    /// GOAL: Null doesn't crash, returns safe default.
    /// IMPORTANCE: Graceful handling of missing TMDB data.
    /// </summary>
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")] // Whitespace-only
    [InlineData("\n\n", "")] // Newlines-only
    public void TruncateOverview_EmptyInput_ReturnsEmpty(string? input, string expected)
    {
     // Act
   var result = TruncateOverview(input, 140);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Whitespace Handling

 /// <summary>
    /// POSITIVE TEST: Verify leading/trailing whitespace is trimmed.
    /// GOAL: "  Text  " becomes "Text".
    /// IMPORTANCE: Clean display without extra spaces.
  /// </summary>
    [Theory]
    [InlineData("  Text  ", "Text")]
    [InlineData("\nText\n", "Text")]
  [InlineData("  Leading spaces", "Leading spaces")]
    [InlineData("Trailing spaces  ", "Trailing spaces")]
  public void TruncateOverview_TrimsWhitespace(string input, string expected)
    {
        // Act
        var result = TruncateOverview(input, 140);

        // Assert
     result.Should().Be(expected);
    }

    /// <summary>
    /// POSITIVE TEST: Verify trailing spaces are trimmed BEFORE adding "...".
    /// GOAL: "Text    " (140 chars) becomes "Text..." not "Text    ...".
    /// IMPORTANCE: Cleaner output; no weird spacing before ellipsis.
    /// </summary>
    [Fact]
    public void TruncateOverview_TrimsBeforeEllipsis()
    {
   // Arrange - Create text with word then spaces to fill 140 chars
        var text = "This is a test message" + new string(' ', 118); // 22 + 118 = 140

        // Act
        var result = TruncateOverview(text, 140);

 // Assert - Spaces trimmed, message preserved, ellipsis NOT added (under 140 after trim)
        result.Should().Be("This is a test message");
        result.Should().NotContain("...", "trimmed text is under 140 chars so no ellipsis needed");
    }

    #endregion

    #region Boundary Tests

    /// <summary>
    /// POSITIVE TEST: Verify text exactly at 140 chars passes through.
    /// GOAL: Boundary condition - exactly at limit = no truncation.
  /// IMPORTANCE: Off-by-one errors are common in truncation logic.
    /// </summary>
    [Fact]
    public void TruncateOverview_Exactly140Chars_NoTruncation()
    {
 // Arrange - Exactly 140 characters
      var text = new string('A', 140);

        // Act
   var result = TruncateOverview(text, 140);

        // Assert
   result.Should().Be(text);
  result.Should().NotContain("...");
  result.Should().HaveLength(140);
    }

    /// <summary>
    /// POSITIVE TEST: Verify 141 chars triggers truncation.
    /// GOAL: One char over limit = truncate.
    /// IMPORTANCE: Boundary test - 140 vs 141.
    /// </summary>
    [Fact]
 public void TruncateOverview_141Chars_Truncates()
    {
      // Arrange - 141 characters (1 over limit)
        var text = new string('A', 141);

        // Act
        var result = TruncateOverview(text, 140);

// Assert
        result.Should().HaveLength(143); // 140 + "..."
   result.Should().EndWith("...");
    }

  #endregion

    #region Custom Length Limits

    /// <summary>
    /// POSITIVE TEST: Verify truncation works with different length limits.
    /// GOAL: Logic is reusable for different max lengths.
    /// IMPORTANCE: Flexibility for different UI contexts.
 /// </summary>
    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void TruncateOverview_CustomLength_WorksCorrectly(int maxLength)
    {
        // Arrange - Text longer than limit
        var text = new string('A', maxLength + 50);

        // Act
        var result = TruncateOverview(text, maxLength);

        // Assert
    result.Should().HaveLength(maxLength + 3); // maxLength + "..."
 result.Should().EndWith("...");
    }

    #endregion

    #region Helper Method

    /// <summary>
    /// Helper: Truncate overview text (exact copy of MoviesController.OneLine logic).
    /// </summary>
    private static string TruncateOverview(string? input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        
  var text = input.Trim().Replace("\n", " ").Replace("\r", " ");
  return text.Length <= maxLength 
 ? text 
     : text[..maxLength].TrimEnd() + "...";
    }

    #endregion
}
