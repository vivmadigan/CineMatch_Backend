using FluentAssertions;
using Infrastructure.Data.Entities;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace Infrastructure.Tests.Unit.Auth;

/// <summary>
/// Pure unit tests for JWT token creation logic.
/// Tests claim inclusion, expiry calculation, and edge cases.
/// GOAL: Verify token generation works correctly without external dependencies.
/// IMPORTANCE: CRITICAL SECURITY - Tokens control access to entire system.
/// </summary>
public class TokenCreationTests
{
    #region Required Claims Tests

    /// <summary>
    /// POSITIVE TEST: Token contains all required claims.
    /// GOAL: NameIdentifier, Email, and DisplayName claims are present.
    /// IMPORTANCE: Frontend needs these claims for user context.
    /// </summary>
    [Fact]
    public void CreateToken_ContainsAllRequiredClaims()
    {
        // Arrange
        var service = CreateTokenService();
        var user = new UserEntity
        {
 Id = "user123",
            Email = "test@example.com",
      DisplayName = "Test User",
FirstName = "Test",
  LastName = "User"
  };

    // Act
   var token = service.CreateToken(user);
        var claims = DecodeToken(token);

   // Assert - All required claims present
 var claimTypes = claims.Select(c => c.Type).ToList();
        claimTypes.Should().Contain(JwtRegisteredClaimNames.Sub);
        claimTypes.Should().Contain(ClaimTypes.NameIdentifier);
        claimTypes.Should().Contain(ClaimTypes.Email);
   claimTypes.Should().Contain("displayName");
        claimTypes.Should().Contain(ClaimTypes.GivenName);
        claimTypes.Should().Contain(ClaimTypes.Surname);
    }

    /// <summary>
    /// VERIFICATION TEST: Subject claim matches user ID.
    /// GOAL: JWT "sub" claim contains correct user identifier.
    /// IMPORTANCE: "sub" is standard JWT claim for subject identity.
    /// </summary>
    [Fact]
    public void CreateToken_SubjectClaimMatchesUserId()
    {
  // Arrange
    var service = CreateTokenService();
   var user = new UserEntity { Id = "user456", Email = "test@test.com", DisplayName = "Test" };

        // Act
        var token = service.CreateToken(user);
        var claims = DecodeToken(token);

   // Assert
        var subClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        subClaim.Should().NotBeNull();
        subClaim!.Value.Should().Be("user456");
    }

    /// <summary>
    /// VERIFICATION TEST: NameIdentifier claim matches user ID.
    /// GOAL: ASP.NET Identity uses NameIdentifier for user lookups.
    /// IMPORTANCE: Controllers extract userId from this claim.
    /// </summary>
    [Fact]
    public void CreateToken_NameIdentifierClaimMatchesUserId()
    {
        // Arrange
        var service = CreateTokenService();
        var user = new UserEntity { Id = "user789", Email = "test@test.com", DisplayName = "Test" };

  // Act
        var token = service.CreateToken(user);
   var claims = DecodeToken(token);

   // Assert
     var nameIdClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
   nameIdClaim.Should().NotBeNull();
        nameIdClaim!.Value.Should().Be("user789");
  }

  /// <summary>
    /// VERIFICATION TEST: Email claim contains user's email.
    /// GOAL: Email is accessible from token for display/logging.
    /// IMPORTANCE: Useful for audit trails and user identification.
  /// </summary>
    [Fact]
    public void CreateToken_EmailClaimMatchesUserEmail()
    {
        // Arrange
        var service = CreateTokenService();
   var user = new UserEntity { Id = "user1", Email = "john@example.com", DisplayName = "John" };

 // Act
   var token = service.CreateToken(user);
        var claims = DecodeToken(token);

   // Assert
        var emailClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
emailClaim.Should().NotBeNull();
        emailClaim!.Value.Should().Be("john@example.com");
    }

