using Infrastructure.Models.Chat;
using Infrastructure.Services.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Presentation.Controllers
{
    /// <summary>
    /// Chat management endpoints for listing rooms, fetching history, and leaving rooms.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
 [Produces("application/json")]
    public class ChatsController : ControllerBase
    {
   /// <summary>
 /// List all chat rooms for the current user.
   /// </summary>
     /// <param name="chatService">Chat service (injected)</param>
     /// <param name="ct">Cancellation token</param>
  /// <remarks>
     /// Example response:
        /// 
        ///     [
        ///       {
  /// "roomId": "c5a5a0a4-5e2d-4a6a-9b7b-7c6d3d1e2f90",
   ///         "otherUserId": "8bd1e3b8-8f30-4a9f-9b0e-8a8c6e2c0d71",
        ///     "otherDisplayName": "Casey",
  ///         "lastText": "See you then!",
   ///       "lastAt": "2025-01-27T12:34:56Z"
 ///       }
   ///     ]
   /// </remarks>
        /// <response code="200">List of chat rooms with last message preview</response>
     /// <response code="401">User not authenticated</response>
     [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ChatRoomListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
      public async Task<IActionResult> ListRooms(
    [FromServices] IChatService chatService,
    CancellationToken ct = default)
        {
         var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
       if (string.IsNullOrEmpty(userId)) return Unauthorized();

     var rooms = await chatService.ListMyRoomsAsync(userId, ct);
  return Ok(rooms);
     }

        /// <summary>
        /// Get message history for a specific chat room.
   /// </summary>
     /// <param name="chatService">Chat service (injected)</param>
     /// <param name="roomId">Chat room ID</param>
 /// <param name="take">Number of messages to return (default 50, max 100)</param>
      /// <param name="beforeUtc">Get messages before this timestamp (for pagination)</param>
   /// <param name="ct">Cancellation token</param>
      /// <remarks>
   /// Example response:
     /// 
    ///     [
        ///       {
        ///         "id": "a1b2c3d4-...",
  ///  "roomId": "c5a5a0a4-...",
   ///  "senderId": "user-a-id",
///         "senderDisplayName": "Alex",
        ///         "text": "Hey! Want to watch on Friday?",
   ///  "sentAt": "2025-01-27T12:00:00Z"
   ///       },
        ///  {
        ///         "id": "e5f6g7h8-...",
   /// "roomId": "c5a5a0a4-...",
     ///         "senderId": "user-b-id",
        ///         "senderDisplayName": "Casey",
   ///         "text": "Sure! 7pm works?",
        ///         "sentAt": "2025-01-27T12:05:00Z"
        ///       }
 ///     ]
   /// </remarks>
   /// <response code="200">List of messages, newest first</response>
    /// <response code="401">User not authenticated</response>
        /// <response code="403">User not a member of this room</response>
  [HttpGet("{roomId:guid}/messages")]
 [ProducesResponseType(typeof(IEnumerable<ChatMessageDto>), StatusCodes.Status200OK)]
 [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
      public async Task<IActionResult> GetMessages(
    [FromServices] IChatService chatService,
      Guid roomId,
 [FromQuery] int take = 50,
[FromQuery] DateTime? beforeUtc = null,
  CancellationToken ct = default)
 {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (string.IsNullOrEmpty(userId)) return Unauthorized();

   try
      {
  var messages = await chatService.GetMessagesAsync(roomId, take, beforeUtc, userId, ct);
           return Ok(messages);
     }
       catch (InvalidOperationException ex)
            {
   return StatusCode(403, new { error = ex.Message });
   }
        }

        /// <summary>
   /// Leave a chat room (mark membership as inactive).
        /// </summary>
     /// <param name="chatService">Chat service (injected)</param>
   /// <param name="roomId">Chat room ID</param>
  /// <param name="ct">Cancellation token</param>
     /// <response code="204">Successfully left the room</response>
     /// <response code="401">User not authenticated</response>
      /// <response code="404">User not a member of this room</response>
[HttpPost("{roomId:guid}/leave")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LeaveRoom(
     [FromServices] IChatService chatService,
            Guid roomId,
    CancellationToken ct = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
       if (string.IsNullOrEmpty(userId)) return Unauthorized();

    try
    {
       await chatService.LeaveAsync(roomId, userId, ct);
    return NoContent();
            }
  catch (InvalidOperationException ex)
     {
     return NotFound(new { error = ex.Message });
   }
      }
    }
}
