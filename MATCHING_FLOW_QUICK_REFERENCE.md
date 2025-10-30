# ?? Quick Reference: Current Matching Flow

## ? **Current Implementation (Manual Matching)**

### **Step 1: Like Movies** ??
```
User clicks ?? on movie
? Saved to database
? NO match requests created
? NO notifications sent
```

### **Step 2: View Candidates** ??
```
User clicks "Matches" in navbar
? Shows users who liked same movies
? Status: "none" (no requests yet)
? Shows "Match" button
```

### **Step 3: Send Match Request** ??
```
User clicks "Match" button
? Creates request: You ? Other User
? Other user gets notification
? Your button shows "Pending"
```

### **Step 4: Accept Match** ?
```
Other user clicks "Accept Match"
? Chat room created instantly!
? Both users get "It's a match! ??"
? Can chat immediately
```

---

## ?? **API Endpoints**

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/movies/{tmdbId}/like` | POST | Like a movie (**NO** auto-match) |
| `/api/matches/candidates` | GET | Get potential matches |
| `/api/matches/request` | POST | Send/accept match request |
| `/api/matches/decline` | POST | Decline incoming request |
| `/api/matches/active` | GET | Get chat rooms (matched users) |
| `/api/matches/status/{userId}` | GET | Check status with specific user |

---

## ?? **SignalR Events**

### **matchRequestReceived**
**When:** Someone clicks "Match" on you  
**Action:** Show toast with "View" button

### **mutualMatch**
**When:** Both users matched  
**Action:** Show "It's a match! ??" with "Open Chat" button

---

## ?? **Match Status Values**

| Status | Button | Meaning |
|--------|--------|---------|
| `none` | "Match" (green) | No requests sent |
| `pending_sent` | "Pending" (disabled) | You're waiting for response |
| `pending_received` | "Accept/Decline" | They want to match with you |
| `matched` | (not shown) | Appears in Active Matches instead |

---

## ?? **Key Points**

? **Manual Control:** Users must click "Match" button  
? **No Auto-Requests:** Liking movies doesn't create match requests  
? **Two-Way Approval:** Both users must accept  
? **Real-Time:** SignalR notifications for instant feedback  
? **Private:** Other users don't see your likes unless you match

---

**This is the current, working implementation!** ??
