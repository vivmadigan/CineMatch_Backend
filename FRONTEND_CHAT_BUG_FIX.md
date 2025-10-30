# ?? Chat Page Blank Screen Bug - Frontend Fix Required

## ?? Critical Issue Identified

When sending a chat message, the page goes blank and the chat disappears. Backend logs confirm this is caused by **premature membership deactivation** triggered by the frontend.

---

## ?? Root Cause Analysis

### What's Happening:

1. ? User sends message successfully
2. ? Backend saves message to database
3. ? Backend broadcasts message via SignalR
4. ? **Frontend immediately calls `leaveRoom()`**
5. ? Backend marks `ChatMembership.IsActive = false`
6. ? Page goes blank because user is no longer an active member

### Backend Logs Confirm:

```sql
-- Message saved successfully ?
INSERT INTO [ChatMessages] ([Id], [RoomId], [SenderId], [SentAt], [Text])
VALUES (@p0, @p1, @p2, @p3, @p4);

-- Then membership is deactivated ?
UPDATE [ChatMemberships] SET [IsActive] = @p0, [LeftAt] = @p1
WHERE [RoomId] = @p2 AND [UserId] = @p3;
-- IsActive = FALSE, LeftAt = current timestamp
```

**This happens immediately after sending a message, not when the user navigates away.**

---

## ? Backend Status: Working Correctly

The backend is functioning as designed:
- ? Messages are saved successfully
- ? SignalR broadcasts work correctly
- ? Membership management is correct
- ? All API endpoints return proper responses

**No backend changes required!**

---

## ? Frontend Bug: Incorrect `useEffect` Dependencies

### Problem Location: `src/pages/Chat.tsx`

The React component is likely calling `leaveRoom()` on every message send due to incorrect `useEffect` dependencies.

### ? **WRONG Implementation (Current Bug):**

```typescript
// BUG: This causes cleanup to run on every render
useEffect(() => {
  if (!roomId) return;
  
  initializeChat();
  
  return () => {
// ? This runs EVERY TIME messages state changes!
    signalRChatService.leaveRoom(roomId);
  };
}, [roomId, messages]);  // ? BUG: 'messages' in dependency array
```

**Why this is wrong:**
1. `messages` is in the dependency array
2. Every new message updates `messages` state
3. React re-runs the effect
4. Cleanup function calls `leaveRoom()`
5. Backend deactivates membership
6. Page goes blank

---

## ? Frontend Fix Required

### ?? **CORRECT Implementation:**

```typescript
// FIX: Only cleanup when actually leaving the page
useEffect(() => {
  if (!roomId) return;

  const initialize = async () => {
    try {
  // 1. Connect to SignalR
      await signalRChatService.connect();
      
  // 2. Join room
      await signalRChatService.joinRoom(roomId);
      
  // 3. Fetch metadata
      const meta = await chatService.getRoomMetadata(roomId);
      setRoomMeta(meta);
      
      // 4. Load messages
    const history = await chatService.getMessages(roomId, 50);
      setMessages(history);
    
      // 5. Listen for messages
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
  };
  
  initialize();
  
  // Cleanup ONLY runs when component unmounts (user navigates away)
  return () => {
    signalRChatService.leaveRoom(roomId);
  };
}, [roomId]);  // ? FIX: Only depend on roomId, NOT messages!
```

---

## ?? Key Changes Required

### 1. Remove `messages` from dependency array

**Before (Bug):**
```typescript
}, [roomId, messages]);  // ? Causes re-render on every message
```

**After (Fix):**
```typescript
}, [roomId]);  // ? Only re-runs when roomId changes
```

### 2. Don't call `leaveRoom()` in message handlers

**? WRONG:**
```typescript
async function handleSendMessage(e: React.FormEvent) {
  e.preventDefault();
await signalRChatService.sendMessage(roomId, inputText.trim());
  await signalRChatService.leaveRoom(roomId);  // ? DON'T DO THIS
  setInputText('');
}
```

**? CORRECT:**
```typescript
async function handleSendMessage(e: React.FormEvent) {
  e.preventDefault();
  if (!roomId || !inputText.trim()) return;
  
  try {
    await signalRChatService.sendMessage(roomId, inputText.trim());
    setInputText('');
    // ? No leaveRoom call here - cleanup will handle it on unmount
  } catch (error) {
    console.error('Failed to send message:', error);
    toast.error('Failed to send message');
  }
}
```

### 3. Ensure event listeners don't cause re-renders

The `onReceiveMessage` listener should be set up once and use functional state updates:

```typescript
// ? Use functional update to avoid stale closures
setMessages(prev => [...prev, frontendMsg]);
```

---

## ?? Complete Fixed `Chat.tsx` Component

Here's the corrected implementation:

