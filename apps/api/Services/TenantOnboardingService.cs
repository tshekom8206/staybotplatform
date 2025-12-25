using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Templates;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Hostr.Api.Services;

public class TenantOnboardingService : ITenantOnboardingService
{
    private readonly HostrDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<TenantOnboardingService> _logger;
    private readonly IConfiguration _configuration;

    public TenantOnboardingService(
        HostrDbContext context,
        UserManager<User> userManager,
        ILogger<TenantOnboardingService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<TenantOnboardingResponse> OnboardTenantAsync(TenantOnboardingRequest request)
    {
        var onboardingId = Guid.NewGuid().ToString();
        
        _logger.LogInformation("Starting tenant onboarding process {OnboardingId} for {CompanyName}", 
            onboardingId, request.CompanyInfo.Name);

        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // Step 1: Validate request
            var validationResult = await ValidateOnboardingRequestAsync(request);
            if (!validationResult.IsValid)
            {
                return new TenantOnboardingResponse
                {
                    Success = false,
                    ErrorMessage = validationResult.ErrorMessage,
                    OnboardingId = onboardingId
                };
            }

            // Step 2: Generate unique slug if not provided
            var slug = string.IsNullOrWhiteSpace(request.CompanyInfo.Slug) 
                ? await GenerateUniqueSlugAsync(request.CompanyInfo.Name)
                : request.CompanyInfo.Slug;

            // Step 3: Create tenant
            var tenant = await CreateTenantAsync(request.CompanyInfo, slug);
            
            // Step 4: Create WhatsApp configuration
            var whatsAppNumber = await CreateWhatsAppConfigurationAsync(tenant.Id, request.WhatsAppConfig);
            
            // Step 5: Create owner user
            var ownerCredentials = await CreateOwnerUserAsync(tenant.Id, request.OwnerUser);
            
            // Step 6: Create staff users
            var staffCredentials = await CreateStaffUsersAsync(tenant.Id, request.StaffUsers);
            
            // Step 7: Seed data based on options
            var seededData = await SeedTenantDataAsync(tenant.Id, request.SeedingOptions);

            await transaction.CommitAsync();

            _logger.LogInformation("Tenant onboarding completed successfully for {TenantId} - {CompanyName}", 
                tenant.Id, request.CompanyInfo.Name);

            return new TenantOnboardingResponse
            {
                Success = true,
                TenantId = tenant.Id,
                Slug = tenant.Slug,
                OnboardingId = onboardingId,
                Credentials = new UserCredentials
                {
                    OwnerEmail = request.OwnerUser.Email,
                    TempPassword = ownerCredentials.Password,
                    LoginUrl = GetLoginUrl(),
                    StaffCredentials = staffCredentials
                },
                WhatsAppSetup = new WhatsAppSetup
                {
                    WebhookUrl = GetWebhookUrl(),
                    VerifyToken = whatsAppNumber.PhoneNumberId, // Using as verify token for simplicity
                    ConfigurationValid = true
                },
                NextSteps = GenerateNextSteps(request.CompanyInfo.Name, slug)
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Tenant onboarding failed for {OnboardingId}", onboardingId);
            
            return new TenantOnboardingResponse
            {
                Success = false,
                ErrorMessage = "An error occurred during onboarding. Please try again.",
                OnboardingId = onboardingId
            };
        }
    }

    private async Task<(bool IsValid, string? ErrorMessage)> ValidateOnboardingRequestAsync(TenantOnboardingRequest request)
    {
        // Check slug availability
        if (!string.IsNullOrWhiteSpace(request.CompanyInfo.Slug))
        {
            var slugExists = await ValidateSlugAvailabilityAsync(request.CompanyInfo.Slug);
            if (!slugExists)
            {
                return (false, $"Slug '{request.CompanyInfo.Slug}' is already taken.");
            }
        }

        // Check email availability
        var emailExists = await ValidateEmailAvailabilityAsync(request.OwnerUser.Email);
        if (!emailExists)
        {
            return (false, $"Email '{request.OwnerUser.Email}' is already registered.");
        }

        // Check staff email availability
        foreach (var staffUser in request.StaffUsers)
        {
            var staffEmailExists = await ValidateEmailAvailabilityAsync(staffUser.Email);
            if (!staffEmailExists)
            {
                return (false, $"Staff email '{staffUser.Email}' is already registered.");
            }
        }

        return (true, null);
    }

    private async Task<Tenant> CreateTenantAsync(CompanyInfo companyInfo, string slug)
    {
        var tenant = new Tenant
        {
            Name = companyInfo.Name,
            Slug = slug,
            Timezone = companyInfo.Timezone,
            Plan = companyInfo.Plan,
            ThemePrimary = companyInfo.ThemePrimary ?? "#2563eb",
            Status = "Active",
            RetentionDays = companyInfo.Plan switch
            {
                "Premium" => 365,
                "Standard" => 180,
                _ => 90
            },
            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created tenant {TenantId} - {TenantName}", tenant.Id, tenant.Name);
        return tenant;
    }

    private async Task<WhatsAppNumber> CreateWhatsAppConfigurationAsync(int tenantId, WhatsAppConfig config)
    {
        var whatsAppNumber = new WhatsAppNumber
        {
            TenantId = tenantId,
            WabaId = config.WabaId,
            PhoneNumberId = config.PhoneNumberId,
            PageAccessToken = config.PageAccessToken,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        _context.WhatsAppNumbers.Add(whatsAppNumber);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created WhatsApp configuration for tenant {TenantId}", tenantId);
        return whatsAppNumber;
    }

    private async Task<(int UserId, string Password)> CreateOwnerUserAsync(int tenantId, OwnerUser ownerUser)
    {
        var tempPassword = await GenerateSecurePasswordAsync();
        
        var user = new User
        {
            UserName = ownerUser.Email,
            Email = ownerUser.Email,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
        {
            throw new Exception($"Failed to create owner user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        // Create UserTenant relationship
        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenantId,
            Role = "Owner",
            CreatedAt = DateTime.UtcNow
        };

        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created owner user {UserId} for tenant {TenantId}", user.Id, tenantId);
        return (user.Id, tempPassword);
    }

    private async Task<List<StaffCredential>> CreateStaffUsersAsync(int tenantId, List<StaffUser> staffUsers)
    {
        var staffCredentials = new List<StaffCredential>();

        foreach (var staffUser in staffUsers)
        {
            var tempPassword = await GenerateSecurePasswordAsync();
            
            var user = new User
            {
                UserName = staffUser.Email,
                Email = staffUser.Email,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create staff user {staffUser.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            // Create UserTenant relationship
            var userTenant = new UserTenant
            {
                UserId = user.Id,
                TenantId = tenantId,
                Role = staffUser.Role,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserTenants.Add(userTenant);
            
            staffCredentials.Add(new StaffCredential
            {
                Email = staffUser.Email,
                Role = staffUser.Role,
                TempPassword = tempPassword
            });

            _logger.LogInformation("Created staff user {UserId} with role {Role} for tenant {TenantId}", 
                user.Id, staffUser.Role, tenantId);
        }

        await _context.SaveChangesAsync();
        return staffCredentials;
    }

    private async Task<Dictionary<string, int>> SeedTenantDataAsync(int tenantId, SeedingOptions options)
    {
        var seededCounts = new Dictionary<string, int>();

        if (options.RequestItems)
        {
            var requestItems = DefaultDataTemplates.RequestItems.GetAllDefaultItems(tenantId);
            _context.RequestItems.AddRange(requestItems);
            seededCounts["RequestItems"] = requestItems.Count;
        }

        if (options.BusinessInfo)
        {
            var businessInfo = DefaultDataTemplates.BusinessInfoTemplates.GetEssentialBusinessInfo(tenantId);
            _context.BusinessInfo.AddRange(businessInfo);
            seededCounts["BusinessInfo"] = businessInfo.Count;
        }

        if (options.EmergencyData)
        {
            var emergencyTypes = DefaultDataTemplates.EmergencyTypes.GetDefaultEmergencyTypes(tenantId);
            _context.EmergencyTypes.AddRange(emergencyTypes);
            seededCounts["EmergencyTypes"] = emergencyTypes.Count;
        }

        if (options.LostAndFoundCategories)
        {
            var categories = DefaultDataTemplates.LostAndFoundCategories.GetDefaultCategories(tenantId);
            _context.LostAndFoundCategories.AddRange(categories);
            seededCounts["LostAndFoundCategories"] = categories.Count;
        }

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Seeded data for tenant {TenantId}: {SeededData}", 
            tenantId, string.Join(", ", seededCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}")));

        return seededCounts;
    }

    public async Task<bool> ValidateSlugAvailabilityAsync(string slug)
    {
        var exists = await _context.Tenants.AnyAsync(t => t.Slug == slug.ToLower());
        return !exists;
    }

    public async Task<bool> ValidateEmailAvailabilityAsync(string email)
    {
        var exists = await _userManager.FindByEmailAsync(email);
        return exists == null;
    }

    public async Task<string> GenerateUniqueSlugAsync(string companyName)
    {
        // Convert company name to slug format
        var baseSlug = Regex.Replace(companyName.ToLower(), @"[^a-z0-9\s-]", "")
                           .Trim()
                           .Replace(' ', '-')
                           .Replace("--", "-");

        var slug = baseSlug;
        var counter = 1;

        while (!await ValidateSlugAvailabilityAsync(slug))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        return slug;
    }

    public Task<string> GenerateSecurePasswordAsync()
    {
        const string upperChars = "ABCDEFGHJKLMNOPQRSTUVWXYZ";
        const string lowerChars = "abcdefghijkmnopqrstuvwxyz"; 
        const string digitChars = "0123456789";
        const string symbolChars = "!@#$%^&*";
        
        var random = new Random();
        var password = new List<char>();
        
        // Ensure at least one character from each required category
        password.Add(upperChars[random.Next(upperChars.Length)]); // Uppercase
        password.Add(lowerChars[random.Next(lowerChars.Length)]); // Lowercase
        password.Add(digitChars[random.Next(digitChars.Length)]); // Digit
        password.Add(symbolChars[random.Next(symbolChars.Length)]); // Special char
        
        // Fill remaining 8 characters with random selection from all categories
        const string allChars = upperChars + lowerChars + digitChars + symbolChars;
        for (int i = 4; i < 12; i++)
        {
            password.Add(allChars[random.Next(allChars.Length)]);
        }
        
        // Shuffle the password to randomize position of required characters
        for (int i = password.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }
        
        return Task.FromResult(new string(password.ToArray()));
    }

    private string GetLoginUrl()
    {
        return _configuration["App:FrontendUrl"] ?? "https://app.hostr.com/login";
    }

    private string GetWebhookUrl()
    {
        var baseUrl = _configuration["App:ApiBaseUrl"] ?? "https://api.hostr.com";
        return $"{baseUrl}/webhook";
    }

    private List<string> GenerateNextSteps(string companyName, string slug)
    {
        return new List<string>
        {
            "Configure WhatsApp webhook URL in your Meta Business account",
            "Owner should log in and change the temporary password",
            "Review and customize the pre-populated RequestItems for your hotel",
            "Update WiFi credentials and hotel information in Business Info",
            "Add your hotel's specific menu items if using room service",
            "Test the WhatsApp integration by sending a message",
            $"Your hotel dashboard will be available at: /dashboard/{slug}"
        };
    }
}