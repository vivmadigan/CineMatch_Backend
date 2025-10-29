namespace Infrastructure.Models.Matches
{
    // API CONTRACT (response) — result of a match request.
    // If matched=true, a chat room was created; if false, request was saved and awaiting reciprocal.
    public sealed class MatchResultDto
    {
        public bool Matched { get; set; }
        public Guid? RoomId { get; set; }
    }
}
