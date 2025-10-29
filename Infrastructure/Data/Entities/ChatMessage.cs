using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Data.Entities
{
    // EF ENTITY — a chat message sent in a room.
    // Messages are immutable once sent (no edit/delete in MVP).
    //
    // Keys & Indexes:
    // - GUID PK for unique identification
    // - Composite index (RoomId, SentAt DESC) for fast chronological retrieval
    // - Index on SenderId for user's message history

    [Index(nameof(RoomId), nameof(SentAt), IsDescending = new[] { false, true })]
    [Index(nameof(SenderId))]
    public class ChatMessage
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid RoomId { get; set; }

        [Required]
        [MaxLength(450)]
        public string SenderId { get; set; } = "";

        [Required]
        [MaxLength(2000)]
        public string Text { get; set; } = "";

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(RoomId))]
        public ChatRoom? Room { get; set; }

        [ForeignKey(nameof(SenderId))]
        public UserEntity? Sender { get; set; }
    }
}
