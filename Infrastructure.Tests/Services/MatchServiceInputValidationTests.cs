using FluentAssertions;
using Infrastructure.Services;
using Infrastructure.Services.Matches;
using Infrastructure.Tests.Helpers;
using Infrastructure.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for MatchService input validation and boundary conditions.
/// Tests edge cases for method parameters: negative values, max/min integers, null handling.
/// GOAL: Ensure robust input validation prevents crashes and returns appropriate errors.
/// </summary>
public class MatchServiceInputValidationTests
{
    #region TmdbId Validation Tests

    /// <summary>
    /// NEGATIVE TEST: Verify RequestAsync with negative TmdbId is handled gracefully.
    /// GOAL: Negative movie IDs should be rejected or handled safely.
    /// IMPORTANCE: Database constraints may fail; service should validate first.
    /// </summary>
    [Fact]
    public async Task RequestAsync_WithNegativeTmdbId_CompletesWithoutCrash()
    {
        // Arrange
  using var context = DbFixture.CreateContext();
 var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

        // Act - Request with negative movie ID
        var act = async () => await service.RequestAsync(user1.Id, user2.Id, -1, CancellationToken.None);

        // Assert - Should not crash (controller validates, but service should handle gracefully)
        await act.Should().NotThrowAsync();
    }

    /// <summary>
/// NEGATIVE TEST: Verify RequestAsync with zero TmdbId.
/// GOAL: Zero is invalid movie ID; should be handled gracefully.
/// IMPORTANCE: Common input error; must not crash service.
/// </summary>
    [Fact]
    public async Task RequestAsync_WithZeroTmdbId_CompletesWithoutCrash()
    {
 // Arrange
        using var context = DbFixture.CreateContext();
  var user1 = await DbFixture.CreateTestUserAsync(context);
     var user2 = await DbFixture.CreateTestUserAsync(context);
    var service = new MatchService(context, new MockNotificationService());

    // Act
   var act = async () => await service.RequestAsync(user1.Id, user2.Id, 0, CancellationToken.None);

    // Assert
        await act.Should().NotThrowAsync();
}

    /// <summary>
    /// POSITIVE TEST: Verify RequestAsync with maximum integer TmdbId.
    /// GOAL: Boundary condition test; Int32.MaxValue should be handled.
    /// IMPORTANCE: Edge case that might expose overflow issues.
    /// </summary>
    [Fact]
    public async Task RequestAsync_WithMaxIntTmdbId_HandlesGracefully()
    {
        // Arrange
  using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);
  var service = new MatchService(context, new MockNotificationService());

      // Act
        var result = await service.RequestAsync(user1.Id, user2.Id, int.MaxValue, CancellationToken.None);

        // Assert - Should complete without error
    result.Should().NotBeNull();
        result.Matched.Should().BeFalse(); // No mutual match
    }

    /// <summary>
    /// NEGATIVE TEST: Verify DeclineMatchAsync with negative TmdbId.
    /// GOAL: Decline should handle invalid movie IDs gracefully.
    /// IMPORTANCE: Consistent validation across all methods.
    /// </summary>
  [Fact]
    public async Task DeclineMatchAsync_WithNegativeTmdbId_CompletesWithoutCrash()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

     // Act
   var act = async () => await service.DeclineMatchAsync(user1.Id, user2.Id, -999, CancellationToken.None);

        // Assert - Should not crash (idempotent - no request exists)
await act.Should().NotThrowAsync();
    }

    #endregion

    #region UserId Validation Tests

    /// <summary>
    /// NEGATIVE TEST: Verify RequestAsync with empty string userId throws DbUpdateException.
    /// GOAL: Empty userId violates foreign key constraint.
    /// IMPORTANCE: Database-level validation enforced.
    /// </summary>
    [Fact]
    public async Task RequestAsync_WithEmptyStringUserId_ThrowsDbUpdateException()
    {
    // Arrange
  using var context = DbFixture.CreateContext();
   var user = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

      // Act - Empty target user ID (violates FK constraint)
        var act = async () => await service.RequestAsync(user.Id, "", 27205, CancellationToken.None);

        // Assert - Throws DbUpdateException due to foreign key constraint
    await act.Should().ThrowAsync<DbUpdateException>();
 }

    /// <summary>
    /// NEGATIVE TEST: Verify RequestAsync with null userId throws DbUpdateException.
