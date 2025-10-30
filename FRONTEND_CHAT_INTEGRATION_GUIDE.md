# Frontend Chat Integration Guide

## ?? Overview
The backend chat system is **100% complete** with real-time SignalR support, JWT authentication, and full CRUD operations for messages. This guide provides everything you need to integrate the chat functionality into the React frontend.

---

## ?? Backend Configuration

| Setting | Value |
|---------|-------|
| **Base URL** | `https://localhost:7119` |
| **SignalR Hub** | `wss://localhost:7119/chathub` |
| **JWT Storage** | `access_token` (localStorage) |
| **CORS** | Already configured for `localhost:5173` and `localhost:5174` |
| **Auth Method** | Bearer token (HTTP) + Query string (WebSocket) |

---

## ?? Implementation Checklist

### 1. Environment Configuration

Update your `.env` or `.env.local` file:

```env
VITE_API_MODE=live
VITE_API_BASE_URL=https://localhost:7119
```

---

### 2. SignalR Service Setup

**File:** `src/lib/services/SignalRChatService.ts`

#### Connection Configuration

```typescript
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

class SignalRChatService {
  private connection: HubConnection | null = null;
  
  async connect(): Promise<void> {
    const token = localStorage.getItem('access_token');
    if (!token) {
      throw new Error('No access token found');
    }
    
 this.connection = new HubConnectionBuilder()
      .withUrl(`${import.meta.env.VITE_API_BASE_URL}/chathub`, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();
    
    await this.connection.start();
    console.log('[SignalR] Connected to ChatHub');
  }
  
  async disconnect(): Promise<void> {
    if (this.connection) {
    await this.connection.stop();
      console.log('[SignalR] Disconnected from ChatHub');
    }
  }
  
  // Hub methods
  async joinRoom(roomId: string): Promise<void> {
    if (!this.connection) throw new Error('Not connected');
    await this.connection.invoke('JoinRoom', roomId);
  }
  
  async leaveRoom(roomId: string): Promise<void> {
    if (!this.connection) throw new Error('Not connected');
    await this.connection.invoke('LeaveRoom', roomId);
  }
  
  async sendMessage(roomId: string, content: string): Promise<void> {
    if (!this.connection) throw new Error('Not connected');
    await this.connection.invoke('SendMessage', roomId, content);
  }
  
  // Event listeners
  onMutualMatch(callback: (data: MutualMatchEvent) => void): void {
    if (!this.connection) throw new Error('Not connected');
    this.connection.on('mutualMatch', callback);
  }
  
  onReceiveMessage(callback: (message: BackendMessage) => void): void {
    if (!this.connection) throw new Error('Not connected');
 this.connection.on('ReceiveMessage', callback);
  }
}

export const signalRChatService = new SignalRChatService();
```

#### Event Type Definitions

```typescript
// Backend event: mutualMatch
export interface MutualMatchEvent {
  type: 'mutualMatch';
  matchId: string;
  roomId: string;  // ?? Navigate to /chat/{roomId}
  user: {
    id: string;
    displayName: string;
  };
  sharedMovieTitle: string;
  timestamp: string;  // ISO 8601 format
}

// Backend event: ReceiveMessage
export interface BackendMessage {
  id: string;
  roomId: string;
  senderId: string;
  senderDisplayName: string;  // ?? NOT senderName
  text: string;         // ?? NOT content
  sentAt: string;  // ?? NOT timestamp (ISO 8601 string)
}
```

---

### 3. Chat API Service

**File:** `src/lib/services/ChatService.ts`

#### API Endpoints

