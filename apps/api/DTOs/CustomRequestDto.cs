namespace Hostr.Api.DTOs;

public class CustomRequestDto
{
    public string Description { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public string? Timing { get; set; } // "before-arrival", "check-in", "later", "asap"
    public string? Department { get; set; } // Optional, defaults to "Concierge"
    public string? Source { get; set; } // "prepare_page", "housekeeping_page", etc.
}
