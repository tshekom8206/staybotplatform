using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class TenantOnboardingRequest
{
    [Required]
    public CompanyInfo CompanyInfo { get; set; } = null!;
    
    [Required]
    public WhatsAppConfig WhatsAppConfig { get; set; } = null!;
    
    [Required]
    public OwnerUser OwnerUser { get; set; } = null!;
    
    public List<StaffUser> StaffUsers { get; set; } = new();
    
    public SeedingOptions SeedingOptions { get; set; } = new();
}

public class CompanyInfo
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Slug { get; set; }
    
    [MaxLength(50)]
    public string Timezone { get; set; } = "UTC";
    
    [MaxLength(20)]
    public string Plan { get; set; } = "Standard"; // Basic|Standard|Premium
    
    [MaxLength(7)]
    public string? ThemePrimary { get; set; }
}

public class WhatsAppConfig
{
    [Required, MaxLength(100)]
    public string WabaId { get; set; } = string.Empty;
    
    [Required, MaxLength(100)]
    public string PhoneNumberId { get; set; } = string.Empty;
    
    [Required, MaxLength(500)]
    public string PageAccessToken { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? VerifyToken { get; set; }
}

public class OwnerUser
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;
    
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Phone, MaxLength(20)]
    public string? PhoneNumber { get; set; }
}

public class StaffUser
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;
    
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Required, MaxLength(20)]
    public string Role { get; set; } = string.Empty; // Manager|FrontDesk|Housekeeping|Maintenance|Concierge
    
    [Phone, MaxLength(20)]
    public string? PhoneNumber { get; set; }
}

public class SeedingOptions
{
    public bool RequestItems { get; set; } = true;
    public bool MenuSystem { get; set; } = false;
    public bool BusinessInfo { get; set; } = true;
    public bool Templates { get; set; } = true;
    public bool EmergencyData { get; set; } = true;
    public bool LostAndFoundCategories { get; set; } = true;
}

public class TenantOnboardingResponse
{
    public bool Success { get; set; }
    public int TenantId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string OnboardingId { get; set; } = string.Empty;
    public UserCredentials Credentials { get; set; } = null!;
    public WhatsAppSetup WhatsAppSetup { get; set; } = null!;
    public List<string> NextSteps { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class UserCredentials
{
    public string OwnerEmail { get; set; } = string.Empty;
    public string TempPassword { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
    public List<StaffCredential> StaffCredentials { get; set; } = new();
}

public class StaffCredential
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TempPassword { get; set; } = string.Empty;
}

public class WhatsAppSetup
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
    public bool ConfigurationValid { get; set; }
}

public class OnboardingResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? TenantId { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}