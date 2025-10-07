using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services
{
    public interface IConfigurationFirstPipelineService
    {
        Task<PipelineResponse> ProcessMessageAsync(string message, TenantContext tenantContext);
        Task<bool> BypassLLMIfConfiguredAsync(string message, TenantContext tenantContext);
        Task<PipelineResponse> GetDirectConfiguredResponseAsync(string message, TenantContext tenantContext);
        Task<string> EnhanceSystemPromptWithConfigurationAsync(string basePrompt, TenantContext tenantContext);
        Task<PipelineValidationResult> ValidatePipelineConfigurationAsync(int tenantId);
    }

    public class ConfigurationFirstPipelineService : IConfigurationFirstPipelineService
    {
        private readonly IDataSourcePriorityService _dataSourcePriorityService;
        private readonly IConfigurationBasedResponseService _configurationBasedResponseService;
        private readonly IResponseValidationService _responseValidationService;
        private readonly ILogger<ConfigurationFirstPipelineService> _logger;

        // Configuration-first patterns that should bypass LLM entirely
        private readonly Dictionary<string, List<string>> _directConfigurationPatterns = new()
        {
            { "smoking_policy", new List<string> { "smoke", "smoking", "cigarette", "vape", "tobacco", "where can i smoke" } },
            { "wifi_info", new List<string> { "wifi", "internet", "password", "network name", "connection", "wi-fi" } },
            { "check_times", new List<string> { "check in", "check out", "checkin", "checkout", "arrival time", "departure time" } },
            { "contact_info", new List<string> { "phone number", "contact", "reception", "front desk", "call", "reach" } },
            { "pricing_info", new List<string> { "price", "cost", "rate", "how much", "charges", "fees" } },
            { "location_info", new List<string> { "address", "location", "where are you", "directions", "how to get" } },
            { "hours_info", new List<string> { "hours", "open", "close", "time", "schedule", "when do you" } }
        };

        public ConfigurationFirstPipelineService(
            IDataSourcePriorityService dataSourcePriorityService,
            IConfigurationBasedResponseService configurationBasedResponseService,
            IResponseValidationService responseValidationService,
            ILogger<ConfigurationFirstPipelineService> logger)
        {
            _dataSourcePriorityService = dataSourcePriorityService;
            _configurationBasedResponseService = configurationBasedResponseService;
            _responseValidationService = responseValidationService;
            _logger = logger;
        }

        public async Task<PipelineResponse> ProcessMessageAsync(string message, TenantContext tenantContext)
        {
            try
            {
                _logger.LogInformation("Starting configuration-first pipeline for message: '{Message}'", message);

                var response = new PipelineResponse
                {
                    Message = message,
                    TenantId = tenantContext.TenantId,
                    ProcessingSteps = new List<ProcessingStep>()
                };

                // Step 1: Check if we can provide a direct configured response (bypass LLM)
                response.ProcessingSteps.Add(new ProcessingStep
                {
                    StepName = "Direct Configuration Check",
                    StartTime = DateTime.UtcNow
                });

                var directResponse = await GetDirectConfiguredResponseAsync(message, tenantContext);

                response.ProcessingSteps.Last().EndTime = DateTime.UtcNow;
                response.ProcessingSteps.Last().Success = directResponse.HasDirectResponse;

                if (directResponse.HasDirectResponse)
                {
                    response.FinalResponse = directResponse.Response;
                    response.Source = "DirectConfiguration";
                    response.BypassedLLM = true;
                    response.Confidence = directResponse.Confidence;

                    _logger.LogInformation("Direct configured response found, bypassing LLM");
                    return response;
                }

                // Step 2: If no direct response, enhance system prompt with configuration context
                response.ProcessingSteps.Add(new ProcessingStep
                {
                    StepName = "System Prompt Enhancement",
                    StartTime = DateTime.UtcNow
                });

                var enhancedPrompt = await EnhanceSystemPromptWithConfigurationAsync("", tenantContext);

                response.ProcessingSteps.Last().EndTime = DateTime.UtcNow;
                response.ProcessingSteps.Last().Success = !string.IsNullOrEmpty(enhancedPrompt);
                response.ProcessingSteps.Last().Details = $"Enhanced prompt length: {enhancedPrompt.Length} characters";

                // Step 3: Set up for LLM processing with strict configuration boundaries
                response.Source = "LLMWithConfigurationValidation";
                response.BypassedLLM = false;
                response.EnhancedSystemPrompt = enhancedPrompt;
                response.RequiresLLMProcessing = true;

                _logger.LogInformation("Configuration-first pipeline completed, requires LLM processing with enhanced prompt");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in configuration-first pipeline for message: '{Message}'", message);

                return new PipelineResponse
                {
                    Message = message,
                    TenantId = tenantContext.TenantId,
                    Source = "Error",
                    FinalResponse = "I'm having trouble processing your request. Let me connect you with our front desk for assistance.",
                    ProcessingSteps = new List<ProcessingStep>
                    {
                        new ProcessingStep
                        {
                            StepName = "Error Handling",
                            StartTime = DateTime.UtcNow,
                            EndTime = DateTime.UtcNow,
                            Success = false,
                            Details = ex.Message
                        }
                    }
                };
            }
        }

        public async Task<bool> BypassLLMIfConfiguredAsync(string message, TenantContext tenantContext)
        {
            try
            {
                var messageLower = message.ToLower();

                // Check if message matches direct configuration patterns
                foreach (var pattern in _directConfigurationPatterns)
                {
                    if (pattern.Value.Any(keyword => messageLower.Contains(keyword)))
                    {
                        // Check if we have configured data for this category
                        var hasConfiguredData = await _dataSourcePriorityService.HasConfiguredDataAsync(pattern.Key, tenantContext);

                        if (hasConfiguredData)
                        {
                            _logger.LogInformation("Direct configuration available for pattern: {Pattern}", pattern.Key);
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if LLM can be bypassed");
                return false;
            }
        }

        public async Task<PipelineResponse> GetDirectConfiguredResponseAsync(string message, TenantContext tenantContext)
        {
            try
            {
                var response = new PipelineResponse
                {
                    Message = message,
                    TenantId = tenantContext.TenantId,
                    HasDirectResponse = false
                };

                var messageLower = message.ToLower();

                // Check each configuration pattern
                foreach (var pattern in _directConfigurationPatterns)
                {
                    if (pattern.Value.Any(keyword => messageLower.Contains(keyword)))
                    {
                        var configuredData = await _dataSourcePriorityService.GetSpecificDataAsync(
                            pattern.Key, message, tenantContext);

                        if (!string.IsNullOrEmpty(configuredData))
                        {
                            response.HasDirectResponse = true;
                            response.Response = FormatDirectResponse(pattern.Key, configuredData, message);
                            response.Confidence = 0.95; // High confidence for direct configuration
                            response.Source = "DirectConfiguration";

                            _logger.LogInformation("Direct configured response found for pattern: {Pattern}", pattern.Key);
                            break;
                        }
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting direct configured response");
                return new PipelineResponse
                {
                    Message = message,
                    TenantId = tenantContext.TenantId,
                    HasDirectResponse = false
                };
            }
        }

        public async Task<string> EnhanceSystemPromptWithConfigurationAsync(string basePrompt, TenantContext tenantContext)
        {
            try
            {
                var enhancedPrompt = new System.Text.StringBuilder();

                // Add configuration context to system prompt
                enhancedPrompt.AppendLine("## CURRENT PROPERTY CONFIGURATION DATA");
                enhancedPrompt.AppendLine();

                // Get all available data sources and their priority
                var dataSources = await _dataSourcePriorityService.GetDataSourcePriorityOrderAsync(tenantContext.TenantId);

                foreach (var source in dataSources.Where(s => s.IsAvailable).OrderByDescending(s => s.Priority))
                {
                    var sourceData = await GetSourceDataForPrompt(source.SourceType, tenantContext);
                    if (!string.IsNullOrEmpty(sourceData))
                    {
                        enhancedPrompt.AppendLine($"### {source.SourceType.ToUpper()} (Priority: {source.Priority})");
                        enhancedPrompt.AppendLine(sourceData);
                        enhancedPrompt.AppendLine();
                    }
                }

                // Add strict validation instructions
                enhancedPrompt.AppendLine("## STRICT RESPONSE VALIDATION REQUIREMENTS");
                enhancedPrompt.AppendLine("- ONLY use information from the configuration sections above");
                enhancedPrompt.AppendLine("- If information is not explicitly listed above, respond with: 'I don't have that specific information configured. Let me connect you with our front desk for accurate details.'");
                enhancedPrompt.AppendLine("- NEVER use generic hotel industry knowledge");
                enhancedPrompt.AppendLine("- NEVER use words like 'typically', 'usually', 'most hotels', 'generally'");
                enhancedPrompt.AppendLine();

                // Add base prompt if provided
                if (!string.IsNullOrEmpty(basePrompt))
                {
                    enhancedPrompt.AppendLine("## BASE INSTRUCTIONS");
                    enhancedPrompt.AppendLine(basePrompt);
                }

                var result = enhancedPrompt.ToString();
                _logger.LogInformation("Enhanced system prompt created with {Length} characters", result.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enhancing system prompt with configuration");
                return basePrompt ?? "";
            }
        }

        public async Task<PipelineValidationResult> ValidatePipelineConfigurationAsync(int tenantId)
        {
            try
            {
                var result = new PipelineValidationResult
                {
                    TenantId = tenantId,
                    IsValid = true,
                    Issues = new List<string>(),
                    DataSourceValidation = await _dataSourcePriorityService.ValidateDataConsistencyAsync(tenantId)
                };

                // Check if essential configuration categories have data
                var tenantContext = new TenantContext { TenantId = tenantId };
                var essentialCategories = new[] { "smoking", "wifi", "contact", "services" };

                foreach (var category in essentialCategories)
                {
                    var hasData = await _dataSourcePriorityService.HasConfiguredDataAsync(category, tenantContext);
                    if (!hasData)
                    {
                        result.Issues.Add($"Missing configuration for essential category: {category}");
                    }
                }

                // Check data source priority conflicts
                var dataSources = await _dataSourcePriorityService.GetDataSourcePriorityOrderAsync(tenantId);
                var availableSources = dataSources.Where(s => s.IsAvailable).ToList();

                if (availableSources.Count < 2)
                {
                    result.Issues.Add("Insufficient data sources available - recommend configuring at least HotelInfo and BusinessInfo");
                }

                // Validate that high-priority sources have comprehensive data
                var hotelInfoSource = availableSources.FirstOrDefault(s => s.SourceType == "HotelInfo");
                if (hotelInfoSource == null)
                {
                    result.Issues.Add("HotelInfo data source not available - this is the highest priority source");
                }

                result.IsValid = !result.Issues.Any() && result.DataSourceValidation.IsConsistent;

                _logger.LogInformation("Pipeline configuration validation completed for tenant {TenantId}: {IsValid}",
                    tenantId, result.IsValid);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating pipeline configuration for tenant: {TenantId}", tenantId);
                return new PipelineValidationResult
                {
                    TenantId = tenantId,
                    IsValid = false,
                    Issues = new List<string> { "Validation system error occurred" }
                };
            }
        }

        #region Private Helper Methods

        private string FormatDirectResponse(string category, string configuredData, string originalMessage)
        {
            return category switch
            {
                "smoking_policy" => $"Here's our smoking policy: {configuredData}",
                "wifi_info" => $"WiFi Information: {configuredData}",
                "check_times" => $"Check-in/Check-out Times: {configuredData}",
                "contact_info" => $"Contact Information: {configuredData}",
                "pricing_info" => $"Pricing: {configuredData}",
                "location_info" => $"Location: {configuredData}",
                "hours_info" => $"Hours: {configuredData}",
                _ => configuredData
            };
        }

        private async Task<string?> GetSourceDataForPrompt(string sourceType, TenantContext tenantContext)
        {
            try
            {
                // This would get actual data from each source type
                // For now, return placeholder indicating the source is configured
                var hasData = await _dataSourcePriorityService.HasConfiguredDataAsync(sourceType.ToLower(), tenantContext);

                if (hasData)
                {
                    return $"{sourceType} data is configured and available for this property.";
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting source data for prompt: {SourceType}", sourceType);
                return null;
            }
        }

        #endregion
    }

    // Supporting classes
    public class PipelineResponse
    {
        public string Message { get; set; } = "";
        public int TenantId { get; set; }
        public bool HasDirectResponse { get; set; }
        public string? Response { get; set; }
        public string? FinalResponse { get; set; }
        public string Source { get; set; } = "";
        public bool BypassedLLM { get; set; }
        public bool RequiresLLMProcessing { get; set; }
        public double Confidence { get; set; }
        public string? EnhancedSystemPrompt { get; set; }
        public List<ProcessingStep> ProcessingSteps { get; set; } = new();
    }

    public class ProcessingStep
    {
        public string StepName { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool Success { get; set; }
        public string? Details { get; set; }
        public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
    }

    public class PipelineValidationResult
    {
        public int TenantId { get; set; }
        public bool IsValid { get; set; }
        public List<string> Issues { get; set; } = new();
        public DataSourceValidationResult? DataSourceValidation { get; set; }
    }
}