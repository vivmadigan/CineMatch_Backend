using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Data.Entities
{
    // EF ENTITY — tracks which users belong to which chat rooms.
  // Supports soft delete (LeftAt) so users can leave/rejoin rooms.
    //
 // Keys & Indexes:
    // - Composite PK (RoomId, UserId) prevents duplicate memberships
    // - IsActive indicates current membership status
    // - LeftAt tracks when user left (null if still active)

  [PrimaryKey(nameof(RoomId), nameof(UserId))]
 public class ChatMembership
    {
        public Guid RoomId { get; set; }

     [MaxLength(450)]
  public string UserId { get; set; } = "";

public bool IsActive { get; set; } = true;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LeftAt { get; set; }

        // Navigation properties
  [ForeignKey(nameof(RoomId))]
        public ChatRoom? Room { get; set; }

        [ForeignKey(nameof(UserId))]
        public UserEntity? User { get; set; }
 }
}
