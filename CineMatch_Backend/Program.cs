using Infrastructure.Data.Context;
using Infrastructure.Data.Entities;
using Infrastructure.Services;
using Infrastructure.Services.Matches;
using Infrastructure.Services.Chat;
using Infrastructure.External; 
using Infrastructure.Options;
using Presentation.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Register DbContext with SQL Server (but allow tests to override)
if (builder.Environment.EnvironmentName != "Test")
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services
  .AddIdentityCore<UserEntity>(o =>
  {
      o.User.RequireUniqueEmail = true;
      o.Password.RequiredLength = 8;
  })
  .AddRoles<IdentityRole>()
  .AddEntityFrameworkStores<ApplicationDbContext>()
  .AddSignInManager();

// JWT Bearer authentication (supports both HTTP headers and WebSocket query string)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var cfg = builder.Configuration;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = cfg["Jwt:Issuer"],
            ValidAudience = cfg["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(cfg["Jwt:SecretKey"]!)),
            ClockSkew = TimeSpan.FromMinutes(2) // optional
        };

        // Allow JWT from query string for SignalR WebSocket connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
// Why a *typed HttpClient* (IHttpClientFactory + ITmdbClient/TmdbClient)?
// - Centralized config per external API: BaseAddress/Timeout/headers live in one place.
// - Safe lifetime management: IHttpClientFactory avoids socket exhaustion & auto-refreshes DNS.
// - Strong typing + DI: you inject ITmdbClient, not raw HttpClient; cleaner controllers & easy mocking in tests.
// - Extensible: per-client policies (retry, circuit breaker) can be added later without touching call sites.
// - Separation of concerns: TmdbClient owns HTTP details; the rest of the app just calls methods.


// Bind the "TMDB" config section into TmdbOptions; DI exposes it as IOptions<TmdbOptions>.
builder.Services.Configure<TmdbOptions>(builder.Configuration.GetSection("TMDB"));

// Register a *typed* HttpClient for the TMDB client.
// IHttpClientFactory will construct an HttpClient with these settings whenever ITmdbClient is requested.
builder.Services.AddHttpClient<ITmdbClient, TmdbClient>((sp, client) =>
{
    // Pull strongly-typed options (merged from appsettings + user-secrets + env).
    var opt = sp.GetRequiredService<IOptions<TmdbOptions>>().Value;

    // Ensure trailing slash so the "/3/" segment remains when we append relative paths (no leading slash later).
    var baseUrl = (opt.BaseUrl ?? "https://api.themoviedb.org/3").TrimEnd('/') + "/";
    client.BaseAddress = new Uri(baseUrl);

    // Sensible per-API timeout; faster failure beats hanging requests.
    client.Timeout = TimeSpan.FromSeconds(8);

    // Optional: if you switch to TMDB v4 bearer auth later.
    // if (!string.IsNullOrWhiteSpace(opt.ReadAccessToken))
    //     client.DefaultRequestHeaders.Authorization =
    //         new AuthenticationHeaderValue("Bearer", opt.ReadAccessToken);
});

// Why: Adds a process-local cache you can use anywhere via DI.
// Good for single-instance or dev. For multi-instance, later swap to Redis (IDistributedCache).
builder.Services.AddMemoryCache();

builder.Services.AddAuthorization();

// CORS policies
builder.Services.AddCors(options =>
{
    // Development policy for frontend at localhost:5173 (Vite default)
    // Supports both regular HTTP requests and SignalR WebSocket connections
    options.AddPolicy("frontend-dev", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
          .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
          .WithHeaders()
           .WithExposedHeaders("*");
        // Note: No AllowCredentials() - we use bearer tokens in Authorization header or query string
    });
});

// Dependency Injection
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IPreferenceService, PreferenceService>();
builder.Services.AddScoped<IUserLikesService, UserLikesService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IChatService, ChatService>();

// SignalR for real-time chat
builder.Services.AddSignalR();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "CineMatch", Version = "v1" });

    // Include XML comments for parameter descriptions in Swagger UI
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste only your JWT (no 'Bearer ' prefix).",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(x => x.SwaggerEndpoint("/swagger/v1/swagger.json", "CineMatch v1"));

// Redirect root URL "/" to "/swagger"
// "^$" is a regular expression that matches an empty path (i.e., exactly the site root).
app.UseRewriter(new RewriteOptions().AddRedirect("^$", "swagger"));
app.UseHttpsRedirection();

// Apply CORS policies (before authentication/authorization)
app.UseCors("frontend-dev");

// Authentication BEFORE Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");

app.Run();

// Make Program class accessible to WebApplicationFactory for integration tests
public partial class Program { }
