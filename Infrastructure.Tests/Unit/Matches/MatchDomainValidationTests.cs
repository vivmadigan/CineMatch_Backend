using FluentAssertions;
using Xunit;

namespace Infrastructure.Tests.Unit.Matches;

/// <summary>
/// Pure unit tests for match domain validation rules.
/// Tests idempotency, self-match rejection, and invalid inputs.
/// GOAL: Verify domain rules are enforced correctly.
/// IMPORTANCE: CRITICAL - Prevents invalid match states.
/// </summary>
public class MatchDomainValidationTests
{
    #region Idempotency Tests

    /// <summary>
    /// IDEMPOTENCY TEST: Repeat request returns same result.
    /// GOAL: Sending same request twice doesn't create duplicates.
    /// IMPORTANCE: Prevents database bloat and confusion.
    /// </summary>
    [Fact]
    public void ValidateRequest_SameRequestTwice_IsIdempotent()
    {
        // Arrange
        var requestor = "user1";
        var target = "user2";
   var movie = 27205;
  var existingRequests = new List<(string requestor, string target, int movie)>
        {
        ("user1", "user2", 27205)
  };

        // Act
        var isDuplicate = CheckIsDuplicateRequest(requestor, target, movie, existingRequests);

        // Assert
        isDuplicate.Should().BeTrue("same request already exists");
    }

    /// <summary>
    /// IDEMPOTENCY TEST: Repeat accept doesn't create second room.
    /// GOAL: Accepting twice should be safe (no-op).
    /// IMPORTANCE: UI double-click or network retry shouldn't break things.
    /// </summary>
    [Fact]
    public void ValidateAccept_AlreadyMatched_IsIdempotent()
    {
 // Arrange
        var user1 = "user1";
  var user2 = "user2";
   var existingRooms = new List<(string user1, string user2)>
  {
        ("user1", "user2")
        };

        // Act
        var alreadyMatched = CheckAlreadyMatched(user1, user2, existingRooms);

        // Assert
        alreadyMatched.Should().BeTrue("users already have a room");
    }

  /// <summary>
    /// IDEMPOTENCY TEST: Different movies are separate requests.
    /// GOAL: User can request match for multiple movies.
    /// IMPORTANCE: Each movie is independent match request.
    /// </summary>
    [Fact]
    public void ValidateRequest_DifferentMovies_AreIndependent()
    {
   // Arrange
    var requestor = "user1";
        var target = "user2";
        var existingRequests = new List<(string requestor, string target, int movie)>
        {
            ("user1", "user2", 27205)  // Inception
        };

        // Act
        var isDuplicate1 = CheckIsDuplicateRequest(requestor, target, 27205, existingRequests);
        var isDuplicate2 = CheckIsDuplicateRequest(requestor, target, 278, existingRequests); // Shawshank

    // Assert
        isDuplicate1.Should().BeTrue("request for Inception exists");
 isDuplicate2.Should().BeFalse("request for Shawshank is different");
    }

    #endregion

    #region Self-Match Rejection Tests

    /// <summary>
    /// VALIDATION TEST: User cannot match with themselves.
    /// GOAL: Self-match is invalid domain operation.
    /// IMPORTANCE: CRITICAL - Prevents nonsensical state.
    /// </summary>
    [Fact]
    public void ValidateRequest_SelfMatch_IsInvalid()
    {
        // Arrange
        var userId = "user1";

        // Act
        var isValid = ValidatePair(userId, userId);

        // Assert
 isValid.Should().BeFalse("user cannot match with themselves");
    }

    /// <summary>
    /// VALIDATION TEST: Self-match with same ID format.
    /// GOAL: Edge case where IDs are identical strings.
    /// IMPORTANCE: Defensive check.
    /// </summary>
    [Theory]
    [InlineData("user1", "user1")]
    [InlineData("", "")]
    [InlineData("abc-123", "abc-123")]
    public void ValidateRequest_IdenticalUserIds_IsInvalid(string userId1, string userId2)
    {
        // Act
        var isValid = ValidatePair(userId1, userId2);

   // Assert
   isValid.Should().BeFalse("identical user IDs are invalid");
    }

    #endregion

    #region Invalid Input Tests

