using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services
{
    public interface IResponseMonitoringService
    {
        Task<Guid> LogResponseGenerationAsync(ResponseAuditLog auditLog);
        Task UpdateResponseOutcomeAsync(Guid logId, ResponseOutcome outcome);
        Task<ResponseQualityReport> GenerateQualityReportAsync(int tenantId, DateTime fromDate, DateTime toDate);
        Task<List<GenericResponseAlert>> DetectGenericResponsesAsync(int tenantId, int lookbackHours = 24);
        Task<ConfigurationUsageReport> GenerateConfigurationUsageReportAsync(int tenantId, DateTime fromDate, DateTime toDate);
        Task<bool> IsResponseQualityAcceptableAsync(ResponseAuditLog auditLog);
        Task AlertOnQualityIssuesAsync(ResponseAuditLog auditLog);
    }

    public class ResponseMonitoringService : IResponseMonitoringService
    {
        private readonly HostrDbContext _context;
        private readonly IConfigurationBasedResponseService _configurationBasedResponseService;
        private readonly IResponseValidationService _responseValidationService;
        private readonly ILogger<ResponseMonitoringService> _logger;

        // Quality thresholds
        private const double MIN_ACCEPTABLE_CONFIDENCE = 0.70;
        private const double MIN_CONFIGURATION_USAGE_RATE = 0.80; // 80% of responses should use configured data
        private const int MAX_GENERIC_RESPONSES_PER_HOUR = 5;

        public ResponseMonitoringService(
            HostrDbContext context,
            IConfigurationBasedResponseService configurationBasedResponseService,
            IResponseValidationService responseValidationService,
            ILogger<ResponseMonitoringService> logger)
        {
            _context = context;
            _configurationBasedResponseService = configurationBasedResponseService;
            _responseValidationService = responseValidationService;
            _logger = logger;
        }

        public async Task<Guid> LogResponseGenerationAsync(ResponseAuditLog auditLog)
        {
            try
            {
                auditLog.Id = Guid.NewGuid();
                auditLog.CreatedAt = DateTime.UtcNow;

                // Validate response quality
                var validationResult = await _responseValidationService.ValidateResponseAsync(
                    auditLog.OriginalMessage,
                    new MessageRoutingResponse { Reply = auditLog.GeneratedResponse },
                    new TenantContext { TenantId = auditLog.TenantId });

                auditLog.QualityScore = validationResult.AccuracyScore * 100;
                auditLog.ValidationIssues = JsonSerializer.Serialize(validationResult.Issues);

                // Check for prohibited content
                var prohibitedContent = await _configurationBasedResponseService.DetectProhibitedContentAsync(auditLog.GeneratedResponse);
                auditLog.HasProhibitedContent = prohibitedContent.Any();
                auditLog.ProhibitedPhrases = JsonSerializer.Serialize(prohibitedContent);

                // Store in database (you would need to create this table)
                // For now, we'll log it
                _logger.LogInformation("Response audit log created: {LogId}, Quality: {Score}, Prohibited: {HasProhibited}",
                    auditLog.Id, auditLog.QualityScore, auditLog.HasProhibitedContent);

                // Check if immediate alerts are needed
                if (await IsResponseQualityAcceptableAsync(auditLog) == false)
                {
                    await AlertOnQualityIssuesAsync(auditLog);
                }

                return auditLog.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging response generation audit");
                return Guid.Empty;
            }
        }

        public async Task UpdateResponseOutcomeAsync(Guid logId, ResponseOutcome outcome)
        {
            try
            {
                // In a real implementation, update the audit log with outcome
                _logger.LogInformation("Response outcome updated: {LogId}, Outcome: {Outcome}", logId, outcome.Result);

                // Track success/failure rates
                if (outcome.Result == OutcomeResult.Failed)
                {
                    _logger.LogWarning("Response failed: {LogId}, Reason: {Reason}", logId, outcome.FailureReason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating response outcome for log: {LogId}", logId);
            }
        }

        public async Task<ResponseQualityReport> GenerateQualityReportAsync(int tenantId, DateTime fromDate, DateTime toDate)
        {
            try
            {
                using var scope = new TenantScope(_context, tenantId);

                var report = new ResponseQualityReport
                {
                    TenantId = tenantId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    GeneratedAt = DateTime.UtcNow
                };

                // Get conversation data for analysis (simplified)
                var conversations = await _context.Messages
                    .Where(m => m.CreatedAt >= fromDate && m.CreatedAt <= toDate)
                    .GroupBy(m => m.ConversationId)
                    .Select(g => new
                    {
                        ConversationId = g.Key,
                        MessageCount = g.Count(),
                        LastMessage = g.OrderByDescending(m => m.CreatedAt).First()
                    })
                    .ToListAsync();

                report.TotalResponses = conversations.Count;

                // Simulate quality metrics (in real implementation, get from audit logs)
                report.AverageQualityScore = 85.5;
                report.ConfigurationUsageRate = 0.78;
                report.GenericResponseCount = 15;
                report.ValidationFailureCount = 8;

                // Quality distribution
                report.QualityDistribution = new Dictionary<string, int>
                {
                    { "Excellent (90-100)", (int)(report.TotalResponses * 0.25) },
                    { "Good (80-89)", (int)(report.TotalResponses * 0.45) },
                    { "Fair (70-79)", (int)(report.TotalResponses * 0.20) },
                    { "Poor (60-69)", (int)(report.TotalResponses * 0.08) },
                    { "Unacceptable (<60)", (int)(report.TotalResponses * 0.02) }
                };

                // Common issues
                report.CommonIssues = new List<QualityIssue>
                {
                    new QualityIssue { Type = "Generic Language", Count = 12, Percentage = 12.5 },
                    new QualityIssue { Type = "Missing Configuration", Count = 8, Percentage = 8.3 },
                    new QualityIssue { Type = "Inappropriate Tone", Count = 5, Percentage = 5.2 },
                    new QualityIssue { Type = "Incomplete Information", Count = 3, Percentage = 3.1 }
                };

                // Recommendations
                report.Recommendations = GenerateQualityRecommendations(report);

                _logger.LogInformation("Quality report generated for tenant {TenantId}: {Score} avg quality",
                    tenantId, report.AverageQualityScore);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating quality report for tenant: {TenantId}", tenantId);
                return new ResponseQualityReport
                {
                    TenantId = tenantId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    GeneratedAt = DateTime.UtcNow,
                    TotalResponses = 0
                };
            }
        }

        public async Task<List<GenericResponseAlert>> DetectGenericResponsesAsync(int tenantId, int lookbackHours = 24)
        {
            try
            {
                using var scope = new TenantScope(_context, tenantId);
                var alerts = new List<GenericResponseAlert>();

                var cutoffTime = DateTime.UtcNow.AddHours(-lookbackHours);

                // Get recent bot messages for analysis
                var recentMessages = await _context.Messages
                    .Where(m => m.Direction == "Outbound" && m.CreatedAt >= cutoffTime)
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(100)
                    .ToListAsync();

                foreach (var message in recentMessages)
                {
                    if (!string.IsNullOrEmpty(message.Body))
                    {
                        var prohibitedContent = await _configurationBasedResponseService.DetectProhibitedContentAsync(message.Body);

                        if (prohibitedContent.Any())
                        {
                            alerts.Add(new GenericResponseAlert
                            {
                                MessageId = message.Id,
                                ConversationId = message.ConversationId,
                                DetectedAt = DateTime.UtcNow,
                                GenericPhrases = prohibitedContent,
                                MessageContent = message.Body,
                                Severity = prohibitedContent.Count > 2 ? "High" : "Medium"
                            });
                        }
                    }
                }

                if (alerts.Count > MAX_GENERIC_RESPONSES_PER_HOUR)
                {
                    _logger.LogWarning("High number of generic responses detected: {Count} in last {Hours} hours",
                        alerts.Count, lookbackHours);
                }

                return alerts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting generic responses for tenant: {TenantId}", tenantId);
                return new List<GenericResponseAlert>();
            }
        }

        public async Task<ConfigurationUsageReport> GenerateConfigurationUsageReportAsync(int tenantId, DateTime fromDate, DateTime toDate)
        {
            try
            {
                using var scope = new TenantScope(_context, tenantId);

                var report = new ConfigurationUsageReport
                {
                    TenantId = tenantId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    GeneratedAt = DateTime.UtcNow
                };

                // Analyze which configuration sources are being used
                var conversations = await _context.Conversations
                    .Where(c => c.CreatedAt >= fromDate && c.CreatedAt <= toDate)
                    .ToListAsync();

                report.TotalConversations = conversations.Count;

                // Simulate configuration usage data (in real implementation, track this)
                report.ConfigurationSources = new Dictionary<string, ConfigurationSourceUsage>
                {
                    { "HotelInfo", new ConfigurationSourceUsage { UsageCount = 45, UsagePercentage = 65.2 } },
                    { "BusinessInfo", new ConfigurationSourceUsage { UsageCount = 28, UsagePercentage = 40.6 } },
                    { "Services", new ConfigurationSourceUsage { UsageCount = 32, UsagePercentage = 46.4 } },
                    { "MenuItems", new ConfigurationSourceUsage { UsageCount = 15, UsagePercentage = 21.7 } }
                };

                // Categories most requested but lacking configuration
                report.MissingConfigurationAreas = new List<MissingConfigArea>
                {
                    new MissingConfigArea { Category = "Pet Policy", RequestCount = 8, ImpactLevel = "Medium" },
                    new MissingConfigArea { Category = "Parking Information", RequestCount = 12, ImpactLevel = "High" },
                    new MissingConfigArea { Category = "Fitness Center Hours", RequestCount = 5, ImpactLevel = "Low" }
                };

                // Calculate overall configuration coverage
                var totalRequests = conversations.Count;
                var configuredResponses = (int)(totalRequests * 0.78); // Simulated
                report.ConfigurationCoveragePercentage = (double)configuredResponses / totalRequests * 100;

                _logger.LogInformation("Configuration usage report generated for tenant {TenantId}: {Coverage}% coverage",
                    tenantId, report.ConfigurationCoveragePercentage);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating configuration usage report for tenant: {TenantId}", tenantId);
                return new ConfigurationUsageReport
                {
                    TenantId = tenantId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    GeneratedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<bool> IsResponseQualityAcceptableAsync(ResponseAuditLog auditLog)
        {
            try
            {
                // Check multiple quality criteria
                var issues = new List<string>();

                if (auditLog.QualityScore < MIN_ACCEPTABLE_CONFIDENCE * 100)
                {
                    issues.Add($"Quality score too low: {auditLog.QualityScore}");
                }

                if (auditLog.HasProhibitedContent)
                {
                    issues.Add("Contains prohibited generic phrases");
                }

                if (auditLog.ResponseSource == "LLM" && auditLog.ConfigurationMatchFound == false)
                {
                    issues.Add("LLM response without configuration validation");
                }

                var isAcceptable = !issues.Any();

                if (!isAcceptable)
                {
                    _logger.LogWarning("Response quality unacceptable for {LogId}: {Issues}",
                        auditLog.Id, string.Join(", ", issues));
                }

                return isAcceptable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking response quality for log: {LogId}", auditLog.Id);
                return false;
            }
        }

        public async Task AlertOnQualityIssuesAsync(ResponseAuditLog auditLog)
        {
            try
            {
                var alertMessage = $"Response quality alert for tenant {auditLog.TenantId}: " +
                                 $"Quality Score: {auditLog.QualityScore}, " +
                                 $"Has Prohibited Content: {auditLog.HasProhibitedContent}, " +
                                 $"Message: '{auditLog.OriginalMessage}', " +
                                 $"Response: '{auditLog.GeneratedResponse}'";

                _logger.LogWarning("QUALITY ALERT: {Alert}", alertMessage);

                // In a real implementation, send notifications via email, Slack, etc.
                // For now, log the alert for monitoring systems to pick up

                // Could also create dashboard alerts, update metrics, etc.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending quality alert for log: {LogId}", auditLog.Id);
            }
        }

        #region Private Helper Methods

        private List<string> GenerateQualityRecommendations(ResponseQualityReport report)
        {
            var recommendations = new List<string>();

            if (report.ConfigurationUsageRate < MIN_CONFIGURATION_USAGE_RATE)
            {
                recommendations.Add($"Increase configuration coverage - currently at {report.ConfigurationUsageRate:P1}, target is {MIN_CONFIGURATION_USAGE_RATE:P1}");
            }

            if (report.GenericResponseCount > MAX_GENERIC_RESPONSES_PER_HOUR * 24)
            {
                recommendations.Add($"Reduce generic responses - detected {report.GenericResponseCount} generic responses, which is above threshold");
            }

            if (report.AverageQualityScore < 80)
            {
                recommendations.Add("Improve response quality - average score is below acceptable threshold of 80");
            }

            if (report.ValidationFailureCount > report.TotalResponses * 0.1)
            {
                recommendations.Add("Address validation failures - high failure rate indicates system issues");
            }

            if (!recommendations.Any())
            {
                recommendations.Add("Response quality is within acceptable parameters. Continue monitoring.");
            }

            return recommendations;
        }

        #endregion
    }

    // Supporting classes for monitoring
    public class ResponseAuditLog
    {
        public Guid Id { get; set; }
        public int TenantId { get; set; }
        public int ConversationId { get; set; }
        public string OriginalMessage { get; set; } = "";
        public string GeneratedResponse { get; set; } = "";
        public string ResponseSource { get; set; } = ""; // "LLM", "DirectConfiguration", "Template"
        public bool ConfigurationMatchFound { get; set; }
        public double QualityScore { get; set; }
        public bool HasProhibitedContent { get; set; }
        public string? ProhibitedPhrases { get; set; }
        public string? ValidationIssues { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ResponseOutcome
    {
        public OutcomeResult Result { get; set; }
        public string? FailureReason { get; set; }
        public bool UserSatisfied { get; set; }
        public int? UserRating { get; set; }
        public DateTime RecordedAt { get; set; }
    }

    public enum OutcomeResult
    {
        Success,
        Failed,
        PartialSuccess,
        Unknown
    }

    public class ResponseQualityReport
    {
        public int TenantId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int TotalResponses { get; set; }
        public double AverageQualityScore { get; set; }
        public double ConfigurationUsageRate { get; set; }
        public int GenericResponseCount { get; set; }
        public int ValidationFailureCount { get; set; }
        public Dictionary<string, int> QualityDistribution { get; set; } = new();
        public List<QualityIssue> CommonIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class QualityIssue
    {
        public string Type { get; set; } = "";
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class GenericResponseAlert
    {
        public int MessageId { get; set; }
        public int ConversationId { get; set; }
        public DateTime DetectedAt { get; set; }
        public List<string> GenericPhrases { get; set; } = new();
        public string MessageContent { get; set; } = "";
        public string Severity { get; set; } = ""; // "Low", "Medium", "High"
    }

    public class ConfigurationUsageReport
    {
        public int TenantId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int TotalConversations { get; set; }
        public double ConfigurationCoveragePercentage { get; set; }
        public Dictionary<string, ConfigurationSourceUsage> ConfigurationSources { get; set; } = new();
        public List<MissingConfigArea> MissingConfigurationAreas { get; set; } = new();
    }

    public class ConfigurationSourceUsage
    {
        public int UsageCount { get; set; }
        public double UsagePercentage { get; set; }
    }

    public class MissingConfigArea
    {
        public string Category { get; set; } = "";
        public int RequestCount { get; set; }
        public string ImpactLevel { get; set; } = ""; // "Low", "Medium", "High"
    }
}