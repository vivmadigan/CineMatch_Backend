namespace Infrastructure.Models.Chat
{
 // API CONTRACT (response) — a single chat message.
    public sealed class ChatMessageDto
  {
        public Guid Id { get; set; }
   public Guid RoomId { get; set; }
        public string SenderId { get; set; } = "";
        public string SenderDisplayName { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime SentAt { get; set; }
    }
}