```typescript
import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
});

// Add JWT token to all requests
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('access_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export class ChatService {
  // List user's conversations
  async listConversations(userId: string): Promise<Conversation[]> {
    const response = await api.get<BackendConversation[]>('/api/Chats');
    return response.data.map(mapRoomToConversation);
  }
  
  // Get room metadata (when joining)
  async getRoomMetadata(roomId: string): Promise<RoomMetadata> {
    const response = await api.get<BackendConversation>(`/api/Chats/${roomId}`);
    return {
      roomId: response.data.roomId,
      otherUserId: response.data.otherUserId,
      otherDisplayName: response.data.otherDisplayName,
      tmdbId: response.data.tmdbId
    };
  }
  
  // Get message history with pagination
  async getMessages(
    roomId: string, 
    take: number = 50, 
    beforeUtc?: Date
  ): Promise<Message[]> {
    const params: any = { take };
    if (beforeUtc) {
      params.beforeUtc = beforeUtc.toISOString();
    }
    
 const response = await api.get<BackendMessage[]>(
      `/api/Chats/${roomId}/messages`,
 { params }
    );
    
    return response.data.map(mapBackendMessageToFrontend);
  }
  
  // Leave a room
  async leaveRoom(roomId: string): Promise<void> {
    await api.post(`/api/Chats/${roomId}/leave`);
  }
}

export const chatService = new ChatService();
```

#### Backend Response Types

```typescript
// GET /api/Chats response
export interface BackendConversation {
  roomId: string;
  otherUserId: string;
  otherDisplayName: string;
  tmdbId: number | null;
  lastText: string | null;
  lastAt: string | null;  // ISO 8601 DateTime string
}

// GET /api/Chats/{roomId}/messages response
export interface BackendMessage {
  id: string;
  roomId: string;
  senderId: string;
  senderDisplayName: string;  // ?? Property name difference
  text: string;           // ?? Property name difference
  sentAt: string;          // ?? Property name difference (ISO 8601)
}
```

#### Frontend UI Types

```typescript
export interface Conversation {
  id: string;
  roomId: string;
  otherUser: {
    id: string;
    displayName: string;
    avatar: string | null;  // Backend doesn't provide this yet
  };
  lastMessage: string | null;
  lastMessageTime: Date | undefined;
  unreadCount: number;  // Backend doesn't track this yet
}

export interface Message {
  id: string;
  roomId: string;
  senderId: string;
  senderName: string;    // ?? Maps from senderDisplayName
  content: string;  // ?? Maps from text
  timestamp: Date;       // ?? Maps from sentAt (converted to Date)
}

export interface RoomMetadata {
  roomId: string;
  otherUserId: string;
  otherDisplayName: string;
  tmdbId: number | null;
}
```

#### Data Mapping Functions

```typescript
// ?? CRITICAL: Property name mapping required
export function mapBackendMessageToFrontend(backend: BackendMessage): Message {
  return {
    id: backend.id,
    roomId: backend.roomId,
    senderId: backend.senderId,
    senderName: backend.senderDisplayName,  // ?? Name change
    content: backend.text,            // ?? Name change
    timestamp: new Date(backend.sentAt)     // ?? String to Date conversion
  };
}

export function mapRoomToConversation(room: BackendConversation): Conversation {
  return {
    id: room.roomId,
    roomId: room.roomId,
    otherUser: {
      id: room.otherUserId,
      displayName: room.otherDisplayName,
      avatar: null  // Backend doesn't provide avatar URLs yet
    },
    lastMessage: room.lastText,
    lastMessageTime: room.lastAt ? new Date(room.lastAt) : undefined,
    unreadCount: 0  // Backend doesn't track unread count yet
  };
}
```

---

### 4. Match Service Update

**File:** `src/lib/services/MatchService.ts`

#### Fix `acceptMatch()` Endpoint

**?? CRITICAL:** The backend expects exact property names in the request body.

```typescript
async acceptMatch(targetUserId: string, tmdbId: number): Promise<void> {
  try {
    const response = await api.post<MatchResponse>('/api/Matches/request', {
    targetUserId,  // ?? Must be exact property name (camelCase)
      tmdbId
    });
    
    if (response.data.matched) {
      // Mutual match detected!
      toast.success("It's a match! ??");
      navigate(`/chat/${response.data.roomId}`);
    } else {
      // One-way request sent
      toast.success("Match request sent!");
    }
  } catch (error) {
    console.error('Match request failed:', error);
    toast.error('Failed to send match request');
  }
}

interface MatchResponse {
  matched: boolean;
  roomId: string | null;  // Only present if matched === true
}
```

---

### 5. Conversations List Page

**File:** `src/pages/Chats.tsx`

