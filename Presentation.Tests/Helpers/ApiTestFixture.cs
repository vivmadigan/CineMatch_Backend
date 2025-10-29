using Infrastructure.Data.Context;
using Infrastructure.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Presentation.Tests.Helpers;

/// <summary>
/// WebApplicationFactory fixture for integration tests.
/// Replaces SQL Server with SQLite in-memory and provides authenticated HttpClient helpers.
/// </summary>
public class ApiTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Test BEFORE services are configured
        builder.UseEnvironment("Test");

        // Add test configuration with JWT settings
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "TestSecretKeyForJwtTokenGenerationInIntegrationTests123456789",
                ["Jwt:Issuer"] = "CineMatchTest",
                ["Jwt:Audience"] = "CineMatchTest",
                ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:", // Not used but might be referenced
                ["TMDB:ApiKey"] = "test-api-key",
                ["TMDB:BaseUrl"] = "https://api.themoviedb.org/3",
                ["TMDB:ImageBase"] = "https://image.tmdb.org/t/p/",
                ["TMDB:DefaultLanguage"] = "en-US",
                ["TMDB:DefaultRegion"] = "US"
            });
        });

        builder.ConfigureServices(services =>
         {
             // Find and remove SQL Server DbContext registration
             var dbContextDescriptor = services.SingleOrDefault(
d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

             if (dbContextDescriptor != null)
             {
                 services.Remove(dbContextDescriptor);
             }

             // Also remove ApplicationDbContext registration
             var contextDescriptor = services.SingleOrDefault(
                  d => d.ServiceType == typeof(ApplicationDbContext));

             if (contextDescriptor != null)
             {
                 services.Remove(contextDescriptor);
             }

             // Create SQLite in-memory connection that stays open
             _connection = new SqliteConnection("DataSource=:memory:");
             _connection.Open();

             // Add SQLite DbContext
             services.AddDbContext<ApplicationDbContext>((sp, options) =>
             {
                 options.UseSqlite(_connection);
             });
         });
    }

    public async Task InitializeAsync()
    {
        // Ensure schema is created after server starts
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Create an HttpClient with JWT authentication.
    /// Registers a new user and sets the Authorization header.
    /// </summary>
    public async Task<(HttpClient Client, string UserId, string Token)> CreateAuthenticatedClientAsync(
        string? email = null,
string? displayName = null)
    {
        var client = CreateClient();

        var uniqueId = Guid.NewGuid().ToString()[..8];
        var signupDto = new SignUpDto
        {
            Email = email ?? $"test{uniqueId}@test.com",
            Password = "TestPass123!",
            DisplayName = displayName ?? $"User{uniqueId}",
            FirstName = "Test",
            LastName = "User"
        };

        var response = await client.PostAsJsonAsync("/api/signup", signupDto);
        response.EnsureSuccessStatusCode();

        var authData = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        if (authData == null || string.IsNullOrEmpty(authData.Token))
        {
            throw new InvalidOperationException("Failed to get JWT token from signup");
        }

        client.DefaultRequestHeaders.Authorization =
     new AuthenticationHeaderValue("Bearer", authData.Token);

        return (client, authData.UserId, authData.Token);
    }

    /// <summary>
    /// Create multiple authenticated clients with different users.
    /// </summary>
    public async Task<List<(HttpClient Client, string UserId, string Token)>> CreateAuthenticatedClientsAsync(int count)
    {
        var clients = new List<(HttpClient, string, string)>();
        for (int i = 0; i < count; i++)
        {
            clients.Add(await CreateAuthenticatedClientAsync());
        }
        return clients;
    }

    public new async Task DisposeAsync()
    {
        _connection?.Close();
        _connection?.Dispose();
        await base.DisposeAsync();
    }
}

/// <summary>
/// Collection definition for sharing fixture across test classes.
/// </summary>
[CollectionDefinition(nameof(ApiTestCollection))]
public class ApiTestCollection : ICollectionFixture<ApiTestFixture>
{
}
