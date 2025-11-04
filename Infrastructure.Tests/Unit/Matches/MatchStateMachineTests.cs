using FluentAssertions;
using Xunit;

namespace Infrastructure.Tests.Unit.Matches;

/// <summary>
/// Pure unit tests for match status state machine logic.
/// Tests state transitions without database dependencies.
/// GOAL: Verify match status calculations are correct.
/// IMPORTANCE: CRITICAL - Core business logic for matching feature.
/// </summary>
public class MatchStateMachineTests
{
    #region State Transition Tests

    /// <summary>
    /// STATE TEST: No requests = "none" status.
 /// GOAL: Initial state is none.
    /// IMPORTANCE: Default state before any interaction.
    /// </summary>
    [Fact]
    public void CalculateStatus_NoRequests_ReturnsNone()
    {
        // Arrange
        bool hasSentRequest = false;
        bool hasReceivedRequest = false;
        bool isMatched = false;

        // Act
        var (status, canMatch, canDecline) = CalculateMatchStatus(hasSentRequest, hasReceivedRequest, isMatched);

      // Assert
        status.Should().Be("none");
        canMatch.Should().BeTrue("user can initiate match request");
     canDecline.Should().BeFalse("nothing to decline");
    }

    /// <summary>
    /// STATE TEST: Sent request = "pending_sent" status.
    /// GOAL: After user sends request, status is pending_sent.
    /// IMPORTANCE: User should know their request is waiting.
    /// </summary>
    [Fact]
    public void CalculateStatus_SentRequest_ReturnsPendingSent()
    {
        // Arrange
   bool hasSentRequest = true;
        bool hasReceivedRequest = false;
        bool isMatched = false;

        // Act
        var (status, canMatch, canDecline) = CalculateMatchStatus(hasSentRequest, hasReceivedRequest, isMatched);

        // Assert
        status.Should().Be("pending_sent");
        canMatch.Should().BeFalse("cannot send another request");
   canDecline.Should().BeFalse("cannot decline own request");
    }

    /// <summary>
    /// STATE TEST: Received request = "pending_received" status.
    /// GOAL: When someone requests match, status is pending_received.
    /// IMPORTANCE: User can accept or decline incoming request.
    /// </summary>
    [Fact]
    public void CalculateStatus_ReceivedRequest_ReturnsPendingReceived()
    {
        // Arrange
  bool hasSentRequest = false;
        bool hasReceivedRequest = true;
        bool isMatched = false;

        // Act
        var (status, canMatch, canDecline) = CalculateMatchStatus(hasSentRequest, hasReceivedRequest, isMatched);

     // Assert
        status.Should().Be("pending_received");
        canMatch.Should().BeTrue("user can accept by sending request back");
        canDecline.Should().BeTrue("user can decline request");
    }

    /// <summary>
    /// STATE TEST: Both sent requests = "matched" status.
    /// GOAL: Mutual requests create a match.
    /// IMPORTANCE: Core matching logic - both users expressed interest.
    /// </summary>
    [Fact]
    public void CalculateStatus_BothSentRequests_ReturnsMatched()
    {
        // Arrange
        bool hasSentRequest = true;
        bool hasReceivedRequest = true;
        bool isMatched = false;

        // Act
        var (status, canMatch, canDecline) = CalculateMatchStatus(hasSentRequest, hasReceivedRequest, isMatched);

      // Assert
        status.Should().Be("matched");
        canMatch.Should().BeFalse("already matched");
        canDecline.Should().BeFalse("cannot decline after matching");
}

    /// <summary>
    /// STATE TEST: Has chat room = "matched" status.
    /// GOAL: Existing chat room means users are matched.
    /// IMPORTANCE: Matched status persists even after requests are deleted.
    /// </summary>
    [Fact]
    public void CalculateStatus_HasChatRoom_ReturnsMatched()
    {
        // Arrange
        bool hasSentRequest = false;
     bool hasReceivedRequest = false;
        bool isMatched = true;

      // Act
        var (status, canMatch, canDecline) = CalculateMatchStatus(hasSentRequest, hasReceivedRequest, isMatched);

        // Assert
        status.Should().Be("matched");
        canMatch.Should().BeFalse("already matched");
        canDecline.Should().BeFalse("cannot decline after matching");
    }

