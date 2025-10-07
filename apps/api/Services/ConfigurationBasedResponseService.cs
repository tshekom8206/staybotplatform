using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services
{
    public interface IConfigurationBasedResponseService
    {
        Task<ConfigurationResponse> GetConfiguredResponseAsync(string query, int tenantId);
        Task<ValidationResult> ValidateResponseAgainstConfigurationAsync(string response, string originalQuery, int tenantId);
        Task<Dictionary<string, ConfigurationSource>> GetAuthoritativeDataSourcesAsync(int tenantId);
        Task<List<string>> DetectProhibitedContentAsync(string response);
        Task<bool> HasConfiguredInformationAsync(string topic, int tenantId);
    }

    public class ConfigurationBasedResponseService : IConfigurationBasedResponseService
    {
        private readonly HostrDbContext _context;
        private readonly IDataSourcePriorityService _dataSourcePriorityService;
        private readonly ILogger<ConfigurationBasedResponseService> _logger;

        // Prohibited generic phrases that indicate non-configured responses
        private readonly string[] _prohibitedPhrases = new[]
        {
            "typically", "usually", "generally", "most hotels", "in general", "commonly",
            "standard practice", "normally", "as is common", "like most", "standard hotel",
            "typical hotel", "in my experience", "often", "tends to", "might be",
            "probably", "likely", "i believe", "i think", "i assume", "presumably"
        };

        // Keywords that indicate specific information types
        private readonly Dictionary<string, List<string>> _informationCategories = new()
        {
            { "smoking_policy", new List<string> { "smoke", "smoking", "cigarette", "vape", "tobacco" } },
            { "pet_policy", new List<string> { "pet", "dog", "cat", "animal", "pets allowed" } },
            { "cancellation_policy", new List<string> { "cancel", "cancellation", "refund", "booking change" } },
            { "child_policy", new List<string> { "child", "children", "kids", "baby", "infant", "age limit" } },
            { "pricing", new List<string> { "price", "cost", "rate", "fee", "charge", "payment" } },
            { "hours", new List<string> { "hours", "time", "open", "close", "available", "schedule" } },
            { "contact", new List<string> { "phone", "number", "contact", "call", "reach", "speak to" } },
            { "wifi", new List<string> { "wifi", "internet", "password", "network", "connection" } },
            { "parking", new List<string> { "parking", "car", "vehicle", "park" } },
            { "amenities", new List<string> { "pool", "gym", "spa", "restaurant", "bar", "facilities" } },
            { "location", new List<string> { "address", "location", "where", "directions", "distance" } },
            { "check_in_out", new List<string> { "check in", "check out", "checkin", "checkout", "arrival", "departure" } }
        };

        public ConfigurationBasedResponseService(
            HostrDbContext context,
            IDataSourcePriorityService dataSourcePriorityService,
            ILogger<ConfigurationBasedResponseService> logger)
        {
            _context = context;
            _dataSourcePriorityService = dataSourcePriorityService;
            _logger = logger;
        }

        public async Task<ConfigurationResponse> GetConfiguredResponseAsync(string query, int tenantId)
        {
            try
            {
                using var scope = new TenantScope(_context, tenantId);
                var queryLower = query.ToLower();

                _logger.LogInformation("Searching for configured response to query: '{Query}' for tenant {TenantId}", query, tenantId);

                // 1. Check FAQs first (exact matches)
                var faqResponse = await CheckFAQsForExactMatch(queryLower, tenantId);
                if (faqResponse != null)
                {
                    _logger.LogInformation("Found exact FAQ match for query");
                    return faqResponse;
                }

                // 2. Check specific information categories
                var category = DetermineInformationCategory(queryLower);
                if (!string.IsNullOrEmpty(category))
                {
                    var categoryResponse = await GetCategorySpecificResponse(category, tenantId);
                    if (categoryResponse != null)
                    {
                        _logger.LogInformation("Found configured {Category} information", category);
                        return categoryResponse;
                    }
                }

                // 3. Check BusinessInfo for general information
                var businessInfoResponse = await CheckBusinessInfoForMatch(queryLower, tenantId);
                if (businessInfoResponse != null)
                {
                    _logger.LogInformation("Found BusinessInfo match for query");
                    return businessInfoResponse;
                }

                // 4. Check Services for service-related queries
                var serviceResponse = await CheckServicesForMatch(queryLower, tenantId);
                if (serviceResponse != null)
                {
                    _logger.LogInformation("Found Service match for query");
                    return serviceResponse;
                }

                // No configured information found
                _logger.LogWarning("No configured information found for query: '{Query}'", query);
                return new ConfigurationResponse
                {
                    HasConfiguredData = false,
                    Response = "I don't have that specific information in my current database. Let me connect you with our staff who can provide you with accurate, up-to-date details.",
                    DataSource = ConfigurationSource.None,
                    RequiresStaffEscalation = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configured response for query: '{Query}'", query);
                return new ConfigurationResponse
                {
                    HasConfiguredData = false,
                    Response = "I'm experiencing a technical issue accessing our information database. Please let me connect you with our staff for assistance.",
                    DataSource = ConfigurationSource.Error,
                    RequiresStaffEscalation = true
                };
            }
        }

        public async Task<ValidationResult> ValidateResponseAgainstConfigurationAsync(string response, string originalQuery, int tenantId)
        {
            var result = new ValidationResult
            {
                IsValid = true,
                Issues = new List<ValidationIssue>()
            };

            try
            {
                // 1. Check for prohibited generic phrases
                var prohibitedContent = await DetectProhibitedContentAsync(response);
                if (prohibitedContent.Any())
                {
                    result.IsValid = false;
                    result.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationType.ContentAppropriate,
                        Severity = ValidationSeverity.Error,
                        Description = $"Response contains prohibited generic phrases: {string.Join(", ", prohibitedContent)}",
                        ConfidenceLevel = 0.0
                    });
                }

                // 2. Check if response contains information that should be configured
                var category = DetermineInformationCategory(originalQuery.ToLower());
                if (!string.IsNullOrEmpty(category))
                {
                    var hasConfiguredData = await HasConfiguredInformationAsync(category, tenantId);
                    if (hasConfiguredData)
                    {
                        // Verify response uses configured data
                        var configuredResponse = await GetCategorySpecificResponse(category, tenantId);
                        if (configuredResponse != null && !ResponseMatchesConfiguration(response, configuredResponse))
                        {
                            result.Issues.Add(new ValidationIssue
                            {
                                Type = ValidationType.BusinessLogic,
                                Severity = ValidationSeverity.Error,
                                Description = $"Response doesn't match configured {category} information",
                                ConfidenceLevel = 0.0
                            });
                        }
                    }
                }

                // 3. Check for unverified factual claims
                if (ContainsUnverifiedFactualClaims(response))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationType.FactualAccuracy,
                        Severity = ValidationSeverity.Warning,
                        Description = "Response contains potentially unverified factual claims",
                        ConfidenceLevel = 0.70
                    });
                }

                // Calculate overall score
                if (result.Issues.Any(i => i.Severity == ValidationSeverity.Error))
                {
                    result.IsValid = false;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating response against configuration");
                return new ValidationResult
                {
                    IsValid = false,
                    Issues = new List<ValidationIssue>
                    {
                        new ValidationIssue
                        {
                            Type = ValidationType.DataIntegrity,
                            Severity = ValidationSeverity.Critical,
                            Description = "Validation system error",
                            ConfidenceLevel = 0.0
                        }
                    }
                };
            }
        }

        public async Task<Dictionary<string, ConfigurationSource>> GetAuthoritativeDataSourcesAsync(int tenantId)
        {
            var authoritativeSources = new Dictionary<string, ConfigurationSource>
            {
                { "smoking_policy", ConfigurationSource.HotelInfo },
                { "pet_policy", ConfigurationSource.HotelInfo },
                { "cancellation_policy", ConfigurationSource.HotelInfo },
                { "child_policy", ConfigurationSource.HotelInfo },
                { "check_in_out", ConfigurationSource.HotelInfo },
                { "amenities", ConfigurationSource.Services },
                { "pricing", ConfigurationSource.Services },
                { "hours", ConfigurationSource.Services },
                { "wifi", ConfigurationSource.BusinessInfo },
                { "contact", ConfigurationSource.EmergencyContacts },
                { "location", ConfigurationSource.BusinessInfo },
                { "general_info", ConfigurationSource.BusinessInfo }
            };

            return authoritativeSources;
        }

        public async Task<List<string>> DetectProhibitedContentAsync(string response)
        {
            var detectedProhibited = new List<string>();
            var responseLower = response.ToLower();

            foreach (var phrase in _prohibitedPhrases)
            {
                if (responseLower.Contains(phrase))
                {
                    detectedProhibited.Add(phrase);
                }
            }

            return detectedProhibited;
        }

        public async Task<bool> HasConfiguredInformationAsync(string topic, int tenantId)
        {
            try
            {
                using var scope = new TenantScope(_context, tenantId);

                return topic switch
                {
                    "smoking_policy" => await _context.HotelInfos.AnyAsync(h => h.TenantId == tenantId && !string.IsNullOrEmpty(h.SmokingPolicy)),
                    "pet_policy" => await _context.HotelInfos.AnyAsync(h => h.TenantId == tenantId && !string.IsNullOrEmpty(h.PetPolicy)),
                    "cancellation_policy" => await _context.HotelInfos.AnyAsync(h => h.TenantId == tenantId && !string.IsNullOrEmpty(h.CancellationPolicy)),
                    "child_policy" => await _context.HotelInfos.AnyAsync(h => h.TenantId == tenantId && !string.IsNullOrEmpty(h.ChildPolicy)),
                    "wifi" => await _context.BusinessInfo.AnyAsync(b => b.TenantId == tenantId && b.Category == "wifi_credentials"),
                    "amenities" => await _context.Services.AnyAsync(s => s.TenantId == tenantId && s.IsAvailable),
                    _ => await _context.BusinessInfo.AnyAsync(b => b.TenantId == tenantId && b.IsActive)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if configuration exists for topic: {Topic}", topic);
                return false;
            }
        }

        private async Task<ConfigurationResponse?> CheckFAQsForExactMatch(string query, int tenantId)
        {
            try
            {
                var faq = await _context.FAQs
                    .Where(f => f.TenantId == tenantId)
                    .FirstOrDefaultAsync(f => f.Question.ToLower().Contains(query) || query.Contains(f.Question.ToLower()));

                if (faq != null)
                {
                    return new ConfigurationResponse
                    {
                        HasConfiguredData = true,
                        Response = faq.Answer,
                        DataSource = ConfigurationSource.FAQ,
                        ConfigurationReference = $"FAQ #{faq.Id}"
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking FAQs for query: {Query}", query);
                return null;
            }
        }

        private string DetermineInformationCategory(string query)
        {
            foreach (var category in _informationCategories)
            {
                if (category.Value.Any(keyword => query.Contains(keyword)))
                {
                    return category.Key;
                }
            }
            return "";
        }

        private async Task<ConfigurationResponse?> GetCategorySpecificResponse(string category, int tenantId)
        {
            try
            {
                using var scope = new TenantScope(_context, tenantId);

                switch (category)
                {
                    case "smoking_policy":
                        var smokingPolicy = await _context.HotelInfos
                            .Where(h => h.TenantId == tenantId && !string.IsNullOrEmpty(h.SmokingPolicy))
                            .Select(h => h.SmokingPolicy)
                            .FirstOrDefaultAsync();

                        if (!string.IsNullOrEmpty(smokingPolicy))
                        {
                            return new ConfigurationResponse
                            {
                                HasConfiguredData = true,
                                Response = smokingPolicy,
                                DataSource = ConfigurationSource.HotelInfo,
                                ConfigurationReference = "HotelInfo.SmokingPolicy"
                            };
                        }
                        break;

                    case "pet_policy":
                        var petPolicy = await _context.HotelInfos
                            .Where(h => h.TenantId == tenantId && !string.IsNullOrEmpty(h.PetPolicy))
                            .Select(h => h.PetPolicy)
                            .FirstOrDefaultAsync();

                        if (!string.IsNullOrEmpty(petPolicy))
                        {
                            return new ConfigurationResponse
                            {
                                HasConfiguredData = true,
                                Response = petPolicy,
                                DataSource = ConfigurationSource.HotelInfo,
                                ConfigurationReference = "HotelInfo.PetPolicy"
                            };
                        }
                        break;

                    case "cancellation_policy":
                        var cancellationPolicy = await _context.HotelInfos
                            .Where(h => h.TenantId == tenantId && !string.IsNullOrEmpty(h.CancellationPolicy))
                            .Select(h => h.CancellationPolicy)
                            .FirstOrDefaultAsync();

                        if (!string.IsNullOrEmpty(cancellationPolicy))
                        {
                            return new ConfigurationResponse
                            {
                                HasConfiguredData = true,
                                Response = cancellationPolicy,
                                DataSource = ConfigurationSource.HotelInfo,
                                ConfigurationReference = "HotelInfo.CancellationPolicy"
                            };
                        }
                        break;

                    case "wifi":
                        var wifiInfo = await _context.BusinessInfo
                            .Where(b => b.TenantId == tenantId && b.Category == "wifi_credentials")
                            .FirstOrDefaultAsync();

                        if (wifiInfo != null)
                        {
                            return new ConfigurationResponse
                            {
                                HasConfiguredData = true,
                                Response = wifiInfo.Content,
                                DataSource = ConfigurationSource.BusinessInfo,
                                ConfigurationReference = $"BusinessInfo #{wifiInfo.Id}"
                            };
                        }
                        break;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category-specific response for: {Category}", category);
                return null;
            }
        }

        private async Task<ConfigurationResponse?> CheckBusinessInfoForMatch(string query, int tenantId)
        {
            try
            {
                var businessInfo = await _context.BusinessInfo
                    .Where(b => b.TenantId == tenantId && b.IsActive)
                    .Where(b => b.Title.ToLower().Contains(query) || b.Content.ToLower().Contains(query))
                    .FirstOrDefaultAsync();

                if (businessInfo != null)
                {
                    return new ConfigurationResponse
                    {
                        HasConfiguredData = true,
                        Response = $"{businessInfo.Title}: {businessInfo.Content}",
                        DataSource = ConfigurationSource.BusinessInfo,
                        ConfigurationReference = $"BusinessInfo #{businessInfo.Id}"
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking BusinessInfo for query: {Query}", query);
                return null;
            }
        }

        private async Task<ConfigurationResponse?> CheckServicesForMatch(string query, int tenantId)
        {
            try
            {
                var service = await _context.Services
                    .Where(s => s.TenantId == tenantId && s.IsAvailable)
                    .Where(s => s.Name.ToLower().Contains(query) || s.Description.ToLower().Contains(query))
                    .FirstOrDefaultAsync();

                if (service != null)
                {
                    var response = $"{service.Name}: {service.Description}";
                    if (service.IsChargeable && service.Price.HasValue)
                    {
                        response += $" - {service.Currency}{service.Price} {service.PricingUnit ?? ""}";
                    }
                    if (!string.IsNullOrEmpty(service.AvailableHours))
                    {
                        response += $" (Available: {service.AvailableHours})";
                    }

                    return new ConfigurationResponse
                    {
                        HasConfiguredData = true,
                        Response = response,
                        DataSource = ConfigurationSource.Services,
                        ConfigurationReference = $"Service #{service.Id}"
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Services for query: {Query}", query);
                return null;
            }
        }

        private bool ResponseMatchesConfiguration(string response, ConfigurationResponse configuredResponse)
        {
            // Simple similarity check - could be enhanced with more sophisticated matching
            var responseLower = response.ToLower();
            var configuredLower = configuredResponse.Response.ToLower();

            // Check if key terms from configured response appear in the actual response
            var configuredWords = configuredLower.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3).ToList();

            var matchingWords = configuredWords.Count(word => responseLower.Contains(word));
            var matchPercentage = (double)matchingWords / configuredWords.Count;

            return matchPercentage >= 0.6; // 60% of key words should match
        }

        private bool ContainsUnverifiedFactualClaims(string response)
        {
            var responseLower = response.ToLower();

            // Look for specific claims that should have configuration backing
            var factualClaimIndicators = new[]
            {
                "costs", "price is", "fee of", "charge of", "located at", "address is",
                "open from", "closes at", "available until", "phone number is",
                "hours are", "policy states", "rules are", "fee for"
            };

            return factualClaimIndicators.Any(indicator => responseLower.Contains(indicator));
        }
    }

    // Supporting classes
    public class ConfigurationResponse
    {
        public bool HasConfiguredData { get; set; }
        public string Response { get; set; } = "";
        public ConfigurationSource DataSource { get; set; }
        public string ConfigurationReference { get; set; } = "";
        public bool RequiresStaffEscalation { get; set; }
    }

    public enum ConfigurationSource
    {
        None,
        FAQ,
        HotelInfo,
        BusinessInfo,
        Services,
        EmergencyContacts,
        Error
    }

}