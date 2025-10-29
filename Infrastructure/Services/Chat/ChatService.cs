using Infrastructure.Data.Context;
using Infrastructure.Data.Entities;
using Infrastructure.Models.Chat;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Chat
{
    public sealed class ChatService : IChatService
    {
        private readonly ApplicationDbContext _db;

        public ChatService(ApplicationDbContext db) => _db = db;

        public async Task<IReadOnlyList<ChatRoomListItemDto>> ListMyRoomsAsync(string userId, CancellationToken ct)
        {
            // 1) Get all rooms where user is an active member
            var myMemberships = await _db.ChatMemberships
              .AsNoTracking()
                    .Where(m => m.UserId == userId && m.IsActive)
                .Select(m => new { m.RoomId, m.UserId })
       .ToListAsync(ct);

            if (myMemberships.Count == 0)
                return Array.Empty<ChatRoomListItemDto>();

            var roomIds = myMemberships.Select(m => m.RoomId).ToList();

            // 2) Get other members (1 other user per room in MVP)
            var otherMembers = await _db.ChatMemberships
     .AsNoTracking()
    .Where(m => roomIds.Contains(m.RoomId) && m.UserId != userId)
    .Select(m => new { m.RoomId, m.UserId })
    .ToListAsync(ct);

            var otherUserIds = otherMembers.Select(m => m.UserId).Distinct().ToList();

            // 3) Get user display names
            var users = await _db.Users
      .AsNoTracking()
            .Where(u => otherUserIds.Contains(u.Id))
        .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

            // 4) Get last message per room
            var lastMessages = await _db.ChatMessages
                 .AsNoTracking()
                       .Where(msg => roomIds.Contains(msg.RoomId))
                    .GroupBy(msg => msg.RoomId)
                   .Select(g => new
                   {
                       RoomId = g.Key,
                       LastText = g.OrderByDescending(m => m.SentAt).First().Text,
                       LastAt = g.Max(m => m.SentAt)
                   })
                        .ToListAsync(ct);

            // 5) Map to DTOs
            var result = myMemberships
             .Select(membership =>
      {
          var otherMember = otherMembers.FirstOrDefault(om => om.RoomId == membership.RoomId);
          var lastMsg = lastMessages.FirstOrDefault(lm => lm.RoomId == membership.RoomId);

          return new ChatRoomListItemDto
          {
              RoomId = membership.RoomId,
              OtherUserId = otherMember?.UserId ?? "",
              OtherDisplayName = otherMember != null && users.TryGetValue(otherMember.UserId, out var name)
 ? name
: "Unknown",
              LastText = lastMsg?.LastText,
              LastAt = lastMsg?.LastAt
          };
      })
            .OrderByDescending(r => r.LastAt ?? DateTime.MinValue)
           .ToList();

            return result;
        }

        public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid roomId, int take, DateTime? beforeUtc, string userId, CancellationToken ct)
        {
            // 1) Validate user is a member of the room
            var isMember = await _db.ChatMemberships
           .AnyAsync(m => m.RoomId == roomId && m.UserId == userId, ct);

            if (!isMember)
                throw new InvalidOperationException("User is not a member of this room");

            // 2) Build query for messages
            var query = _db.ChatMessages
                .AsNoTracking()
        .Where(m => m.RoomId == roomId);

            if (beforeUtc.HasValue)
                query = query.Where(m => m.SentAt < beforeUtc.Value);

            var messages = await query
              .OrderByDescending(m => m.SentAt)
                   .Take(Math.Max(1, Math.Min(take, 100))) // Clamp between 1-100
                 .ToListAsync(ct);

            // 3) Get sender display names
            var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
            var senders = await _db.Users
       .AsNoTracking()
        .Where(u => senderIds.Contains(u.Id))
 .Select(u => new { u.Id, u.DisplayName })
       .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

            // 4) Map to DTOs
            var result = messages
     .Select(m => new ChatMessageDto
     {
         Id = m.Id,
         RoomId = m.RoomId,
         SenderId = m.SenderId,
         SenderDisplayName = senders.TryGetValue(m.SenderId, out var name) ? name : "Unknown",
         Text = m.Text,
         SentAt = m.SentAt
     })
          .ToList();

            return result;
        }

        public async Task<ChatMessageDto> AppendAsync(Guid roomId, string userId, string text, CancellationToken ct)
        {
            // 1) Validate text
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Message text cannot be empty", nameof(text));

            if (text.Length > 2000)
                throw new ArgumentException("Message text cannot exceed 2000 characters", nameof(text));

            // 2) Validate user is an active member
            var membership = await _db.ChatMemberships
               .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);

            if (membership == null || !membership.IsActive)
                throw new InvalidOperationException("User is not an active member of this room");

            // 3) Create and save message
            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                SenderId = userId,
                Text = text.Trim(),
                SentAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync(ct);

            // 4) Get sender display name
            var sender = await _db.Users
                     .AsNoTracking()
           .Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName })
            .FirstOrDefaultAsync(ct);

            // 5) Return DTO
            return new ChatMessageDto
            {
                Id = message.Id,
                RoomId = message.RoomId,
                SenderId = message.SenderId,
                SenderDisplayName = sender?.DisplayName ?? "Unknown",
                Text = message.Text,
                SentAt = message.SentAt
            };
        }

        public async Task LeaveAsync(Guid roomId, string userId, CancellationToken ct)
        {
            // 1) Find membership
            var membership = await _db.ChatMemberships
      .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);

            if (membership == null)
                throw new InvalidOperationException("User is not a member of this room");

            // 2) Mark as inactive
            if (membership.IsActive)
            {
                membership.IsActive = false;
                membership.LeftAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }

        public async Task ReactivateMembershipAsync(Guid roomId, string userId, CancellationToken ct)
        {
            // 1) Find membership
            var membership = await _db.ChatMemberships
 .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);

            if (membership == null)
                throw new InvalidOperationException("User is not a member of this room");

            // 2) Reactivate if inactive
            if (!membership.IsActive)
            {
                membership.IsActive = true;
                membership.JoinedAt = DateTime.UtcNow;
                membership.LeftAt = null;
                await _db.SaveChangesAsync(ct);
            }
        }
    }
}