```typescript
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { chatService } from '@/lib/services/ChatService';
import { signalRChatService } from '@/lib/services/SignalRChatService';
import { Conversation } from '@/types/chat';
import { useAuth } from '@/hooks/useAuth';

export function ChatsPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [loading, setLoading] = useState(true);
  
  useEffect(() => {
 loadConversations();
    
    // Listen for new matches
    signalRChatService.onMutualMatch((data) => {
    console.log('Mutual match received:', data);
      toast.success(`It's a match with ${data.user.displayName}!`);
    
      // Refresh conversations list
      loadConversations();
      
      // Optionally auto-navigate to chat
      // navigate(`/chat/${data.roomId}`);
    });
  }, []);
  
  async function loadConversations() {
  try {
      setLoading(true);
      const data = await chatService.listConversations(user.id);
   setConversations(data);
    } catch (error) {
      console.error('Failed to load conversations:', error);
      toast.error('Failed to load conversations');
    } finally {
      setLoading(false);
    }
  }
  
  return (
    <div>
      <h1>Conversations</h1>
      {loading ? (
   <p>Loading...</p>
      ) : conversations.length === 0 ? (
  <p>No conversations yet. Start matching!</p>
      ) : (
        <ul>
          {conversations.map(conv => (
     <li 
    key={conv.roomId}
   onClick={() => navigate(`/chat/${conv.roomId}`)}
           style={{ cursor: 'pointer' }}
            >
 <strong>{conv.otherUser.displayName}</strong>
  {conv.lastMessage && <p>{conv.lastMessage}</p>}
       {conv.lastMessageTime && (
    <small>{conv.lastMessageTime.toLocaleString()}</small>
              )}
 </li>
          ))}
        </ul>
      )}
    </div>
  );
}
```

---

### 6. Chat Room Page

**File:** `src/pages/Chat.tsx`

```typescript
import { useEffect, useState, useRef } from 'react';
import { useParams } from 'react-router-dom';
import { chatService } from '@/lib/services/ChatService';
import { signalRChatService } from '@/lib/services/SignalRChatService';
import { Message, RoomMetadata } from '@/types/chat';
import { useAuth } from '@/hooks/useAuth';

export function ChatPage() {
  const { roomId } = useParams<{ roomId: string }>();
  const { user } = useAuth();
  const [messages, setMessages] = useState<Message[]>([]);
  const [roomMeta, setRoomMeta] = useState<RoomMetadata | null>(null);
  const [inputText, setInputText] = useState('');
  const [loading, setLoading] = useState(true);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  
  useEffect(() => {
  if (!roomId) return;
    
    initializeChat();
    
    return () => {
    // Cleanup on unmount
    signalRChatService.leaveRoom(roomId);
    };
  }, [roomId]);
  
  async function initializeChat() {
    if (!roomId) return;
    
    try {
    setLoading(true);
      
      // 1. Ensure SignalR is connected
      await signalRChatService.connect();
      
    // 2. Join the room (SignalR group)
      await signalRChatService.joinRoom(roomId);
      
   // 3. Fetch room metadata
      const meta = await chatService.getRoomMetadata(roomId);
    setRoomMeta(meta);
      
      // 4. Load message history
      const history = await chatService.getMessages(roomId, 50);
      setMessages(history);
      
      // 5. Listen for incoming messages
      signalRChatService.onReceiveMessage((backendMsg) => {
   if (backendMsg.roomId === roomId) {
      const frontendMsg = mapBackendMessageToFrontend(backendMsg);
          setMessages(prev => [...prev, frontendMsg]);
   scrollToBottom();
        }
      });
      
      scrollToBottom();
    } catch (error) {
      console.error('Failed to initialize chat:', error);
      toast.error('Failed to load chat');
    } finally {
      setLoading(false);
    }
  }
  
  async function handleSendMessage(e: React.FormEvent) {
    e.preventDefault();
    if (!roomId || !inputText.trim()) return;
    
  try {
      // Send via SignalR hub method
      await signalRChatService.sendMessage(roomId, inputText.trim());
   setInputText('');
      
 // Message will come back via ReceiveMessage event
    } catch (error) {
    console.error('Failed to send message:', error);
 toast.error('Failed to send message');
    }
  }
  
  async function loadMoreMessages() {
    if (!roomId || messages.length === 0) return;
    
    try {
      const oldestMessage = messages[0];
      const olderMessages = await chatService.getMessages(
     roomId,
        50,
        oldestMessage.timestamp  // beforeUtc parameter
      );
      
   setMessages(prev => [...olderMessages, ...prev]);
    } catch (error) {
      console.error('Failed to load more messages:', error);
    }
}
  
  function scrollToBottom() {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }
  
  if (loading) {
    return <div>Loading chat...</div>;
  }
  
  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100vh' }}>
  {/* Header */}
    <header style={{ padding: '1rem', borderBottom: '1px solid #ccc' }}>
 <h2>{roomMeta?.otherDisplayName || 'Chat'}</h2>
      </header>
      
      {/* Messages */}
      <div style={{ flex: 1, overflowY: 'auto', padding: '1rem' }}>
        {messages.length > 0 && (
          <button onClick={loadMoreMessages}>Load More</button>
        )}
        
        {messages.map(msg => (
        <div 
      key={msg.id}
     style={{
         textAlign: msg.senderId === user.id ? 'right' : 'left',
          margin: '0.5rem 0'
     }}
          >
   <strong>{msg.senderName}:</strong>
            <p>{msg.content}</p>
            <small>{msg.timestamp.toLocaleString()}</small>
          </div>
        ))}
      
        <div ref={messagesEndRef} />
  </div>
      
   {/* Input */}
      <form 
   onSubmit={handleSendMessage}
     style={{ padding: '1rem', borderTop: '1px solid #ccc' }}
      >
        <input
 type="text"
    value={inputText}
     onChange={(e) => setInputText(e.target.value)}
          placeholder="Type a message..."
        maxLength={2000}
          style={{ width: '80%', padding: '0.5rem' }}
        />
        <button type="submit" style={{ width: '18%', marginLeft: '2%' }}>
    Send
        </button>
      </form>
 </div>
  );
}
```

---

### 7. Notification Service Update

**File:** `src/lib/services/NotificationService.ts`

#### Update Event Name

```typescript
// ? OLD (WRONG):
connection.on('NewMatch', handleMatch);