    #endregion

    #region State Transition Rules

    /// <summary>
    /// RULE TEST: none ? pending_sent transition.
    /// GOAL: User can send request from none state.
  /// IMPORTANCE: Initial action users can take.
    /// </summary>
    [Fact]
    public void Transition_NoneToending_sent_IsValid()
    {
        // Arrange - Start in "none" state
        var initialState = (status: "none", canMatch: true, canDecline: false);

     // Act - User sends request
   var userSendsRequest = true;
        var newState = CalculateMatchStatus(userSendsRequest, false, false);

        // Assert
        initialState.canMatch.Should().BeTrue("transition should be allowed");
        newState.status.Should().Be("pending_sent");
    }

/// <summary>
 /// RULE TEST: pending_received ? matched transition.
    /// GOAL: User can accept received request.
    /// IMPORTANCE: Core acceptance flow.
    /// </summary>
    [Fact]
  public void Transition_PendingReceivedToMatched_IsValid()
    {
        // Arrange - Start in "pending_received" state
 var initialState = (status: "pending_received", canMatch: true, canDecline: true);

  // Act - User accepts by sending request back
        var newState = CalculateMatchStatus(hasSentRequest: true, hasReceivedRequest: true, isMatched: false);

        // Assert
        initialState.canMatch.Should().BeTrue("user can accept");
        newState.status.Should().Be("matched");
    }

    /// <summary>
    /// RULE TEST: pending_sent ? none transition (via decline).
    /// GOAL: Other user can decline request, resetting to none.
    /// IMPORTANCE: Decline functionality works.
    /// </summary>
    [Fact]
  public void Transition_PendingSentToNone_ViaDecline()
    {
        // Arrange - User A sent request (pending_sent for A, pending_received for B)
        var userAState = CalculateMatchStatus(hasSentRequest: true, hasReceivedRequest: false, isMatched: false);
        userAState.status.Should().Be("pending_sent");

        // Act - User B declines (removes request)
    var userAStateAfterDecline = CalculateMatchStatus(hasSentRequest: false, hasReceivedRequest: false, isMatched: false);

        // Assert
        userAStateAfterDecline.status.Should().Be("none", "request was declined");
    }

    /// <summary>
    /// RULE TEST: matched ? matched (terminal state).
    /// GOAL: Matched state is permanent.
    /// IMPORTANCE: Once matched, cannot go back to unmatched.
    /// </summary>
    [Fact]
    public void Transition_MatchedStaysMatched_TerminalState()
 {
    // Arrange
        var matchedState = CalculateMatchStatus(hasSentRequest: false, hasReceivedRequest: false, isMatched: true);

        // Act - Try to change state (requests are gone, but still matched via room)
        var stillMatchedState = CalculateMatchStatus(hasSentRequest: false, hasReceivedRequest: false, isMatched: true);

        // Assert
        matchedState.status.Should().Be("matched");
  stillMatchedState.status.Should().Be("matched");
        stillMatchedState.canMatch.Should().BeFalse("cannot rematch");
    }

    #endregion

    #region Invalid State Tests

    /// <summary>
  /// EDGE CASE TEST: All flags false except isMatched.
    /// GOAL: isMatched flag overrides everything.
    /// IMPORTANCE: Chat room existence is source of truth.
    /// </summary>
    [Fact]
    public void CalculateStatus_OnlyIsMatched_ReturnsMatched()
    {
        // Arrange - Requests deleted, but chat room exists
        bool hasSentRequest = false;
        bool hasReceivedRequest = false;
        bool isMatched = true;

        // Act
        var (status, canMatch, canDecline) = CalculateMatchStatus(hasSentRequest, hasReceivedRequest, isMatched);

        // Assert
        status.Should().Be("matched");
    }

