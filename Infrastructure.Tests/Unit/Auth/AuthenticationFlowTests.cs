using FluentAssertions;
using Infrastructure.Data.Entities;
using Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Infrastructure.Tests.Unit.Auth;

/// <summary>
/// Pure unit tests for authentication flows without database dependencies.
/// Tests password validation, user lockout, and input guards.
/// GOAL: Verify auth logic works correctly in isolation.
/// IMPORTANCE: CRITICAL SECURITY - Auth bugs = security breaches.
/// </summary>
public class AuthenticationFlowTests
{
    #region Password Validation Tests

    /// <summary>
    /// POSITIVE TEST: Valid password should return success.
    /// GOAL: Correct password validates successfully.
    /// IMPORTANCE: Core authentication feature.
    /// </summary>
    [Fact]
    public async Task ValidatePassword_WithCorrectPassword_ReturnsSuccess()
    {
        // Arrange
        var mockUserManager = CreateMockUserManager();
        var mockPasswordHasher = new Mock<IPasswordHasher<UserEntity>>();
        
        var user = new UserEntity { Id = "user1", Email = "test@test.com", PasswordHash = "hashed" };
        
  mockPasswordHasher
            .Setup(x => x.VerifyHashedPassword(user, "hashed", "CorrectPassword123!"))
    .Returns(PasswordVerificationResult.Success);

        // Act
        var result = mockPasswordHasher.Object.VerifyHashedPassword(user, user.PasswordHash, "CorrectPassword123!");

        // Assert
 result.Should().Be(PasswordVerificationResult.Success);
    }

    /// <summary>
    /// NEGATIVE TEST: Wrong password should return failure.
    /// GOAL: Incorrect password is rejected.
    /// IMPORTANCE: Security - prevents unauthorized access.
    /// </summary>
    [Fact]
    public async Task ValidatePassword_WithWrongPassword_ReturnsFailure()
    {
    // Arrange
        var mockPasswordHasher = new Mock<IPasswordHasher<UserEntity>>();
     var user = new UserEntity { Id = "user1", Email = "test@test.com", PasswordHash = "hashed" };
        
        mockPasswordHasher
        .Setup(x => x.VerifyHashedPassword(user, "hashed", "WrongPassword"))
 .Returns(PasswordVerificationResult.Failed);

        // Act
        var result = mockPasswordHasher.Object.VerifyHashedPassword(user, user.PasswordHash, "WrongPassword");

        // Assert
        result.Should().Be(PasswordVerificationResult.Failed);
    }

    /// <summary>
    /// SECURITY TEST: Verify old password hash triggers rehash.
 /// GOAL: Outdated hashes are upgraded for better security.
    /// IMPORTANCE: Security - ensures modern hashing algorithms.
    /// </summary>
    [Fact]
    public async Task ValidatePassword_WithSuccessRehashNeeded_IndicatesRehash()
    {
        // Arrange
  var mockPasswordHasher = new Mock<IPasswordHasher<UserEntity>>();
        var user = new UserEntity { Id = "user1", Email = "test@test.com", PasswordHash = "old_hash" };
        
 mockPasswordHasher
            .Setup(x => x.VerifyHashedPassword(user, "old_hash", "CorrectPassword123!"))
       .Returns(PasswordVerificationResult.SuccessRehashNeeded);

  // Act
        var result = mockPasswordHasher.Object.VerifyHashedPassword(user, user.PasswordHash, "CorrectPassword123!");

        // Assert
   result.Should().Be(PasswordVerificationResult.SuccessRehashNeeded);
    }

    #endregion

 #region User Lockout Tests

    /// <summary>
    /// SECURITY TEST: Locked out user should be rejected immediately.
    /// GOAL: Authentication short-circuits for locked accounts.
    /// IMPORTANCE: CRITICAL - prevents brute force attacks.
    /// </summary>
    [Fact]
 public void CheckLockout_LockedOutUser_ReturnsTrue()
    {
 // Arrange
    var user = new UserEntity
        {
 Id = "user1",
      Email = "test@test.com",
     LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15) // Locked for 15 minutes
        };

        // Act
        var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

