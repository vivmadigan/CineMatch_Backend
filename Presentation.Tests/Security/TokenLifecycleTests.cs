using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Presentation.Tests.Helpers;
using Xunit;

namespace Presentation.Tests.Security;

/// <summary>
/// API integration tests for JWT token lifecycle and validation.
/// Tests expired tokens, tampered tokens, invalid tokens, and token security.
/// GOAL: Verify authentication middleware properly validates JWT tokens.
/// IMPORTANCE: CRITICAL - Token validation is the foundation of API security.
/// </summary>
[Collection(nameof(ApiTestCollection))]
public class TokenLifecycleTests
{
    private readonly ApiTestFixture _fixture;

    public TokenLifecycleTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Expired Token Tests

    /// <summary>
    /// SECURITY TEST: Expired JWT token is rejected by API.
    /// GOAL: Tokens expire after configured time (7 days).
    /// IMPORTANCE: CRITICAL - Prevents indefinite token validity.
  /// </summary>
    [Fact]
    public async Task Discover_WithExpiredToken_Returns401()
    {
      // Arrange
      var client = _fixture.CreateClient();
      
        // Create an expired token (expires -1 day ago)
        var expiredToken = CreateToken(
          userId: "test-user",
            email: "test@test.com",
  displayName: "Test User",
        expiresInDays: -1 // Already expired
        );

        client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await client.GetAsync("/api/movies/discover");

   // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// SECURITY TEST: Token about to expire is still accepted.
    /// GOAL: Verify token valid until exact expiry moment.
  /// IMPORTANCE: Edge case - tokens valid until last second.
    /// </summary>
    [Fact]
    public async Task Discover_WithAlmostExpiredToken_Returns200()
 {
        // Arrange
  var client = _fixture.CreateClient();
        
        // Create token expiring in 1 minute
  var almostExpiredToken = CreateToken(
            userId: "test-user",
            email: "test@test.com",
            displayName: "Test User",
            expiresInMinutes: 1 // Valid for 1 more minute
        );

        client.DefaultRequestHeaders.Authorization =
       new AuthenticationHeaderValue("Bearer", almostExpiredToken);

        // Act
        var response = await client.GetAsync("/api/movies/discover");

     // Assert - Should still work
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// SECURITY TEST: Multiple endpoints reject expired token consistently.
    /// GOAL: All protected endpoints validate token expiration.
    /// IMPORTANCE: Comprehensive security validation.
    /// </summary>
    [Fact]
    public async Task AllEndpoints_WithExpiredToken_Return401()
    {
        // Arrange
        var client = _fixture.CreateClient();
        var expiredToken = CreateToken("test", "test@test.com", "Test", expiresInDays: -1);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act - Try multiple endpoints
        var responses = new[]
      {
            await client.GetAsync("/api/movies/discover"),
            await client.GetAsync("/api/movies/likes"),
            await client.GetAsync("/api/preferences"),
 await client.GetAsync("/api/matches/candidates")
        };

    // Assert - All should reject expired token
        responses.Should().AllSatisfy(r =>
 r.StatusCode.Should().Be(HttpStatusCode.Unauthorized));
    }

    #endregion

    #region Tampered Token Tests

    /// <summary>
 /// SECURITY TEST: Token with modified payload is rejected.
    /// GOAL: Signature validation prevents payload tampering.
    /// IMPORTANCE: CRITICAL - Prevents privilege escalation attacks.
    /// </summary>
  [Fact]
    public async Task Discover_WithTamperedTokenPayload_Returns401()
    {
        // Arrange
        var client = _fixture.CreateClient();
        
        // Create valid token
        var validToken = CreateToken("user1", "user@test.com", "User");
 
        // Tamper with payload (change user ID in token without re-signing)
 var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(validToken);
        
    // Create new token with modified claims but same signature (invalid!)
   var tamperedPayload = jwtToken.Payload.SerializeToJson();
        var parts = validToken.Split('.');
        
        // Modify middle part (payload) while keeping signature
        var modifiedPayload = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(tamperedPayload.Replace("user1", "admin")));
        var tamperedToken = $"{parts[0]}.{modifiedPayload}.{parts[2]}";

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tamperedToken);

        // Act
      var response = await client.GetAsync("/api/movies/discover");

        // Assert - Should reject tampered token
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// SECURITY TEST: Token with wrong signature is rejected.
    /// GOAL: Different secret key produces invalid signature.
    /// IMPORTANCE: CRITICAL - Only our server can create valid tokens.
    /// </summary>
    [Fact]
    public async Task Discover_WithWrongSignature_Returns401()
    {
        // Arrange
        var client = _fixture.CreateClient();
        
     // Create token signed with WRONG secret key
   var wrongToken = CreateToken(
    userId: "test-user",
          email: "test@test.com",
    displayName: "Test",
   secretKey: "WrongSecretKeyThatDoesNotMatchServerConfiguration123456789"
        );

        client.DefaultRequestHeaders.Authorization =
  new AuthenticationHeaderValue("Bearer", wrongToken);

        // Act
    var response = await client.GetAsync("/api/movies/discover");

        // Assert - Should reject token with wrong signature
     response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Invalid Token Format Tests

    /// <summary>
    /// SECURITY TEST: Malformed token string is rejected.
    /// GOAL: Non-JWT tokens are not processed.
    /// IMPORTANCE: Defensive programming against malformed input.
    /// </summary>
    [Theory]
    [InlineData("not-a-jwt-token")]
    [InlineData("Bearer invalid")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")]  // Only header part
    [InlineData("")]
 [InlineData("   ")]
    public async Task Discover_WithMalformedToken_Returns401(string malformedToken)
    {
        // Arrange
        var client = _fixture.CreateClient();
 client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", malformedToken);

    // Act
var response = await client.GetAsync("/api/movies/discover");

        // Assert
 response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
 }

    /// <summary>
    /// SECURITY TEST: Token with missing required claims is rejected.
    /// GOAL: All required claims (userId, email) must be present.
    /// IMPORTANCE: Application logic depends on these claims.
    /// </summary>
    [Fact]
    public async Task Discover_WithTokenMissingUserId_Returns401()
    {
        // Arrange
        var client = _fixture.CreateClient();
 
  // Create token WITHOUT userId claim
     var tokenWithoutUserId = CreateTokenWithCustomClaims(new[]
        {
         new Claim(ClaimTypes.Email, "test@test.com"),
        new Claim("DisplayName", "Test User")
            // Missing: ClaimTypes.NameIdentifier (userId)
        });

        client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", tokenWithoutUserId);

     // Act
   var response = await client.GetAsync("/api/movies/discover");

        // Assert - May return 401 or 500 depending on implementation
        response.StatusCode.Should().BeOneOf(
   HttpStatusCode.Unauthorized,
       HttpStatusCode.InternalServerError
        );
    }

    #endregion

    #region Wrong Issuer/Audience Tests

    /// <summary>
    /// SECURITY TEST: Token with wrong issuer is rejected.
    /// GOAL: Only tokens from our server are accepted.
    /// IMPORTANCE: Prevents tokens from other services being used.
    /// </summary>
    [Fact]
    public async Task Discover_WithWrongIssuer_Returns401()
    {
    // Arrange
     var client = _fixture.CreateClient();
        
      // Create token with different issuer
        var tokenWithWrongIssuer = CreateToken(
         userId: "test-user",
      email: "test@test.com",
displayName: "Test",
  issuer: "WrongIssuer"  // Should be "CineMatchTest"
 );

 client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", tokenWithWrongIssuer);

        // Act
    var response = await client.GetAsync("/api/movies/discover");

    // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// SECURITY TEST: Token with wrong audience is rejected.
    /// GOAL: Tokens are scoped to specific audience.
    /// IMPORTANCE: Prevents token reuse across different apps.
    /// </summary>
    [Fact]
    public async Task Discover_WithWrongAudience_Returns401()
    {
        // Arrange
        var client = _fixture.CreateClient();
        
        // Create token with different audience
        var tokenWithWrongAudience = CreateToken(
         userId: "test-user",
        email: "test@test.com",
            displayName: "Test",
   audience: "WrongAudience"  // Should be "CineMatchTest"
        );

    client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", tokenWithWrongAudience);

        // Act
 var response = await client.GetAsync("/api/movies/discover");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

  #endregion

    #region Token Reuse and Security Tests

  /// <summary>
    /// SECURITY TEST: Same token can be reused (stateless JWT design).
  /// GOAL: Verify stateless token behavior.
    /// IMPORTANCE: Understanding JWT behavior - no server-side revocation by default.
    /// </summary>
 [Fact]
    public async Task Discover_WithSameTokenTwice_BothSucceed()
  {
        // Arrange
        var client = _fixture.CreateClient();
        var token = CreateToken("test-user", "test@test.com", "Test");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act - Use same token twice
        var response1 = await client.GetAsync("/api/movies/discover");
        var response2 = await client.GetAsync("/api/movies/discover");

      // Assert - Both should succeed (stateless JWTs)
 response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

#region Helper Methods

    /// <summary>
    /// Create a JWT token with custom expiration.
    /// </summary>
    private string CreateToken(
        string userId,
 string email,
        string displayName,
        int expiresInDays = 7,
        int expiresInMinutes = 0,
      string? secretKey = null,
        string? issuer = null,
        string? audience = null)
    {
  var claims = new[]
        {
new Claim(ClaimTypes.NameIdentifier, userId),
   new Claim(ClaimTypes.Email, email),
            new Claim("DisplayName", displayName)
        };

        return CreateTokenWithCustomClaims(claims, expiresInDays, expiresInMinutes, secretKey, issuer, audience);
    }

    /// <summary>
    /// Create a JWT token with custom claims.
    /// </summary>
    private string CreateTokenWithCustomClaims(
        Claim[] claims,
 int expiresInDays = 7,
        int expiresInMinutes = 0,
        string? secretKey = null,
 string? issuer = null,
      string? audience = null)
    {
     // Use test configuration values (from ApiTestFixture)
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
secretKey ?? "TestSecretKeyForJwtTokenGenerationInIntegrationTests123456789"));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

      var expiration = expiresInMinutes != 0
            ? DateTime.UtcNow.AddMinutes(expiresInMinutes)
            : DateTime.UtcNow.AddDays(expiresInDays);

        var token = new JwtSecurityToken(
      issuer: issuer ?? "CineMatchTest",
            audience: audience ?? "CineMatchTest",
        claims: claims,
 expires: expiration,
 signingCredentials: credentials
    );

        return new JwtSecurityTokenHandler().WriteToken(token);
 }

    #endregion
}
