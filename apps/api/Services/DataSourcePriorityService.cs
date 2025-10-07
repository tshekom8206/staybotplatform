using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services
{
    public interface IDataSourcePriorityService
    {
        Task<DataSourceResult> GetPrioritizedDataAsync(string category, string query, TenantContext tenantContext);
        Task<List<DataSource>> GetDataSourcePriorityOrderAsync(int tenantId);
        Task<string?> GetSpecificDataAsync(string category, string key, TenantContext tenantContext);
        Task<bool> HasConfiguredDataAsync(string category, TenantContext tenantContext);
        Task<DataSourceValidationResult> ValidateDataConsistencyAsync(int tenantId);
    }

    public class DataSourcePriorityService : IDataSourcePriorityService
    {
        private readonly HostrDbContext _context;
        private readonly ILogger<DataSourcePriorityService> _logger;

        // Priority order: Higher number = Higher priority
        private readonly Dictionary<string, int> _dataSourcePriorities = new()
        {
            { "HotelInfo", 100 },           // Newest, most comprehensive hotel information
            { "BusinessInfo", 90 },         // Legacy business information
            { "Services", 85 },             // Service-specific configurations
            { "TenantConfiguration", 80 },  // Tenant-specific settings
            { "Amenities", 75 },           // Hotel amenities (legacy reference)
            { "Policies", 70 },            // Hotel policies and rules
            { "ContactInfo", 65 },         // Contact information
            { "FAQ", 60 },                 // Frequently asked questions
            { "KnowledgeBase", 50 },       // General knowledge base chunks
            { "Default", 10 }              // System defaults (lowest priority)
        };

        public DataSourcePriorityService(HostrDbContext context, ILogger<DataSourcePriorityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<DataSourceResult> GetPrioritizedDataAsync(string category, string query, TenantContext tenantContext)
        {
            try
            {
                _logger.LogInformation("Getting prioritized data for category: {Category}, query: {Query}", category, query);

                using var scope = new TenantScope(_context, tenantContext.TenantId);

                var result = new DataSourceResult
                {
                    Category = category,
                    Query = query,
                    FoundData = false,
                    Sources = new List<DataSourceEntry>()
                };

                // Get data from each source in priority order
                var dataSources = await GetDataSourcesForCategoryAsync(category, tenantContext.TenantId);

                foreach (var source in dataSources.OrderByDescending(s => s.Priority))
                {
                    var sourceData = await GetDataFromSourceAsync(source, query, tenantContext.TenantId);

                    if (sourceData != null && !string.IsNullOrWhiteSpace(sourceData.Content))
                    {
                        result.Sources.Add(sourceData);

                        if (!result.FoundData)
                        {
                            result.FoundData = true;
                            result.PrimarySource = source.SourceType;
                            result.PrimaryContent = sourceData.Content;
                            result.Confidence = sourceData.Confidence;
                        }
                    }
                }

                _logger.LogInformation("Data retrieval completed: Found={Found}, Sources={SourceCount}, Primary={Primary}",
                    result.FoundData, result.Sources.Count, result.PrimarySource);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting prioritized data for category: {Category}", category);
                return new DataSourceResult
                {
                    Category = category,
                    Query = query,
                    FoundData = false,
                    Sources = new List<DataSourceEntry>()
                };
            }
        }

        public async Task<List<DataSource>> GetDataSourcePriorityOrderAsync(int tenantId)
        {
            var sources = new List<DataSource>();

            foreach (var priority in _dataSourcePriorities.OrderByDescending(kvp => kvp.Value))
            {
                sources.Add(new DataSource
                {
                    SourceType = priority.Key,
                    Priority = priority.Value,
                    IsAvailable = await CheckSourceAvailabilityAsync(priority.Key, tenantId)
                });
            }

            return sources;
        }

        public async Task<string?> GetSpecificDataAsync(string category, string key, TenantContext tenantContext)
        {
            try
            {
                using var scope = new TenantScope(_context, tenantContext.TenantId);

                // Search in priority order until we find the data
                switch (category.ToLower())
                {
                    case "smoking":
                    case "smoking_policy":
                        return await GetSmokingPolicyDataAsync(tenantContext.TenantId);

                    case "wifi":
                    case "internet":
                        return await GetWifiDataAsync(tenantContext.TenantId);

                    case "checkout":
                    case "checkin":
                        return await GetCheckInOutDataAsync(key, tenantContext.TenantId);

                    case "contact":
                    case "phone":
                        return await GetContactDataAsync(key, tenantContext.TenantId);

                    case "amenities":
                        return await GetAmenityDataAsync(key, tenantContext.TenantId);

                    case "services":
                        return await GetServiceDataAsync(key, tenantContext.TenantId);

                    default:
                        return await GetGenericDataAsync(category, key, tenantContext.TenantId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting specific data: {Category}.{Key}", category, key);
                return null;
            }
        }

        public async Task<bool> HasConfiguredDataAsync(string category, TenantContext tenantContext)
        {
            try
            {
                using var scope = new TenantScope(_context, tenantContext.TenantId);

                return category.ToLower() switch
                {
                    "smoking" => await _context.HotelInfos.AnyAsync(h => !string.IsNullOrEmpty(h.SmokingPolicy)) ||
                                await _context.BusinessInfo.AnyAsync(b => b.Category == "policies"),

                    "wifi" => await _context.BusinessInfo.AnyAsync(b => b.Category == "wifi_credentials") ||
                             await _context.Services.AnyAsync(s => s.Name.ToLower().Contains("wifi")),

                    "amenities" => await _context.Services.AnyAsync(),

                    "services" => await _context.Services.AnyAsync(),

                    "contact" => await _context.HotelInfos.AnyAsync(h => !string.IsNullOrEmpty(h.Phone) || !string.IsNullOrEmpty(h.Email)) ||
                                await _context.BusinessInfo.AnyAsync(b => b.Category == "contact"),

                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if category has configured data: {Category}", category);
                return false;
            }
        }

        public async Task<DataSourceValidationResult> ValidateDataConsistencyAsync(int tenantId)
        {
            try
            {
                using var scope = new TenantScope(_context, tenantId);

                var result = new DataSourceValidationResult
                {
                    TenantId = tenantId,
                    IsConsistent = true,
                    Issues = new List<DataConsistencyIssue>()
                };

                // Check for conflicting smoking policies
                await ValidateSmokingPolicyConsistencyAsync(result);

                // Check for conflicting WiFi information
                await ValidateWifiConsistencyAsync(result);

                // Check for conflicting contact information
                await ValidateContactConsistencyAsync(result);

                // Check for conflicting pricing information
                await ValidatePricingConsistencyAsync(result);

                result.IsConsistent = !result.Issues.Any(i => i.Severity == "High");

                _logger.LogInformation("Data consistency validation completed for tenant {TenantId}: {IsConsistent}",
                    tenantId, result.IsConsistent);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating data consistency for tenant: {TenantId}", tenantId);
                return new DataSourceValidationResult
                {
                    TenantId = tenantId,
                    IsConsistent = false,
                    Issues = new List<DataConsistencyIssue>
                    {
                        new DataConsistencyIssue
                        {
                            Category = "System",
                            Issue = "Validation system error",
                            Severity = "High"
                        }
                    }
                };
            }
        }

        #region Private Helper Methods

        private async Task<List<DataSource>> GetDataSourcesForCategoryAsync(string category, int tenantId)
        {
            var sources = new List<DataSource>();

            // Add relevant sources based on category
            switch (category.ToLower())
            {
                case "smoking":
                case "wifi":
                case "contact":
                case "amenities":
                    sources.AddRange(new[]
                    {
                        new DataSource { SourceType = "HotelInfo", Priority = _dataSourcePriorities["HotelInfo"] },
                        new DataSource { SourceType = "BusinessInfo", Priority = _dataSourcePriorities["BusinessInfo"] },
                        new DataSource { SourceType = "FAQ", Priority = _dataSourcePriorities["FAQ"] }
                    });
                    break;

                case "services":
                    sources.AddRange(new[]
                    {
                        new DataSource { SourceType = "Services", Priority = _dataSourcePriorities["Services"] },
                        new DataSource { SourceType = "HotelInfo", Priority = _dataSourcePriorities["HotelInfo"] },
                        new DataSource { SourceType = "FAQ", Priority = _dataSourcePriorities["FAQ"] }
                    });
                    break;

                default:
                    // For unknown categories, check all sources
                    sources.AddRange(_dataSourcePriorities.Select(kvp =>
                        new DataSource { SourceType = kvp.Key, Priority = kvp.Value }));
                    break;
            }

            // Check availability for each source
            foreach (var source in sources)
            {
                source.IsAvailable = await CheckSourceAvailabilityAsync(source.SourceType, tenantId);
            }

            return sources.Where(s => s.IsAvailable).ToList();
        }

        private async Task<DataSourceEntry?> GetDataFromSourceAsync(DataSource source, string query, int tenantId)
        {
            try
            {
                return source.SourceType switch
                {
                    "HotelInfo" => await GetHotelInfoDataAsync(query, tenantId),
                    "BusinessInfo" => await GetBusinessInfoDataAsync(query, tenantId),
                    "Services" => await GetServicesDataAsync(query, tenantId),
                    "Amenities" => await GetAmenitiesDataAsync(query, tenantId),
                    "FAQ" => await GetFAQDataAsync(query, tenantId),
                    "KnowledgeBase" => await GetKnowledgeBaseDataAsync(query, tenantId),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting data from source: {SourceType}", source.SourceType);
                return null;
            }
        }

        private async Task<DataSourceEntry?> GetHotelInfoDataAsync(string query, int tenantId)
        {
            var hotelInfo = await _context.HotelInfos.FirstOrDefaultAsync();
            if (hotelInfo == null) return null;

            var queryLower = query.ToLower();
            string? content = null;
            double confidence = 0.0;

            if (queryLower.Contains("smok"))
            {
                content = hotelInfo.SmokingPolicy;
                confidence = 0.95;
            }
            else if (queryLower.Contains("contact") || queryLower.Contains("phone"))
            {
                content = !string.IsNullOrEmpty(hotelInfo.Phone) ? $"Phone: {hotelInfo.Phone}" : null;
                if (string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(hotelInfo.Email))
                {
                    content = $"Email: {hotelInfo.Email}";
                }
                confidence = 0.95;
            }
            else if (queryLower.Contains("checkout") || queryLower.Contains("checkin"))
            {
                content = $"Check-in: {hotelInfo.CheckInTime}, Check-out: {hotelInfo.CheckOutTime}";
                confidence = 0.95;
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                return new DataSourceEntry
                {
                    SourceType = "HotelInfo",
                    Content = content,
                    Confidence = confidence,
                    LastUpdated = hotelInfo.UpdatedAt
                };
            }

            return null;
        }

        private async Task<DataSourceEntry?> GetBusinessInfoDataAsync(string query, int tenantId)
        {
            var businessInfo = await _context.BusinessInfo.FirstOrDefaultAsync();
            if (businessInfo == null) return null;

            var queryLower = query.ToLower();
            string? content = null;
            double confidence = 0.0;

            if (queryLower.Contains("smok"))
            {
                // Extract smoking policy from JSON content
                if (!string.IsNullOrEmpty(businessInfo.Content))
                {
                    try
                    {
                        var contentJson = JsonSerializer.Deserialize<Dictionary<string, object>>(businessInfo.Content);
                        content = contentJson?.ContainsKey("smoking") == true ? contentJson["smoking"]?.ToString() : null;
                    }
                    catch { /* Ignore JSON parse errors */ }
                }
                confidence = 0.85; // Lower than HotelInfo
            }
            else if (queryLower.Contains("wifi") || queryLower.Contains("internet"))
            {
                // Extract WiFi password from JSON content
                if (!string.IsNullOrEmpty(businessInfo.Content))
                {
                    try
                    {
                        var contentJson = JsonSerializer.Deserialize<Dictionary<string, object>>(businessInfo.Content);
                        var password = contentJson?.ContainsKey("password") == true ? contentJson["password"]?.ToString() : null;
                        content = !string.IsNullOrEmpty(password) ? $"WiFi Password: {password}" : null;
                    }
                    catch { /* Ignore JSON parse errors */ }
                }
                confidence = 0.85;
            }
            else if (queryLower.Contains("contact") || queryLower.Contains("phone"))
            {
                // Extract contact phone from JSON content
                if (!string.IsNullOrEmpty(businessInfo.Content))
                {
                    try
                    {
                        var contentJson = JsonSerializer.Deserialize<Dictionary<string, object>>(businessInfo.Content);
                        content = contentJson?.ContainsKey("phone") == true ? contentJson["phone"]?.ToString() : null;
                    }
                    catch { /* Ignore JSON parse errors */ }
                }
                confidence = 0.85;
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                return new DataSourceEntry
                {
                    SourceType = "BusinessInfo",
                    Content = content,
                    Confidence = confidence,
                    LastUpdated = businessInfo.UpdatedAt
                };
            }

            return null;
        }

        private async Task<DataSourceEntry?> GetServicesDataAsync(string query, int tenantId)
        {
            var queryLower = query.ToLower();

            var service = await _context.Services
                .FirstOrDefaultAsync(s => s.Name.ToLower().Contains(queryLower) ||
                                        s.Description.ToLower().Contains(queryLower));

            if (service != null)
            {
                var content = $"{service.Name}: {service.Description}";
                if (service.Price.HasValue)
                {
                    content += $" - Price: {service.Price:C}";
                }

                return new DataSourceEntry
                {
                    SourceType = "Services",
                    Content = content,
                    Confidence = 0.90,
                    LastUpdated = service.UpdatedAt
                };
            }

            return null;
        }

        private async Task<DataSourceEntry?> GetAmenitiesDataAsync(string query, int tenantId)
        {
            var queryLower = query.ToLower();

            var service = await _context.Services
                .Where(s => s.TenantId == tenantId && s.IsAvailable)
                .FirstOrDefaultAsync(s => s.Name.ToLower().Contains(queryLower) ||
                                        s.Description.ToLower().Contains(queryLower));

            if (service != null)
            {
                return new DataSourceEntry
                {
                    SourceType = "Services",
                    Content = $"{service.Name}: {service.Description}",
                    Confidence = 0.85,
                    LastUpdated = service.UpdatedAt
                };
            }

            return null;
        }

        private async Task<DataSourceEntry?> GetFAQDataAsync(string query, int tenantId)
        {
            var queryLower = query.ToLower();

            var faq = await _context.FAQs
                .FirstOrDefaultAsync(f => f.Question.ToLower().Contains(queryLower));

            if (faq != null)
            {
                return new DataSourceEntry
                {
                    SourceType = "FAQ",
                    Content = faq.Answer,
                    Confidence = 0.80,
                    LastUpdated = faq.UpdatedAt
                };
            }

            return null;
        }

        private async Task<DataSourceEntry?> GetKnowledgeBaseDataAsync(string query, int tenantId)
        {
            // This would use semantic search with embeddings in a real implementation
            // For now, simple text search
            var queryLower = query.ToLower();

            var chunk = await _context.KnowledgeBaseChunks
                .FirstOrDefaultAsync(k => k.Content.ToLower().Contains(queryLower));

            if (chunk != null)
            {
                return new DataSourceEntry
                {
                    SourceType = "KnowledgeBase",
                    Content = chunk.Content,
                    Confidence = 0.70,
                    LastUpdated = chunk.UpdatedAt
                };
            }

            return null;
        }

        private async Task<bool> CheckSourceAvailabilityAsync(string sourceType, int tenantId)
        {
            using var scope = new TenantScope(_context, tenantId);

            return sourceType switch
            {
                "HotelInfo" => await _context.HotelInfos.AnyAsync(),
                "BusinessInfo" => await _context.BusinessInfo.AnyAsync(),
                "Services" => await _context.Services.AnyAsync(),
                "Amenities" => await _context.Services.AnyAsync(),
                "FAQ" => await _context.FAQs.AnyAsync(),
                "KnowledgeBase" => await _context.KnowledgeBaseChunks.AnyAsync(),
                _ => false
            };
        }

        // Specific data retrieval methods
        private async Task<string?> GetSmokingPolicyDataAsync(int tenantId)
        {
            // Try HotelInfo first (highest priority)
            var hotelInfo = await _context.HotelInfos.FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(hotelInfo?.SmokingPolicy))
            {
                return hotelInfo.SmokingPolicy;
            }

            // Fallback to BusinessInfo
            var businessInfo = await _context.BusinessInfo
                .Where(b => b.Category == "policies")
                .FirstOrDefaultAsync();

            // Extract smoking policy from JSON content
            if (businessInfo != null && !string.IsNullOrEmpty(businessInfo.Content))
            {
                try
                {
                    var content = JsonSerializer.Deserialize<Dictionary<string, object>>(businessInfo.Content);
                    return content?.ContainsKey("smoking") == true ? content["smoking"]?.ToString() : null;
                }
                catch { /* Ignore JSON parse errors */ }
            }

            return null;
        }

        private async Task<string?> GetWifiDataAsync(int tenantId)
        {
            // HotelInfo doesn't have WiFi info, check services and business info

            var businessInfo = await _context.BusinessInfo
                .Where(b => b.Category == "wifi_credentials")
                .FirstOrDefaultAsync();

            // Extract WiFi password from JSON content
            if (businessInfo != null && !string.IsNullOrEmpty(businessInfo.Content))
            {
                try
                {
                    var content = JsonSerializer.Deserialize<Dictionary<string, object>>(businessInfo.Content);
                    var password = content?.ContainsKey("password") == true ? content["password"]?.ToString() : null;
                    return !string.IsNullOrEmpty(password) ? $"WiFi Password: {password}" : null;
                }
                catch { /* Ignore JSON parse errors */ }
            }

            return null;
        }

        private async Task<string?> GetCheckInOutDataAsync(string key, int tenantId)
        {
            var hotelInfo = await _context.HotelInfos.FirstOrDefaultAsync();
            if (hotelInfo != null)
            {
                if (key.ToLower().Contains("checkin") && !string.IsNullOrEmpty(hotelInfo.CheckInTime))
                {
                    return $"Check-in time: {hotelInfo.CheckInTime}";
                }
                else if (key.ToLower().Contains("checkout") && !string.IsNullOrEmpty(hotelInfo.CheckOutTime))
                {
                    return $"Check-out time: {hotelInfo.CheckOutTime}";
                }
            }

            return null;
        }

        private async Task<string?> GetContactDataAsync(string key, int tenantId)
        {
            var hotelInfo = await _context.HotelInfos.FirstOrDefaultAsync();
            if (hotelInfo != null)
            {
                if (!string.IsNullOrEmpty(hotelInfo.Phone))
                {
                    return $"Phone: {hotelInfo.Phone}";
                }
                if (!string.IsNullOrEmpty(hotelInfo.Email))
                {
                    return $"Email: {hotelInfo.Email}";
                }
            }

            var businessInfo = await _context.BusinessInfo
                .Where(b => b.Category == "contact" && b.TenantId == tenantId)
                .FirstOrDefaultAsync();

            // Extract phone from JSON content
            if (businessInfo != null && !string.IsNullOrEmpty(businessInfo.Content))
            {
                try
                {
                    var content = JsonSerializer.Deserialize<Dictionary<string, object>>(businessInfo.Content);
                    if (content?.ContainsKey("phone") == true)
                    {
                        return content["phone"]?.ToString();
                    }
                }
                catch { /* Ignore JSON parse errors */ }
            }

            return null;
        }

        private async Task<string?> GetAmenityDataAsync(string key, int tenantId)
        {
            // Use Services table instead of non-existent Amenities table
            var service = await _context.Services
                .Where(s => s.TenantId == tenantId && s.IsAvailable &&
                           s.Name.ToLower().Contains(key.ToLower()))
                .FirstOrDefaultAsync();

            return service != null ? $"{service.Name}: {service.Description}" : null;
        }

        private async Task<string?> GetServiceDataAsync(string key, int tenantId)
        {
            var service = await _context.Services
                .FirstOrDefaultAsync(s => s.Name.ToLower().Contains(key.ToLower()));

            if (service != null)
            {
                var result = $"{service.Name}: {service.Description}";
                if (service.Price.HasValue)
                {
                    result += $" - Price: {service.Price:C}";
                }
                return result;
            }

            return null;
        }

        private async Task<string?> GetGenericDataAsync(string category, string key, int tenantId)
        {
            // Try to find relevant data in knowledge base or FAQ
            var combinedQuery = $"{category} {key}".ToLower();

            var faq = await _context.FAQs
                .FirstOrDefaultAsync(f => f.Question.ToLower().Contains(combinedQuery) ||
                                        f.Answer.ToLower().Contains(combinedQuery));

            if (faq != null)
            {
                return faq.Answer;
            }

            var chunk = await _context.KnowledgeBaseChunks
                .FirstOrDefaultAsync(k => k.Content.ToLower().Contains(combinedQuery));

            return chunk?.Content;
        }

        // Data consistency validation methods
        private async Task ValidateSmokingPolicyConsistencyAsync(DataSourceValidationResult result)
        {
            var hotelPolicy = await _context.HotelInfos
                .Select(h => h.SmokingPolicy)
                .FirstOrDefaultAsync();

            // Extract smoking policy from BusinessInfo JSON content
            var businessInfo = await _context.BusinessInfo
                .Where(b => b.Category == "policies" && b.TenantId == result.TenantId)
                .FirstOrDefaultAsync();

            string? businessPolicy = null;
            if (businessInfo != null && !string.IsNullOrEmpty(businessInfo.Content))
            {
                try
                {
                    var content = JsonSerializer.Deserialize<Dictionary<string, object>>(businessInfo.Content);
                    businessPolicy = content?.ContainsKey("smoking") == true ? content["smoking"]?.ToString() : null;
                }
                catch { /* Ignore JSON parse errors */ }
            }

            if (!string.IsNullOrEmpty(hotelPolicy) && !string.IsNullOrEmpty(businessPolicy) &&
                !hotelPolicy.Equals(businessPolicy, StringComparison.OrdinalIgnoreCase))
            {
                result.Issues.Add(new DataConsistencyIssue
                {
                    Category = "SmokingPolicy",
                    Issue = "Conflicting smoking policies between HotelInfo and BusinessInfo",
                    Severity = "High",
                    Details = $"HotelInfo: '{hotelPolicy}' vs BusinessInfo: '{businessPolicy}'"
                });
            }
        }

        private async Task ValidateWifiConsistencyAsync(DataSourceValidationResult result)
        {
            // HotelInfo doesn't have WiFi info, only check BusinessInfo

            // Extract WiFi from BusinessInfo JSON content
            var businessWifiInfo = await _context.BusinessInfo
                .Where(b => b.Category == "wifi_credentials" && b.TenantId == result.TenantId)
                .FirstOrDefaultAsync();

            string? businessWifi = null;
            if (businessWifiInfo != null && !string.IsNullOrEmpty(businessWifiInfo.Content))
            {
                try
                {
                    var content = JsonSerializer.Deserialize<Dictionary<string, object>>(businessWifiInfo.Content);
                    businessWifi = content?.ContainsKey("password") == true ? content["password"]?.ToString() : null;
                }
                catch { /* Ignore JSON parse errors */ }
            }

            // Since HotelInfo doesn't have WiFi info, just check if BusinessInfo has it configured
            if (!string.IsNullOrEmpty(businessWifi))
            {
                // WiFi info is only in BusinessInfo, no conflicts to check
                return;
            }
        }

        private async Task ValidateContactConsistencyAsync(DataSourceValidationResult result)
        {
            var hotelInfo = await _context.HotelInfos.FirstOrDefaultAsync();
            var hotelContact = hotelInfo?.Phone ?? hotelInfo?.Email;

            // Extract contact phone from BusinessInfo JSON content
            var businessContactInfo = await _context.BusinessInfo
                .Where(b => b.Category == "contact" && b.TenantId == result.TenantId)
                .FirstOrDefaultAsync();

            string? businessContact = null;
            if (businessContactInfo != null && !string.IsNullOrEmpty(businessContactInfo.Content))
            {
                try
                {
                    var content = JsonSerializer.Deserialize<Dictionary<string, object>>(businessContactInfo.Content);
                    businessContact = content?.ContainsKey("phone") == true ? content["phone"]?.ToString() : null;
                }
                catch { /* Ignore JSON parse errors */ }
            }

            if (!string.IsNullOrEmpty(hotelContact) && !string.IsNullOrEmpty(businessContact))
            {
                // Extract phone numbers and compare
                if (!hotelContact.Contains(businessContact) && !businessContact.Contains(hotelContact))
                {
                    result.Issues.Add(new DataConsistencyIssue
                    {
                        Category = "ContactInfo",
                        Issue = "Potentially conflicting contact information",
                        Severity = "Medium",
                        Details = "Multiple contact configurations found - verify consistency"
                    });
                }
            }
        }

        private async Task ValidatePricingConsistencyAsync(DataSourceValidationResult result)
        {
            // Check for duplicate services with different prices
            var services = await _context.Services
                .GroupBy(s => s.Name.ToLower())
                .Where(g => g.Count() > 1)
                .ToListAsync();

            foreach (var serviceGroup in services)
            {
                var prices = serviceGroup.Select(s => s.Price).Distinct().Where(p => p.HasValue).ToList();
                if (prices.Count > 1)
                {
                    result.Issues.Add(new DataConsistencyIssue
                    {
                        Category = "ServicePricing",
                        Issue = $"Duplicate service '{serviceGroup.Key}' with different prices",
                        Severity = "High",
                        Details = $"Conflicting prices: {string.Join(", ", prices.Select(p => p?.ToString("C") ?? "N/A"))}"
                    });
                }
            }
        }

        #endregion
    }

    // Supporting classes
    public class DataSourceResult
    {
        public string Category { get; set; } = "";
        public string Query { get; set; } = "";
        public bool FoundData { get; set; }
        public string? PrimarySource { get; set; }
        public string? PrimaryContent { get; set; }
        public double Confidence { get; set; }
        public List<DataSourceEntry> Sources { get; set; } = new();
    }

    public class DataSource
    {
        public string SourceType { get; set; } = "";
        public int Priority { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class DataSourceEntry
    {
        public string SourceType { get; set; } = "";
        public string Content { get; set; } = "";
        public double Confidence { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class DataSourceValidationResult
    {
        public int TenantId { get; set; }
        public bool IsConsistent { get; set; }
        public List<DataConsistencyIssue> Issues { get; set; } = new();
    }

    public class DataConsistencyIssue
    {
        public string Category { get; set; } = "";
        public string Issue { get; set; } = "";
        public string Severity { get; set; } = ""; // "Low", "Medium", "High"
        public string? Details { get; set; }
    }
}