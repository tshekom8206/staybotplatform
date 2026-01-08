using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Hostr.Api.Models;

public class RoomPreference
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? BookingId { get; set; }
    
    [Required, MaxLength(10)]
    public string RoomNumber { get; set; } = string.Empty;
    
    [Required, MaxLength(50)]
    public string PreferenceType { get; set; } = string.Empty;
    
    [Required]
    public JsonDocument PreferenceValue { get; set; } = null!;
    
    public string? Notes { get; set; }
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Active";
    
    public int? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Booking? Booking { get; set; }
    public virtual User? AcknowledgedByUser { get; set; }
    public virtual ICollection<RoomPreferenceHistory> History { get; set; } = new List<RoomPreferenceHistory>();
}

public class RoomPreferenceHistory
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int RoomPreferenceId { get; set; }
    
    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty;
    
    public int? ChangedBy { get; set; }
    public JsonDocument? OldValue { get; set; }
    public JsonDocument? NewValue { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual RoomPreference RoomPreference { get; set; } = null!;
    public virtual User? ChangedByUser { get; set; }
}

public class CreateRoomPreferenceRequest
{
    [Required]
    public string PreferenceType { get; set; } = string.Empty;
    
    [Required]
    public JsonDocument PreferenceValue { get; set; } = null!;
    
    public string? Notes { get; set; }
}

public class RoomPreferenceResponse
{
    public int Id { get; set; }
    public string PreferenceType { get; set; } = string.Empty;
    public JsonDocument PreferenceValue { get; set; } = null!;
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class StaffRoomPreferenceView
{
    public int Id { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string PreferenceType { get; set; } = string.Empty;
    public string PreferenceLabel { get; set; } = string.Empty;
    public string PreferenceDetails { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AcknowledgedByName { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
