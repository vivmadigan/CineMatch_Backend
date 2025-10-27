using FluentAssertions;
using Infrastructure.Services;
using Infrastructure.Services.Matches;
using Infrastructure.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Advanced edge case tests for MatchService.
/// </summary>
public class MatchServiceAdvancedTests
{
    [Fact]
    public async Task GetCandidatesAsync_WithZeroLikes_ReturnsEmpty()
    {
      // Arrange
        using var context = DbFixture.CreateContext();
   var user = await DbFixture.CreateTestUserAsync(context);
      var service = new MatchService(context);

  // Act - User has no likes
        var candidates = await service.GetCandidatesAsync(user.Id, 10, CancellationToken.None);

        // Assert
        candidates.Should().BeEmpty();
    }

  [Fact]
    public async Task GetCandidatesAsync_WithNonExistentUser_ReturnsEmpty()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
    var service = new MatchService(context);

    // Act
        var candidates = await service.GetCandidatesAsync("non-existent-id", 10, CancellationToken.None);

   // Assert
     candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestAsync_IsIdempotent()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context);

      // Act - Send same request twice
        var result1 = await service.RequestAsync(user1.Id, user2.Id, 123, CancellationToken.None);
        var result2 = await service.RequestAsync(user1.Id, user2.Id, 123, CancellationToken.None);

        // Assert
 result1.Matched.Should().BeFalse();
        result2.Matched.Should().BeFalse();
    }

    [Fact]
    public async Task RequestAsync_RemovesReciprocalRequest()
    {
     // Arrange
  using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
      var user2 = await DbFixture.CreateTestUserAsync(context);
var service = new MatchService(context);

        await service.RequestAsync(user1.Id, user2.Id, 123, CancellationToken.None);
        var requestsBefore = await context.MatchRequests.CountAsync();

        // Act
     await service.RequestAsync(user2.Id, user1.Id, 123, CancellationToken.None);
        var requestsAfter = await context.MatchRequests.CountAsync();

        // Assert
        requestsAfter.Should().BeLessThan(requestsBefore);
    }
}
