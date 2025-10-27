using Infrastructure.Services.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Presentation.Hubs
{
    /// <summary>
    /// SignalR hub for real-time chat messaging.
    /// Clients connect via WebSocket at /chathub with JWT authentication.
    /// </summary>
    [Authorize]
 public class ChatHub : Hub
    {
   private readonly IChatService _chatService;

        public ChatHub(IChatService chatService)
  {
     _chatService = chatService;
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
