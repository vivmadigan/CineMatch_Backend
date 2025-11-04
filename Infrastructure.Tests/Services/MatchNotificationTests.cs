using FluentAssertions;
using Infrastructure.Services.Matches;
using Infrastructure.Services.Notifications;
using Infrastructure.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Tests for match request notification delivery via SignalR.
/// GOAL: Ensure notifications are sent correctly for match requests and mutual matches.
/// IMPORTANCE: HIGH PRIORITY - Notifications are core to real-time user experience.
/// </summary>
public class MatchNotificationTests
{
    [Fact]
    public async Task RequestAsync_SendsNotificationToTargetUser()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
   var user2 = await DbFixture.CreateTestUserAsync(context);
        
   var mockNotificationService = new Mock<INotificationService>();
        var service = new MatchService(context, mockNotificationService.Object);

        // Act
       await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

        // Assert
        mockNotificationService.Verify(
 x => x.SendMatchNotificationAsync(user2.Id, It.IsAny<object>()),
     Times.Once);
    }

    [Fact]
 public async Task RequestAsync_DuplicateRequest_DoesNotSendNotification()
    {
        // Arrange
 using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);

 var mockNotificationService = new Mock<INotificationService>();
        var service = new MatchService(context, mockNotificationService.Object);

        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        mockNotificationService.Invocations.Clear();

 // Act
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

        // Assert
        mockNotificationService.Verify(
   x => x.SendMatchNotificationAsync(It.IsAny<string>(), It.IsAny<object>()),
      Times.Never);
    }

    [Fact]
    public async Task RequestAsync_MutualMatch_NotifiesBothUsers()
    {
        // Arrange
  using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);

        var mockNotificationService = new Mock<INotificationService>();
        var service = new MatchService(context, mockNotificationService.Object);

        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
        mockNotificationService.Invocations.Clear();

        // Act
        await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

 // Assert
        mockNotificationService.Verify(
 x => x.SendMatchNotificationAsync(It.IsAny<string>(), It.IsAny<object>()),
    Times.Exactly(2));
    }

    [Fact]
    public async Task RequestAsync_NotificationFailure_StillSavesRequest()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
  var user2 = await DbFixture.CreateTestUserAsync(context);

        var mockNotificationService = new Mock<INotificationService>();
      mockNotificationService
   .Setup(x => x.SendMatchNotificationAsync(It.IsAny<string>(), It.IsAny<object>()))
       .ThrowsAsync(new Exception("SignalR failed"));

        var service = new MatchService(context, mockNotificationService.Object);

        // Act
        var result = await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);

        // Assert
        result.Matched.Should().BeFalse();
        var savedRequest = await context.MatchRequests
     .FirstOrDefaultAsync(r => r.RequestorId == user1.Id && r.TargetUserId == user2.Id);
        savedRequest.Should().NotBeNull();
    }

    [Fact]
    public async Task RequestAsync_MutualMatchNotificationFailure_StillCreatesRoom()
    {
// Arrange
 using var context = DbFixture.CreateContext();
        var user1 = await DbFixture.CreateTestUserAsync(context);
        var user2 = await DbFixture.CreateTestUserAsync(context);

        var mockNotificationService = new Mock<INotificationService>();
     mockNotificationService
            .Setup(x => x.SendMatchNotificationAsync(It.IsAny<string>(), It.IsAny<object>()))
    .ThrowsAsync(new Exception("SignalR failed"));

        var service = new MatchService(context, mockNotificationService.Object);

        // Act
        await service.RequestAsync(user1.Id, user2.Id, 27205, CancellationToken.None);
      var result = await service.RequestAsync(user2.Id, user1.Id, 27205, CancellationToken.None);

        // Assert
        result.Matched.Should().BeTrue();
     result.RoomId.Should().NotBeNull();
    }
}
