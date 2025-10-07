namespace Hostr.Contracts.DTOs.Common;

public record UserDto
{
    public int Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public string Role { get; init; } = string.Empty;
}

public record TenantDto
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Timezone { get; init; } = string.Empty;
    public string Plan { get; init; } = string.Empty;
    public string ThemePrimary { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Dictionary<string, bool> Features { get; init; } = new();
}

public record PaginatedResult<T>
{
    public IEnumerable<T> Items { get; init; } = Enumerable.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public string? Code { get; init; }
}

public record HealthStatus
{
    public string Status { get; init; } = string.Empty;
    public Dictionary<string, object> Checks { get; init; } = new();
}