```typescript
import { useEffect, useState, useRef } from 'react';
import { useParams } from 'react-router-dom';
import { chatService } from '@/lib/services/ChatService';
import { signalRChatService } from '@/lib/services/SignalRChatService';
import { Message, RoomMetadata } from '@/types/chat';
import { useAuth } from '@/hooks/useAuth';
import { mapBackendMessageToFrontend } from '@/lib/utils/chatMappers';
import { toast } from 'sonner';

export function ChatPage() {
  const { roomId } = useParams<{ roomId: string }>();
  const { user } = useAuth();
  const [messages, setMessages] = useState<Message[]>([]);
  const [roomMeta, setRoomMeta] = useState<RoomMetadata | null>(null);
  const [inputText, setInputText] = useState('');
  const [loading, setLoading] = useState(true);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  
  // ? FIX: Only depend on roomId, NOT messages
  useEffect(() => {
    if (!roomId) return;
    
    const initialize = async () => {
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
            // ? Use functional update to avoid stale state
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
    };
    
    initialize();
    
    // ? Cleanup ONLY runs on unmount (when user navigates away)
    return () => {
      console.log(`[Chat] Leaving room ${roomId}`);
      signalRChatService.leaveRoom(roomId);
    };
  }, [roomId]);  // ? CRITICAL: Only roomId in dependencies
  
  async function handleSendMessage(e: React.FormEvent) {
    e.preventDefault();
    if (!roomId || !inputText.trim()) return;
    
    try {
      // Send via SignalR hub method
      await signalRChatService.sendMessage(roomId, inputText.trim());
      setInputText('');
      
      // ? Message will come back via ReceiveMessage event
      // ? NO leaveRoom call here!
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
      
      // ? Prepend older messages
      setMessages(prev => [...olderMessages, ...prev]);
  } catch (error) {
      console.error('Failed to load more messages:', error);
      toast.error('Failed to load more messages');
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

## ?? Testing the Fix

### Before Fix:
1. ? Send message ? Page goes blank
2. ? Backend logs show: `UPDATE ChatMemberships SET IsActive = false`
3. ? User can't see messages

### After Fix:
1. ? Send message ? Message appears immediately
2. ? Page stays visible
3. ? Backend logs show: No membership update after message send
4. ? Navigate away ? Backend logs show: `UPDATE ChatMemberships SET IsActive = false`

---

## ?? How to Verify the Fix

### 1. Check Browser Console:

**Before fix:**
```
[Chat] Leaving room {roomId}
// ? This appears after EVERY message send
```

**After fix:**
```
[Chat] Leaving room {roomId}
// ? This only appears when navigating away
```

### 2. Check Backend Logs:

**During active chat (after fix):**
```sql
-- ? Only message inserts, no membership updates
INSERT INTO [ChatMessages] ([Id], [RoomId], [SenderId], [SentAt], [Text])
VALUES (...);
```

**When navigating away (after fix):**
```sql
-- ? Now membership is deactivated (correct timing)
UPDATE [ChatMemberships] SET [IsActive] = 0, [LeftAt] = '...'
WHERE [RoomId] = '...' AND [UserId] = '...';
```

---

## ?? Additional Recommendations

### 1. Add Debug Logging (Temporary)

```typescript
useEffect(() => {
  if (!roomId) return;
  
  console.log(`[Chat] Initializing chat for room ${roomId}`);
  
  // ... initialization code ...
  
  return () => {
    console.log(`[Chat] Cleanup: Leaving room ${roomId}`);
    signalRChatService.leaveRoom(roomId);
  };
}, [roomId]);
```

### 2. Consider Using `useCallback` for Event Handlers

```typescript
const handleReceiveMessage = useCallback((backendMsg: BackendMessage) => {
  if (backendMsg.roomId === roomId) {
    const frontendMsg = mapBackendMessageToFrontend(backendMsg);
  setMessages(prev => [...prev, frontendMsg]);
    scrollToBottom();
  }
}, [roomId]);

