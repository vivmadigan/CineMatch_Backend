using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Preferences
{
    // API CONTRACT (response) — what the frontend reads back for the current user.
    public sealed class GetPreferencesDto
    {
        public List<int> GenreIds { get; set; } = new();
        public string Length { get; set; } = "medium";
    }
}
