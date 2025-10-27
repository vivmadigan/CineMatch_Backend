using FluentAssertions;
using Infrastructure.Data.Entities;
using Infrastructure.Services.Chat;
using Infrastructure.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ChatService.
/// Tests messaging, room management, and membership validation.
/// </summary>
public class ChatServiceTests
{
    [Fact]
    public async Task AppendAsync_UserNotActiveMember_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        context.ChatRooms.Add(room);
        await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AppendAsync(room.Id, user.Id, "Hello", CancellationToken.None));
    }

    [Fact]
    public async Task AppendAsync_WithValidData_SavesAndReturnsDto()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
        {
            RoomId = room.Id,
            UserId = user.Id,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);
        await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act
        var result = await service.AppendAsync(room.Id, user.Id, "Hello World!", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().Be("Hello World!");
        result.SenderId.Should().Be(user.Id);
        result.SenderDisplayName.Should().Be(user.DisplayName);
        result.RoomId.Should().Be(room.Id);
    }

    [Fact]
    public async Task LeaveAsync_SetsIsActiveFalse()
    {
        // Arrange
        using var context = DbFixture.CreateContext();
        var user = await DbFixture.CreateTestUserAsync(context);
        var room = new ChatRoom { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var membership = new ChatMembership
        {
            RoomId = room.Id,
            UserId = user.Id,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };
        context.ChatRooms.Add(room);
        context.ChatMemberships.Add(membership);
        await context.SaveChangesAsync();

        var service = new ChatService(context);

        // Act
        await service.LeaveAsync(room.Id, user.Id, CancellationToken.None);

        // Assert
        var updatedMembership = context.ChatMemberships.First(m => m.RoomId == room.Id && m.UserId == user.Id);
        updatedMembership.IsActive.Should().BeFalse();
        updatedMembership.LeftAt.Should().NotBeNull();
    }
}