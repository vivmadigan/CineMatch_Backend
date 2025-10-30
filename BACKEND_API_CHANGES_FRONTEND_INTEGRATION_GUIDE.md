# ?? **Backend API Changes - Frontend Integration Guide**

## ?? **Document Overview**

This document outlines **all backend changes** made during this session and provides comprehensive guidance for frontend integration. It covers three major feature implementations:

1. **Manual Two-Way Matching System**
2. **Match Status & Shared Movies Enhancement**
3. **Real-Time Match System (Phase 1 & 2)**

---

## ?? **Breaking Changes Summary**

### **?? Critical Changes:**

1. **Candidates endpoint now filters out matched users**
   - Matched users no longer appear in `GET /api/matches/candidates`
   - Use new `GET /api/matches/active` endpoint to see matched users

2. **Match requests are now one-way**
   - Liking a movie creates request: `A ? B` (not bidirectional)
   - Chat room only created when both users manually accept

3. **New SignalR events added**
   - `matchRequestReceived` - When someone sends you a match request
   - `mutualMatch` - When both users match (replaces old `NewMatch`)

---

## ?? **Part 1: Manual Two-Way Matching System**

### **What Changed:**

The matching system was converted from **instant bidirectional matching** to **manual two-way matching** where users must explicitly accept or decline match requests.

---

### **1.1 Match Request Creation (When Liking Movies)**

**Endpoint:** No change to `POST /api/movies/{tmdbId}/like`

**New Behavior:**
- When User A likes a movie that User B already liked:
  - ? Creates **ONE-WAY** request: `A ? B`
  - ? Does NOT create: `B ? A`
  - ? Does NOT create chat room automatically

**Example:**
```json
POST /api/movies/550/like
{
  "title": "Fight Club",
  "posterPath": "/pB8BM7pdSp6B6Ih7QZ4DrQ3PmJK.jpg",
  "releaseYear": "1999"
}

// Backend creates: A ? B match request
// User B will see User A in their candidates list
```

---

### **1.2 Accepting Match Requests**

**Endpoint:** `POST /api/matches/request`

**Request:**
```json
{
  "targetUserId": "user-guid-to-match-with",
  "tmdbId": 550
}
```

**Response (No Mutual Match Yet):**
```json
{
  "matched": false,
  "roomId": null
}
```

**Response (Mutual Match - Chat Room Created):**
```json
{
  "matched": true,
  "roomId": "room-guid-123"
}
```

**Frontend Logic:**
```typescript
const handleMatch = async (candidateUserId: string, tmdbId: number) => {
  const response = await fetch('/api/matches/request', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
body: JSON.stringify({
 targetUserId: candidateUserId,
      tmdbId: tmdbId
    })
  });
  
  const result = await response.json();
  
  if (result.matched) {
    // ? Mutual match! Chat room created
    toast.success("It's a match! ??");
    navigate(`/chat/${result.roomId}`);
  } else {
    // ? Request sent, waiting for other user
    toast.info("Match request sent!");
    // Update button to "Pending"
  }
};
```

---

### **1.3 Declining Match Requests (NEW)**

**Endpoint:** `POST /api/matches/decline` *(NEW)*

**Request:**
```json
{
  "targetUserId": "user-guid-who-sent-request",
"tmdbId": 550
}
```

**Response:** `204 No Content`

**Use Case:** User B declines User A's match request

**Frontend Logic:**
```typescript
const handleDecline = async (requestorUserId: string, tmdbId: number) => {
  await fetch('/api/matches/decline', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      targetUserId: requestorUserId,
      tmdbId: tmdbId
    })
  });
  
  toast.info("Match request declined");
  // Refresh candidates list (user will be removed)
  queryClient.invalidateQueries(['candidates']);
};
```

---

## ?? **Part 2: Match Status & Shared Movies Enhancement**

### **What Changed:**

The candidates API now provides **persistent match status** and **full movie details**, eliminating the need for frontend state management and additional API calls.

