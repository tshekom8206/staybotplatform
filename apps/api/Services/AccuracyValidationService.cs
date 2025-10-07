using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hostr.Api.Services;

public interface IAccuracyValidationService
{
    Task<ValidationResult> ValidateResponseAccuracyAsync(ValidationRequest request);
    Task<AccuracyMetrics> CalculateAccuracyMetricsAsync(int tenantId, DateTime? startDate = null, DateTime? endDate = null);
    Task<ValidationResult> ValidateAgainstFactsAsync(string response, int tenantId);
    Task<ValidationResult> ValidateConsistencyAsync(string response, List<string> conversationHistory);
    Task<ValidationResult> ValidateTimelyRelevanceAsync(string response, TimeContext timeContext);
    Task<List<ValidationIssue>> PerformComprehensiveValidationAsync(string response, ValidationContext context);
}

public enum ValidationType
{
    FactualAccuracy = 1,
    TemporalRelevance = 2,
    ConsistencyCheck = 3,
    BusinessLogic = 4,
    ContentAppropriate = 5,
    DataIntegrity = 6
}

public enum ValidationSeverity
{
    Info = 1,        // Minor suggestion
    Warning = 2,     // Potential issue
    Error = 3,       // Definite problem
    Critical = 4     // Must be fixed
}

public class ValidationRequest
{
    public string Response { get; set; } = string.Empty;
    public int TenantId { get; set; }
    public int ConversationId { get; set; }
    public string UserMessage { get; set; } = string.Empty;
    public List<string> ConversationHistory { get; set; } = new();
    public TimeContext TimeContext { get; set; } = new();
    public Dictionary<string, object> BusinessContext { get; set; } = new();
}

public class ValidationResult
{
    public bool IsValid { get; set; } = true;
    public double AccuracyScore { get; set; } = 1.0; // 0.0 to 1.0
    public List<ValidationIssue> Issues { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
    public ValidationType PrimaryType { get; set; }
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    public string? CorrectedResponse { get; set; }
}

public class ValidationIssue
{
    public ValidationType Type { get; set; }
    public ValidationSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AffectedText { get; set; } = string.Empty;
    public string? SuggestedCorrection { get; set; }
    public double ConfidenceLevel { get; set; } = 0.8;
}

public class AccuracyMetrics
{
    public double OverallAccuracy { get; set; }
    public Dictionary<ValidationType, double> AccuracyByType { get; set; } = new();
    public int TotalValidations { get; set; }
    public int TotalIssues { get; set; }
    public Dictionary<ValidationSeverity, int> IssuesBySeverity { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class ValidationContext
{
    public int TenantId { get; set; }
    public TimeContext TimeContext { get; set; } = new();
    public List<string> ConversationHistory { get; set; } = new();
    public Dictionary<string, object> BusinessData { get; set; } = new();
    public List<string> KnownFacts { get; set; } = new();
}

public class AccuracyValidationService : IAccuracyValidationService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<AccuracyValidationService> _logger;
    private readonly ITemporalContextService _temporalContext;
    private readonly IBusinessRulesEngine _businessRules;

    // Validation patterns for different fact types
    private static readonly Dictionary<string, Regex> FactValidationPatterns = new()
    {
        { "time", new Regex(@"\b(\d{1,2}:\d{2}\s?(AM|PM|am|pm)?|\d{1,2}\s?(AM|PM|am|pm))\b", RegexOptions.IgnoreCase) },
        { "price", new Regex(@"\$\d+(\.\d{2})?|\d+\s?dollars?", RegexOptions.IgnoreCase) },
        { "room_number", new Regex(@"\b\d{3,4}\b", RegexOptions.IgnoreCase) },
        { "phone", new Regex(@"\+?[\d\s\-\(\)]{10,}", RegexOptions.IgnoreCase) },
        { "email", new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.IgnoreCase) }
    };

    public AccuracyValidationService(
        HostrDbContext context,
        ILogger<AccuracyValidationService> logger,
        ITemporalContextService temporalContext,
        IBusinessRulesEngine businessRules)
    {
        _context = context;
        _logger = logger;
        _temporalContext = temporalContext;
        _businessRules = businessRules;
    }

