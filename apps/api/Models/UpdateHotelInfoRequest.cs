namespace Hostr.Api.Models;

public class UpdateHotelInfoRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Logo { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public AddressDto? Address { get; set; }
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }
    public int? NumberOfRooms { get; set; }
    public int? NumberOfFloors { get; set; }
    public int? EstablishedYear { get; set; }
    public string[]? SupportedLanguages { get; set; }
    public string? DefaultLanguage { get; set; }
    public string[]? Features { get; set; }
    public SocialMediaDto? SocialMedia { get; set; }
    public PoliciesDto? Policies { get; set; }
    public SettingsDto? Settings { get; set; }
    public WifiDto? Wifi { get; set; }

    /// <summary>
    /// Comma-separated list of valid room numbers for this property.
    /// Example: "101,102,103,201,202,203"
    /// </summary>
    public string? ValidRooms { get; set; }
}

public class AddressDto
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class SocialMediaDto
{
    public string? Facebook { get; set; }
    public string? Twitter { get; set; }
    public string? Instagram { get; set; }
    public string? Linkedin { get; set; }
}

public class PoliciesDto
{
    public string? CancellationPolicy { get; set; }
    public string? PetPolicy { get; set; }
    public string? SmokingPolicy { get; set; }
    public string? ChildPolicy { get; set; }
}

public class SettingsDto
{
    public bool? AllowOnlineBooking { get; set; }
    public bool? RequirePhoneVerification { get; set; }
    public bool? EnableNotifications { get; set; }
    public bool? EnableChatbot { get; set; }
    public string? Timezone { get; set; }
    public string? Currency { get; set; }
}

public class WifiDto
{
    public string? Network { get; set; }
    public string? Password { get; set; }
}