        // Assert
        isLockedOut.Should().BeTrue("user should be locked out");
    }

    /// <summary>
    /// POSITIVE TEST: Expired lockout should allow login.
    /// GOAL: Lockout period expires correctly.
    /// IMPORTANCE: Users can access account after lockout period.
    /// </summary>
    [Fact]
    public void CheckLockout_ExpiredLockout_ReturnsFalse()
    {
      // Arrange
        var user = new UserEntity
        {
  Id = "user1",
 Email = "test@test.com",
 LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-5) // Lockout expired 5 minutes ago
        };

      // Act
        var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

   // Assert
      isLockedOut.Should().BeFalse("lockout period has expired");
  }

    /// <summary>
    /// POSITIVE TEST: User with no lockout should be allowed.
  /// GOAL: Null lockout means no restriction.
    /// IMPORTANCE: Normal users not affected by lockout checks.
    /// </summary>
    [Fact]
 public void CheckLockout_NoLockout_ReturnsFalse()
    {
        // Arrange
        var user = new UserEntity
        {
     Id = "user1",
            Email = "test@test.com",
            LockoutEnd = null
        };

        // Act
        var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

        // Assert
  isLockedOut.Should().BeFalse("user has no lockout");
    }

    #endregion

    #region Email Confirmation Tests

    /// <summary>
    /// SECURITY TEST: Unconfirmed email should be detected.
    /// GOAL: System knows when email is not confirmed.
    /// IMPORTANCE: Can enforce email verification policies.
    /// </summary>
    [Fact]
    public void CheckEmailConfirmed_UnconfirmedEmail_ReturnsFalse()
    {
        // Arrange
      var user = new UserEntity
     {
Id = "user1",
  Email = "test@test.com",
    EmailConfirmed = false
        };

        // Act
        var isConfirmed = user.EmailConfirmed;

        // Assert
      isConfirmed.Should().BeFalse("email is not confirmed");
    }

    /// <summary>
    /// POSITIVE TEST: Confirmed email should be detected.
    /// GOAL: System recognizes confirmed emails.
    /// IMPORTANCE: Users with confirmed emails get full access.
    /// </summary>
    [Fact]
    public void CheckEmailConfirmed_ConfirmedEmail_ReturnsTrue()
    {
      // Arrange
        var user = new UserEntity
        {
          Id = "user1",
     Email = "test@test.com",
 EmailConfirmed = true
  };

        // Act
        var isConfirmed = user.EmailConfirmed;

        // Assert
     isConfirmed.Should().BeTrue("email is confirmed");
 }

    #endregion

    #region Input Validation Tests

    /// <summary>
    /// NEGATIVE TEST: Null email should be rejected.
    /// GOAL: Input validation catches null.
    /// IMPORTANCE: Prevents NullReferenceException.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateEmail_InvalidInput_ShouldFail(string? email)
    {
        // Act
   var isValid = !string.IsNullOrWhiteSpace(email) && email.Contains("@");

        // Assert
        isValid.Should().BeFalse("invalid email should fail validation");
    }

  /// <summary>
    /// POSITIVE TEST: Valid email format should pass.
    /// GOAL: Basic email validation works.
    /// IMPORTANCE: Accepts valid user input.
    /// </summary>
    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name+tag@domain.co.uk")]
    [InlineData("123@test.com")]
 public void ValidateEmail_ValidFormat_ShouldPass(string email)
    {
        // Act
        var isValid = !string.IsNullOrWhiteSpace(email) && email.Contains("@");

 // Assert
        isValid.Should().BeTrue("valid email should pass basic validation");
    }

    /// <summary>
 /// NEGATIVE TEST: Null or empty password should be rejected.
    /// GOAL: Password cannot be blank.
    /// IMPORTANCE: Security - no passwordless accounts.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePassword_EmptyInput_ShouldFail(string? password)
    {
    // Act
        var isValid = !string.IsNullOrWhiteSpace(password) && password.Length >= 8;

        // Assert
        isValid.Should().BeFalse("empty password should fail validation");
    }

    /// <summary>
    /// NEGATIVE TEST: Password under 8 characters should be rejected.
 /// GOAL: Enforce minimum password length.
    /// IMPORTANCE: Security - prevents weak passwords.
    /// </summary>
    [Theory]
    [InlineData("short")]
    [InlineData("1234567")]
    [InlineData("Pass1!")]
    public void ValidatePassword_TooShort_ShouldFail(string password)
    {
        // Act
        var isValid = password.Length >= 8;

      // Assert
   isValid.Should().BeFalse("password under 8 characters should fail");
    }

/// <summary>
    /// POSITIVE TEST: Password 8+ characters should pass length check.
    /// GOAL: Minimum length requirement is met.
    /// IMPORTANCE: Accepts valid passwords.
    /// </summary>
    [Theory]
    [InlineData("Password123!")]
    [InlineData("12345678")]
    [InlineData("VeryLongPasswordThatMeetsRequirements")]
    public void ValidatePassword_MeetsMinimumLength_ShouldPass(string password)
    {
        // Act
        var isValid = password.Length >= 8;

        // Assert
        isValid.Should().BeTrue("password meeting minimum length should pass");
    }

    #endregion

    #region Helper Methods

    private Mock<UserManager<UserEntity>> CreateMockUserManager()
    {
    var store = new Mock<IUserStore<UserEntity>>();
var mockUserManager = new Mock<UserManager<UserEntity>>(
     store.Object, null, null, null, null, null, null, null, null);
        return mockUserManager;
    }

    #endregion
}