    public async Task<ValidationResult> ValidateResponseAccuracyAsync(ValidationRequest request)
    {
        try
        {
            var result = new ValidationResult
            {
                PrimaryType = ValidationType.FactualAccuracy
            };

            // Create validation context
            var validationContext = new ValidationContext
            {
                TenantId = request.TenantId,
                TimeContext = request.TimeContext,
                ConversationHistory = request.ConversationHistory,
                BusinessData = request.BusinessContext
            };

            // Perform comprehensive validation
            var issues = await PerformComprehensiveValidationAsync(request.Response, validationContext);
            result.Issues = issues;

            // Calculate accuracy score based on issues
            result.AccuracyScore = CalculateAccuracyScore(issues);
            result.IsValid = result.AccuracyScore >= 0.7; // 70% threshold

            // Generate suggestions based on issues
            result.Suggestions = GenerateSuggestions(issues);

            // Attempt to generate corrected response if needed
            if (!result.IsValid && issues.Any(i => i.Severity >= ValidationSeverity.Error))
            {
                result.CorrectedResponse = await GenerateCorrectedResponseAsync(request.Response, issues);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating response accuracy");
            return new ValidationResult
            {
                IsValid = false,
                AccuracyScore = 0.0,
                Issues = new List<ValidationIssue>
                {
                    new ValidationIssue
                    {
                        Type = ValidationType.DataIntegrity,
                        Severity = ValidationSeverity.Critical,
                        Description = "Validation system error",
                        ConfidenceLevel = 1.0
                    }
                }
            };
        }
    }

    public async Task<AccuracyMetrics> CalculateAccuracyMetricsAsync(int tenantId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            // In a real implementation, you would retrieve validation history from database
            // For now, return mock metrics
            var metrics = new AccuracyMetrics
            {
                OverallAccuracy = 0.85,
                TotalValidations = 100,
                TotalIssues = 15
            };

            metrics.AccuracyByType = new Dictionary<ValidationType, double>
            {
                { ValidationType.FactualAccuracy, 0.92 },
                { ValidationType.TemporalRelevance, 0.88 },
                { ValidationType.ConsistencyCheck, 0.85 },
                { ValidationType.BusinessLogic, 0.90 },
                { ValidationType.ContentAppropriate, 0.95 }
            };

            metrics.IssuesBySeverity = new Dictionary<ValidationSeverity, int>
            {
                { ValidationSeverity.Info, 5 },
                { ValidationSeverity.Warning, 7 },
                { ValidationSeverity.Error, 3 },
                { ValidationSeverity.Critical, 0 }
            };

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating accuracy metrics for tenant {TenantId}", tenantId);
            return new AccuracyMetrics();
        }
    }

    public async Task<ValidationResult> ValidateAgainstFactsAsync(string response, int tenantId)
    {
        try
        {
            var issues = new List<ValidationIssue>();

            // Validate against known business facts
            var hotelInfo = await _context.HotelInfos.FirstOrDefaultAsync(h => h.TenantId == tenantId);
            if (hotelInfo != null)
            {
                // Check for inconsistent hotel information
                if (response.Contains("check-in") && !response.Contains(hotelInfo.CheckInTime))
                {
                    // Only flag if response mentions a different time
                    var timeMatches = FactValidationPatterns["time"].Matches(response);
                    if (timeMatches.Count > 0)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Type = ValidationType.FactualAccuracy,
                            Severity = ValidationSeverity.Warning,
                            Description = "Check-in time may not match hotel policy",
                            AffectedText = timeMatches[0].Value,
                            SuggestedCorrection = hotelInfo.CheckInTime,
                            ConfidenceLevel = 0.7
                        });
                    }
                }
            }

            // Validate menu items exist
            await ValidateMenuItemsAsync(response, tenantId, issues);

            // Validate service availability
            await ValidateServiceAvailabilityAsync(response, tenantId, issues);

