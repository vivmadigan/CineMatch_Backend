using Infrastructure.Services.Notifications;

namespace Infrastructure.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of INotificationService for testing.
    /// Can simulate success or failure scenarios.
    /// </summary>
    public class MockNotificationService : INotificationService
    {
        private readonly bool _shouldFail;

        public MockNotificationService(bool shouldFail = false)
        {
            _shouldFail = shouldFail;
        }

        public Task SendMatchNotificationAsync(string targetUserId, object matchData)
        {
            if (_shouldFail)
            {
                throw new InvalidOperationException("Mock notification service configured to fail");
            }

            // No-op for tests (success case)
            return Task.CompletedTask;
        }
    }
}
