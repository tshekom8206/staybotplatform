using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Hostr.Api.Services
{
    public interface IResponseValidationService
    {
        Task<ValidationResult> ValidateResponseAsync(string originalMessage, MessageRoutingResponse response, TenantContext tenantContext);
        Task<bool> IsResponseAppropriateLengthAsync(string response);
        Task<bool> ContainsRequiredInformationAsync(string originalMessage, string response);
        Task<bool> IsBusinessContextAppropriateAsync(string response, TenantContext tenantContext);
        Task<ValidationResult> ValidateHotelSpecificContentAsync(string response, TenantContext tenantContext);
    }

    public class ResponseValidationService : IResponseValidationService
    {
        private readonly HostrDbContext _context;
        private readonly ILogger<ResponseValidationService> _logger;

        // Response quality thresholds and validation rules
        private const int MIN_RESPONSE_LENGTH = 10;
        private const int MAX_RESPONSE_LENGTH = 1000;
        private const int IDEAL_RESPONSE_LENGTH_MIN = 50;
        private const int IDEAL_RESPONSE_LENGTH_MAX = 300;

        public ResponseValidationService(HostrDbContext context, ILogger<ResponseValidationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateResponseAsync(string originalMessage, MessageRoutingResponse response, TenantContext tenantContext)
        {
            var result = new ValidationResult
            {
                IsValid = true,
                Issues = new List<ValidationIssue>(),
                AccuracyScore = 1.0
            };

            try
            {
                // 1. Check response length appropriateness
                var lengthValid = await IsResponseAppropriateLengthAsync(response.Reply ?? "");
                if (!lengthValid)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationType.ContentAppropriate,
                        Severity = ValidationSeverity.Warning,
                        Description = "Response length is not appropriate for the query",
                        ConfidenceLevel = 0.85
                    });
                }

                // 2. Check if response contains required information
                var containsInfo = await ContainsRequiredInformationAsync(originalMessage, response.Reply ?? "");
                if (!containsInfo)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationType.BusinessLogic,
                        Severity = ValidationSeverity.Error,
                        Description = "Response may not address the user's specific question",
                        ConfidenceLevel = 0.70
                    });
                }

                // 3. Check business context appropriateness
                var businessContextOk = await IsBusinessContextAppropriateAsync(response.Reply ?? "", tenantContext);
                if (!businessContextOk)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationType.BusinessLogic,
                        Severity = ValidationSeverity.Error,
                        Description = "Response is not appropriate for hotel/hospitality context",
                        ConfidenceLevel = 0.65
                    });
                }

                // 4. Validate hotel-specific content
                var hotelContentValidation = await ValidateHotelSpecificContentAsync(response.Reply ?? "", tenantContext);
                result.Issues.AddRange(hotelContentValidation.Issues);

                // 5. Check for common response issues
                await ValidateCommonIssuesAsync(response.Reply ?? "", result);

                // 6. Validate action appropriateness if action is present
                if (response.Action != null)
                {
                    await ValidateActionAppropriatenessAsync(originalMessage, response.Action, result);
                }

                // Calculate overall score and validity
                CalculateAccuracyScore(result);

                _logger.LogInformation("Response validation completed: Valid={IsValid}, Score={Score}, Issues={IssueCount}",
                    result.IsValid, result.AccuracyScore, result.Issues.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during response validation");

                // Return a validation result indicating an error occurred
                return new ValidationResult
                {
                    IsValid = false,
                    Issues = new List<ValidationIssue>
                    {
                        new ValidationIssue
                        {
                            Type = ValidationType.DataIntegrity,
                            Severity = ValidationSeverity.Critical,
                            Description = "Validation system error occurred",
                            ConfidenceLevel = 0.50
                        }
                    },
                    AccuracyScore = 0.50
                };
            }
        }

        public async Task<bool> IsResponseAppropriateLengthAsync(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return false;

            var length = response.Length;

            // Too short responses are likely not helpful
            if (length < MIN_RESPONSE_LENGTH)
                return false;

            // Extremely long responses may overwhelm users
            if (length > MAX_RESPONSE_LENGTH)
                return false;

            return true;
        }

        public async Task<bool> ContainsRequiredInformationAsync(string originalMessage, string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return false;

            var originalLower = originalMessage.ToLower();
            var responseLower = response.ToLower();

            // Define key information requirements based on message type
            var informationChecks = new Dictionary<string, List<string>>
            {
                // Menu requests should contain menu-related content
                { "menu", new List<string> { "menu", "food", "meal", "breakfast", "lunch", "dinner", "available", "options" } },

                // WiFi requests should contain connection info
                { "wifi", new List<string> { "wifi", "password", "network", "internet", "connect", "access" } },

                // Room service requests should acknowledge the request
                { "towel", new List<string> { "towel", "deliver", "bring", "room", "housekeeping", "send" } },
                { "clean", new List<string> { "clean", "housekeeping", "room", "service", "arrange" } },

                // Emergency requests should show urgency
                { "emergency", new List<string> { "help", "assist", "urgent", "immediately", "right away", "emergency" } },

                // Check-in/out should contain relevant info
                { "check", new List<string> { "check", "time", "reception", "desk", "process" } }
            };

            // Check if the response appropriately addresses the original message
            foreach (var check in informationChecks)
            {
                if (originalLower.Contains(check.Key))
                {
                    var hasRequiredInfo = check.Value.Any(required => responseLower.Contains(required));
                    if (!hasRequiredInfo)
                    {
                        _logger.LogWarning("Response lacks required information for {RequestType}: {Response}",
                            check.Key, response);
                        return false;
                    }
                }
            }

            return true;
        }

        public async Task<bool> IsBusinessContextAppropriateAsync(string response, TenantContext tenantContext)
        {
            var responseLower = response.ToLower();

            // Check for inappropriate content
            var inappropriateContent = new[]
            {
                "refund", "money back", "compensation", // Avoid promising financial compensation
                "free upgrade", "complimentary upgrade", // Avoid promising upgrades without authority
                "manager will call", "manager will contact", // Avoid promising manager contact
                "definitely", "absolutely guarantee", "promise", // Avoid absolute guarantees
                "immediately", "right now", "instantly" // Avoid unrealistic time promises
            };

            var hasInappropriateContent = inappropriateContent.Any(inappropriate => responseLower.Contains(inappropriate));
            if (hasInappropriateContent)
            {
                _logger.LogWarning("Response contains potentially inappropriate business content: {Response}", response);
                return false;
            }

            // Check for required hospitality tone
            var hospitalityIndicators = new[]
            {
                "happy to", "pleased to", "glad to", // Positive hospitality language
                "help", "assist", "support", // Service-oriented language
                "thank you", "thanks", // Polite language
                "sorry", "apologize", // Empathy when appropriate
                "please", "certainly", "of course" // Courteous language
            };

            var hasHospitalityTone = hospitalityIndicators.Any(indicator => responseLower.Contains(indicator));

            // For longer responses, expect some hospitality language
            if (response.Length > 100 && !hasHospitalityTone)
            {
                _logger.LogDebug("Long response lacks hospitality tone: {Response}", response);
                return false;
            }

            return true;
        }

        public async Task<ValidationResult> ValidateHotelSpecificContentAsync(string response, TenantContext tenantContext)
        {
            var result = new ValidationResult
            {
                IsValid = true,
                Issues = new List<ValidationIssue>()
            };

            try
            {
                // Check if response mentions specific hotel details that might be incorrect
                var responseLower = response.ToLower();

                // Check for generic hotel mentions that should be tenant-specific
                if (responseLower.Contains("our hotel") || responseLower.Contains("this hotel"))
                {
                    // This is actually good - using hotel references
                }

                // Check for specific room numbers in responses (should only be used when appropriate)
                if (responseLower.Contains("room ") && responseLower.Contains("number"))
                {
                    // Room number mentions should be contextually appropriate
                    var hasRoomContext = responseLower.Contains("deliver to") ||
                                        responseLower.Contains("bring to") ||
                                        responseLower.Contains("send to");

                    if (!hasRoomContext)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Type = ValidationType.BusinessLogic,
                            Severity = ValidationSeverity.Warning,
                            Description = "Room number mentioned without clear delivery context",
                            ConfidenceLevel = 0.80
                        });
                    }
                }

                // Check for time-specific promises that might not be realistic
                var timePromises = new[] { "5 minutes", "10 minutes", "right away", "immediately" };
                var hasTimePromise = timePromises.Any(promise => responseLower.Contains(promise));

                if (hasTimePromise)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationType.BusinessLogic,
                        Severity = ValidationSeverity.Warning,
                        Description = "Response contains specific time promises that may not be realistic",
                        ConfidenceLevel = 0.85
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating hotel-specific content");
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationType.DataIntegrity,
                    Severity = ValidationSeverity.Warning,
                    Description = "Error during hotel content validation",
                    ConfidenceLevel = 0.70
                });
                return result;
            }
        }

        private async Task ValidateCommonIssuesAsync(string response, ValidationResult result)
        {
            var responseLower = response.ToLower();

            // Check for repetitive content
            var words = responseLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var wordCounts = words.GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count());
            var excessiveRepetition = wordCounts.Any(kvp => kvp.Value > 5 && kvp.Key.Length > 3);

            if (excessiveRepetition)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationType.ContentAppropriate,
                    Severity = ValidationSeverity.Warning,
                    Description = "Response contains excessive word repetition",
                    ConfidenceLevel = 0.75
                });
            }

            // Check for placeholder text that might indicate incomplete processing
            var placeholders = new[] { "[", "]", "{", "}", "xxx", "placeholder", "todo", "fixme" };
            var hasPlaceholders = placeholders.Any(ph => responseLower.Contains(ph));

            if (hasPlaceholders)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationType.ContentAppropriate,
                    Severity = ValidationSeverity.Error,
                    Description = "Response contains placeholder text or incomplete content",
                    ConfidenceLevel = 0.60
                });
            }

            // Check for overly technical language
            var technicalTerms = new[] { "api", "endpoint", "server", "database", "config", "admin", "debug" };
            var hasTechnicalTerms = technicalTerms.Any(term => responseLower.Contains(term));

            if (hasTechnicalTerms)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationType.ContentAppropriate,
                    Severity = ValidationSeverity.Warning,
                    Description = "Response contains technical terms inappropriate for guests",
                    ConfidenceLevel = 0.80
                });
            }
        }

        private async Task ValidateActionAppropriatenessAsync(object action, object originalMessage, ValidationResult result)
        {
            try
            {
                var actionStr = action.ToString();
                var messageStr = originalMessage.ToString();

                if (string.IsNullOrEmpty(actionStr) || string.IsNullOrEmpty(messageStr))
                    return;

                var actionLower = actionStr.ToLower();
                var messageLower = messageStr.ToLower();

                // Check if action matches the message intent
                var actionChecks = new Dictionary<string, List<string>>
                {
                    { "create_task", new List<string> { "need", "want", "bring", "send", "deliver", "clean", "fix" } },
                    { "menu", new List<string> { "menu", "food", "eat", "order", "meal" } },
                    { "wifi", new List<string> { "wifi", "internet", "connection", "password" } }
                };

                foreach (var check in actionChecks)
                {
                    if (actionLower.Contains(check.Key))
                    {
                        var isAppropriate = check.Value.Any(keyword => messageLower.Contains(keyword));
                        if (!isAppropriate)
                        {
                            result.Issues.Add(new ValidationIssue
                            {
                                Type = ValidationType.BusinessLogic,
                                Severity = ValidationSeverity.Error,
                                Description = $"Action {check.Key} does not match message intent",
                                ConfidenceLevel = 0.70
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating action appropriateness");
            }
        }

        private void CalculateAccuracyScore(ValidationResult result)
        {
            if (!result.Issues.Any())
            {
                result.AccuracyScore = 1.0;
                result.IsValid = true;
                return;
            }

            // Calculate weighted score based on issue severity
            var totalPenalty = 0.0;
            foreach (var issue in result.Issues)
            {
                var penalty = issue.Severity switch
                {
                    ValidationSeverity.Info => 0.05,
                    ValidationSeverity.Warning => 0.15,
                    ValidationSeverity.Error => 0.30,
                    ValidationSeverity.Critical => 0.50,
                    _ => 0.10
                };
                totalPenalty += penalty;
            }

            result.AccuracyScore = Math.Max(0.0, 1.0 - totalPenalty);

            // Consider response invalid if score is too low or has critical issues
            var hasCriticalIssues = result.Issues.Any(i => i.Severity == ValidationSeverity.Critical);
            result.IsValid = result.AccuracyScore >= 0.60 && !hasCriticalIssues;
        }
    }

}