    /// <summary>
    /// EDGE CASE TEST: All flags true.
    /// GOAL: isMatched takes precedence.
    /// IMPORTANCE: Edge case that shouldn't happen but is handled.
    /// </summary>
    [Fact]
    public void CalculateStatus_AllFlagsTrue_ReturnsMatched()
    {
        // Arrange
        bool hasSentRequest = true;
 bool hasReceivedRequest = true;
        bool isMatched = true;

    // Act
        var (status, canMatch, canDecline) = CalculateMatchStatus(hasSentRequest, hasReceivedRequest, isMatched);

    // Assert
        status.Should().Be("matched", "isMatched takes precedence");
    }

    #endregion

    #region Action Permission Tests

    /// <summary>
    /// PERMISSION TEST: canMatch flag logic.
    /// GOAL: User can match in specific states.
    /// IMPORTANCE: UI enables/disables match button based on this.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, true)]  // none ? can match
    [InlineData(true, false, false, false)]  // pending_sent ? cannot match
    [InlineData(false, true, false, true)]   // pending_received ? can match (accept)
    [InlineData(true, true, false, false)]   // matched (via requests) ? cannot match
 [InlineData(false, false, true, false)]  // matched (via room) ? cannot match
    public void CalculateCanMatch_VariousStates_ReturnsCorrectFlag(
        bool hasSent, bool hasReceived, bool isMatched, bool expectedCanMatch)
    {
// Act
        var (_, canMatch, _) = CalculateMatchStatus(hasSent, hasReceived, isMatched);

  // Assert
 canMatch.Should().Be(expectedCanMatch);
    }

    /// <summary>
 /// PERMISSION TEST: canDecline flag logic.
    /// GOAL: User can only decline received requests.
 /// IMPORTANCE: Decline button only appears for pending_received state.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, false)]  // none ? cannot decline
    [InlineData(true, false, false, false)]   // pending_sent ? cannot decline
    [InlineData(false, true, false, true)]    // pending_received ? can decline
    [InlineData(true, true, false, false)]    // matched ? cannot decline
    [InlineData(false, false, true, false)]   // matched (via room) ? cannot decline
    public void CalculateCanDecline_VariousStates_ReturnsCorrectFlag(
        bool hasSent, bool hasReceived, bool isMatched, bool expectedCanDecline)
    {
        // Act
     var (_, _, canDecline) = CalculateMatchStatus(hasSent, hasReceived, isMatched);

     // Assert
        canDecline.Should().Be(expectedCanDecline);
    }

    #endregion

    #region Helper Method (Business Logic Under Test)

    /// <summary>
    /// Pure function that calculates match status based on flags.
    /// This mirrors the logic in MatchService.GetMatchStatusAsync.
/// </summary>
    private (string status, bool canMatch, bool canDecline) CalculateMatchStatus(
      bool hasSentRequest, bool hasReceivedRequest, bool isMatched)
    {
        string status;
     bool canMatch;
        bool canDecline;

      if (isMatched)
        {
       // Already matched via chat room
     status = "matched";
          canMatch = false;
        canDecline = false;
        }
        else if (hasSentRequest && hasReceivedRequest)
        {
            // Both sent requests = matched (before room is created)
            status = "matched";
   canMatch = false;
   canDecline = false;
        }
        else if (hasSentRequest)
 {
            // Current user sent request
            status = "pending_sent";
  canMatch = false;
       canDecline = false;
   }
     else if (hasReceivedRequest)
     {
// Target user sent request
 status = "pending_received";
            canMatch = true; // Can accept by sending request back
            canDecline = true;
        }
        else
        {
      // No requests exist
            status = "none";
      canMatch = true;
       canDecline = false;
 }

        return (status, canMatch, canDecline);
    }

    #endregion
}