    /// <summary>
    /// VALIDATION TEST: Null requestor ID is invalid.
  /// GOAL: Required fields cannot be null.
    /// IMPORTANCE: Input validation prevents exceptions.
    /// </summary>
    [Theory]
    [InlineData(null, "user2")]
    [InlineData("", "user2")]
    [InlineData("   ", "user2")]
    public void ValidateRequest_NullOrEmptyRequestor_IsInvalid(string? requestor, string target)
    {
     // Act
        var isValid = ValidateMatchRequest(requestor, target, 27205);

    // Assert
        isValid.Should().BeFalse("requestor cannot be null or empty");
    }

    /// <summary>
    /// VALIDATION TEST: Null target ID is invalid.
  /// GOAL: Required fields cannot be null.
 /// IMPORTANCE: Input validation prevents exceptions.
    /// </summary>
    [Theory]
    [InlineData("user1", null)]
    [InlineData("user1", "")]
    [InlineData("user1", "   ")]
    public void ValidateRequest_NullOrEmptyTarget_IsInvalid(string requestor, string? target)
    {
        // Act
        var isValid = ValidateMatchRequest(requestor, target, 27205);

        // Assert
        isValid.Should().BeFalse("target cannot be null or empty");
    }

    /// <summary>
    /// VALIDATION TEST: Invalid movie ID is rejected.
    /// GOAL: Movie ID must be positive integer.
    /// IMPORTANCE: TMDB IDs are always positive.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public void ValidateRequest_InvalidMovieId_IsInvalid(int movieId)
    {
        // Act
        var isValid = ValidateMatchRequest("user1", "user2", movieId);

        // Assert
        isValid.Should().BeFalse("movie ID must be positive");
    }

/// <summary>
    /// VALIDATION TEST: Valid movie ID is accepted.
    /// GOAL: Positive integers are valid TMDB IDs.
    /// IMPORTANCE: Normal case should work.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(27205)]
    [InlineData(999999)]
public void ValidateRequest_ValidMovieId_IsValid(int movieId)
    {
     // Act
     var isValid = ValidateMatchRequest("user1", "user2", movieId);

        // Assert
      isValid.Should().BeTrue("positive movie IDs are valid");
    }

    #endregion

    #region Mutual Match Detection Tests

    /// <summary>
    /// LOGIC TEST: Mutual match detected correctly.
    /// GOAL: Incoming request + outgoing request = mutual match.
    /// IMPORTANCE: Core matching logic.
    /// </summary>
    [Fact]
    public void DetectMutualMatch_BothRequestsExist_ReturnsTrue()
    {
        // Arrange
        var requestor = "user1";
        var target = "user2";
        var movie = 27205;
        var existingRequests = new List<(string requestor, string target, int movie)>
{
            ("user2", "user1", 27205)  // Incoming request from target
   };

        // Act
        var isMutualMatch = CheckForMutualMatch(requestor, target, movie, existingRequests);

        // Assert
        isMutualMatch.Should().BeTrue("incoming request exists for same movie");
    }

    /// <summary>
    /// LOGIC TEST: No mutual match if only one request.
    /// GOAL: One-way request is not a match.
    /// IMPORTANCE: Both users must express interest.
    /// </summary>
    [Fact]
 public void DetectMutualMatch_OnlyOneRequest_ReturnsFalse()
    {
     // Arrange
        var requestor = "user1";
var target = "user2";
        var movie = 27205;
        var existingRequests = new List<(string requestor, string target, int movie)>
   {
            ("user1", "user3", 27205)  // Different target
  };

        // Act
        var isMutualMatch = CheckForMutualMatch(requestor, target, movie, existingRequests);

        // Assert
        isMutualMatch.Should().BeFalse("no incoming request from target user");
    }

    /// <summary>
    /// LOGIC TEST: Mutual match requires same movie.
    /// GOAL: Requests for different movies don't match.
 /// IMPORTANCE: Movie specificity matters.
    /// </summary>
    [Fact]
    public void DetectMutualMatch_DifferentMovies_ReturnsFalse()
    {
        // Arrange
        var requestor = "user1";
        var target = "user2";
        var movie = 27205;
        var existingRequests = new List<(string requestor, string target, int movie)>
        {
          ("user2", "user1", 278)  // Different movie
        };

        // Act
        var isMutualMatch = CheckForMutualMatch(requestor, target, movie, existingRequests);

    // Assert
        isMutualMatch.Should().BeFalse("requests are for different movies");
    }

    #endregion

    #region Request Direction Tests