---

### **2.1 Updated CandidateDto Model**

**New Fields Added:**

| Field | Type | Description |
|-------|------|-------------|
| `sharedMovies` | `List<SharedMovieDto>` | Full movie details (title, poster, year) |
| `matchStatus` | `string` | Match status: `"none"`, `"pending_sent"`, `"pending_received"`, `"matched"` |
| `requestSentAt` | `DateTime?` | When current user sent request (null if no request) |

**Example Response:**
```json
GET /api/matches/candidates

[
  {
    "userId": "user-guid-123",
    "displayName": "Alex",
    "overlapCount": 3,
    "sharedMovieIds": [550, 27205, 157336],
    "sharedMovies": [
      {
    "tmdbId": 550,
        "title": "Fight Club",
        "posterUrl": "https://image.tmdb.org/t/p/w342/pB8BM7pdSp6B6Ih7QZ4DrQ3PmJK.jpg",
        "releaseYear": "1999"
      },
      {
      "tmdbId": 27205,
        "title": "Inception",
        "posterUrl": "https://image.tmdb.org/t/p/w342/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg",
 "releaseYear": "2010"
      }
    ],
    "matchStatus": "pending_sent",
    "requestSentAt": "2025-01-31T10:00:00Z"
  }
]
```

---

### **2.2 Match Status Values**

| Status | Meaning | Button State | Action Available |
|--------|---------|--------------|------------------|
| `"none"` | No requests exist | "Match" button enabled | Can send match request |
| `"pending_sent"` | Current user sent request | "Pending" button disabled | Waiting for response |
| `"pending_received"` | Candidate sent request | "Accept/Decline" buttons | Can accept or decline |
| `"matched"` | Chat room exists | "Matched!" button disabled | Should NOT appear in candidates |

**Frontend Button Logic:**
```typescript
const renderMatchButton = (candidate: CandidateDto) => {
  switch (candidate.matchStatus) {
    case 'none':
      return (
        <Button onClick={() => handleMatch(candidate.userId)}>
          Match
   </Button>
      );
    
    case 'pending_sent':
      return (
        <Button disabled>
          Pending {candidate.requestSentAt && `(${formatDistanceToNow(candidate.requestSentAt)})`}
        </Button>
  );
    
    case 'pending_received':
      return (
        <div className="flex gap-2">
          <Button onClick={() => handleMatch(candidate.userId)}>
         Accept Match
    </Button>
          <Button variant="outline" onClick={() => handleDecline(candidate.userId)}>
       Decline
</Button>
        </div>
      );
    
    case 'matched':
      // This should not appear in candidates list
      // But if it does, show:
      return <Button disabled>Matched!</Button>;
  }
};
```

---

### **2.3 Shared Movies Data**

**No Additional API Calls Needed:**

**Before (OLD):**
```typescript
// ? Had to fetch movie details separately
const movieDetails = await Promise.all(
  candidate.sharedMovieIds.map(id => 
    fetch(`https://api.themoviedb.org/3/movie/${id}`)
  )
);
```

**After (NEW):**
```typescript
// ? Movie details already included!
candidate.sharedMovies.forEach(movie => {
  <img src={movie.posterUrl} alt={movie.title} />
  <p>{movie.title} ({movie.releaseYear})</p>
});
```

---

## ?? **Part 3: Real-Time Match System (Phase 1 & 2)**

### **What Changed:**

Added real-time SignalR notifications, filtered candidates list, active matches endpoint, and match status checking.

---

### **3.1 Filtered Candidates List**

**?? BREAKING CHANGE:**

**Endpoint:** `GET /api/matches/candidates`

**New Behavior:**
- ? **No longer returns matched users** (users with chat rooms)
- ? Only returns potential matches (unmatched users)
- Matched users appear in `GET /api/matches/active` instead

**Migration:**
```typescript
// OLD: Candidates list showed everyone (including matched)
const candidates = await fetch('/api/matches/candidates');
// candidates included matched users with disabled buttons

