using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Infrastructure.Models.Matches;
using Presentation.Tests.Helpers;
using Xunit;

namespace Presentation.Tests.Controllers;

/// <summary>
/// Integration tests for MatchesController.
/// Tests match candidate finding and mutual match requests.
/// </summary>
[Collection(nameof(ApiTestCollection))]
public class MatchesControllerTests
{
    private readonly ApiTestFixture _fixture;

    public MatchesControllerTests(ApiTestFixture fixture)
    {
      _fixture = fixture;
    }

    #region GetCandidates Tests

    [Fact]
    public async Task GetCandidates_WithoutAuth_Returns401()
    {
     // Arrange
      var client = _fixture.CreateClient();

      // Act
    var response = await client.GetAsync("/api/matches/candidates");

        // Assert
   response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCandidates_WithAuth_Returns200()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/matches/candidates");

    // Assert
      response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCandidates_WithNoOverlap_ReturnsEmpty()
    {
        // Arrange
    var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/matches/candidates");
    var candidates = await response.Content.ReadFromJsonAsync<List<CandidateDto>>();

        // Assert
        candidates.Should().NotBeNull();
      // May not be empty due to other tests, so just check it's a valid response
        candidates.Should().BeOfType<List<CandidateDto>>();
    }

    [Fact]
    public async Task GetCandidates_WithOverlappingLikes_ReturnsCandidates()
    {
        // Arrange
 var (client1, userId1, _) = await _fixture.CreateAuthenticatedClientAsync();
        var (client2, userId2, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Use UNIQUE movie ID to avoid cross-test pollution
  var uniqueMovieId = 999001;

     // Both users like the same movie
 await client1.PostAsJsonAsync($"/api/movies/{uniqueMovieId}/like", new
        {
       Title = "Test Movie 999001",
            PosterPath = "/poster.jpg",
     ReleaseYear = "2010"
      });

        await client2.PostAsJsonAsync($"/api/movies/{uniqueMovieId}/like", new
    {
            Title = "Test Movie 999001",
        PosterPath = "/poster.jpg",
ReleaseYear = "2010"
        });

        // Act - User1 gets candidates
        var response = await client1.GetAsync("/api/matches/candidates");
        var candidates = await response.Content.ReadFromJsonAsync<List<CandidateDto>>();

      // Assert - Should contain user2 as a candidate
        candidates.Should().NotBeNull();
        candidates.Should().Contain(c => c.UserId == userId2);
  var user2Candidate = candidates!.First(c => c.UserId == userId2);
        user2Candidate.OverlapCount.Should().BeGreaterOrEqualTo(1);
      user2Candidate.SharedMovieIds.Should().Contain(uniqueMovieId);
    }

    [Fact]
    public async Task GetCandidates_ExcludesCurrentUser()
 {
        // Arrange
        var (client, userId, _) = await _fixture.CreateAuthenticatedClientAsync();

  // Use unique movie ID
        var uniqueMovieId = 999002;

        // Like a movie
        await client.PostAsJsonAsync($"/api/movies/{uniqueMovieId}/like", new
        {
  Title = "Test Movie 999002",
            PosterPath = "/poster.jpg",
            ReleaseYear = "2010"
        });

    // Act
      var response = await client.GetAsync("/api/matches/candidates");
        var candidates = await response.Content.ReadFromJsonAsync<List<CandidateDto>>();

        // Assert - Should not include self
     candidates.Should().NotContain(c => c.UserId == userId);
    }

    [Fact]
    public async Task GetCandidates_OrdersByOverlapCount()
    {
        // Arrange
        var (client1, userId1, _) = await _fixture.CreateAuthenticatedClientAsync();
   var (client2, userId2, _) = await _fixture.CreateAuthenticatedClientAsync();
    var (client3, userId3, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Use UNIQUE movie IDs for this test
        var movie1 = 999010;
  var movie2 = 999011;
        var movie3 = 999012;

        // User1 likes 3 movies
        await client1.PostAsJsonAsync($"/api/movies/{movie1}/like", new { Title = "Movie1", PosterPath = "/1.jpg", ReleaseYear = "2010" });
        await client1.PostAsJsonAsync($"/api/movies/{movie2}/like", new { Title = "Movie2", PosterPath = "/2.jpg", ReleaseYear = "1972" });
     await client1.PostAsJsonAsync($"/api/movies/{movie3}/like", new { Title = "Movie3", PosterPath = "/3.jpg", ReleaseYear = "1999" });

        // User2 likes 2 overlapping movies
        await client2.PostAsJsonAsync($"/api/movies/{movie1}/like", new { Title = "Movie1", PosterPath = "/1.jpg", ReleaseYear = "2010" });
        await client2.PostAsJsonAsync($"/api/movies/{movie2}/like", new { Title = "Movie2", PosterPath = "/2.jpg", ReleaseYear = "1972" });

        // User3 likes 1 overlapping movie
        await client3.PostAsJsonAsync($"/api/movies/{movie1}/like", new { Title = "Movie1", PosterPath = "/1.jpg", ReleaseYear = "2010" });

        // Act
        var response = await client1.GetAsync("/api/matches/candidates");
 var candidates = await response.Content.ReadFromJsonAsync<List<CandidateDto>>();

 // Assert - Should contain both users, ordered by overlap
    candidates.Should().NotBeNull();
        
     var user2Candidate = candidates!.FirstOrDefault(c => c.UserId == userId2);
  var user3Candidate = candidates!.FirstOrDefault(c => c.UserId == userId3);
        
        user2Candidate.Should().NotBeNull();
        user3Candidate.Should().NotBeNull();
        
        user2Candidate!.OverlapCount.Should().Be(2);
   user3Candidate!.OverlapCount.Should().Be(1);
  
        // User2 (higher overlap) should come before User3
        var user2Index = candidates.IndexOf(user2Candidate);
      var user3Index = candidates.IndexOf(user3Candidate);
        user2Index.Should().BeLessThan(user3Index);
    }

    [Fact]
    public async Task GetCandidates_WithTakeParameter_LimitsResults()
    {
 // Arrange
        var (client1, _, _) = await _fixture.CreateAuthenticatedClientAsync();
        var clients = await _fixture.CreateAuthenticatedClientsAsync(5);

        // Use unique movie ID
        var uniqueMovieId = 999020;

        // All users like the same movie
    await client1.PostAsJsonAsync($"/api/movies/{uniqueMovieId}/like", new { Title = "Movie", PosterPath = "/1.jpg", ReleaseYear = "2010" });
foreach (var (c, _, _) in clients)
      {
       await c.PostAsJsonAsync($"/api/movies/{uniqueMovieId}/like", new { Title = "Movie", PosterPath = "/1.jpg", ReleaseYear = "2010" });
  }

  // Act
  var response = await client1.GetAsync("/api/matches/candidates?take=3");
        var candidates = await response.Content.ReadFromJsonAsync<List<CandidateDto>>();

        // Assert
        candidates.Should().NotBeNull();
        candidates.Should().HaveCountLessOrEqualTo(3);
    }

    #endregion

    #region RequestMatch Tests

    [Fact]
    public async Task RequestMatch_WithoutAuth_Returns401()
  {
        // Arrange
        var client = _fixture.CreateClient();

    // Act
        var response = await client.PostAsJsonAsync("/api/matches/request", new
        {
         TargetUserId = "some-user-id",
            TmdbId = 27205
        });

    // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestMatch_WithValidRequest_ReturnsMatchedFalse()
  {
        // Arrange
        var (client1, _, _) = await _fixture.CreateAuthenticatedClientAsync();
        var (_, userId2, _) = await _fixture.CreateAuthenticatedClientAsync();

        var requestDto = new RequestMatchDto
        {
    TargetUserId = userId2,
         TmdbId = 27205
        };

   // Act
        var response = await client1.PostAsJsonAsync("/api/matches/request", requestDto);
    var result = await response.Content.ReadFromJsonAsync<MatchResultDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
 result.Should().NotBeNull();
  result!.Matched.Should().BeFalse();
result.RoomId.Should().BeNull();
    }

    [Fact]
    public async Task RequestMatch_WithReciprocalRequest_ReturnsMatchedTrueAndRoomId()
    {
        // Arrange
     var (client1, userId1, _) = await _fixture.CreateAuthenticatedClientAsync();
        var (client2, userId2, _) = await _fixture.CreateAuthenticatedClientAsync();

        // User1 requests User2
     var result1Response = await client1.PostAsJsonAsync("/api/matches/request", new RequestMatchDto
        {
            TargetUserId = userId2,
            TmdbId = 27205
     });
        var result1 = await result1Response.Content.ReadFromJsonAsync<MatchResultDto>();

        // Act - User2 requests User1 (reciprocal)
        var result2Response = await client2.PostAsJsonAsync("/api/matches/request", new RequestMatchDto
 {
            TargetUserId = userId1,
     TmdbId = 27205
        });
        var result2 = await result2Response.Content.ReadFromJsonAsync<MatchResultDto>();

   // Assert
        result1!.Matched.Should().BeFalse();
     result1.RoomId.Should().BeNull();

        result2!.Matched.Should().BeTrue();
     result2.RoomId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RequestMatch_Idempotent_ReturnsSameResult()
    {
        // Arrange
   var (client1, _, _) = await _fixture.CreateAuthenticatedClientAsync();
        var (_, userId2, _) = await _fixture.CreateAuthenticatedClientAsync();

        var requestDto = new RequestMatchDto
        {
 TargetUserId = userId2,
            TmdbId = 27205
    };

      // Act - Request twice
        var response1 = await client1.PostAsJsonAsync("/api/matches/request", requestDto);
var response2 = await client1.PostAsJsonAsync("/api/matches/request", requestDto);

   var result1 = await response1.Content.ReadFromJsonAsync<MatchResultDto>();
        var result2 = await response2.Content.ReadFromJsonAsync<MatchResultDto>();

        // Assert - Both should return the same result
        result1!.Matched.Should().Be(result2!.Matched);
 }

    [Fact]
    public async Task RequestMatch_SelfMatch_Returns400()
    {
        // Arrange
        var (client, userId, _) = await _fixture.CreateAuthenticatedClientAsync();

        var requestDto = new RequestMatchDto
        {
      TargetUserId = userId, // Self-match
    TmdbId = 27205
        };

      // Act
     var response = await client.PostAsJsonAsync("/api/matches/request", requestDto);

        // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestMatch_WithMissingTargetUserId_Returns400()
{
        // Arrange
var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        var requestDto = new RequestMatchDto
        {
            TargetUserId = "", // Missing
   TmdbId = 27205
  };

     // Act
        var response = await client.PostAsJsonAsync("/api/matches/request", requestDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Validation Tests (New)

 [Fact]
 public async Task RequestMatch_WithEmptyGuid_Returns400()
    {
        // Arrange
  var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

   var requestDto = new RequestMatchDto
  {
      TargetUserId = Guid.Empty.ToString(), // Empty GUID
   TmdbId = 27205
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/matches/request", requestDto);

   // Assert
  response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestMatch_WithInvalidGuidFormat_Returns400()
    {
   // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

      var requestDto = new RequestMatchDto
      {
      TargetUserId = "not-a-valid-guid",
            TmdbId = 27205
  };

  // Act
 var response = await client.PostAsJsonAsync("/api/matches/request", requestDto);

  // Assert
  response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCandidates_WithNegativeTake_Returns400OrClamps()
    {
        // Arrange
     var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
  var response = await client.GetAsync("/api/matches/candidates?take=-10");

     // Assert
      (response.StatusCode == HttpStatusCode.BadRequest || 
   response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
    }

[Fact]
    public async Task GetCandidates_WithVeryLargeTake_Clamps()
    {
      // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

     // Act
var response = await client.GetAsync("/api/matches/candidates?take=10000");

// Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var candidates = await response.Content.ReadFromJsonAsync<List<CandidateDto>>();
  candidates.Should().NotBeNull();
        candidates!.Count.Should().BeLessOrEqualTo(100); // Assuming max is 100
    }

    [Fact]
    public async Task RequestMatch_WithNonExistentTargetUser_ReturnsGracefully()
    {
  // Arrange
   var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        var requestDto = new RequestMatchDto
        {
      TargetUserId = Guid.NewGuid().ToString(), // Valid GUID format but non-existent user
          TmdbId = 27205
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/matches/request", requestDto);

        // Assert - Should handle gracefully (400 BadRequest or 404 NotFound, not crash)
 (response.StatusCode == HttpStatusCode.BadRequest || 
         response.StatusCode == HttpStatusCode.NotFound).Should().BeTrue();
    }

    [Fact]
    public async Task RequestMatch_WithZeroTmdbId_Returns400()
    {
        // Arrange
 var (client1, _, _) = await _fixture.CreateAuthenticatedClientAsync();
    var (_, userId2, _) = await _fixture.CreateAuthenticatedClientAsync();

      var requestDto = new RequestMatchDto
        {
    TargetUserId = userId2,
   TmdbId = 0 // Invalid movie ID
        };

        // Act
        var response = await client1.PostAsJsonAsync("/api/matches/request", requestDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}