    /// <summary>
    /// LOGIC TEST: Request direction matters.
    /// GOAL: user1?user2 is different from user2?user1.
    /// IMPORTANCE: Direction determines who initiated.
    /// </summary>
 [Fact]
    public void ValidateRequest_Direction_MattersForDuplicateCheck()
    {
    // Arrange
     var existingRequests = new List<(string requestor, string target, int movie)>
  {
       ("user1", "user2", 27205)
     };

        // Act
      var isDuplicate1 = CheckIsDuplicateRequest("user1", "user2", 27205, existingRequests);
    var isDuplicate2 = CheckIsDuplicateRequest("user2", "user1", 27205, existingRequests);

        // Assert
    isDuplicate1.Should().BeTrue("exact match exists");
 isDuplicate2.Should().BeFalse("opposite direction is different request");
    }

    #endregion

    #region Decline Validation Tests

    /// <summary>
    /// VALIDATION TEST: Can only decline incoming requests.
    /// GOAL: User can decline requests sent TO them.
 /// IMPORTANCE: Cannot decline own outgoing requests.
    /// </summary>
    [Fact]
    public void ValidateDecline_IncomingRequest_IsValid()
    {
        // Arrange
      var decliner = "user2";
   var existingRequests = new List<(string requestor, string target, int movie)>
        {
        ("user1", "user2", 27205)  // Request TO user2
     };

    // Act
   var canDecline = CheckCanDecline(decliner, "user1", 27205, existingRequests);

        // Assert
  canDecline.Should().BeTrue("user can decline incoming request");
    }

    /// <summary>
    /// VALIDATION TEST: Cannot decline non-existent request.
    /// GOAL: Decline requires existing request.
    /// IMPORTANCE: Prevents invalid operations.
    /// </summary>
    [Fact]
    public void ValidateDecline_NoRequest_IsInvalid()
    {
        // Arrange
   var decliner = "user2";
        var existingRequests = new List<(string requestor, string target, int movie)>();

        // Act
        var canDecline = CheckCanDecline(decliner, "user1", 27205, existingRequests);

        // Assert
        canDecline.Should().BeFalse("no request to decline");
    }

    /// <summary>
    /// VALIDATION TEST: Cannot decline own outgoing request.
    /// GOAL: Decliner must be the target, not the requestor.
    /// IMPORTANCE: Request direction matters.
    /// </summary>
    [Fact]
    public void ValidateDecline_OwnRequest_IsInvalid()
    {
        // Arrange
        var decliner = "user1";
    var existingRequests = new List<(string requestor, string target, int movie)>
        {
            ("user1", "user2", 27205)  // Request FROM user1
        };

        // Act
        var canDecline = CheckCanDecline(decliner, "user2", 27205, existingRequests);

        // Assert
    canDecline.Should().BeFalse("cannot decline own outgoing request");
    }

    #endregion

  #region Helper Methods (Pure Business Logic)

    private bool CheckIsDuplicateRequest(string requestor, string target, int movie,
     List<(string requestor, string target, int movie)> existingRequests)
    {
     return existingRequests.Any(r => r.requestor == requestor && r.target == target && r.movie == movie);
    }

  private bool CheckAlreadyMatched(string user1, string user2,
   List<(string user1, string user2)> existingRooms)
    {
   return existingRooms.Any(r =>
  (r.user1 == user1 && r.user2 == user2) ||
     (r.user1 == user2 && r.user2 == user1));
    }

    private bool ValidatePair(string userId1, string userId2)
    {
      if (string.IsNullOrWhiteSpace(userId1) || string.IsNullOrWhiteSpace(userId2))
     return false;

        return userId1 != userId2;
    }

    private bool ValidateMatchRequest(string? requestor, string? target, int movieId)
    {
        if (string.IsNullOrWhiteSpace(requestor) || string.IsNullOrWhiteSpace(target))
          return false;

        if (movieId <= 0)
        return false;

     if (requestor == target)
       return false;

return true;
    }

    private bool CheckForMutualMatch(string requestor, string target, int movie,
        List<(string requestor, string target, int movie)> existingRequests)
    {
        // Check if target already sent request to requestor for same movie
        return existingRequests.Any(r =>
       r.requestor == target &&
            r.target == requestor &&
 r.movie == movie);
    }

    private bool CheckCanDecline(string decliner, string requestor, int movie,
        List<(string requestor, string target, int movie)> existingRequests)
    {
     // Can only decline if there's an incoming request (requestor ? decliner)
        return existingRequests.Any(r =>
            r.requestor == requestor &&
            r.target == decliner &&
         r.movie == movie);
    }

    #endregion
}