// NEW: Candidates list only shows unmatched users
const candidates = await fetch('/api/matches/candidates');
// Only potential matches (matchStatus: "none", "pending_sent", "pending_received")

// To see matched users:
const activeMatches = await fetch('/api/matches/active');
// Returns users with chat rooms
```

---

### **3.2 Real-Time SignalR Events**

**Setup SignalR Connection:**
```typescript
import { HubConnectionBuilder } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
  .withUrl('/chathub', {
    accessTokenFactory: () => localStorage.getItem('token')
  })
  .withAutomaticReconnect()
  .build();

await connection.start();
```

---

### **3.2.1 Event: `matchRequestReceived`**

**When:** User A sends match request to User B

**Payload:**
```json
{
  "type": "matchRequestReceived",
  "matchId": "request-userA-userB",
  "user": {
    "id": "userA-guid",
    "displayName": "Alex"
  },
  "sharedMovieTitle": "Inception",
  "sharedMovieTmdbId": 27205,
  "sharedMoviesCount": 3,
  "timestamp": "2025-01-31T12:30:00Z",
  "message": "Alex wants to match with you!"
}
```

**Frontend Handler:**
```typescript
connection.on('matchRequestReceived', (notification) => {
  // Show toast notification
  toast.info(notification.message, {
    action: {
      label: 'View',
      onClick: () => navigate('/matches')
    }
});
  
  // Refresh candidates list (user will show "pending_received")
  queryClient.invalidateQueries(['candidates']);
  
  // Optional: Increment notification badge
  setNotificationCount(prev => prev + 1);
});
```

---

### **3.2.2 Event: `mutualMatch`**

**When:** Both users have matched (chat room created)

**Payload:**
```json
{
  "type": "mutualMatch",
  "matchId": "mutual-userA-userB",
  "roomId": "room-guid-123",
  "user": {
    "id": "userB-guid",
 "displayName": "Jordan"
  },
  "sharedMovieTitle": "Inception",
  "timestamp": "2025-01-31T12:30:00Z"
}
```

**Frontend Handler:**
```typescript
connection.on('mutualMatch', (notification) => {
  // Show "It's a match!" toast
  toast.success(`It's a match! ?? ${notification.user.displayName}`, {
    action: {
      label: 'Open Chat',
      onClick: () => navigate(`/chat/${notification.roomId}`)
    }
  });
  
  // Refresh candidates list (user will be removed)
  queryClient.invalidateQueries(['candidates']);
  
  // Refresh active matches (user will appear there)
  queryClient.invalidateQueries(['activeMatches']);
});
```

---

### **3.3 Active Matches Endpoint (NEW)**

**Endpoint:** `GET /api/matches/active` *(NEW)*

**Purpose:** Get all users you've matched with (have chat rooms)

**Response:**
```json
[
  {
    "userId": "user-guid-123",
    "displayName": "Alex",
    "roomId": "room-guid-456",
    "matchedAt": "2025-01-31T10:00:00Z",
    "lastMessageAt": "2025-01-31T12:30:00Z",
    "lastMessage": "Hey! Want to watch Inception tonight?",
    "unreadCount": 2,
    "sharedMovies": [
      {
        "tmdbId": 27205,
        "title": "Inception",
        "posterUrl": "https://...",
        "releaseYear": "2010"
  }
    ]
  }
]
```

**Frontend Usage:**
```typescript
const ActiveMatchesPage = () => {
  const { data: matches } = useQuery(['activeMatches'], () =>
    fetch('/api/matches/active').then(r => r.json())
  );
  
  return (
    <div>
      <h1>Active Matches</h1>
      {matches?.map(match => (
        <div key={match.userId} onClick={() => navigate(`/chat/${match.roomId}`)}>
          <img src={match.sharedMovies[0]?.posterUrl} alt="User" />
    <div>
            <h3>{match.displayName}</h3>
    <p>{match.lastMessage || 'Start a conversation'}</p>
     {match.unreadCount > 0 && (
  <Badge>{match.unreadCount}</Badge>
            )}
     </div>
   </div>
      ))}
    </div>
  );
};
```

---

### **3.4 Match Status Endpoint (NEW)**

**Endpoint:** `GET /api/matches/status/{userId}` *(NEW)*

**Purpose:** Check match status with a specific user (for profile pages)

**Response:**
```json
{
  "status": "pending_sent",
  "canMatch": false,
  "canDecline": false,
  "requestSentAt": "2025-01-31T10:00:00Z",
  "roomId": null,
  "sharedMovies": [
    {
      "tmdbId": 27205,
      "title": "Inception",
      "posterUrl": "https://...",
   "releaseYear": "2010"
    }
  ]
}
```

**Frontend Usage:**
```typescript
const UserProfilePage = ({ userId }: { userId: string }) => {
  const { data: status } = useQuery(['matchStatus', userId], () =>
    fetch(`/api/matches/status/${userId}`).then(r => r.json())
  );
  
  return (
    <div>
      <h2>User Profile</h2>
      <p>Shared movies: {status?.sharedMovies.length}</p>
      
      {status?.status === 'none' && status.canMatch && (
        <Button onClick={() => handleMatch(userId)}>Match</Button>
      )}
      
      {status?.status === 'pending_sent' && (
        <Button disabled>
          Pending {status.requestSentAt && `(${formatDistance(status.requestSentAt)})`}
        </Button>
      )}
      
      {status?.status === 'pending_received' && (
        <>
      <Button onClick={() => handleMatch(userId)}>Accept Match</Button>
     <Button onClick={() => handleDecline(userId)}>Decline</Button>
        </>
      )}
      
      {status?.status === 'matched' && (
    <Button onClick={() => navigate(`/chat/${status.roomId}`)}>
          Open Chat
        </Button>
      )}
    </div>
  );
};
```

---

## ??? **Complete User Flow (End-to-End)**

### **Scenario: User A and User B Match**

#### **Step 1: User A Likes Movie**
```
User A: POST /api/movies/550/like