/// GOAL: Null userId violates NOT NULL constraint.
    /// IMPORTANCE: Database enforces data integrity.
    /// </summary>
    [Fact]
    public async Task RequestAsync_WithNullUserId_ThrowsDbUpdateException()
    {
     // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

    // Act - Null target user ID (violates NOT NULL constraint)
        var act = async () => await service.RequestAsync(user.Id, null!, 27205, CancellationToken.None);

        // Assert - Throws DbUpdateException due to NOT NULL constraint
     await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
    /// NEGATIVE TEST: Verify GetCandidatesAsync with empty userId.
    /// GOAL: Empty userId returns empty candidates (no matches possible).
    /// IMPORTANCE: Defensive programming; graceful degradation.
    /// </summary>
    [Fact]
    public async Task GetCandidatesAsync_WithEmptyUserId_ReturnsEmpty()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
   var service = new MatchService(context, new MockNotificationService());

        // Act
        var result = await service.GetCandidatesAsync("", 20, CancellationToken.None);

    // Assert - Empty result (no likes found for empty userId)
        result.Should().BeEmpty();
    }

    /// <summary>
    /// NEGATIVE TEST: Verify GetMatchStatusAsync with very long userId.
    /// GOAL: Extremely long strings shouldn't cause buffer overflows.
    /// IMPORTANCE: Security - prevents potential DoS attacks.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithVeryLongUserId_HandlesGracefully()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

        var longUserId = new string('A', 10000); // 10,000 characters

        // Act
        var result = await service.GetMatchStatusAsync(user.Id, longUserId, CancellationToken.None);

        // Assert - Should complete without error (no match found)
   result.Should().NotBeNull();
