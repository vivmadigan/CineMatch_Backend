namespace Infrastructure.Models.Chat
{
    // API CONTRACT (response) — a chat room in the user's list.
    // Shows other user's info and last message preview.
    public sealed class ChatRoomListItemDto
    {
        public Guid RoomId { get; set; }
        public string OtherUserId { get; set; } = "";
        public string OtherDisplayName { get; set; } = "";
        public int? TmdbId { get; set; } // Movie that matched the users (null for legacy rooms)
        public string? LastText { get; set; }
        public DateTime? LastAt { get; set; }
    }
}