Backend:
- Saves like to database
- Creates match request: A ? B
- No notification sent yet (User B will see in candidates list)
```

#### **Step 2: User A Views Candidates**
```
User A: GET /api/matches/candidates

Response:
[
  {
    "userId": "userB",
    "displayName": "Jordan",
    "matchStatus": "pending_sent",  // ? Shows request was sent
    "requestSentAt": "2025-01-31T10:00:00Z",
    "sharedMovies": [...]
  }
]

User A sees: "Pending (sent 2 hours ago)"
```

#### **Step 3: User B Views Candidates**
```
User B: GET /api/matches/candidates

Response:
[
  {
 "userId": "userA",
    "displayName": "Alex",
    "matchStatus": "pending_received",  // ? Shows incoming request
    "requestSentAt": null,
    "sharedMovies": [...]
  }
]

User B sees: "Accept Match" and "Decline" buttons
```

#### **Step 4: User B Accepts Match**
```
User B: POST /api/matches/request
{
  "targetUserId": "userA",
  "tmdbId": 550
}

Backend:
- Detects mutual interest (A ? B request exists)
- Creates chat room
- Removes both match requests
- Sends SignalR "mutualMatch" to BOTH users ?

Response:
{
  "matched": true,
  "roomId": "room-guid-123"
}
```

#### **Step 5: Both Users Get Real-Time Notification**
```
User A's screen:
- SignalR event received: "mutualMatch"
- Toast: "It's a match! ?? Jordan also liked Fight Club [Open Chat]"
- Candidates list updates (Jordan removed)
- Active matches updates (Jordan appears)