result.Status.Should().Be("none");
    }

    #endregion

    #region Take Parameter Validation Tests

    /// <summary>
    /// NEGATIVE TEST: Verify GetCandidatesAsync with zero take parameter.
  /// GOAL: Math.Max(1, take) should clamp to minimum 1.
    /// IMPORTANCE: Prevents empty result from invalid input.
    /// </summary>
    [Fact]
    public async Task GetCandidatesAsync_WithZeroTake_ReturnsAtLeastOne()
    {
// Arrange
        using var context = DbFixture.CreateContext();
    var user1 = await DbFixture.CreateTestUserAsync(context);
 var user2 = await DbFixture.CreateTestUserAsync(context);
    var likesService = new UserLikesService(context);
    var service = new MatchService(context, new MockNotificationService());

      // Both like same movie
    await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", null, null, CancellationToken.None);

        // Act - Request 0 candidates
var result = await service.GetCandidatesAsync(user1.Id, 0, CancellationToken.None);

        // Assert - Should return at least 1 (Math.Max(1, take))
        result.Should().ContainSingle();
    }

    /// <summary>
    /// NEGATIVE TEST: Verify GetCandidatesAsync with negative take parameter.
    /// GOAL: Negative values clamped to 1.
    /// IMPORTANCE: Prevents errors from invalid query limits.
    /// </summary>
    [Fact]
    public async Task GetCandidatesAsync_WithNegativeTake_ClampsToOne()
  {
   // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
    var user2 = await DbFixture.CreateTestUserAsync(context);
var likesService = new UserLikesService(context);
        var service = new MatchService(context, new MockNotificationService());

 // Both like same movie
      await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", null, null, CancellationToken.None);

        // Act - Request negative candidates
    var result = await service.GetCandidatesAsync(user1.Id, -10, CancellationToken.None);

        // Assert - Should return at least 1
        result.Should().ContainSingle();
    }

    /// <summary>
    /// POSITIVE TEST: Verify GetCandidatesAsync with extremely large take parameter.
    /// GOAL: Large values don't cause performance issues or overflow.
    /// IMPORTANCE: Prevents potential DoS from excessive database queries.
    /// </summary>
    [Fact]
    public async Task GetCandidatesAsync_WithVeryLargeTake_DoesNotCrash()
    {
  // Arrange
        using var context = DbFixture.CreateContext();
  var user = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

        // Act - Request 1 million candidates
  var result = await service.GetCandidatesAsync(user.Id, 1_000_000, CancellationToken.None);

   // Assert - Should complete without error (returns whatever exists)
result.Should().NotBeNull();
    }

    #endregion

    #region Boundary Condition Tests

    /// <summary>
    /// POSITIVE TEST: Verify RequestAsync with minimum valid TmdbId (1).
    /// GOAL: TmdbId=1 is technically valid; should work.
    /// IMPORTANCE: Boundary condition at lower limit.
    /// </summary>
    [Fact]
    public async Task RequestAsync_WithTmdbIdOne_Succeeds()
    {
  // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);
    var service = new MatchService(context, new MockNotificationService());

    // Act
        var result = await service.RequestAsync(user1.Id, user2.Id, 1, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Matched.Should().BeFalse();
    }

    /// <summary>
    /// NEGATIVE TEST: Verify GetCandidatesAsync with Int32.MaxValue take.
    /// GOAL: Extreme boundary test; should not overflow.
    /// IMPORTANCE: Prevents integer overflow edge cases.
    /// </summary>
  [Fact]
    public async Task GetCandidatesAsync_WithMaxIntTake_HandlesGracefully()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

        // Act
      var result = await service.GetCandidatesAsync(user.Id, int.MaxValue, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    /// <summary>
    /// NEGATIVE TEST: Verify GetCandidatesAsync with Int32.MinValue take.
    /// GOAL: Minimum integer value clamped to 1.
    /// IMPORTANCE: Extreme boundary test for negative values.
    /// </summary>
    [Fact]
    public async Task GetCandidatesAsync_WithMinIntTake_ClampsToOne()
    {
        // Arrange
      using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);
   var likesService = new UserLikesService(context);
var service = new MatchService(context, new MockNotificationService());

        // Both like same movie
        await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", null, null, CancellationToken.None);
     await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", null, null, CancellationToken.None);

        // Act
        var result = await service.GetCandidatesAsync(user1.Id, int.MinValue, CancellationToken.None);

        // Assert - Should clamp to 1
        result.Should().ContainSingle();
    }

    #endregion

    #region Special Character Tests

    /// <summary>
  /// NEGATIVE TEST: Verify RequestAsync with special characters in userId throws DbUpdateException.
    /// GOAL: Non-existent userId violates foreign key constraint.
    /// IMPORTANCE: Database enforces referential integrity; EF Core parameterizes queries preventing SQL injection.
    /// </summary>
    [Fact]
    public async Task RequestAsync_WithSpecialCharactersInUserId_ThrowsDbUpdateException()
    {
 // Arrange
        using var context = DbFixture.CreateContext();
      var user = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

        var maliciousUserId = "'; DROP TABLE Users; --";

        // Act - Non-existent userId (violates FK constraint)
        var act = async () => await service.RequestAsync(user.Id, maliciousUserId, 27205, CancellationToken.None);

        // Assert - Throws DbUpdateException; EF Core parameterization prevents SQL injection
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
    /// NEGATIVE TEST: Verify GetMatchStatusAsync with Unicode characters in userId.
    /// GOAL: International characters handled correctly.
    /// IMPORTANCE: Globalization support.
    /// </summary>
    [Fact]
    public async Task GetMatchStatusAsync_WithUnicodeUserId_HandlesGracefully()
    {
        // Arrange
    using var context = DbFixture.CreateContext();
 var user = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

        var unicodeUserId = "??ID????";

        // Act
        var result = await service.GetMatchStatusAsync(user.Id, unicodeUserId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
  result.Status.Should().Be("none");
    }

    #endregion
}
