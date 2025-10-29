using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Data.Entities
{
    // EF ENTITY — a chat room created when two users mutually match on a movie.
    // Represents a conversation space between matched users.
    //
    // Keys & Indexes:
    // - GUID PK for unique identification
    // - One-to-many relationship with ChatMembership (who's in this room)

    public class ChatRoom
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<ChatMembership> Memberships { get; set; } = new List<ChatMembership>();
    }
}
