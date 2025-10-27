using FluentAssertions;
using Infrastructure.Services;
using Infrastructure.Services.Matches;
using Infrastructure.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for MatchService.
/// Tests candidate finding, ordering, and mutual matching logic.
/// </summary>
public class MatchServiceTests
{
    [Fact]
    public async Task GetCandidatesAsync_ExcludesCurrentUser()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);

        var likesService = new UserLikesService(context);
        await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", null, null, CancellationToken.None);

        var matchService = new MatchService(context);

        // Act
        var candidates = await matchService.GetCandidatesAsync(user1.Id, 20, CancellationToken.None);

        // Assert
        candidates.Should().NotContain(c => c.UserId == user1.Id);
        candidates.Should().ContainSingle(c => c.UserId == user2.Id);
    }

    [Fact]
    public async Task GetCandidatesAsync_OrdersByOverlapCount_ThenRecency()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var currentUser = await DbFixture.CreateTestUserAsync(context);
        var user1 = await DbFixture.CreateTestUserAsync(context, displayName: "User1");
        var user2 = await DbFixture.CreateTestUserAsync(context, displayName: "User2");

        var likesService = new UserLikesService(context);

        // Current user likes 3 movies
        await likesService.UpsertLikeAsync(currentUser.Id, 27205, "Inception", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(currentUser.Id, 238, "The Godfather", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(currentUser.Id, 603, "The Matrix", null, null, CancellationToken.None);

        // User1 likes 2 overlapping movies
        await likesService.UpsertLikeAsync(user1.Id, 27205, "Inception", null, null, CancellationToken.None);
        await likesService.UpsertLikeAsync(user1.Id, 238, "The Godfather", null, null, CancellationToken.None);

        // User2 likes 1 overlapping movie
        await likesService.UpsertLikeAsync(user2.Id, 27205, "Inception", null, null, CancellationToken.None);

        var matchService = new MatchService(context);

        // Act
        var candidates = await matchService.GetCandidatesAsync(currentUser.Id, 20, CancellationToken.None);

        // Assert
        candidates.Should().HaveCount(2);
        candidates.First().UserId.Should().Be(user1.Id); // Higher overlap
        candidates.First().OverlapCount.Should().Be(2);
        candidates.Last().UserId.Should().Be(user2.Id);
        candidates.Last().OverlapCount.Should().Be(1);
    }

    [Fact]
    public async Task RequestAsync_WithReciprocalRequest_CreatesRoom()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);

        var matchService = new MatchService(context);

        // User1 requests User2
        var result1 = await matchService.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

        // Act - User2 requests User1 (reciprocal)
        var result2 = await matchService.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Assert
        result1.Matched.Should().BeFalse();
        result1.RoomId.Should().BeNull();

        result2.Matched.Should().BeTrue();
        result2.RoomId.Should().NotBeEmpty();

        // Verify room and memberships created
        var room = context.ChatRooms.FirstOrDefault(r => r.Id == result2.RoomId);
        room.Should().NotBeNull();

        var memberships = context.ChatMemberships.Where(m => m.RoomId == result2.RoomId).ToList();
        memberships.Should().HaveCount(2);
        memberships.Should().Contain(m => m.UserId == user1.Id && m.IsActive);
        memberships.Should().Contain(m => m.UserId == user2.Id && m.IsActive);
    }
}
