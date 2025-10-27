namespace Infrastructure.Models.Matches
{
    // API CONTRACT (response) — a user who shares movie likes with the current user.
    public sealed class CandidateDto
    {
        public string UserId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int OverlapCount { get; set; }
      public List<int> SharedMovieIds { get; set; } = [];
    }
}
