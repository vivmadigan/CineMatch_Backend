using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Infrastructure.Models;
using Presentation.Tests.Helpers;
using Xunit;

namespace Presentation.Tests.Controllers;

/// <summary>
/// Integration tests for SignInController.
/// Tests authentication, token generation, and error handling.
/// </summary>
[Collection(nameof(ApiTestCollection))]
public class SignInControllerTests
{
    private readonly ApiTestFixture _fixture;
    private const string TestPassword = "TestPass123!"; // Known password from ApiTestFixture

    public SignInControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Positive Scenarios

    [Fact]
    public async Task SignIn_WithValidCredentials_Returns200AndToken()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"signin{uniqueId}@test.com";

        // Create user via signup
        await _fixture.CreateAuthenticatedClientAsync(email: email, displayName: $"SignIn{uniqueId}");

        var unauthClient = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = email,
            Password = TestPassword
        };

        // Act
        var response = await unauthClient.PostAsJsonAsync("/api/signin", signInDto);
        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
        result.Email.Should().Be(email);
        result.DisplayName.Should().Be($"SignIn{uniqueId}");
    }

    [Fact]
    public async Task SignIn_TokenCanBeUsedForAuthenticatedRequests()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"tokenuse{uniqueId}@test.com";

        await _fixture.CreateAuthenticatedClientAsync(email: email);

        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = email,
            Password = TestPassword
        };
        var signInResponse = await client.PostAsJsonAsync("/api/signin", signInDto);
        var authResult = await signInResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Act - Use token for authenticated request
        client.DefaultRequestHeaders.Authorization =
       new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult!.Token);
        var preferencesResponse = await client.GetAsync("/api/preferences");

        // Assert
        preferencesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignIn_CaseInsensitiveEmail_Succeeds()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"CaseTest{uniqueId}@Test.COM";

        await _fixture.CreateAuthenticatedClientAsync(email: email.ToLower());

        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = email, // Different case
            Password = TestPassword
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signin", signInDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignIn_ReturnsNewTokenEachTime()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"multitoken{uniqueId}@test.com";

        await _fixture.CreateAuthenticatedClientAsync(email: email);

        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = email,
            Password = TestPassword
        };

        // Act - Sign in twice with delay to ensure different timestamps
        var response1 = await client.PostAsJsonAsync("/api/signin", signInDto);
        var result1 = await response1.Content.ReadFromJsonAsync<AuthResponseDto>();

        await Task.Delay(1100); // Ensure different issued-at time (JWT uses seconds precision)

        var response2 = await client.PostAsJsonAsync("/api/signin", signInDto);
        var result2 = await response2.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Assert - Tokens should be different due to different iat (issued-at) claim
        result1!.Token.Should().NotBe(result2!.Token);
    }

    [Fact]
    public async Task SignIn_TokenContainsCorrectClaims()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"claims{uniqueId}@test.com";
        var displayName = $"Claims{uniqueId}";

        var (_, userId, _) = await _fixture.CreateAuthenticatedClientAsync(
                   email: email,
                   displayName: displayName
               );

        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = email,
            Password = TestPassword
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signin", signInDto);
        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Decode token
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result!.Token);

        // Assert - Check for correct claim types (displayName, not Name)
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == email);
        jwtToken.Claims.Should().Contain(c => c.Type == "displayName" && c.Value == displayName); // Changed from ClaimTypes.Name
    }

    #endregion

    #region Negative Scenarios

    [Fact]
    public async Task SignIn_WithWrongPassword_Returns401Unauthorized()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"wrongpw{uniqueId}@test.com";

        await _fixture.CreateAuthenticatedClientAsync(email: email);

        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = email,
            Password = "WrongPassword123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signin", signInDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task SignIn_WithNonExistentEmail_Returns401Unauthorized()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = "nonexistent@test.com",
            Password = TestPassword
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signin", signInDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task SignIn_WithInvalidEmailFormat_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = "not-an-email",
            Password = TestPassword
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signin", signInDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignIn_WithEmptyEmail_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = "",
            Password = TestPassword
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signin", signInDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignIn_WithEmptyPassword_Returns400BadRequest()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"emptypw{uniqueId}@test.com";

        await _fixture.CreateAuthenticatedClientAsync(email: email);

        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = email,
            Password = ""
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signin", signInDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignIn_WithNullCredentials_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/signin", (SignInDto)null!);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignIn_WithSqlInjectionAttempt_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = "admin' OR '1'='1", // SQL injection attempt
            Password = "password"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signin", signInDto);

        // Assert - Invalid email format returns 400 BadRequest (validation happens first)
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignIn_WithXssAttemptInEmail_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = "<script>alert('XSS')</script>@test.com",
            Password = TestPassword
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signin", signInDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignIn_MultipleFailedAttempts_DoesNotLockout()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"lockout{uniqueId}@test.com";

        await _fixture.CreateAuthenticatedClientAsync(email: email);

        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = email,
            Password = "WrongPassword123!"
        };

        // Act - Try multiple failed attempts
        for (int i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/signin", signInDto);
        }

        // Try with correct password
        signInDto.Password = TestPassword;
        var response = await client.PostAsJsonAsync("/api/signin", signInDto);

        // Assert - Should still succeed (lockout is disabled)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignIn_WithVeryLongPassword_HandlesGracefully()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"longpw{uniqueId}@test.com";

        await _fixture.CreateAuthenticatedClientAsync(email: email);

        var client = _fixture.CreateClient();
        var signInDto = new SignInDto
        {
            Email = email,
            Password = new string('a', 10000) // Very long password
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signin", signInDto);

        // Assert - Should handle without crashing
        (response.StatusCode == HttpStatusCode.Unauthorized ||
        response.StatusCode == HttpStatusCode.BadRequest).Should().BeTrue();
    }

    #endregion
}