User B's screen:
- SignalR event received: "mutualMatch"
- Toast: "It's a match! ?? Alex also liked Fight Club [Open Chat]"
- Candidates list updates (Alex removed)
- Active matches updates (Alex appears)
```

#### **Step 6: User A Views Active Matches**
```
User A: GET /api/matches/active

Response:
[
  {
    "userId": "userB",
    "displayName": "Jordan",
    "roomId": "room-guid-123",
    "lastMessage": null,
    "unreadCount": 0,
    "sharedMovies": [...]
  }
]

User A clicks: Navigate to /chat/room-guid-123
```

---

## ?? **API Endpoints Summary**

### **Existing Endpoints (Modified Behavior):**

| Endpoint | Method | Changes |
|----------|--------|---------|
| `/api/movies/{tmdbId}/like` | POST | Now creates ONE-WAY match requests |
| `/api/matches/candidates` | GET | **BREAKING:** Filters out matched users, added `matchStatus` & `requestSentAt` |
| `/api/matches/request` | POST | Now handles manual match acceptance |

### **New Endpoints:**

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/matches/decline` | POST | Decline incoming match request |
| `/api/matches/active` | GET | Get all active matches (chat rooms) |
| `/api/matches/status/{userId}` | GET | Get match status with specific user |

### **New SignalR Events:**

| Event | When | Payload |
|-------|------|---------|
| `matchRequestReceived` | Someone sends you match request | User info, shared movie, count |
| `mutualMatch` | Both users matched | User info, **roomId**, shared movie |

---

## ? **Migration Checklist**

### **For Candidates List Page:**

- [ ] Update to handle 4 match statuses (`none`, `pending_sent`, `pending_received`, `matched`)
- [ ] Render appropriate buttons based on `matchStatus`
- [ ] Display `requestSentAt` timestamp for pending requests
- [ ] Use `sharedMovies` array (no additional API calls needed)
- [ ] Setup SignalR listener for `matchRequestReceived` event
- [ ] Setup SignalR listener for `mutualMatch` event
- [ ] Invalidate query cache on SignalR events

### **For Active Matches Page (NEW):**

- [ ] Create new page/component for active matches
- [ ] Fetch from `GET /api/matches/active`
- [ ] Display last message preview
- [ ] Display unread count badge
- [ ] Navigate to chat room on click

### **For User Profile Pages:**

- [ ] Add match button to profile pages
- [ ] Fetch match status from `GET /api/matches/status/{userId}`
- [ ] Render button based on `status` and `canMatch`/`canDecline` flags
- [ ] Handle accept/decline actions

### **For Match Requests:**

- [ ] Update `POST /api/matches/request` calls
- [ ] Handle response: `matched: true` ? navigate to chat
- [ ] Handle response: `matched: false` ? show "Pending"
- [ ] Add `POST /api/matches/decline` handler

### **For SignalR Integration:**

- [ ] Setup SignalR connection to `/chathub`
- [ ] Listen for `matchRequestReceived` event
- [ ] Listen for `mutualMatch` event
- [ ] Show toast notifications
- [ ] Invalidate React Query caches on events
- [ ] Handle reconnection scenarios

---

## ?? **Testing Checklist**

### **Test 1: One-Way Request Creation**
```
? User A likes Movie X
? Check candidates: User B appears with matchStatus: "none"
? User A clicks "Match" on User B
? Check candidates: User B now shows matchStatus: "pending_sent"
? User B's screen: SignalR toast "Alex wants to match!"
```

### **Test 2: Mutual Match**
```
? User B clicks "Accept" on User A
? Response: { matched: true, roomId: "..." }
? Both users see toast: "It's a match! ??"
? User A's candidates: User B removed
? User B's candidates: User A removed
? GET /api/matches/active: Both users appear
```

### **Test 3: Declined Match**
```
? User A sends request to User C
? User C clicks "Decline"
? User C's candidates: User A removed
? User A's candidates: User C shows matchStatus: "none" (request removed)
```

