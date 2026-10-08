namespace StudentProjects.Application.Contracts;

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, Guid UserId, string Email, string FullName, string Role);
public sealed record CurrentUserResponse(Guid UserId, string Email, string FullName, string Role);