useEffect(() => {
  // ...
  signalRChatService.onReceiveMessage(handleReceiveMessage);
  // ...
}, [roomId, handleReceiveMessage]);
```

### 3. Cleanup Event Listeners Properly

If your `signalRChatService.onReceiveMessage()` doesn't automatically remove old listeners, you might have duplicate listeners. Add cleanup:

```typescript
useEffect(() => {
  // ...
  
  const messageHandler = (backendMsg: BackendMessage) => {
    if (backendMsg.roomId === roomId) {
      const frontendMsg = mapBackendMessageToFrontend(backendMsg);
      setMessages(prev => [...prev, frontendMsg]);
  scrollToBottom();
    }
  };
  
  signalRChatService.onReceiveMessage(messageHandler);
  
  return () => {
    // Remove listener before leaving
    signalRChatService.offReceiveMessage(messageHandler);
 signalRChatService.leaveRoom(roomId);
  };
}, [roomId]);
```

---

## ?? Summary

| Issue | Cause | Fix |
|-------|-------|-----|
| **Page goes blank after sending message** | `messages` in useEffect dependency array | Remove `messages` from dependencies |
| **Membership deactivated prematurely** | Cleanup runs on every render | Only depend on `roomId` |
| **SignalR disconnects unexpectedly** | `leaveRoom()` called too early | Only call in cleanup on unmount |

---

## ? Acceptance Criteria

After applying the fix, the following should work:

- [ ] Send message ? Message appears in chat
- [ ] Chat page stays visible after sending message
- [ ] Multiple messages can be sent without issues
- [ ] Real-time messages from other user appear
- [ ] Navigate away ? Backend logs show membership deactivation
- [ ] Return to chat ? Room rejoins successfully
- [ ] No duplicate messages
- [ ] No memory leaks from event listeners

---

## ?? If Issues Persist

If the bug continues after applying the fix, check these additional areas:

### 1. SignalR Service Implementation

Ensure `SignalRChatService.leaveRoom()` is only called from `Chat.tsx` cleanup:

```typescript
// SignalRChatService.ts
async leaveRoom(roomId: string): Promise<void> {
  console.log(`[SignalR] Leaving room: ${roomId}`);  // Debug log
  if (!this.connection) throw new Error('Not connected');
  await this.connection.invoke('LeaveRoom', roomId);
}
```

### 2. Router Navigation Guards

Check if your router has navigation guards that might trigger cleanup:

```typescript
// If using react-router v6
const navigate = useNavigate();

// Ensure navigation doesn't trigger premature cleanup
```

### 3. Parent Component Re-renders

Check if a parent component is causing `Chat.tsx` to re-mount:

```typescript
// Add key to prevent unwanted re-mounts
<Route path="/chat/:roomId" element={<ChatPage key={roomId} />} />
```

### 4. ?? NEW: Check Conversations List Page (Chats.tsx)

**CRITICAL:** Ensure `Chats.tsx` is NOT calling `leaveRoom()` in its useEffect:

```typescript
// ? WRONG - Chats.tsx should NOT call leaveRoom
useEffect(() => {
  loadConversations();
  
  return () => {
    signalRChatService.leaveRoom(someRoomId);  // ? REMOVE THIS
  };
}, []);

// ? CORRECT - Chats.tsx should ONLY load conversations
useEffect(() => {
  loadConversations();
  // ? NO cleanup function needed here
}, []);
```

**Only `Chat.tsx` should call `leaveRoom()` in cleanup!**

---

## ?? Debugging: "User is not a member of this room" Error

If you see this error in backend logs:

```
Failed to invoke hub method 'JoinRoom'.
Microsoft.AspNetCore.SignalR.HubException: User is not a member of this room
```

**This means the frontend deactivated the membership BEFORE trying to join.**

### Check These:

1. **Browser Console:** Look for `[Chat] Leaving room` logs appearing **before** the chat page loads
2. **Backend Logs:** Look for `UPDATE ChatMemberships SET IsActive = false` appearing **before** `JoinRoom` attempt
3. **Component Mounting:** Add logs to verify `Chat.tsx` mount/unmount timing

### Example Debug Logs:

```typescript
// Add to Chat.tsx
useEffect(() => {
  console.log(`[Chat] MOUNTING for room ${roomId}`);
  
  const init = async () => {
    console.log(`[Chat] Initializing room ${roomId}`);
    // ... init code ...
  };
  
  init();
  
  return () => {
    console.log(`[Chat] UNMOUNTING - Leaving room ${roomId}`);
    signalRChatService.leaveRoom(roomId);
  };
}, [roomId]);
```

**Expected logs when opening chat:**
```
[Chat] MOUNTING for room {roomId}
[Chat] Initializing room {roomId}
[SignalR] Joining room: {roomId}
? Room joined successfully
```

**BUG logs (frontend needs fix):**
```
[Chat] MOUNTING for room {roomId}
[Chat] UNMOUNTING - Leaving room {roomId}? Too early!
[SignalR] Leaving room: {roomId}  ? Before joining!
[Chat] Initializing room {roomId}
[SignalR] Joining room: {roomId}
? Error: User is not a member of this room
```

---

## ?? Support

If you need clarification or encounter additional issues:

1. Check browser console for `[Chat]` debug logs
2. Check backend console for membership update logs
3. Verify `useEffect` dependency array has **only** `[roomId]`
4. Add temporary debug logs to confirm cleanup timing

**The backend is working correctly - this is a frontend React lifecycle issue!**

---

## ?? Expected Outcome

After applying this fix:
- ? Chat messages work seamlessly
- ? Page stays visible during chat session
- ? Real-time messaging works perfectly
- ? User can send unlimited messages without issues
- ? Cleanup only happens when navigating away

**Happy chatting! ??**
