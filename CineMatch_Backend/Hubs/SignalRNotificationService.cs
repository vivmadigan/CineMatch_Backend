using Infrastructure.Services.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace Presentation.Hubs
{
    /// <summary>
    /// SignalR-based implementation of INotificationService.
    /// Sends real-time notifications to connected users via ChatHub.
    /// </summary>
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<ChatHub> _hubContext;

        public SignalRNotificationService(IHubContext<ChatHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendMatchNotificationAsync(string targetUserId, object matchData)
        {
            await ChatHub.NotifyNewMatch(_hubContext, targetUserId, matchData);
        }
    }
}