// ? NEW (CORRECT):
connection.on('mutualMatch', (data: MutualMatchEvent) => {
  console.log('Mutual match detected:', data);
toast.success(`It's a match with ${data.user.displayName}!`);
  
  // Update conversations list (if on Chats page)
  window.dispatchEvent(new CustomEvent('refreshConversations'));
  
  // Optionally auto-navigate to chat
  // navigate(`/chat/${data.roomId}`);
});
```

---

## ?? Critical Property Name Mappings

### ?? Backend vs Frontend Differences

| Backend Property | Frontend Property | Type Conversion |
|-----------------|-------------------|-----------------|
| `senderDisplayName` | `senderName` | none |
| `text` | `content` | none |
| `sentAt` | `timestamp` | `string` ? `Date` |
| `roomId` | `roomId` | none (same) |

**Always use the mapper functions provided above to avoid bugs!**

---

## ?? API Reference

### Match Request

```
POST /api/Matches/request
Authorization: Bearer {token}
Content-Type: application/json

Request Body:
{
  "targetUserId": "user-guid",
  "tmdbId": 27205
}

Response (Mutual Match):
{
  "matched": true,
  "roomId": "room-guid"
}

Response (Pending):
{
  "matched": false,
  "roomId": null
}
```

### List Conversations

```
GET /api/Chats
Authorization: Bearer {token}

Response: BackendConversation[]
```

### Get Room Metadata

```
GET /api/Chats/{roomId}
Authorization: Bearer {token}

Response: BackendConversation
```

### Get Message History

```
GET /api/Chats/{roomId}/messages?take=50&beforeUtc=2025-01-29T19:22:31Z
Authorization: Bearer {token}

Response: BackendMessage[]
```

### Leave Room

```
POST /api/Chats/{roomId}/leave
Authorization: Bearer {token}

Response: 204 No Content
```

---

## ?? SignalR Hub Methods

### Client ? Server

```typescript
// Join a room
await connection.invoke('JoinRoom', roomId: string);

// Send a message
await connection.invoke('SendMessage', roomId: string, text: string);

// Leave a room
await connection.invoke('LeaveRoom', roomId: string);
```

### Server ? Client Events

```typescript
// Mutual match notification
connection.on('mutualMatch', (data: MutualMatchEvent) => { ... });

