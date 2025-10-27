using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Infrastructure.Preferences;
using Presentation.Tests.Helpers;
using Xunit;

namespace Presentation.Tests.Controllers;

/// <summary>
/// Integration tests for PreferencesController.
/// Tests save/get flow and validation with real HTTP requests.
/// </summary>
[Collection(nameof(ApiTestCollection))]
public class PreferencesControllerTests
{
    private readonly ApiTestFixture _fixture;

    public PreferencesControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_WithoutAuth_Returns401()
    {
        // Arrange
        var client = _fixture.CreateClient();

     // Act
        var response = await client.GetAsync("/api/preferences");

   // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

    [Fact]
    public async Task Get_WithAuth_Returns200()
    {
        // Arrange
   var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

   // Act
        var response = await client.GetAsync("/api/preferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_WithNoPreferences_ReturnsDefaults()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/preferences");
        var result = await response.Content.ReadFromJsonAsync<GetPreferencesDto>();

   // Assert
  response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.GenreIds.Should().NotBeNull();
        result.Length.Should().NotBeNullOrEmpty();
}

    [Fact]
    public async Task SaveThenGet_ReturnsUpdatedPreferences()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

      var saveDto = new SavePreferenceDto
        {
  GenreIds = [28, 35, 878],
    Length = "medium"
        };

   // Act - Save preferences
        var saveResponse = await client.PostAsJsonAsync("/api/preferences", saveDto);
        saveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act - Get preferences
        var getResponse = await client.GetAsync("/api/preferences");
        var result = await getResponse.Content.ReadFromJsonAsync<GetPreferencesDto>();

    // Assert
 getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.GenreIds.Should().BeEquivalentTo(new[] { 28, 35, 878 });
  result.Length.Should().Be("medium");
    }

    [Fact]
    public async Task Save_WithInvalidLength_Returns400()
    {
      // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        var saveDto = new SavePreferenceDto
        {
            GenreIds = [28],
        Length = "invalid"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/preferences", saveDto);

    // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

[Fact]
    public async Task Save_WithoutAuth_Returns401()
    {
   // Arrange
        var client = _fixture.CreateClient();

        var saveDto = new SavePreferenceDto
        {
            GenreIds = [28],
            Length = "medium"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/preferences", saveDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Save_WithEmptyGenres_Succeeds()
    {
        // Arrange
   var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        var saveDto = new SavePreferenceDto
        {
            GenreIds = [],
            Length = "short"
  };

        // Act
        var saveResponse = await client.PostAsJsonAsync("/api/preferences", saveDto);
 var getResponse = await client.GetAsync("/api/preferences");
        var result = await getResponse.Content.ReadFromJsonAsync<GetPreferencesDto>();

        // Assert
        saveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        result!.GenreIds.Should().BeEmpty();
        result.Length.Should().Be("short");
    }

    [Fact]
    public async Task Save_MultipleUpdates_LastWriteWins()
    {
   // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

      // Act - Save first preference
        await client.PostAsJsonAsync("/api/preferences", new SavePreferenceDto
        {
     GenreIds = [28],
     Length = "short"
        });

        // Act - Save second preference
   await client.PostAsJsonAsync("/api/preferences", new SavePreferenceDto
        {
     GenreIds = [35, 18],
        Length = "long"
        });

   // Act - Get final result
   var getResponse = await client.GetAsync("/api/preferences");
        var result = await getResponse.Content.ReadFromJsonAsync<GetPreferencesDto>();

        // Assert
        result!.GenreIds.Should().BeEquivalentTo(new[] { 35, 18 });
        result.Length.Should().Be("long");
    }
}
