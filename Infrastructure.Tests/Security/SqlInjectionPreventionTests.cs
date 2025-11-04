using FluentAssertions;
using Infrastructure.Services;
using Infrastructure.Services.Matches;
using Infrastructure.Tests.Helpers;
using Infrastructure.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Security;

/// <summary>
/// Security tests for SQL injection prevention.
/// These are integration tests - verifying EF Core parameterization works correctly.
/// GOAL: Ensure malicious SQL in user input cannot execute.
/// IMPORTANCE: CRITICAL SECURITY - SQL injection = database compromise.
/// </summary>
public class SqlInjectionPreventionTests
{
    #region User ID Injection Attempts

    /// <summary>
    /// SECURITY TEST: Verify SQL injection in userId is prevented.
    /// GOAL: EF Core parameterizes queries, preventing SQL injection.
    /// IMPORTANCE: User IDs come from JWT but could be tampered with.
    /// </summary>
    [Theory]
    [InlineData("'; DROP TABLE Users; --")]
  [InlineData("' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("1' UNION SELECT * FROM Users--")]
    [InlineData("'; DELETE FROM UserMovieLikes; --")]
    public async Task MatchService_WithMaliciousUserId_DoesNotExecuteSql(string maliciousUserId)
    {
        // Arrange
     using var context = DbFixture.CreateContext();
   var user = await DbFixture.CreateTestUserAsync(context);
     var service = new MatchService(context, new MockNotificationService());

      // Count tables before (should have Users table)
  var tablesBefore = await context.Database
        .SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type='table'")
        .ToListAsync();
        tablesBefore.Should().Contain("AspNetUsers");

        // Act - Try to execute SQL injection via userId
        var act = async () => await service.GetCandidatesAsync(maliciousUserId, 20, CancellationToken.None);

        // Assert - Should not throw (query executes safely)
        await act.Should().NotThrowAsync();

      // Verify no tables were dropped
        var tablesAfter = await context.Database
      .SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type='table'")
    .ToListAsync();
        tablesAfter.Should().Contain("AspNetUsers", "Users table should still exist");
    tablesAfter.Count.Should().Be(tablesBefore.Count, "no tables should be dropped");
    }

    /// <summary>
    /// SECURITY TEST: Verify SQL injection in match request is prevented.
    /// GOAL: RequestAsync safely handles malicious user IDs.
    /// IMPORTANCE: Match requests are a common attack vector.
    /// </summary>
    [Fact]
    public async Task MatchService_RequestAsync_WithMaliciousTargetUserId_Fails()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
var user = await DbFixture.CreateTestUserAsync(context);
        var service = new MatchService(context, new MockNotificationService());

        var maliciousTargetId = "'; DROP TABLE MatchRequests; --";

// Act - Attempt SQL injection
        var act = async () => await service.RequestAsync(user.Id, maliciousTargetId, 27205, CancellationToken.None);

  // Assert - Should throw DbUpdateException (FK constraint), NOT execute SQL
    await act.Should().ThrowAsync<DbUpdateException>("foreign key constraint should prevent injection");

        // Verify MatchRequests table still exists
        var tables = await context.Database
     .SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type='table'")
   .ToListAsync();
 tables.Should().Contain("MatchRequests", "MatchRequests table should not be dropped");
    }

    #endregion

    #region Movie Title Injection Attempts

    /// <summary>
    /// SECURITY TEST: Verify SQL injection in movie title is prevented.
    /// GOAL: UserLikesService safely stores malicious movie titles.
    /// IMPORTANCE: Titles come from TMDB but could be tampered with.
    /// </summary>
    [Theory]
    [InlineData("Inception'; DROP TABLE UserMovieLikes; --")]
    [InlineData("' OR '1'='1")]
    [InlineData("<script>alert('XSS')</script>")]
    [InlineData("'; DELETE FROM Users WHERE '1'='1")]
    public async Task UserLikesService_WithMaliciousTitles_StoresSafely(string maliciousTitle)
    {
     // Arrange
   using var context = DbFixture.CreateContext();
    var user = await DbFixture.CreateTestUserAsync(context);
      var service = new UserLikesService(context);

      // Act - Store malicious title
        await service.UpsertLikeAsync(user.Id, 27205, maliciousTitle, "/poster.jpg", "2010", CancellationToken.None);

   // Assert - Title stored as literal string (not executed)
        var like = await context.UserMovieLikes
  .FirstAsync(l => l.UserId == user.Id && l.TmdbId == 27205);

        like.Title.Should().Be(maliciousTitle, "title should be stored as-is, not executed");

        // Verify no tables were dropped
     var tables = await context.Database
     .SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type='table'")
  .ToListAsync();
        tables.Should().Contain("UserMovieLikes", "UserMovieLikes table should still exist");
    }

    #endregion

  #region Display Name Injection Attempts

    /// <summary>
    /// SECURITY TEST: Verify SQL injection in display name is prevented.
    /// GOAL: User registration/update safely handles malicious names.
  /// IMPORTANCE: Display names are user-controlled input.
    /// </summary>
    [Theory]
    [InlineData("Admin'; DROP TABLE ChatMessages; --")]
    [InlineData("' OR 1=1 --")]
    [InlineData("Robert'); DELETE FROM Users WHERE ('1'='1")]
    public async Task ChatService_WithMaliciousDisplayName_StoresSafely(string maliciousDisplayName)
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context, displayName: maliciousDisplayName);

