using Infrastructure.Services.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Presentation.Hubs
{
    /// <summary>
    /// SignalR hub for real-time chat messaging and match notifications.
    /// Clients connect via WebSocket at /chathub with JWT authentication.
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;

        // ? Track userId -> connectionId mapping for targeted notifications
        private static readonly ConcurrentDictionary<string, string> UserConnections = new();

        public ChatHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>
        /// Called when a user connects to the hub.
        /// Tracks their connectionId for targeted notifications.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                UserConnections[userId] = Context.ConnectionId;
                Console.WriteLine($"[ChatHub] User {userId} connected with connection {Context.ConnectionId}");
            }
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a user disconnects from the hub.
        /// Removes their connectionId from tracking.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                UserConnections.TryRemove(userId, out _);
                Console.WriteLine($"[ChatHub] User {userId} disconnected");
            }
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Send a real-time match notification to a specific user.
        /// Called by MatchService when a mutual match is detected.
        /// </summary>
        /// <param name="hubContext">Hub context for sending messages</param>
        /// <param name="targetUserId">User to notify</param>
        /// <param name="matchData">Match notification payload</param>
        public static async Task NotifyNewMatch(IHubContext<ChatHub> hubContext, string targetUserId, object matchData)
        {
            if (UserConnections.TryGetValue(targetUserId, out var connectionId))
            {
                try
                {
                    // Send "mutualMatch" event (frontend listens for this)
                    await hubContext.Clients.Client(connectionId).SendAsync("mutualMatch", matchData);
                    Console.WriteLine($"[ChatHub] ?? Sent mutualMatch notification to user {targetUserId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ChatHub] ? Failed to send notification to {targetUserId}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[ChatHub] ??  User {targetUserId} not connected, cannot send notification");
            }
        }

        /// <summary>
        /// Join a chat room to start receiving messages.
        /// </summary>
        /// <param name="roomId">Chat room ID</param>
        public async Task JoinRoom(Guid roomId)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new HubException("User not authenticated");
            }

            // Validate membership exists; reactivate if inactive
            try
            {
                await _chatService.ReactivateMembershipAsync(roomId, userId, Context.ConnectionAborted);
            }
            catch (InvalidOperationException ex)
            {
                throw new HubException(ex.Message);
            }

            // Add to SignalR group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");
        }

        /// <summary>
        /// Leave a chat room (stop receiving messages).
        /// </summary>
        /// <param name="roomId">Chat room ID</param>
        public async Task LeaveRoom(Guid roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room:{roomId}");
        }

        /// <summary>
        /// Send a message to a chat room.
        /// All connected members receive the message via "ReceiveMessage" event.
        /// </summary>
        /// <param name="roomId">Chat room ID</param>
        /// <param name="text">Message text (max 2000 chars)</param>
        public async Task SendMessage(Guid roomId, string text)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new HubException("User not authenticated");
            }

            try
            {
                // Save message to database
                var messageDto = await _chatService.AppendAsync(roomId, userId, text, Context.ConnectionAborted);

                // Broadcast to all members in the room
                await Clients.Group($"room:{roomId}")
               .SendAsync("ReceiveMessage", messageDto, Context.ConnectionAborted);
            }
            catch (ArgumentException ex)
            {
                throw new HubException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new HubException(ex.Message);
            }
        }
    }
}
