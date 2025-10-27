using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure.Data.Context;
using Infrastructure.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Helpers;

/// <summary>
/// SQLite in-memory database fixture for unit tests.
/// Provides a realistic EF Core context without needing SQL Server.
/// </summary>
public static class DbFixture
{
    /// <summary>
    /// Create a new SQLite in-memory ApplicationDbContext with schema created.
    /// Connection stays open for the lifetime of the context.
    /// </summary>
    public static ApplicationDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }

    /// <summary>
    /// Create a test user and save to the database.
    /// Returns the created user entity.
    /// </summary>
    public static async Task<UserEntity> CreateTestUserAsync(
        ApplicationDbContext db,
        string? email = null,
        string? displayName = null,
        string? firstName = null,
        string? lastName = null)
    {
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = email ?? $"test{uniqueId}@test.com",
            UserName = email ?? $"test{uniqueId}@test.com",
            NormalizedEmail = (email ?? $"test{uniqueId}@test.com").ToUpperInvariant(),
            NormalizedUserName = (email ?? $"test{uniqueId}@test.com").ToUpperInvariant(),
            DisplayName = displayName ?? $"User{uniqueId}",
            FirstName = firstName ?? "Test",
            LastName = lastName ?? "User",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Create multiple test users in batch.
    /// </summary>
    public static async Task<List<UserEntity>> CreateTestUsersAsync(
        ApplicationDbContext db,
        int count)
    {
        var users = new List<UserEntity>();
        for (int i = 0; i < count; i++)
        {
            users.Add(await CreateTestUserAsync(db));
        }
        return users;
    }
}