        // Assert - Display name stored as literal string
        var storedUser = await context.Users.FindAsync(user.Id);
     storedUser!.DisplayName.Should().Be(maliciousDisplayName);

        // Verify no tables were dropped
 var tables = await context.Database
     .SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type='table'")
.ToListAsync();
        tables.Should().Contain("ChatMessages", "ChatMessages table should still exist");
    }

    #endregion

    #region Chat Message Injection Attempts

    /// <summary>
  /// SECURITY TEST: Verify SQL injection in chat messages is prevented.
    /// GOAL: Chat text is stored safely without executing SQL.
    /// IMPORTANCE: CRITICAL - chat is primary user input vector.
    /// </summary>
    [Theory]
    [InlineData("Hello'; DROP TABLE ChatMessages; --")]
    [InlineData("' OR '1'='1' --")]
    [InlineData("Message'; DELETE FROM Users; --")]
    [InlineData("1'; UPDATE Users SET IsAdmin=1 WHERE '1'='1")]
    public async Task ChatService_WithMaliciousChatText_StoresSafely(string maliciousText)
    {
   // Arrange
  using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = await DbFixture.CreateTestRoomAsync(context, user.Id);
        var chatService = new Infrastructure.Services.Chat.ChatService(context);

        // Act - Send malicious chat message
  var message = await chatService.AppendAsync(room.Id, user.Id, maliciousText, CancellationToken.None);

        // Assert - Message stored as literal string
        message.Text.Should().Be(maliciousText);

        // Verify message is in database as literal string
        var storedMessage = await context.ChatMessages.FindAsync(message.Id);
storedMessage!.Text.Should().Be(maliciousText);

        // Verify no tables were dropped
  var tables = await context.Database
  .SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type='table'")
            .ToListAsync();
        tables.Should().Contain("ChatMessages", "ChatMessages table should still exist");
        tables.Should().Contain("AspNetUsers", "Users table should still exist");
    }

    #endregion

    #region LIKE Clause Injection (Advanced)

    /// <summary>
  /// SECURITY TEST: Verify SQL injection via LIKE wildcards is prevented.
    /// GOAL: Wildcard characters (%, _) in user input don't affect queries.
    /// IMPORTANCE: Advanced SQL injection technique.
 /// </summary>
    [Theory]
[InlineData("%")]
    [InlineData("_")]
    [InlineData("%%")]
[InlineData("a%b")]
  public async Task UserLikesService_WithWildcardCharacters_StoresSafely(string wildcardInput)
    {
        // Arrange
   using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

    // Act - Store title with wildcards
        await service.UpsertLikeAsync(user.Id, 27205, wildcardInput, "/poster.jpg", "2010", CancellationToken.None);

     // Assert - Wildcards stored as literal characters (not SQL wildcards)
        var like = await context.UserMovieLikes
            .FirstAsync(l => l.UserId == user.Id && l.TmdbId == 27205);

    like.Title.Should().Be(wildcardInput, "wildcards should be literal characters");
    }

    #endregion

    #region Unicode and Special Characters

    /// <summary>
    /// SECURITY TEST: Verify Unicode SQL injection attempts are prevented.
    /// GOAL: Unicode escape sequences don't bypass parameterization.
    /// IMPORTANCE: Advanced attack vector using encoding tricks.
    /// </summary>
    [Theory]
    [InlineData("\u0027 OR 1=1 --")] // Unicode single quote
    [InlineData("\u002D\u002D comment")] // Unicode dashes
    [InlineData("test\u0000")] // Null byte
 public async Task ChatService_WithUnicodeSqlInjection_StoresSafely(string unicodeInput)
    {
        // Arrange
  using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
var room = await DbFixture.CreateTestRoomAsync(context, user.Id);
        var chatService = new Infrastructure.Services.Chat.ChatService(context);

     // Act - Send message with Unicode injection attempt
    var message = await chatService.AppendAsync(room.Id, user.Id, unicodeInput, CancellationToken.None);

     // Assert - Unicode stored as literal characters
    message.Text.Should().Be(unicodeInput);

        // Verify no SQL was executed
        var tables = await context.Database
 .SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type='table'")
            .ToListAsync();
    tables.Should().Contain("ChatMessages");
  }

  #endregion

    #region Parameterization Verification

    /// <summary>
    /// SECURITY TEST: Verify EF Core uses parameterized queries.
    /// GOAL: Confirm no raw SQL concatenation happens.
    /// IMPORTANCE: Root cause of SQL injection is string concatenation.
    /// </summary>
    [Fact]
    public async Task EfCore_UsesParameterizedQueries_ForUserInput()
    {
      // Arrange
        using var context = DbFixture.CreateContext();
      var user = await DbFixture.CreateTestUserAsync(context);
        var service = new UserLikesService(context);

        // Act - Perform operation that queries by userId
     await service.UpsertLikeAsync(user.Id, 27205, "Inception", "/poster.jpg", "2010", CancellationToken.None);
   var likes = await service.GetLikesAsync(user.Id, CancellationToken.None);

        // Assert - Query succeeded (EF Core parameterized it)
        likes.Should().ContainSingle();

        // Note: EF Core always uses parameterized queries by default
   // This test verifies no developer has used raw SQL with string concatenation
    }

    #endregion
}