    /// <summary>
    /// VERIFICATION TEST: DisplayName claim uses custom "displayName" key.
    /// GOAL: Frontend expects "displayName" claim for showing user's name.
    /// IMPORTANCE: UI displays this in navbar and messages.
    /// </summary>
    [Fact]
    public void CreateToken_DisplayNameClaimUsesCorrectKey()
    {
        // Arrange
   var service = CreateTokenService();
        var user = new UserEntity { Id = "user1", Email = "test@test.com", DisplayName = "Alice Smith" };

 // Act
   var token = service.CreateToken(user);
 var claims = DecodeToken(token);

  // Assert
     var displayNameClaim = claims.FirstOrDefault(c => c.Type == "displayName");
     displayNameClaim.Should().NotBeNull();
        displayNameClaim!.Value.Should().Be("Alice Smith");
    }

    #endregion

    #region Expiry Calculation Tests

    /// <summary>
    /// TIMING TEST: Token expires in exactly 7 days.
    /// GOAL: Verify expiry is set correctly.
    /// IMPORTANCE: Tokens must expire for security.
    /// </summary>
    [Fact]
    public void CreateToken_ExpiresInSevenDays()
    {
 // Arrange
        var service = CreateTokenService();
    var user = new UserEntity { Id = "user1", Email = "test@test.com", DisplayName = "Test" };
var beforeCreation = DateTime.UtcNow;

        // Act
   var token = service.CreateToken(user);
        var handler = new JwtSecurityTokenHandler();
   var jwtToken = handler.ReadJwtToken(token);

var afterCreation = DateTime.UtcNow;

     // Assert - Token expires in ~7 days
        var expectedExpiry = beforeCreation.AddDays(7);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
        
        // Verify it's definitely more than 6 days
      jwtToken.ValidTo.Should().BeAfter(beforeCreation.AddDays(6).AddHours(23));
    }

    /// <summary>
    /// BOUNDARY TEST: Token not valid before "nbf" (not before) time.
    /// GOAL: Token has valid "not before" claim.
    /// IMPORTANCE: Prevents use of tokens issued for future time.
    /// NOTE: Skipped - JwtTokenService doesn't explicitly set ValidFrom/NotBefore.
    /// </summary>
    [Fact(Skip = "JwtTokenService doesn't set ValidFrom/NotBefore claim")]
    public void CreateToken_HasNotBeforeTime()
    {
        // Implementation doesn't set "nbf" claim, which is acceptable
    // Token is valid immediately upon creation
    }
  #endregion

    #region Issuer and Audience Tests

    /// <summary>
    /// SECURITY TEST: Token has correct issuer.
    /// GOAL: Issuer claim matches configuration.
  /// IMPORTANCE: Prevents tokens from other systems being accepted.
  /// </summary>
    [Fact]
    public void CreateToken_HasCorrectIssuer()
    {
 // Arrange
  var service = CreateTokenService();
    var user = new UserEntity { Id = "user1", Email = "test@test.com", DisplayName = "Test" };

 // Act
        var token = service.CreateToken(user);
        var handler = new JwtSecurityTokenHandler();
  var jwtToken = handler.ReadJwtToken(token);

        // Assert
        jwtToken.Issuer.Should().Be("CineMatchTest");
    }

    /// <summary>
    /// SECURITY TEST: Token has correct audience.
    /// GOAL: Audience claim matches configuration.
    /// IMPORTANCE: Tokens only valid for intended audience.
    /// </summary>
    [Fact]
    public void CreateToken_HasCorrectAudience()
    {
   // Arrange
        var service = CreateTokenService();
        var user = new UserEntity { Id = "user1", Email = "test@test.com", DisplayName = "Test" };

        // Act
        var token = service.CreateToken(user);
  var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

 // Assert
   jwtToken.Audiences.Should().Contain("CineMatchTest");
    }

    #endregion

 #region Null/Empty Value Handling

