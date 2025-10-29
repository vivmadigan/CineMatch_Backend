using FluentAssertions;
using Infrastructure.Preferences;
using Infrastructure.Services;
using Infrastructure.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for PreferenceService.
/// Tests positive scenarios (save/get) and negative scenarios (invalid input).
/// </summary>
public class PreferenceServiceTests
{
    [Fact]
    public async Task SaveAsync_WithValidData_StoresPreferences()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

        var dto = new SavePreferenceDto
        {
            GenreIds = new List<int> { 28, 35, 878 },
            Length = "medium"
        };

        // Act
        await service.SaveAsync(user.Id, dto, CancellationToken.None);
        var result = await service.GetAsync(user.Id, CancellationToken.None);

        // Assert
        result.GenreIds.Should().BeEquivalentTo(new[] { 28, 35, 878 });
        result.Length.Should().Be("medium");
    }

    [Fact]
    public async Task SaveAsync_WithInvalidLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

        var dto = new SavePreferenceDto
        {
            GenreIds = new List<int> { 28 },
            Length = "invalid"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SaveAsync(user.Id, dto, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_WithNoPreferences_ReturnsDefaults()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new PreferenceService(context);

        // Act
        var result = await service.GetAsync(user.Id, CancellationToken.None);

        // Assert
        result.GenreIds.Should().BeEmpty();
        result.Length.Should().Be("medium");
    }

    #region Advanced Edge Cases

    [Fact]
    public async Task SaveAsync_WithDuplicateGenres_RemovesDuplicates()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new PreferenceService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        var dto = new SavePreferenceDto
        {
            GenreIds = new List<int> { 28, 35, 28, 35, 12 }, // Duplicates!
            Length = "medium"
        };

        // Act
        await service.SaveAsync(user.Id, dto, CancellationToken.None);
        var result = await service.GetAsync(user.Id, CancellationToken.None);

        // Assert
        var uniqueGenres = result.GenreIds.Distinct().ToList();
        result.GenreIds.Should().HaveCount(uniqueGenres.Count); // No duplicates
        result.GenreIds.Should().Contain(new[] { 28, 35, 12 });
    }

    [Fact]
    public async Task SaveAsync_WithEmptyGenres_SavesEmpty()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new PreferenceService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        var dto = new SavePreferenceDto
        {
            GenreIds = new List<int>(), // Empty
            Length = "medium"
        };

        // Act
        await service.SaveAsync(user.Id, dto, CancellationToken.None);
        var result = await service.GetAsync(user.Id, CancellationToken.None);

        // Assert
        result.GenreIds.Should().BeEmpty();
        result.Length.Should().Be("medium");
    }

    [Fact]
    public async Task SaveAsync_WithMaxGenres_Succeeds()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new PreferenceService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        var dto = new SavePreferenceDto
        {
            GenreIds = Enumerable.Range(1, 20).ToList(), // 20 genres
            Length = "long"
        };

        // Act
        await service.SaveAsync(user.Id, dto, CancellationToken.None);
        var result = await service.GetAsync(user.Id, CancellationToken.None);

        // Assert
        result.GenreIds.Should().HaveCount(20);
    }

    [Fact]
    public async Task SaveAsync_PreservesGenreOrder()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new PreferenceService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        var dto = new SavePreferenceDto
        {
            GenreIds = new List<int> { 35, 12, 28, 18 }, // Specific order
            Length = "medium"
        };

        // Act
        await service.SaveAsync(user.Id, dto, CancellationToken.None);
        var result = await service.GetAsync(user.Id, CancellationToken.None);

        // Assert
        result.GenreIds.Should().ContainInOrder(35, 12, 28, 18);
    }

    [Fact]
    public async Task SaveAsync_UpdatesTimestamp()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new PreferenceService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        var dto1 = new SavePreferenceDto
        {
            GenreIds = new List<int> { 28 },
            Length = "short"
        };
        await service.SaveAsync(user.Id, dto1, CancellationToken.None);
        var first = await service.GetAsync(user.Id, CancellationToken.None);

        await Task.Delay(100);

        var dto2 = new SavePreferenceDto
        {
            GenreIds = new List<int> { 35 },
            Length = "long"
        };

        // Act
        await service.SaveAsync(user.Id, dto2, CancellationToken.None);
        var second = await service.GetAsync(user.Id, CancellationToken.None);

        // Assert
        second.GenreIds.Should().Contain(35);
        second.GenreIds.Should().NotContain(28);
    }

    [Fact]
    public async Task SaveAsync_WithNegativeGenreId_ThrowsArgumentException()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new PreferenceService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        var dto = new SavePreferenceDto
        {
            GenreIds = new List<int> { -1, 28 }, // Negative ID
            Length = "medium"
        };

        // Act & Assert - Service NOW validates negative IDs
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
          service.SaveAsync(user.Id, dto, CancellationToken.None));

        exception.Message.Should().Contain("Genre IDs must be positive integers");
    }

    [Fact]
    public async Task SaveAsync_WithNullDto_ThrowsArgumentNullException()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new PreferenceService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        // Act & Assert - Service NOW validates null DTO
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
         service.SaveAsync(user.Id, null!, CancellationToken.None));

        exception.ParamName.Should().Be("dto");
    }

    [Fact]
    public async Task SaveAsync_WithVeryLargeGenreList_ThrowsArgumentException()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new PreferenceService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        var dto = new SavePreferenceDto
        {
            GenreIds = Enumerable.Range(1, 1000).ToList(), // 1000 genres - exceeds limit!
            Length = "medium"
        };

        // Act & Assert - Service NOW limits list size to 50
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
         service.SaveAsync(user.Id, dto, CancellationToken.None));

        exception.Message.Should().Contain("Cannot select more than 50 genres");
    }

    [Fact]
    public async Task SaveAsync_WithZeroGenreId_ThrowsArgumentException()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new PreferenceService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        var dto = new SavePreferenceDto
        {
            GenreIds = new List<int> { 0, 28 }, // Zero is invalid
            Length = "medium"
        };

        // Act & Assert - Service validates IDs must be positive (> 0)
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
          service.SaveAsync(user.Id, dto, CancellationToken.None));

        exception.Message.Should().Contain("Genre IDs must be positive integers");
    }

    [Fact]
    public async Task SaveAsync_WithExactly50Genres_Succeeds()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new PreferenceService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        var dto = new SavePreferenceDto
        {
            GenreIds = Enumerable.Range(1, 50).ToList(), // Exactly at limit
            Length = "medium"
        };

        // Act
        await service.SaveAsync(user.Id, dto, CancellationToken.None);
        var result = await service.GetAsync(user.Id, CancellationToken.None);

        // Assert
        result.GenreIds.Should().HaveCount(50);
    }

    [Fact]
    public async Task SaveAsync_With51Genres_ThrowsArgumentException()
    {
        // Arrange
        var context = DbFixture.CreateContext();
        var service = new PreferenceService(context);
        var user = await DbFixture.CreateTestUserAsync(context);

        var dto = new SavePreferenceDto
        {
            GenreIds = Enumerable.Range(1, 51).ToList(), // One over limit
            Length = "medium"
        };

        // Act & Assert - Service limits at 50
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
           service.SaveAsync(user.Id, dto, CancellationToken.None));

        exception.Message.Should().Contain("Cannot select more than 50 genres");
    }

    #endregion
}