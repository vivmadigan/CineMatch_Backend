using Infrastructure.Preferences;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public interface IPreferenceService
    {
        Task<GetPreferencesDto> GetAsync(string userId, CancellationToken ct);
        Task SaveAsync(string userId, SavePreferenceDto dto, CancellationToken ct);
    }
}
