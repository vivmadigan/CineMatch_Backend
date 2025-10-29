namespace Infrastructure.Services.Notifications
{
    /// <summary>
    /// Service for sending real-time notifications to users.
    /// Implemented by SignalR ChatHub in Presentation layer.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Send a match notification to a specific user.
        /// </summary>
        /// <param name="targetUserId">User to notify</param>
        /// <param name="matchData">Match notification payload</param>
        Task SendMatchNotificationAsync(string targetUserId, object matchData);
    }
}
