using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface ITenantOnboardingService
{
    Task<TenantOnboardingResponse> OnboardTenantAsync(TenantOnboardingRequest request);
    Task<bool> ValidateSlugAvailabilityAsync(string slug);
    Task<bool> ValidateEmailAvailabilityAsync(string email);
    Task<string> GenerateUniqueSlugAsync(string companyName);
    Task<string> GenerateSecurePasswordAsync();
}