// New message received
connection.on('ReceiveMessage', (message: BackendMessage) => { ... });
```

---

## ? Testing Checklist

### SignalR Connection
- [ ] Connect to `/chathub` with JWT token in query string
- [ ] Connection successful (check browser console)
- [ ] Auto-reconnect works on disconnect

### Match Flow
- [ ] Click "Match" button on candidate
- [ ] POST `/api/Matches/request` with correct body format
- [ ] If `matched: false` ? Show "Request sent" toast
- [ ] If `matched: true` ? Navigate to `/chat/{roomId}`
- [ ] Receive `mutualMatch` event in real-time

### Chat Room
- [ ] Load message history on page mount
- [ ] Display messages with correct sender names
- [ ] Send message via SignalR `SendMessage` hub method
- [ ] Receive real-time messages via `ReceiveMessage` event
- [ ] Messages appear immediately after sending
- [ ] Infinite scroll loads older messages with `beforeUtc`

### Conversations List
- [ ] Display all user's conversations
- [ ] Show last message preview and timestamp
- [ ] Click conversation ? Navigate to `/chat/{roomId}`
- [ ] List updates when `mutualMatch` event received

---

## ?? Common Issues & Solutions

### Issue: "401 Unauthorized" on SignalR connection
**Solution:** Ensure JWT token is passed via query string:
```typescript
.withUrl(`${baseUrl}/chathub`, {
  accessTokenFactory: () => localStorage.getItem('access_token') || ''
})
```

### Issue: Messages not appearing in real-time
**Solution:** 
1. Verify you called `JoinRoom(roomId)` before sending messages
2. Check that `ReceiveMessage` event listener is registered
3. Verify roomId matches in all calls

### Issue: Property undefined errors (e.g., `message.content` is undefined)
**Solution:** You're using backend property names directly. Use the mapper functions:
```typescript
const frontendMsg = mapBackendMessageToFrontend(backendMsg);
```

### Issue: "User is not a member of this room"
**Solution:** The chat room is created when mutual match occurs. Make sure:
1. Both users sent match requests for the same movie
2. `MatchService.RequestAsync()` returned `matched: true`
3. You're using the correct `roomId` from the response

---

## ?? Questions to Resolve Before Implementation

1. **Auto-navigation on mutual match:**
   - Should the app automatically navigate to `/chat/{roomId}` when mutual match occurs?
   - Or show a modal/toast and let user navigate manually?

2. **Conversations list refresh:**
   - Should the conversations list auto-refresh when `mutualMatch` event fires?
   - Or require manual pull-to-refresh?

3. **Message pagination:**
   - Auto-load on scroll to top?
   - Or show "Load More" button?

4. **Unread indicators:**
   - Backend doesn't track unread count yet
   - Should we implement client-side tracking?
   - Or wait for backend feature?

5. **Typing indicators:**
   - Not implemented in backend yet
   - Is this needed for MVP?

---

## ?? Success Criteria

When integration is complete, users should be able to:

? Send match request and receive real-time `mutualMatch` notification  
? Navigate to chat room and see message history  
? Send messages via SignalR  
? Receive real-time messages from other user  
? See conversations list update when new match created  
? Load older messages with pagination  
? Leave chat room and stop receiving messages  

---

## ?? Additional Resources

- **SignalR JavaScript Client Docs:** https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client
- **Backend Documentation:** See `CHAT_BACKEND_COMPLETE.md` in backend repo
- **Backend Hub Code:** `CineMatch_Backend/Hubs/ChatHub.cs`
- **Backend API Controller:** `CineMatch_Backend/Controllers/ChatsController.cs`

---

## ?? Support

If you encounter issues:

1. Check browser console for errors
2. Check backend console for SignalR connection logs
3. Verify JWT token is valid (check expiry)
4. Verify CORS is allowing your origin
5. Test API endpoints with Postman/Thunder Client first

**Backend logs to look for:**
```
[ChatHub] User {userId} connected with connection {connectionId}
[ChatHub] ?? Sent mutualMatch notification to user {userId}
[MatchService] ?? MUTUAL MATCH DETECTED!
[MatchService] ? Chat room created: {roomId}
```

---

**Good luck with the integration! ??**
