namespace Infrastructure.Models;

/// <summary>
/// Authentication response DTO returned from sign-up and sign-in endpoints.
/// </summary>
public sealed class AuthResponseDto
{
    public string Token { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}
