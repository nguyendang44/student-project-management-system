namespace StudentProjects.Application.Contracts;

// Only Student/Lecturer accounts are creatable through v0.4. Admin bootstrap remains separate.
public sealed record UserListItem(Guid Id, string FullName, string Email, string Role,
    bool IsActive, string? StudentCode, string? Faculty, string? Specialty);
public sealed record PagedUsersResponse(IReadOnlyList<UserListItem> Items, int Total, int Page, int PageSize);
public sealed record CreateManagedUserRequest(string? FullName, string? Email, string? Password,
    string? Role, string? StudentCode, string? Faculty, string? Specialty);
public sealed record UpdateManagedUserRequest(string? FullName, string? Email,
    string? StudentCode, string? Faculty, string? Specialty);
public sealed record SetUserStatusRequest(bool? IsActive);
public sealed record UpdateOwnProfileRequest(string? FullName, string? Faculty, string? Specialty);