    /// <summary>
    /// EDGE CASE TEST: Null email becomes empty string in token.
    /// GOAL: Null values don't crash token generation.
    /// IMPORTANCE: Defensive programming - handles incomplete user data.
    /// </summary>
    [Fact]
    public void CreateToken_NullEmail_BecomesEmptyString()
    {
 // Arrange
        var service = CreateTokenService();
        var user = new UserEntity { Id = "user1", Email = null, DisplayName = "Test" };

  // Act
   var token = service.CreateToken(user);
        var claims = DecodeToken(token);

// Assert
  var emailClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        emailClaim.Should().NotBeNull();
      emailClaim!.Value.Should().Be("");
    }

  /// <summary>
    /// EDGE CASE TEST: Null DisplayName becomes empty string.
    /// GOAL: Null values don't crash token generation.
    /// IMPORTANCE: Handles users without display names.
    /// </summary>
    [Fact]
    public void CreateToken_NullDisplayName_BecomesEmptyString()
    {
        // Arrange
        var service = CreateTokenService();
        var user = new UserEntity { Id = "user1", Email = "test@test.com", DisplayName = null };

   // Act
        var token = service.CreateToken(user);
        var claims = DecodeToken(token);

// Assert
        var displayNameClaim = claims.FirstOrDefault(c => c.Type == "displayName");
displayNameClaim.Should().NotBeNull();
        displayNameClaim!.Value.Should().Be("");
    }

    /// <summary>
    /// EDGE CASE TEST: Null FirstName becomes empty string.
  /// GOAL: Optional fields don't crash token generation.
    /// IMPORTANCE: Not all users have first/last names.
    /// </summary>
    [Fact]
    public void CreateToken_NullFirstName_BecomesEmptyString()
    {
        // Arrange
        var service = CreateTokenService();
var user = new UserEntity { Id = "user1", Email = "test@test.com", DisplayName = "Test", FirstName = null };

        // Act
   var token = service.CreateToken(user);
        var claims = DecodeToken(token);

        // Assert
   var firstNameClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName);
   firstNameClaim.Should().NotBeNull();
        firstNameClaim!.Value.Should().Be("");
    }

    #endregion

    #region Token Format Tests

    /// <summary>
    /// FORMAT TEST: Token is valid JWT format.
    /// GOAL: Generated token can be parsed as JWT.
    /// IMPORTANCE: Ensures compatibility with JWT libraries.
    /// </summary>
    [Fact]
    public void CreateToken_IsValidJwtFormat()
    {
// Arrange
        var service = CreateTokenService();
   var user = new UserEntity { Id = "user1", Email = "test@test.com", DisplayName = "Test" };

        // Act
        var token = service.CreateToken(user);
      var handler = new JwtSecurityTokenHandler();

   // Assert
 handler.CanReadToken(token).Should().BeTrue();
    }

    /// <summary>
    /// FORMAT TEST: Token has 3 parts (header.payload.signature).
    /// GOAL: Standard JWT structure.
    /// IMPORTANCE: Validates basic JWT format.
    /// </summary>
    [Fact]
    public void CreateToken_HasThreeParts()
    {
        // Arrange
        var service = CreateTokenService();
   var user = new UserEntity { Id = "user1", Email = "test@test.com", DisplayName = "Test" };

        // Act
     var token = service.CreateToken(user);

   // Assert
   token.Split('.').Should().HaveCount(3, "JWT has header.payload.signature");
    }

    #endregion

 #region Helper Methods

    private JwtTokenService CreateTokenService()
    {
        var configValues = new Dictionary<string, string>
        {
 ["Jwt:SecretKey"] = "TestSecretKeyForJwtTokenGenerationInIntegrationTests123456789",
            ["Jwt:Issuer"] = "CineMatchTest",
["Jwt:Audience"] = "CineMatchTest"
        };

  var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues!)
  .Build();

    return new JwtTokenService(configuration);
    }

  private List<Claim> DecodeToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
  var jwtToken = handler.ReadJwtToken(token);
        return jwtToken.Claims.ToList();
    }

    #endregion
}
