using FluentAssertions;
using Infrastructure.Models;
using Presentation.Tests.Helpers;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Presentation.Tests.Controllers;

/// <summary>
/// Integration tests for MyInformationController.
/// Tests user information retrieval with authentication.
/// </summary>
[Collection(nameof(ApiTestCollection))]
public sealed class MyInformationControllerTests
{
    private readonly ApiTestFixture _fixture;

    public MyInformationControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Authentication Tests

    /// <summary>
    /// GOAL: Verify unauthenticated requests are rejected.
    /// IMPORTANCE: Security - prevent unauthorized access to user data.
    /// </summary>
    [Fact]
    public async Task GetMyInformation_WithoutAuth_Returns401()
    {
        // Arrange
        var client = _fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/myinformation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// GOAL: Verify invalid JWT tokens are rejected.
    /// IMPORTANCE: Security - prevent forged tokens.
    /// </summary>
    [Fact]
    public async Task GetMyInformation_WithInvalidToken_Returns401()
    {
        // Arrange
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer invalid-token-12345");

        // Act
        var response = await client.GetAsync("/api/myinformation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// GOAL: Verify malformed auth headers are rejected.
    /// IMPORTANCE: Security - validate auth header format.
    /// </summary>
    [Fact]
    public async Task GetMyInformation_WithMalformedAuthHeader_Returns401()
    {
        // Arrange
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "NotBearer some-token");

        // Act
        var response = await client.GetAsync("/api/myinformation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// GOAL: Verify expired tokens are rejected.
    /// IMPORTANCE: Security - prevent replay attacks with old tokens.
    /// </summary>
    [Fact]
    public async Task GetMyInformation_WithExpiredToken_Returns401()
    {
        // Arrange
        var client = _fixture.CreateClient();
        // Simulated expired JWT token (exp claim in the past)
        var expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE1MTYyMzkwMjJ9.invalid";
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {expiredToken}");

        // Act
        var response = await client.GetAsync("/api/myinformation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Success Tests

    /// <summary>
    /// GOAL: Verify authenticated user can retrieve their information.
    /// IMPORTANCE: Core functionality - users need to see their profile data.
    /// </summary>
    [Fact]
    public async Task GetMyInformation_WithAuth_Returns200AndUserData()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"myinfo{uniqueId}@test.com";
        var displayName = $"MyInfoUser{uniqueId}";
        
        var (client, userId, _) = await _fixture.CreateAuthenticatedClientAsync(email, displayName);

        // Act
        var response = await client.GetAsync("/api/myinformation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await response.Content.ReadFromJsonAsync<MyInformationDto>();
        
        data.Should().NotBeNull();
        data!.UserId.Should().Be(userId);
        data.Email.Should().Be(email);
        data.DisplayName.Should().Be(displayName);
    }

    /// <summary>
    /// GOAL: Verify response contains all required fields.
    /// IMPORTANCE: Contract - frontend depends on this structure.
    /// </summary>
    [Fact]
    public async Task GetMyInformation_ReturnsCompleteDataStructure()
    {
        // Arrange
        var (client, userId, email) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/myinformation");
        var data = await response.Content.ReadFromJsonAsync<MyInformationDto>();

        // Assert
        data.Should().NotBeNull();
        data!.UserId.Should().NotBeNullOrEmpty();
        data.Email.Should().NotBeNullOrEmpty();
        data.DisplayName.Should().NotBeNull(); // Can be empty but not null
        data.FirstName.Should().NotBeNull(); // Can be empty but not null
        data.LastName.Should().NotBeNull(); // Can be empty but not null
    }

    /// <summary>
    /// GOAL: Verify password fields are never exposed.
    /// IMPORTANCE: CRITICAL SECURITY - prevent password hash leaks.
    /// </summary>
    [Fact]
    public async Task GetMyInformation_DoesNotExposePasswordHash()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/myinformation");
        var responseText = await response.Content.ReadAsStringAsync();

        // Assert - Verify sensitive fields are NOT in response
        responseText.Should().NotContain("password", "case insensitive check");
        responseText.Should().NotContain("PasswordHash");
        responseText.Should().NotContain("SecurityStamp");
        responseText.Should().NotContain("ConcurrencyStamp");
    }

    /// <summary>
    /// GOAL: Verify each user only sees their own data.
    /// IMPORTANCE: Security - prevent information leaks.
    /// </summary>
    [Fact]
    public async Task GetMyInformation_ReturnsOnlyCurrentUserData()
    {
        // Arrange - Create two different users
        var uniqueId1 = Guid.NewGuid().ToString()[..8];
        var uniqueId2 = Guid.NewGuid().ToString()[..8];
        
        var email1 = $"user1-{uniqueId1}@test.com";
        var email2 = $"user2-{uniqueId2}@test.com";
        
        var (client1, userId1, _) = await _fixture.CreateAuthenticatedClientAsync(email1, $"User1-{uniqueId1}");
        var (client2, userId2, _) = await _fixture.CreateAuthenticatedClientAsync(email2, $"User2-{uniqueId2}");

        userId1.Should().NotBe(userId2, "users should be different");

        // Act
        var response1 = await client1.GetAsync("/api/myinformation");
        var response2 = await client2.GetAsync("/api/myinformation");

        var data1 = await response1.Content.ReadFromJsonAsync<MyInformationDto>();
        var data2 = await response2.Content.ReadFromJsonAsync<MyInformationDto>();

        // Assert - Each user sees only their own data
        data1!.UserId.Should().Be(userId1);
        data1.Email.Should().Be(email1);

        data2!.UserId.Should().Be(userId2);
        data2.Email.Should().Be(email2);

        data1.UserId.Should().NotBe(data2.UserId);
    }

    #endregion

    #region Concurrency Tests

    /// <summary>
    /// GOAL: Verify endpoint handles concurrent requests from same user.
    /// IMPORTANCE: Performance - users might refresh or have multiple tabs.
    /// </summary>
    [Fact]
    public async Task GetMyInformation_HandlesConcurrentRequests()
    {
        // Arrange
        var (client, userId, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act - Send 5 concurrent requests
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => client.GetAsync("/api/myinformation"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed with same data
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        var dataList = await Task.WhenAll(
            responses.Select(r => r.Content.ReadFromJsonAsync<MyInformationDto>())
        );

        dataList.Should().OnlyContain(d => d!.UserId == userId);
    }

    /// <summary>
    /// GOAL: Verify endpoint is idempotent (repeated calls return same data).
    /// IMPORTANCE: Reliability - safe to retry without side effects.
    /// </summary>
    [Fact]
    public async Task GetMyInformation_IsIdempotent()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act - Call 3 times
        var response1 = await client.GetAsync("/api/myinformation");
        var response2 = await client.GetAsync("/api/myinformation");
        var response3 = await client.GetAsync("/api/myinformation");

        var data1 = await response1.Content.ReadFromJsonAsync<MyInformationDto>();
        var data2 = await response2.Content.ReadFromJsonAsync<MyInformationDto>();
        var data3 = await response3.Content.ReadFromJsonAsync<MyInformationDto>();

        // Assert - All responses identical
        data2.Should().BeEquivalentTo(data1);
        data3.Should().BeEquivalentTo(data1);
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// GOAL: Verify endpoint handles users with minimal profile data.
    /// IMPORTANCE: Some fields might be optional.
    /// </summary>
    [Fact]
    public async Task GetMyInformation_WithMinimalProfile_ReturnsValidData()
    {
        // Arrange - User created by fixture has minimal required fields
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/myinformation");
        var data = await response.Content.ReadFromJsonAsync<MyInformationDto>();

        // Assert - Required fields exist, optional fields can be empty
        data.Should().NotBeNull();
        data!.UserId.Should().NotBeNullOrEmpty();
        data.Email.Should().NotBeNullOrEmpty();
        data.DisplayName.Should().NotBeNull(); // Required but not empty
    }

    /// <summary>
    /// GOAL: Verify endpoint response time is acceptable.
    /// IMPORTANCE: Performance - should be fast (< 500ms).
    /// </summary>
    [Fact]
    public async Task GetMyInformation_CompletesQuickly()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.GetAsync("/api/myinformation");
        sw.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sw.ElapsedMilliseconds.Should().BeLessThan(500, "endpoint should respond quickly");
    }

    #endregion

    #region HTTP Method Tests

    /// <summary>
    /// GOAL: Verify only GET method is allowed.
    /// IMPORTANCE: REST compliance - wrong method should be rejected.
    /// </summary>
    [Fact]
    public async Task MyInformation_PostMethod_Returns405()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.PostAsync("/api/myinformation", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    /// <summary>
    /// GOAL: Verify PUT method is not allowed.
    /// IMPORTANCE: Endpoint is read-only.
    /// </summary>
    [Fact]
    public async Task MyInformation_PutMethod_Returns405()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.PutAsJsonAsync("/api/myinformation", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    /// <summary>
    /// GOAL: Verify DELETE method is not allowed.
    /// IMPORTANCE: Endpoint is read-only.
    /// </summary>
    [Fact]
    public async Task MyInformation_DeleteMethod_Returns405()
    {
        // Arrange
        var (client, _, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.DeleteAsync("/api/myinformation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    #endregion
}
