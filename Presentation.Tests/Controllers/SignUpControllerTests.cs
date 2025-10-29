using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Infrastructure.Data.Entities;
using Infrastructure.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Presentation.Tests.Helpers;
using Xunit;

namespace Presentation.Tests.Controllers;

/// <summary>
/// Integration tests for SignUpController.
/// Tests user registration, validation, and JWT token generation.
/// </summary>
[Collection(nameof(ApiTestCollection))]
public class SignUpControllerTests
{
    private readonly ApiTestFixture _fixture;

    public SignUpControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Positive Scenarios

    [Fact]
    public async Task SignUp_WithValidData_Returns200AndToken()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var signupDto = new SignUpDto
        {
            Email = $"newuser{uniqueId}@example.com",
            Password = "ValidPass123!",
            DisplayName = $"NewUser{uniqueId}",
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);
        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
        result.UserId.Should().NotBeNullOrEmpty();
        result.Email.Should().Be(signupDto.Email);
        result.DisplayName.Should().Be(signupDto.DisplayName);
    }

    [Fact]
    public async Task SignUp_CreatesUserInDatabase()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var signupDto = new SignUpDto
        {
            Email = $"dbuser{uniqueId}@example.com",
            Password = "ValidPass123!",
            DisplayName = $"DBUser{uniqueId}",
            FirstName = "Database",
            LastName = "Test"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);
        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Assert - Verify user exists in database
        using var scope = _fixture.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        var user = await userManager.FindByIdAsync(result!.UserId);

        user.Should().NotBeNull();
        user!.Email.Should().Be(signupDto.Email);
        user.DisplayName.Should().Be(signupDto.DisplayName);
        user.FirstName.Should().Be(signupDto.FirstName);
        user.LastName.Should().Be(signupDto.LastName);
    }

    [Fact]
    public async Task SignUp_WithMinimumRequiredFields_Succeeds()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var signupDto = new SignUpDto
        {
            Email = $"min{uniqueId}@example.com",
            Password = "Pass123!", // 8 chars + complexity requirements
            DisplayName = $"Mi{uniqueId}",
            FirstName = "Te",
            LastName = "Us"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignUp_WithLongValidPassword_Succeeds()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var signupDto = new SignUpDto
        {
            Email = $"longpass{uniqueId}@example.com",
            Password = "ThisIsAVeryLongPasswordThatShouldStillWork123!@#$%^&*()",
            DisplayName = $"LongPass{uniqueId}",
            FirstName = "Long",
            LastName = "Password"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignUp_TokenIsValidJwt()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var signupDto = new SignUpDto
        {
            Email = $"jwttest{uniqueId}@example.com",
            Password = "ValidPass123!",
            DisplayName = $"JWTTest{uniqueId}",
            FirstName = "JWT",
            LastName = "Test"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);
        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Assert - Token should have 3 parts (header.payload.signature)
        var tokenParts = result!.Token.Split('.');
        tokenParts.Should().HaveCount(3);
        tokenParts.All(part => !string.IsNullOrEmpty(part)).Should().BeTrue();
    }

    [Fact]
    public async Task SignUp_UserCanImmediatelySignIn()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"signin{uniqueId}@example.com";
        var password = "ValidPass123!";

        var signupDto = new SignUpDto
        {
            Email = email,
            Password = password,
            DisplayName = $"SignIn{uniqueId}",
            FirstName = "Sign",
            LastName = "In"
        };

        await client.PostAsJsonAsync("/api/signup", signupDto);

        // Act - Try to sign in immediately
        var signInDto = new SignInDto
        {
            Email = email,
            Password = password
        };
        var signInResponse = await client.PostAsJsonAsync("/api/signin", signInDto);

        // Assert
        signInResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignUp_WithUnicodeCharactersInName_Succeeds()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var signupDto = new SignUpDto
        {
            Email = $"unicode{uniqueId}@example.com",
            Password = "ValidPass123!",
            DisplayName = $"José{uniqueId}",
            FirstName = "Müller",
            LastName = "François"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);
        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.DisplayName.Should().Be(signupDto.DisplayName);
    }

    [Fact]
    public async Task SignUp_DisplayNameIsUnique()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var displayName = $"Unique{uniqueId}";

        // Create first user
        var (_, _, _) = await _fixture.CreateAuthenticatedClientAsync(
            email: $"first{uniqueId}@example.com",
     displayName: displayName
        );

        // Act - Try to create second user with same display name
        var client = _fixture.CreateClient();
        var signupDto = new SignUpDto
        {
            Email = $"second{uniqueId}@example.com",
            Password = "ValidPass123!",
            DisplayName = displayName, // Same display name!
            FirstName = "Test",
            LastName = "User"
        };
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Display Name is already in use");
    }

    #endregion

    #region Negative Scenarios

    [Fact]
    public async Task SignUp_WithDuplicateEmail_Returns409Conflict()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var email = $"duplicate{uniqueId}@example.com";

        // Create first user
        await _fixture.CreateAuthenticatedClientAsync(email: email);

        // Act - Try to create second user with same email
        var client = _fixture.CreateClient();
        var signupDto = new SignUpDto
        {
            Email = email, // Same email!
            Password = "ValidPass123!",
            DisplayName = $"Different{uniqueId}",
            FirstName = "Test",
            LastName = "User"
        };
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Email is already in use");
    }

    [Fact]
    public async Task SignUp_WithInvalidEmail_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var signupDto = new SignUpDto
        {
            Email = "not-an-email", // Invalid format
            Password = "ValidPass123!",
            DisplayName = $"Invalid{uniqueId}",
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignUp_WithShortPassword_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var signupDto = new SignUpDto
        {
            Email = $"shortpw{uniqueId}@example.com",
            Password = "Short1!", // Only 7 chars (min is 8)
            DisplayName = $"Short{uniqueId}",
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignUp_WithMissingRequiredFields_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var signupDto = new SignUpDto
        {
            Email = "test@example.com",
            Password = "ValidPass123!",
            DisplayName = "", // Missing required field
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignUp_WithEmptyPassword_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var signupDto = new SignUpDto
        {
            Email = $"empty{uniqueId}@example.com",
            Password = "", // Empty password
            DisplayName = $"Empty{uniqueId}",
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignUp_WithSpecialCharactersInDisplayName_ValidatesCorrectly()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var signupDto = new SignUpDto
        {
            Email = $"special{uniqueId}@example.com",
            Password = "ValidPass123!",
            DisplayName = $"User{uniqueId}_2024!",
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/signup", signupDto);

        // Assert - Should succeed (special chars allowed in display name)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