            return new ValidationResult
            {
                PrimaryType = ValidationType.FactualAccuracy,
                Issues = issues,
                AccuracyScore = CalculateAccuracyScore(issues),
                IsValid = !issues.Any(i => i.Severity >= ValidationSeverity.Error)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating facts for tenant {TenantId}", tenantId);
            return new ValidationResult { IsValid = false, AccuracyScore = 0.0 };
        }
    }

    public async Task<ValidationResult> ValidateConsistencyAsync(string response, List<string> conversationHistory)
    {
        try
        {
            var issues = new List<ValidationIssue>();

            // Check for contradictions with previous statements
            foreach (var previousMessage in conversationHistory.TakeLast(5)) // Check last 5 messages
            {
                var contradictions = FindContradictions(response, previousMessage);
                issues.AddRange(contradictions);
            }

            // Check for internal contradictions within the response
            var internalContradictions = FindInternalContradictions(response);
            issues.AddRange(internalContradictions);

            return new ValidationResult
            {
                PrimaryType = ValidationType.ConsistencyCheck,
                Issues = issues,
                AccuracyScore = CalculateAccuracyScore(issues),
                IsValid = !issues.Any(i => i.Severity >= ValidationSeverity.Error)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating consistency");
            return new ValidationResult { IsValid = false, AccuracyScore = 0.0 };
        }
    }

    public async Task<ValidationResult> ValidateTimelyRelevanceAsync(string response, TimeContext timeContext)
    {
        try
        {
            var issues = new List<ValidationIssue>();

            // Check if time-sensitive information is current
            if (response.Contains("currently") || response.Contains("now") || response.Contains("today"))
            {
                // Validate business hours
                if (response.Contains("open") && !timeContext.IsBusinessHours)
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationType.TemporalRelevance,
                        Severity = ValidationSeverity.Error,
                        Description = "Claims business is open when it's currently closed",
                        AffectedText = "open",
                        SuggestedCorrection = "closed",
                        ConfidenceLevel = 0.9
                    });
                }

                // Validate meal period relevance
                if (timeContext.MealPeriod != MealPeriod.None)
                {
                    var currentMeal = timeContext.MealPeriod.ToString().ToLower();
                    if (response.Contains("breakfast") && currentMeal != "breakfast" && timeContext.LocalTime.Hour > 11)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Type = ValidationType.TemporalRelevance,
                            Severity = ValidationSeverity.Warning,
                            Description = "Breakfast mentioned outside breakfast hours",
                            AffectedText = "breakfast",
                            ConfidenceLevel = 0.8
                        });
                    }
                }
            }

            return new ValidationResult
            {
                PrimaryType = ValidationType.TemporalRelevance,
                Issues = issues,
                AccuracyScore = CalculateAccuracyScore(issues),
                IsValid = !issues.Any(i => i.Severity >= ValidationSeverity.Error)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating temporal relevance");
            return new ValidationResult { IsValid = false, AccuracyScore = 0.0 };
        }
    }

    public async Task<List<ValidationIssue>> PerformComprehensiveValidationAsync(string response, ValidationContext context)
    {
        var allIssues = new List<ValidationIssue>();

        try
        {
            // Factual accuracy validation
            var factResult = await ValidateAgainstFactsAsync(response, context.TenantId);
            allIssues.AddRange(factResult.Issues);

            // Consistency validation
            var consistencyResult = await ValidateConsistencyAsync(response, context.ConversationHistory);
            allIssues.AddRange(consistencyResult.Issues);

            // Temporal relevance validation
            var temporalResult = await ValidateTimelyRelevanceAsync(response, context.TimeContext);
            allIssues.AddRange(temporalResult.Issues);

            // Business logic validation
            var businessIssues = await ValidateBusinessLogicAsync(response, context.TenantId);
            allIssues.AddRange(businessIssues);

            // Content appropriateness validation
            var contentIssues = ValidateContentAppropriatenessAsync(response);
            allIssues.AddRange(contentIssues);

            return allIssues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in comprehensive validation");
            return new List<ValidationIssue>
            {
                new ValidationIssue
                {
                    Type = ValidationType.DataIntegrity,
                    Severity = ValidationSeverity.Critical,
                    Description = "Validation system error",
                    ConfidenceLevel = 1.0
                }
            };
        }
    }

    // Helper methods

    private double CalculateAccuracyScore(List<ValidationIssue> issues)
    {
        if (!issues.Any()) return 1.0;

        double totalPenalty = 0.0;
        foreach (var issue in issues)
        {
            double penalty = issue.Severity switch
            {
                ValidationSeverity.Info => 0.02,
                ValidationSeverity.Warning => 0.05,
                ValidationSeverity.Error => 0.15,
                ValidationSeverity.Critical => 0.30,
                _ => 0.05
            };
            totalPenalty += penalty * issue.ConfidenceLevel;
        }

        return Math.Max(0.0, 1.0 - totalPenalty);
    }

    private List<string> GenerateSuggestions(List<ValidationIssue> issues)
    {
        var suggestions = new List<string>();

        foreach (var issue in issues.Where(i => i.Severity >= ValidationSeverity.Warning))
        {
            if (!string.IsNullOrEmpty(issue.SuggestedCorrection))
            {
                suggestions.Add($"Consider replacing '{issue.AffectedText}' with '{issue.SuggestedCorrection}'");
            }
            else
            {
                suggestions.Add($"Review and address: {issue.Description}");
            }
        }

        return suggestions.Distinct().ToList();
    }

    private async Task<string?> GenerateCorrectedResponseAsync(string originalResponse, List<ValidationIssue> issues)
    {
        // Simple correction logic - in production, this could use AI
        var corrected = originalResponse;

        foreach (var issue in issues.Where(i => !string.IsNullOrEmpty(i.SuggestedCorrection)))
        {
            corrected = corrected.Replace(issue.AffectedText, issue.SuggestedCorrection!);
        }

        return corrected != originalResponse ? corrected : null;
    }

    private async Task ValidateMenuItemsAsync(string response, int tenantId, List<ValidationIssue> issues)
    {
        var menuItems = await _context.MenuItems
            .Where(m => m.TenantId == tenantId)
            .Select(m => m.Name.ToLower())
            .ToListAsync();

        // Simple validation - check if mentioned items exist
        var words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            var cleanWord = word.ToLower().Trim('.', ',', '!', '?');
            if (cleanWord.Length > 3 && !menuItems.Any(m => m.Contains(cleanWord)))
            {
                // Only flag if it looks like a food item
                if (IsPotentialFoodItem(cleanWord))
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationType.FactualAccuracy,
                        Severity = ValidationSeverity.Warning,
                        Description = "Mentioned item may not be on the menu",
                        AffectedText = word,
                        ConfidenceLevel = 0.6
                    });
                }
            }
        }
    }

    private async Task ValidateServiceAvailabilityAsync(string response, int tenantId, List<ValidationIssue> issues)
    {
        var services = await _context.Services
            .Where(s => s.TenantId == tenantId && s.IsAvailable)
            .Select(s => s.Name.ToLower())
            .ToListAsync();

        // Check if mentioned services are available
        foreach (var service in services)
        {
            if (response.ToLower().Contains(service))
            {
                // Service is mentioned and exists - good
                continue;
            }
        }
    }

    private List<ValidationIssue> FindContradictions(string currentResponse, string previousMessage)
    {
        var issues = new List<ValidationIssue>();

        // Simple contradiction detection - in production, this would be more sophisticated
        var contradictionPairs = new Dictionary<string[], ValidationSeverity>
        {
            { new[] { "available", "unavailable" }, ValidationSeverity.Error },
            { new[] { "open", "closed" }, ValidationSeverity.Error },
            { new[] { "yes", "no" }, ValidationSeverity.Warning },
            { new[] { "can", "cannot" }, ValidationSeverity.Warning }
        };

        foreach (var (words, severity) in contradictionPairs)
        {
            if (currentResponse.ToLower().Contains(words[0]) && previousMessage.ToLower().Contains(words[1]) ||
                currentResponse.ToLower().Contains(words[1]) && previousMessage.ToLower().Contains(words[0]))
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationType.ConsistencyCheck,
                    Severity = severity,
                    Description = $"Potential contradiction with previous statement",
                    AffectedText = currentResponse.Length > 50 ? currentResponse.Substring(0, 50) + "..." : currentResponse,
                    ConfidenceLevel = 0.7
                });
            }
        }

        return issues;
    }

    private List<ValidationIssue> FindInternalContradictions(string response)
    {
        var issues = new List<ValidationIssue>();

        // Check for internal contradictions within the same response
        var sentences = response.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < sentences.Length - 1; i++)
        {
            for (int j = i + 1; j < sentences.Length; j++)
            {
                var contradictions = FindContradictions(sentences[i], sentences[j]);
                issues.AddRange(contradictions);
            }
        }

        return issues;
    }

    private async Task<List<ValidationIssue>> ValidateBusinessLogicAsync(string response, int tenantId)
    {
        var issues = new List<ValidationIssue>();

        try
        {
            // Use business rules engine to validate logical consistency
            // This is a simplified version - in production, this would be more comprehensive
            return issues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating business logic");
            return issues;
        }
    }

    private List<ValidationIssue> ValidateContentAppropriatenessAsync(string response)
    {
        var issues = new List<ValidationIssue>();

        // Check for inappropriate content
        var inappropriateWords = new[] { "damn", "hell", "stupid", "idiot" }; // Simplified list
        foreach (var word in inappropriateWords)
        {
            if (response.ToLower().Contains(word))
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationType.ContentAppropriate,
                    Severity = ValidationSeverity.Warning,
                    Description = "Response contains potentially inappropriate language",
                    AffectedText = word,
                    ConfidenceLevel = 0.8
                });
            }
        }

        return issues;
    }

    private bool IsPotentialFoodItem(string word)
    {
        // Simple heuristic to identify potential food items
        var foodKeywords = new[] { "pizza", "burger", "salad", "soup", "pasta", "chicken", "beef", "fish", "dessert", "cake" };
        return foodKeywords.Any(k => word.Contains(k)) || word.EndsWith("ed") || word.EndsWith("ing");
    }
}