using FluentAssertions;
using Xunit;

namespace Infrastructure.Tests.Unit.BusinessLogic;

/// <summary>
/// Unit tests for match status calculation logic.
/// These are PURE unit tests - testing business rules without database dependencies.
/// GOAL: Verify status determination is correct for all combinations.
/// </summary>
public class MatchStatusCalculationTests
{
    #region Status Determination

    /// <summary>
    /// POSITIVE TEST: Verify match status calculation for all possible states.
    /// GOAL: Correct status string returned based on request/room state.
    /// IMPORTANCE: Frontend displays "Match", "Pending", etc. based on this.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, "none")]
    [InlineData(true, false, false, "pending_sent")]
    [InlineData(false, true, false, "pending_received")]
    [InlineData(true, true, false, "matched")] // Both sent = matched (even without room)
    [InlineData(false, false, true, "matched")] // Room exists = matched
    [InlineData(true, false, true, "matched")] // Room + sent = matched
    [InlineData(false, true, true, "matched")] // Room + received = matched
    [InlineData(true, true, true, "matched")] // All flags = matched
    public void DetermineMatchStatus_ReturnsCorrectStatus(
        bool hasSentRequest, 
        bool hasReceivedRequest, 
      bool hasRoom, 
        string expectedStatus)
    {
        // Act - Simulate MatchService.GetMatchStatusAsync() logic
        var status = DetermineMatchStatus(hasSentRequest, hasReceivedRequest, hasRoom);

        // Assert
        status.Should().Be(expectedStatus);
    }

    /// <summary>
    /// POSITIVE TEST: Verify status is "none" when no interactions exist.
    /// GOAL: Default state for users who haven't interacted.
    /// IMPORTANCE: Most common initial state.
    /// </summary>
    [Fact]
    public void DetermineMatchStatus_WithNoInteractions_ReturnsNone()
    {
        // Act
        var status = DetermineMatchStatus(
            hasSentRequest: false,
            hasReceivedRequest: false,
            hasRoom: false
        );

  // Assert
   status.Should().Be("none");
    }

    /// <summary>
    /// POSITIVE TEST: Verify room existence always means "matched".
    /// GOAL: If chat room exists, status is always "matched" regardless of requests.
    /// IMPORTANCE: Room is proof of mutual match.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void DetermineMatchStatus_WithRoom_AlwaysReturnsMatched(
        bool hasSentRequest,
    bool hasReceivedRequest)
    {
        // Act
    var status = DetermineMatchStatus(hasSentRequest, hasReceivedRequest, hasRoom: true);

      // Assert
        status.Should().Be("matched", "room existence always means matched");
    }

    #endregion

    #region Action Flags (canMatch, canDecline)

    /// <summary>
    /// POSITIVE TEST: Verify canMatch and canDecline flags for UI buttons.
    /// GOAL: UI knows when to show "Match" vs "Decline" buttons.
    /// IMPORTANCE: User interaction depends on these flags.
    /// </summary>
    [Theory]
  [InlineData("none", true, false)] // Can match, can't decline
    [InlineData("pending_sent", false, false)] // Can't match (already sent), can't decline own request
    [InlineData("pending_received", true, true)] // Can match (accept), can decline
    [InlineData("matched", false, false)] // Already matched, no actions needed
 public void DetermineMatchFlags_ReturnsCorrectFlags(
        string status, 
        bool expectedCanMatch, 
      bool expectedCanDecline)
  {
        // Act - Simulate flag determination logic
        var (canMatch, canDecline) = DetermineMatchFlags(status);

        // Assert
        canMatch.Should().Be(expectedCanMatch);
    canDecline.Should().Be(expectedCanDecline);
    }

    /// <summary>
    /// POSITIVE TEST: Verify "pending_received" enables both actions.
    /// GOAL: User can either accept (match) or decline incoming request.
    /// IMPORTANCE: Core user workflow - responding to match requests.
    /// </summary>
    [Fact]
    public void DetermineMatchFlags_PendingReceived_EnablesBothActions()
    {
    // Act
   var (canMatch, canDecline) = DetermineMatchFlags("pending_received");

        // Assert
        canMatch.Should().BeTrue("user can accept the request");
        canDecline.Should().BeTrue("user can decline the request");
    }

    /// <summary>
    /// NEGATIVE TEST: Verify "pending_sent" disables both actions.
    /// GOAL: User who sent request must wait; no double-sending or self-declining.
    /// IMPORTANCE: Prevents duplicate requests and confusing UX.
  /// </summary>
    [Fact]
    public void DetermineMatchFlags_PendingSent_DisablesBothActions()
    {
        // Act
        var (canMatch, canDecline) = DetermineMatchFlags("pending_sent");

        // Assert
        canMatch.Should().BeFalse("can't send request again");
        canDecline.Should().BeFalse("can't decline own sent request");
    }

    /// <summary>
    /// NEGATIVE TEST: Verify "matched" status disables all actions.
    /// GOAL: Once matched, no more matching/declining needed.
 /// IMPORTANCE: Prevents confusion after successful match.
  /// </summary>
 [Fact]
  public void DetermineMatchFlags_Matched_DisablesBothActions()
    {
        // Act
  var (canMatch, canDecline) = DetermineMatchFlags("matched");

        // Assert
        canMatch.Should().BeFalse("already matched");
        canDecline.Should().BeFalse("can't decline after matching");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// NEGATIVE TEST: Verify unknown status defaults to safe state (no actions).
    /// GOAL: Graceful handling of unexpected status values.
    /// IMPORTANCE: Defensive programming; prevents errors from bad data.
    /// </summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
  [InlineData(null)]
    [InlineData("MATCHED")] // Case sensitive
    public void DetermineMatchFlags_UnknownStatus_DisablesBothActions(string? status)
    {
        // Act
        var (canMatch, canDecline) = DetermineMatchFlags(status ?? "");

        // Assert
        canMatch.Should().BeFalse("unknown status = no actions allowed");
        canDecline.Should().BeFalse("unknown status = no actions allowed");
  }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Helper: Determine match status (simulates MatchService.GetMatchStatusAsync logic).
    /// </summary>
    private static string DetermineMatchStatus(bool hasSentRequest, bool hasReceivedRequest, bool hasRoom)
    {
        if (hasRoom) return "matched";
        if (hasSentRequest && hasReceivedRequest) return "matched";
        if (hasReceivedRequest) return "pending_received";
        if (hasSentRequest) return "pending_sent";
        return "none";
    }

    /// <summary>
    /// Helper: Determine canMatch and canDecline flags (simulates MatchService logic).
    /// </summary>
    private static (bool canMatch, bool canDecline) DetermineMatchFlags(string status)
    {
        return status switch
        {
            "none" => (true, false),
   "pending_sent" => (false, false),
         "pending_received" => (true, true),
         "matched" => (false, false),
      _ => (false, false) // Unknown status = no actions
      };
    }

    #endregion
}
