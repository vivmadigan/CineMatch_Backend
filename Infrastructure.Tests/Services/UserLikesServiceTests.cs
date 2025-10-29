using FluentAssertions;
using Infrastructure.Services;
using Infrastructure.Tests.Helpers;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for UserLikesService.
/// Tests like/unlike operations and idempotency.
/// </summary>
public class UserLikesServiceTests
{
    [Fact]
    public async Task UpsertLikeAsync_WithNewLike_CreatesRecord()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        // Act
        await service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);

        // Assert
        likes.Should().ContainSingle();
        likes.First().TmdbId.Should().Be(27205);
        likes.First().Title.Should().Be("Inception");
    }

    [Fact]
    public async Task UpsertLikeAsync_CalledTwice_IsIdempotent()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        // Act
        await service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);
        await service.UpsertLikeAsync(user.Id, 27205, "Inception v2", "/poster2.jpg", "2010", CancellationToken.None);
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);

        // Assert
        likes.Should().ContainSingle();
        likes.First().Title.Should().Be("Inception v2"); // Updated metadata
    }

    [Fact]
    public async Task RemoveLikeAsync_WithExistingLike_DeletesRecord()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        await service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);

        // Act
        await service.RemoveLikeAsync(user.Id, 27205, CancellationToken.None);
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);

        // Assert
        likes.Should().BeEmpty();
    }

    #region Advanced Edge Cases

    [Fact]
    public async Task UpsertLikeAsync_WithVeryLongTitle_Succeeds()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new UserLikesService(context);
        var user = await DbFixture.CreateTestUserAsync(context);
        var longTitle = new string('A', 250);

        // Act
        await service.UpsertLikeAsync(user.Id, 123, longTitle, "/test.jpg", "2020", CancellationToken.None);
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);

        // Assert
        likes.Should().ContainSingle();
        likes.First().Title.Should().HaveLength(250);
    }

    [Fact]
    public async Task UpsertLikeAsync_WithUnicodeCharacters_PreservesContent()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new UserLikesService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        // Act
        await service.UpsertLikeAsync(user.Id, 123, "Amélie 世界 🎬", "/test.jpg", "2001", CancellationToken.None);
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);

        // Assert
        likes.First().Title.Should().Be("Amélie 世界 🎬");
    }

    [Fact]
    public async Task RemoveLikeAsync_NonExistentLike_IsIdempotent()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new UserLikesService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        // Act - Remove like that doesn't exist
        await service.RemoveLikeAsync(user.Id, 999, CancellationToken.None);

        // Assert - Should not throw
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);
        likes.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertLikeAsync_WithNullPosterPath_Succeeds()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new UserLikesService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        // Act
        await service.UpsertLikeAsync(user.Id, 123, "No Poster Movie", null, "2020", CancellationToken.None);
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);

        // Assert
        likes.Should().ContainSingle();
        likes.First().PosterPath.Should().BeNull();
    }

    [Fact]
    public async Task UpsertLikeAsync_MultipleLikes_OrdersByCreatedAtDescending()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new UserLikesService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        await service.UpsertLikeAsync(user.Id, 1, "First", "/1.jpg", "2020", CancellationToken.None);
        await Task.Delay(50);
        await service.UpsertLikeAsync(user.Id, 2, "Second", "/2.jpg", "2021", CancellationToken.None);
        await Task.Delay(50);
        await service.UpsertLikeAsync(user.Id, 3, "Third", "/3.jpg", "2022", CancellationToken.None);

        // Act
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);

        // Assert - Most recent first
        likes.Should().HaveCount(3);
        likes[0].Title.Should().Be("Third");
        likes[1].Title.Should().Be("Second");
        likes[2].Title.Should().Be("First");
    }

    [Fact]
    public async Task UpsertLikeAsync_WithNullTitle_UsesExistingOrEmpty()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new UserLikesService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        // Act - Service should handle null gracefully
        await service.UpsertLikeAsync(user.Id, 123, null, "/test.jpg", "2020", CancellationToken.None);
        var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);

        // Assert
        likes.Should().ContainSingle();
    }

    [Fact]
    public async Task RemoveLikeAsync_WithZeroTmdbId_DoesNotThrow()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new UserLikesService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        // Act & Assert - Should handle gracefully
        await service.RemoveLikeAsync(user.Id, 0, CancellationToken.None);
    }

    #endregion
}