### **Test 4: Real-Time Updates**
```
? User A and B both on candidates page
? User A clicks "Match" on User B
? User B's screen updates without refresh (SignalR)
? User B clicks "Accept"
? User A's screen updates without refresh (SignalR)
? Both navigate to chat
```

---

## ?? **Common Issues & Solutions**

### **Issue 1: Matched users still appear in candidates**
**Cause:** Frontend not updated to filter or backend query failed  
**Solution:** 
- Backend already filters - check API response
- Refresh candidates list after `mutualMatch` event

### **Issue 2: SignalR events not received**
**Cause:** Connection not established or user not authenticated  
**Solution:**
```typescript
// Ensure token is provided
const connection = new HubConnectionBuilder()
  .withUrl('/chathub', {
    accessTokenFactory: () => localStorage.getItem('token')  // ? Add this
  })
  .build();

// Verify connection state
console.log(connection.state); // Should be "Connected"
```

### **Issue 3: Match button doesn't show correct state**
**Cause:** Not checking `matchStatus` field  
**Solution:** Use the `matchStatus` field, not custom state management

### **Issue 4: Movies missing poster images**
**Cause:** Using `sharedMovieIds` instead of `sharedMovies`  
**Solution:** Use `candidate.sharedMovies` which includes full URLs

---

## ?? **TypeScript Types Reference**

```typescript
// CandidateDto
interface Candidate {
  userId: string;
  displayName: string;
  overlapCount: number;
  sharedMovieIds: number[];
  sharedMovies: SharedMovie[];
  matchStatus: 'none' | 'pending_sent' | 'pending_received' | 'matched';
  requestSentAt: string | null;
}

// SharedMovieDto
interface SharedMovie {
  tmdbId: number;
  title: string;
  posterUrl: string;
  releaseYear: string | null;
}

// ActiveMatchDto
interface ActiveMatch {
  userId: string;
  displayName: string;
  roomId: string;
  matchedAt: string;
  lastMessageAt: string | null;
  lastMessage: string | null;
  unreadCount: number;
  sharedMovies: SharedMovie[];
}

// MatchStatusDto
interface MatchStatus {
  status: 'none' | 'pending_sent' | 'pending_received' | 'matched';
  canMatch: boolean;
  canDecline: boolean;
  requestSentAt: string | null;
  roomId: string | null;
  sharedMovies: SharedMovie[];
}

// MatchResultDto
interface MatchResult {
  matched: boolean;
  roomId: string | null;
}

// SignalR Event Payloads
interface MatchRequestReceivedEvent {
  type: 'matchRequestReceived';
  matchId: string;
  user: {
    id: string;
    displayName: string;
  };
  sharedMovieTitle: string;
  sharedMovieTmdbId: number;
  sharedMoviesCount: number;
  timestamp: string;
  message: string;
}

interface MutualMatchEvent {
  type: 'mutualMatch';
  matchId: string;
  roomId: string;
  user: {
    id: string;
    displayName: string;
  };
  sharedMovieTitle: string;
  timestamp: string;
}
```

---

## ?? **Summary**

### **What Frontend Needs to Do:**

1. ? Update candidates list to handle 4 match statuses
2. ? Setup SignalR connection and event listeners
3. ? Create Active Matches page using new endpoint
4. ? Add match buttons to profile pages using status endpoint
5. ? Handle decline action using new endpoint
6. ? Use `sharedMovies` array (no more TMDB API calls)
7. ? Invalidate caches on SignalR events

### **What Backend Provides:**

- ? Persistent match status (no frontend state needed)
- ? Real-time notifications (no polling needed)
- ? Full movie details (no additional API calls)
- ? Filtered candidates (only potential matches)
- ? Active matches with previews
- ? Match status checking for profiles

---

**All backend changes are production-ready and tested!** ??

---

**Last Updated:** January 31, 2025  
**Document Version:** 1.0  
**Status:** ? Complete
