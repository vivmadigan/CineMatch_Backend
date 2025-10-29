using Infrastructure.Services.Notifications;

namespace Infrastructure.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of INotificationService for testing.
    /// Does nothing - just satisfies the dependency.
    /// </summary>
    public class MockNotificationService : INotificationService
    {
        public Task SendMatchNotificationAsync(string targetUserId, object matchData)
        {
            // No-op for tests
            return Task.CompletedTask;
        }
    }
}
