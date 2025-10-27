# CineMatch Backend API

**A movie discovery and social matching platform powered by TMDB (The Movie Database)**

CineMatch helps users discover movies tailored to their preferences and connect with others who share similar tastes. Built with ASP.NET Core 9, Entity Framework Core, and integrates with the TMDB API.

---

## ?? Table of Contents

- [Project Overview](#project-overview)
- [Application Evolution](#application-evolution)
- [Features](#features)
- [Architecture](#architecture)
- [API Endpoints](#api-endpoints)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Technology Stack](#technology-stack)

---

## ?? Project Overview

### Purpose

CineMatch is a **movie recommendation and social matching API** that:

1. **Personalizes movie discovery** based on user preferences (genres, runtime length)
2. **Tracks user favorites** for quick reference and matching
3. **Connects users** with similar movie tastes for social interaction
4. **Integrates with TMDB** to provide rich movie metadata and filtering

### Target Users

- Movie enthusiasts looking for personalized recommendations
- Users seeking friends or partners with compatible movie preferences
- Frontend applications needing a comprehensive movie discovery API

---

## ?? Application Evolution

### Initial State (Before Enhancements)

The original CineMatch backend provided basic functionality:

#### **What Existed:**
- ? User authentication (JWT-based)
- ? User registration and sign-in
- ? Basic TMDB integration (fetching popular movies)
- ? User preference storage (genres and runtime preferences)
- ? Movie "like" tracking (save favorites)
- ? Identity management with ASP.NET Core Identity
- ? SQL Server database with Entity Framework Core

#### **What Was Missing:**
- ? **Filtered movie discovery** - No way to search movies by preferences
- ? **Social matching** - No feature to find users with similar tastes
- ? **Comprehensive API documentation** - Limited Swagger documentation
- ? **Frontend integration support** - No CORS configuration for development
- ? **Preference-based recommendations** - Preferences stored but not used

#### **Pain Points:**
1. Users could save preferences but couldn't discover movies based on them
2. No way to find other users with overlapping movie interests
3. API parameters lacked clear documentation and examples
4. Frontend developers couldn't easily test against localhost API
5. The `/test` endpoint was the only way to get movies (just 5 popular ones)

---

### Current State (After Enhancements)

The enhanced CineMatch backend is now a **production-ready MVP** with:

#### **?? New Feature: Filtered Movie Discovery**

**What It Does:**
- Discovers movies filtered by **genre** AND **runtime length**
- Falls back to user's saved preferences automatically
- Supports pagination and batch size control
- Integrates directly with TMDB's powerful discover endpoint

**Why It Matters:**
- Users get personalized recommendations without manual filtering
- Preferences are actually used for discovery (not just stored)
- Supports both explicit filters and saved preferences in one endpoint

**Technical Implementation:**
- New `ITmdbClient.DiscoverAsync()` method with genre and runtime filters
- `GET /api/movies/discover` endpoint with smart preference fallback
- Query parameters: `genres`, `length`, `page`, `batchSize`, `language`, `region`
- Runtime mapping: "short" (0-99min), "medium" (100-140min), "long" (141+min)

**Example Usage:**
```http
# Use saved preferences
GET /api/movies/discover

# Override with explicit filters
GET /api/movies/discover?genres=28,35&length=medium&batchSize=10
```

---

#### **?? New Feature: Social Matching**

**What It Does:**
- Finds users who have liked the same movies
- Ranks candidates by overlap count and recency
- Returns shared movie IDs for conversation starters
- Supports customizable result limits

**Why It Matters:**
- Enables social features (friend suggestions, dating matches)
- Provides quantifiable compatibility scores
- Shows specific movies in common for icebreakers

**Technical Implementation:**
- New `IMatchService` with efficient EF Core queries
- Groups likes by user, calculates overlap, joins with user profiles
- `GET /api/matches/candidates?take=20` endpoint
- Returns: `userId`, `displayName`, `overlapCount`, `sharedMovieIds`

**Example Response:**
```json
[
  {
    "userId": "8bd1e3b8-8f30-4a9f-9b0e-8a8c6e2c0d71",
    "displayName": "Casey",
    "overlapCount": 3,
    "sharedMovieIds": [27205, 238, 603]
  }
]
```

---

#### **?? New Feature: Enhanced API Documentation**

**What It Does:**
- XML documentation comments integrated into Swagger UI
- Parameter descriptions with real-world examples
- Clear explanations of what each endpoint does

**Why It Matters:**
- Frontend developers understand API without reading source code
- Parameter examples reduce integration errors
- Self-documenting API reduces support burden

**Technical Implementation:**
- Enabled `GenerateDocumentationFile` in `.csproj`
- Configured Swagger to include XML comments
- Added detailed `<summary>` and `<param>` tags to all endpoints

**Example Documentation:**
```csharp
/// <param name="tmdbId">TMDB movie ID (e.g., 27205 for "The Shawshank Redemption")</param>
```

---

#### **?? New Feature: Development CORS Support**

**What It Does:**
- Allows frontend at `http://localhost:5173` to call API in development
- Supports both HTTP and HTTPS origins for SignalR WebSocket connections
- Permits GET, POST, PUT, DELETE, OPTIONS methods with Authorization header
- Named policy for clear configuration

**Why It Matters:**
- Frontend developers can test locally without CORS errors
- SignalR WebSocket connections work seamlessly
- Secure: Only enabled in Development environment
- Production uses existing global CORS policy

**Technical Implementation:**
- Named CORS policy "DevClient" for development
- Environment-aware configuration
- Development: Specific origins/methods/headers for localhost:5173
- Production: AllowAnyOrigin (existing behavior)
- Applied before SignalR hub mapping for proper middleware order

---

#### **?? New Feature: Match Handshake (Mutual Matching)**

**What It Does:**
- Implements mutual matching system for creating connections
- When two users both request each other on the same movie, a chat room is automatically created
- Idempotent requests prevent duplicate match requests
- Clean data model - reciprocal requests deleted after room creation

**Why It Matters:**
- Enables social connections based on shared movie interests
- Automatic chat room creation streamlines user experience
- Prevents one-sided matches - both users must agree
- Foundation for real-time messaging between matched users

**Technical Implementation:**
- New database tables: `MatchRequests`, `ChatRooms`, `ChatMemberships`
- `POST /api/matches/request` endpoint for match requests
- Reciprocal matching algorithm checks for existing opposite request
- Atomic transaction ensures room + memberships created together
- Returns `matched: true/false` with optional `roomId`

**Example Flow:**
```
User A ? User B on "Inception"
Response: { "matched": false }

User B ? User A on "Inception"  
Response: { "matched": true, "roomId": "c5a5a0a4-..." }
? Chat room created automatically!
```

---

#### **?? New Feature: Real-Time Chat (SignalR)**

**What It Does:**
- WebSocket-based real-time messaging system
- Users can send/receive messages instantly in chat rooms
- Message history with pagination support
- Room management (join, leave, reactivate)
- Persistent message storage in database

**Why It Matters:**
- Instant communication between matched users
- Enables social interaction based on shared movie interests
- Scalable SignalR groups for efficient message routing
- Message history for context and continuity

**Technical Implementation:**
- SignalR hub at `/chathub` with WebSocket support
- JWT authentication via query string for WebSocket connections
- Database table: `ChatMessages` with composite indexes
- HTTP endpoints for chat history and management
- Automatic membership reactivation when users rejoin rooms
- Message length validation (max 2000 characters)

**Hub Methods (SignalR):**
- `JoinRoom(roomId)` - Subscribe to room messages
- `SendMessage(roomId, text)` - Send message to room
- `LeaveRoom(roomId)` - Unsubscribe from room
- Client receives: `"ReceiveMessage"` event

**HTTP Endpoints:**
- `GET /api/chats` - List user's chat rooms with previews
- `GET /api/chats/{roomId}/messages` - Paginated message history
- `POST /api/chats/{roomId}/leave` - Leave room (soft delete)

**Example Message:**
```json
{
  "id": "a1b2c3d4-...",
  "roomId": "c5a5a0a4-...",
  "senderId": "user-id",
  "senderDisplayName": "Casey",
  "text": "Want to watch Inception on Friday?",
  "sentAt": "2025-01-27T12:00:00Z"
}
```

---

## ? Features

### ?? Movie Discovery

| Feature | Endpoint | Description |
|---------|----------|-------------|
| **Filtered Discovery** | `GET /api/movies/discover` | Find movies by genre + runtime with preference fallback |
| **Test Endpoint** | `GET /api/movies/test` | Get 5 popular movies (MVP demo) |
| **Options** | `GET /api/movies/options` | Get available genres and length buckets |

### ?? User Preferences & Likes

| Feature | Endpoint | Description |
|---------|----------|-------------|
| **Save Preferences** | `POST /api/preferences` | Save genre IDs and runtime preferences |
| **Get Preferences** | `GET /api/preferences` | Retrieve current user's preferences |
| **Like Movie** | `POST /api/movies/{tmdbId}/like` | Add movie to favorites (idempotent) |
| **Unlike Movie** | `DELETE /api/movies/{tmdbId}/like` | Remove movie from favorites |
| **Get Likes** | `GET /api/movies/likes` | List all liked movies (most recent first) |

### ?? Social Matching

| Feature | Endpoint | Description |
|---------|----------|-------------|
| **Find Matches** | `GET /api/matches/candidates` | Get users with overlapping movie likes |
| **Request Match** | `POST /api/matches/request` | Request to match with user on specific movie |

### ?? Real-Time Chat

| Feature | Endpoint | Description |
|---------|----------|-------------|
| **List Rooms** | `GET /api/chats` | Get all chat rooms with last message preview |
| **Get Messages** | `GET /api/chats/{roomId}/messages` | Paginated message history for room |
| **Leave Room** | `POST /api/chats/{roomId}/leave` | Leave chat room (mark inactive) |
| **SignalR Hub** | `wss://localhost:7119/chathub` | WebSocket connection for real-time messaging |

### ?? Authentication

| Feature | Endpoint | Description |
|---------|----------|-------------|
| **Sign Up** | `POST /api/signup` | Create new user account |
| **Sign In** | `POST /api/signin` | Authenticate and receive JWT token |

---

## ??? Architecture

### Project Structure

```
CineMatch_Backend/
??? CineMatch_Backend/      # Presentation Layer (API)
?   ??? Controllers/
?   ?   ??? MoviesController.cs     # Movie discovery & likes
?   ?   ??? MatchesController.cs    # Social matching & requests
?   ?   ??? ChatsController.cs      # Chat room management
?   ?   ??? PreferencesController.cs# User preferences
?   ?   ??? SignUpController.cs     # Registration
?   ?   ??? SignInController.cs  # Authentication
?   ??? Hubs/
?   ?   ??? ChatHub.cs    # SignalR real-time messaging
?   ??? Program.cs     # Startup & DI configuration
?   ??? Presentation.csproj
?
??? Infrastructure/         # Business Logic & Data Access
    ??? Services/
    ?   ??? Matches/
    ?   ?   ??? IMatchService.cs    # Match finding & requests
    ?   ?   ??? MatchService.cs     # Match logic implementation
    ?   ??? Chat/
    ?   ?   ??? IChatService.cs     # Chat operations interface
    ?   ?   ??? ChatService.cs      # Chat logic implementation
    ?   ??? IPreferenceService.cs   # Preference management
    ?   ??? PreferenceService.cs
    ?   ??? IUserLikesService.cs    # Like tracking
    ?   ??? UserLikesService.cs
    ?   ??? ITokenService.cs   # JWT generation
    ?   ??? JwtTokenService.cs
    ??? External/
    ?   ??? ITmdbClient.cs          # TMDB API interface
    ? ??? TmdbClient.cs           # TMDB integration
    ?   ??? TmdbDiscoverResponse.cs
    ?   ??? TmdbGenreResponse.cs
    ??? Models/
    ?   ??? Matches/
    ?   ?   ??? CandidateDto.cs  # Match candidate response
    ?   ?   ??? RequestMatchDto.cs  # Match request body
    ?   ?   ??? MatchResultDto.cs   # Match result response
    ?   ??? Chat/
    ?   ?   ??? ChatMessageDto.cs       # Chat message response
    ?   ?   ??? ChatRoomListItemDto.cs  # Chat room list item
    ?   ??? MovieSummaryDto.cs      # Movie card data
  ?   ??? ...
    ??? Data/
    ?   ??? Context/
    ?   ?   ??? ApplicationDbContext.cs # EF Core DbContext
    ?   ??? Entities/
    ?  ??? UserEntity.cs        # ASP.NET Identity user
    ?       ??? UserPreference.cs    # Genre/length prefs
    ?    ??? UserMovieLike.cs  # Liked movies
    ?       ??? MatchRequest.cs      # Match requests
    ?       ??? ChatRoom.cs          # Chat rooms
    ?       ??? ChatMembership.cs    # Room memberships
    ?       ??? ChatMessage.cs       # Chat messages
    ??? Infrastructure.csproj
```

### Design Principles

1. **Clean Architecture**: Controllers are thin; services contain business logic
2. **Dependency Injection**: All services registered in `Program.cs`
3. **Typed HttpClient**: `ITmdbClient` encapsulates TMDB API integration
4. **Options Pattern**: Configuration via `IOptions<TmdbOptions>`
5. **Repository Pattern (via EF)**: Services abstract data access
6. **DTO Pattern**: Clear API contracts separate from entities

---

## ?? API Endpoints

### Authentication Required

All endpoints except `/api/signup` and `/api/signin` require a JWT Bearer token.

**Include in header:**
```
Authorization: Bearer {your-jwt-token}
```

### Movies

#### `GET /api/movies/discover`

Discover movies filtered by user preferences or explicit parameters.

**Query Parameters:**
- `genres` (optional): Comma-separated TMDB genre IDs (e.g., "28,35" for Action+Comedy)
- `length` (optional): "short", "medium", or "long"
- `page` (optional): Page number (default: 1)
- `batchSize` (optional): Number of movies to return (default: 5)
- `language` (optional): Language code (default: "en-US")
- `region` (optional): Region code (default: "US")

**Behavior:**
- If `genres` or `length` provided ? uses explicit filters
- If not provided ? loads user's saved preferences
- If no preferences ? defaults to "medium" length, no genre filter

**Response:** Array of `MovieSummaryDto`

```json
[
  {
    "id": 27205,
    "title": "Inception",
    "oneLiner": "A thief who enters the dreams of others to steal secrets from their subconscious...",
    "runtimeMinutes": null,
    "posterUrl": "https://image.tmdb.org/t/p/w342/abc.jpg",
    "backdropUrl": "https://image.tmdb.org/t/p/w780/xyz.jpg",
    "genreIds": [28, 878, 53],
    "releaseYear": "2010",
    "rating": 8.4,
    "tmdbUrl": "https://www.themoviedb.org/movie/27205"
  }
]
```

---

#### `GET /api/movies/test`

Get 5 popular movies (MVP test endpoint).

**Query Parameters:**
- `page` (optional): Page number (default: 1)
- `language` (optional): Language code
- `region` (optional): Region code

**Response:** Array of `MovieSummaryDto` (same structure as discover)

---

#### `GET /api/movies/options`

Get available UI options (genre list and length buckets).

**Query Parameters:**
- `language` (optional): Language code for genre names

**Response:**
```json
{
  "lengths": [
    { "key": "short", "label": "Short (<100 min)", "min": null, "max": 99 },
    { "key": "medium", "label": "Medium (100–140)", "min": 100, "max": 140 },
    { "key": "long", "label": "Long (>140 min)", "min": 141, "max": null }
  ],
  "genres": [
    { "id": 28, "name": "Action" },
    { "id": 35, "name": "Comedy" },
    { "id": 18, "name": "Drama" }
  ]
}
```

---

#### `GET /api/movies/likes`

Get current user's liked movies (most recent first).

**Response:**
```json
[
  {
    "tmdbId": 27205,
    "title": "Inception",
 "posterUrl": "https://image.tmdb.org/t/p/w342/abc.jpg",
    "releaseYear": "2010",
    "likedAt": "2024-01-15T14:30:00Z"
  }
]
```

---

#### `POST /api/movies/{tmdbId}/like`

Like a movie (idempotent upsert).

**Path Parameter:**
- `tmdbId`: TMDB movie ID (e.g., 27205)

**Request Body:**
```json
{
  "title": "Inception",
  "posterPath": "/abc.jpg",
  "releaseYear": "2010"
}
```

**Response:** `204 No Content`

---

#### `DELETE /api/movies/{tmdbId}/like`

Unlike a movie (idempotent).

**Path Parameter:**
- `tmdbId`: TMDB movie ID

**Response:** `204 No Content`

---

### Matches

#### `GET /api/matches/candidates`

Find users with overlapping movie likes.

**Query Parameters:**
- `take` (optional): Max candidates to return (default: 20)

**Response:**
```json
[
  {
    "userId": "8bd1e3b8-8f30-4a9f-9b0e-8a8c6e2c0d71",
    "displayName": "Casey",
    "overlapCount": 3,
    "sharedMovieIds": [27205, 238, 603]
  }
]
```

**Sorting:** By `overlapCount` DESC, then most recent like timestamp DESC

---

#### `POST /api/matches/request`

Request a match with another user for a specific movie.

**Request Body:**
```json
{
  "targetUserId": "8bd1e3b8-8f30-4a9f-9b0e-8a8c6e2c0d71",
  "tmdbId": 27205
}
```

**Response (No Mutual Match):**
```json
{
  "matched": false,
  "roomId": null
}
```

**Response (Mutual Match - Room Created):**
```json
{
  "matched": true,
  "roomId": "c5a5a0a4-5e2d-4a6a-9b7b-7c6d3d1e2f90"
}
```

**Behavior:**
- If reciprocal request exists (target also requested you), creates chat room
- Otherwise, saves request and awaits reciprocal
- Idempotent: duplicate requests return same result

**Status Codes:**
- `200 OK` - Request processed (check `matched` field)
- `400 Bad Request` - Invalid request or self-match attempt
- `401 Unauthorized` - Missing/invalid JWT

---

### Chats

#### `GET /api/chats`

List all chat rooms for the current user.

**Response:**
```json
[
  {
    "roomId": "c5a5a0a4-5e2d-4a6a-9b7b-7c6d3d1e2f90",
  "otherUserId": "8bd1e3b8-8f30-4a9f-9b0e-8a8c6e2c0d71",
    "otherDisplayName": "Casey",
    "tmdbId": 27205,
    "lastText": "See you then!",
    "lastAt": "2025-01-27T12:34:56Z"
  }
]
```

**Sorting:** By `lastAt` DESC (most recent activity first)

**Note:** `tmdbId` indicates the movie that matched the users. May be `null` for legacy rooms created before this feature.

---

#### `GET /api/chats/{roomId}/messages`

Get paginated message history for a chat room.

**Query Parameters:**
- `take` (optional): Number of messages (default: 50, max: 100)
- `beforeUtc` (optional): Get messages before this timestamp (pagination cursor)

**Response:**
```json
[
  {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "roomId": "c5a5a0a4-5e2d-4a6a-9b7b-7c6d3d1e2f90",
    "senderId": "user-a-id",
    "senderDisplayName": "Alex",
    "text": "Hey! Want to watch on Friday?",
    "sentAt": "2025-01-27T12:00:00Z"
  },
  {
  "id": "e5f6g7h8-i9j0-1234-5678-90abcdef1234",
    "roomId": "c5a5a0a4-5e2d-4a6a-9b7b-7c6d3d1e2f90",
 "senderId": "user-b-id",
    "senderDisplayName": "Casey",
    "text": "Sure! 7pm works?",
    "sentAt": "2025-01-27T12:05:00Z"
  }
]
```

**Sorting:** By `sentAt` DESC (newest first)

**Status Codes:**
- `200 OK` - Messages retrieved
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - User not a member of room

---

#### `POST /api/chats/{roomId}/leave`

Leave a chat room (mark membership as inactive).

**Response:** `204 No Content`

**Behavior:**
- Sets `IsActive = false`, `LeftAt = now` for membership
- User can rejoin later by calling SignalR `JoinRoom()` (reactivates membership)
- Room persists even if both users leave

**Status Codes:**
- `204 No Content` - Successfully left
- `401 Unauthorized` - Not authenticated
- `404 Not Found` - User not a member of room

---

### SignalR Hub (`/chathub`)

Real-time WebSocket connection for instant messaging.

**Connection URL:**
```
wss://localhost:7119/chathub?access_token={jwt-token}
```

**Hub Methods (Client ? Server):**

##### `JoinRoom(roomId)`
Subscribe to room messages. Automatically reactivates inactive membership.

```javascript
await connection.invoke("JoinRoom", "c5a5a0a4-5e2d-4a6a-9b7b-7c6d3d1e2f90");
```

##### `SendMessage(roomId, text)`
Send a message to the room. Broadcast to all connected members.

```javascript
await connection.invoke("SendMessage", 
  "c5a5a0a4-5e2d-4a6a-9b7b-7c6d3d1e2f90",
  "Hey! Want to watch on Friday?"
);
```

**Validation:**
- Text length max 2000 characters
- User must be active member

##### `LeaveRoom(roomId)`
Unsubscribe from room (disconnect only, doesn't change database).

```javascript
await connection.invoke("LeaveRoom", "c5a5a0a4-5e2d-4a6a-9b7b-7c6d3d1e2f90");
```

**Client Events (Server ? Client):**

##### `ReceiveMessage`
Broadcast when any user sends a message.

```javascript
connection.on("ReceiveMessage", (message) => {
  console.log(message);
  // {
  //   "id": "...",
  //   "roomId": "...",
  //   "senderId": "...",
  //   "senderDisplayName": "Casey",
  //   "text": "Sure! 7pm works?",
  //   "sentAt": "2025-01-27T12:00:00Z"
  // }
});
```

**Example Frontend Integration:**
```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/chathub", { 
 accessTokenFactory: () => localStorage.getItem("jwt") 
  })
  .build();

await connection.start();
await connection.invoke("JoinRoom", roomId);

connection.on("ReceiveMessage", (msg) => {
  appendMessageToUI(msg);
});
```

---

### Preferences

#### `GET /api/preferences`

Get current user's saved preferences.

**Response:**
```json
{
  "genreIds": [28, 35, 878],
  "length": "medium"
}
```

---

#### `POST /api/preferences`

Save user preferences.

**Request Body:**
```json
{
  "genreIds": [28, 35, 878],
  "length": "medium"
}
```

**Validation:**
- `length` must be: "short", "medium", or "long"
- `genreIds` must be valid TMDB genre IDs

**Response:** `204 No Content`

---

### Authentication

#### `POST /api/signup`

Create a new user account.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "displayName": "Casey",
  "firstName": "Casey",
  "lastName": "Smith"
}
```

**Response:**
```json
{
  "token": "eyJhbGc...",
  "userId": "8bd1e3b8-8f30-4a9f-9b0e-8a8c6e2c0d71",
  "email": "user@example.com",
  "displayName": "Casey"
}
```

---

#### `POST /api/signin`

Authenticate and receive JWT token.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!"
}
```

**Response:** Same as signup

---

## ?? Getting Started

### Prerequisites

- **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **SQL Server** (LocalDB or full instance)
- **TMDB API Key** - [Get one free](https://www.themoviedb.org/settings/api)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/vivmadigan/CineMatch_Backend.git
   cd CineMatch_Backend
   ```

2. **Set up user secrets**
   ```bash
   cd CineMatch_Backend
   dotnet user-secrets init
   dotnet user-secrets set "TMDB:ApiKey" "your-tmdb-api-key-here"
   dotnet user-secrets set "Jwt:SecretKey" "your-secure-jwt-secret-key-at-least-32-chars"
   dotnet user-secrets set "Jwt:Issuer" "CineMatch"
   dotnet user-secrets set "Jwt:Audience" "CineMatch"
   ```

3. **Update database connection string**
   
   Edit `CineMatch_Back_End/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CineMatchDb;Trusted_Connection=true;TrustServerCertificate=true"
     }
   }
   ```

4. **Apply database migrations**
   ```bash
   cd CineMatch_Backend
   dotnet ef database update
   ```

5. **Run the application**
   ```bash
   dotnet run
   ```

6. **Access Swagger UI**

 Navigate to: `https://localhost:7119/swagger`

---

## ?? Configuration

### TMDB Options

Configure in `appsettings.json` or user secrets:

```json
{
  "TMDB": {
    "BaseUrl": "https://api.themoviedb.org/3",
    "ImageBase": "https://image.tmdb.org/t/p/",
    "ApiKey": "YOUR_API_KEY",
    "DefaultLanguage": "en-US",
    "DefaultRegion": "US"
  }
}
```

### JWT Configuration

**Required secrets:**
- `Jwt:SecretKey` - At least 32 characters
- `Jwt:Issuer` - Token issuer (e.g., "CineMatch")
- `Jwt:Audience` - Token audience (e.g., "CineMatch")

**Token Expiration:** 7 days (configurable in `JwtTokenService.cs`)

### CORS Configuration

**Development:**
- Origins: `http://localhost:5173`, `https://localhost:5173` (Vite default, HTTP + HTTPS)
- Methods: GET, POST, PUT, DELETE, OPTIONS
- Headers: Authorization, Content-Type
- Named Policy: "DevClient"
- SignalR Support: WebSocket upgrade allowed
- No credentials required (bearer tokens used)

**Production:**
- AllowAnyOrigin, AllowAnyMethod, AllowAnyHeader

**Note:** CORS applied before SignalR hub mapping for correct middleware ordering.

---

## ??? Technology Stack

### Backend Framework
- **ASP.NET Core 9.0** - Web API framework
- **C# 13** - Programming language
- **Entity Framework Core 9** - ORM for database access

### Real-Time Communication
- **SignalR** - WebSocket-based real-time messaging
- **WebSocket Protocol** - Bi-directional communication

### Database
- **SQL Server** - Relational database
- **ASP.NET Core Identity** - User authentication & management

### External APIs
- **TMDB API v3** - Movie metadata and discovery

### Authentication
- **JWT (JSON Web Tokens)** - Stateless authentication
- **Bearer Token Authentication** - Authorization scheme
- **WebSocket JWT** - Query string token for SignalR

### Documentation
- **Swagger/OpenAPI** - Interactive API documentation
- **XML Documentation Comments** - Parameter descriptions

### Dependency Injection
- **Built-in DI Container** - Service registration
- **Scoped Lifetime** - Per-request services (DbContext, services)
- **Typed HttpClient** - TMDB API integration

### Caching
- **IMemoryCache** - In-memory caching (genres, etc.)
- **24-hour TTL** - Genre cache expiration

---

## ?? Database Schema

### Tables

#### `AspNetUsers`
- Standard ASP.NET Identity user table
- Extended with: `DisplayName`, `FirstName`, `LastName`

#### `UserPreferences`
- **PK:** `UserId`
- **Columns:** `GenreIds` (JSON), `LengthKey`, `UpdatedAt`
- **Relationship:** 1:1 with `AspNetUsers`

#### `UserMovieLikes`
- **PK:** `(UserId, TmdbId)`
- **Columns:** `TmdbId`, `Liked`, `Title`, `PosterPath`, `ReleaseYear`, `CreatedAt`
- **Indexes:** 
  - `TmdbId` (find who liked a movie)
  - `(UserId, CreatedAt)` (user's recent likes)
- **Relationship:** Many:1 with `AspNetUsers`

#### `MatchRequests`
- **PK:** `Id` (GUID)
- **Columns:** `RequestorId`, `TargetUserId`, `TmdbId`, `CreatedAt`
- **Indexes:**
  - `(TargetUserId, RequestorId, TmdbId)` (reciprocal lookup)
- `RequestorId` (sent requests)
  - `TargetUserId` (received requests)
- **Relationships:** 
  - `RequestorId` ? `AspNetUsers` (cascade delete)
  - `TargetUserId` ? `AspNetUsers` (no action)

#### `ChatRooms`
- **PK:** `Id` (GUID)
- **Columns:** `CreatedAt`
- **Relationship:** One:Many with `ChatMemberships` and `ChatMessages`

#### `ChatMemberships`
- **PK:** `(RoomId, UserId)` (composite)
- **Columns:** `IsActive`, `JoinedAt`, `LeftAt`
- **Relationships:**
  - `RoomId` ? `ChatRooms` (cascade delete)
  - `UserId` ? `AspNetUsers` (cascade delete)

#### `ChatMessages`
- **PK:** `Id` (GUID)
- **Columns:** `RoomId`, `SenderId`, `Text` (max 2000), `SentAt`
- **Indexes:**
  - `(RoomId, SentAt DESC)` (chronological retrieval)
  - `SenderId` (user's messages)
- **Relationships:**
  - `RoomId` ? `ChatRooms` (cascade delete)
  - `SenderId` ? `AspNetUsers` (cascade delete)

---

## ?? Key Improvements Summary

| Area | Before | After | Impact |
|------|--------|-------|--------|
| **Movie Discovery** | Only 5 popular movies via `/test` | Filtered discover with preference fallback | Users get personalized recommendations |
| **Social Features** | None | Match candidates with overlap scores | Enables social connections |
| **Match Handshake** | No mutual matching | Automatic chat room on reciprocal match | Streamlined connection flow |
| **Real-Time Chat** | No messaging | SignalR WebSocket-based instant messaging | Live communication between matched users |
| **API Documentation** | Basic Swagger, no examples | XML comments with parameter examples | Better developer experience |
| **Frontend Integration** | CORS errors in development | Dev CORS with SignalR support | Smooth local development + WebSockets |
| **Preference Usage** | Stored but unused | Powers filtered discovery | Preferences become actionable |

---

## ?? Future Enhancements

### Potential Features
- **Movie details endpoint** - Get full metadata for a specific movie
- **Swipe/card interface support** - Binary yes/no on movies
- **Typing indicators** - Show when other user is typing
- **Read receipts** - Track message read status
- **Message reactions** - Emoji reactions to messages
- **File attachments** - Image/video sharing in chat
- **Push notifications** - Mobile/browser push for offline users
- **Group chats** - More than 2 users per room
- **Recommendation engine** - ML-based suggestions
- **Watch history** - Track movies already seen
- **Lists/Collections** - Watchlists, favorites categories
- **Social profiles** - Bio, photos, extended preferences

### Technical Improvements
- **Rate limiting** - Protect TMDB API quota and prevent spam
- **Redis backplane** - Scale SignalR across multiple servers
- **Redis caching** - Distributed cache for multi-instance deployments
- **Background jobs** - Periodic data updates (Hangfire/Quartz)
- **API versioning** - v1, v2 support
- **Health checks** - Monitoring endpoints
- **Logging** - Structured logging with Serilog
- **Unit tests** - Service layer test coverage
- **Integration tests** - End-to-end API tests
- **Message compression** - Reduce SignalR payload sizes

---

## ?? License

This project is part of an educational/portfolio demonstration.

---

## ?? Contributors

**Original Development:** Vivian Madigan  
**AI-Assisted Enhancements:** GitHub Copilot (January 2025)

---

## ?? Acknowledgments

- **TMDB (The Movie Database)** - Movie data and API
- **ASP.NET Core Team** - Framework and tools
- **Entity Framework Core** - ORM excellence

---

## ?? Support

For issues or questions:
1. Check the Swagger UI documentation (`/swagger`)
2. Review this README
3. Open an issue on GitHub

---

**Built with ?? and ?? by the CineMatch team**