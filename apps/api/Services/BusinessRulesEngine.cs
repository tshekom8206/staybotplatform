using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface IBusinessRulesEngine
{
    Task<BusinessRuleResult> EvaluateServiceAvailabilityAsync(string serviceName, int tenantId, DateTime? requestedTime = null);
    Task<BusinessRuleResult> ValidateBookingConstraintsAsync(int tenantId, string guestPhone, DateTime requestedTime);
    Task<BusinessRuleResult> CheckRoomServiceConstraintsAsync(int tenantId, string roomNumber, List<string> requestedItems);
    Task<BusinessRuleResult> EvaluateEmergencyEscalationAsync(string messageContent, int tenantId);
    Task<List<BusinessRule>> GetActiveRulesAsync(int tenantId);
    Task<BusinessRuleResult> ApplyRulesAsync(List<BusinessRule> rules, Dictionary<string, object> context);
}

public enum RuleType
{
    ServiceAvailability = 1,
    BookingConstraint = 2,
    TimeConstraint = 3,
    CapacityLimit = 4,
    EmergencyEscalation = 5,
    MenuAvailability = 6,
    RoomServiceLimit = 7,
    StaffAvailability = 8,
    PriorityGuest = 9,
    ComplianceCheck = 10
}

public enum RuleSeverity
{
    Info = 1,        // Informational only
    Warning = 2,     // Should warn but allow
    Block = 3,       // Must block action
    Escalate = 4     // Escalate to human staff
}

public class BusinessRule
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public RuleType Type { get; set; }
    public RuleSeverity Severity { get; set; }
    public string Conditions { get; set; } = string.Empty; // JSON conditions
    public string Actions { get; set; } = string.Empty; // JSON actions
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 1; // 1 = highest
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public string? DaysOfWeek { get; set; } // e.g., "1,2,3,4,5" for Mon-Fri
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? Description { get; set; }
}

public class BusinessRuleResult
{
    public bool IsAllowed { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    public bool RequiresEscalation { get; set; }
    public bool HasCapacityConstraints { get; set; }
    public List<string> Violations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public List<string> AvailableServices { get; set; } = new();
    public Dictionary<string, object> Context { get; set; } = new();
    public List<BusinessRule> TriggeredRules { get; set; } = new();
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public string? AlternativeSuggestion { get; set; }
}

public class BusinessRulesEngine : IBusinessRulesEngine
{
    private readonly HostrDbContext _context;
    private readonly ILogger<BusinessRulesEngine> _logger;
    private readonly ITemporalContextService _temporalContextService;

    // Built-in rule configurations
    private readonly Dictionary<RuleType, List<BusinessRule>> _builtInRules;

    public BusinessRulesEngine(
        HostrDbContext context,
        ILogger<BusinessRulesEngine> logger,
        ITemporalContextService temporalContextService)
    {
        _context = context;
        _logger = logger;
        _temporalContextService = temporalContextService;
        _builtInRules = InitializeBuiltInRules();
    }

    public async Task<BusinessRuleResult> EvaluateServiceAvailabilityAsync(string serviceName, int tenantId, DateTime? requestedTime = null)
    {
        try
        {
            var result = new BusinessRuleResult();
            var evaluationTime = requestedTime ?? DateTime.UtcNow;
            var timeContext = await _temporalContextService.GetCurrentTimeContextAsync(tenantId);

            // Check service exists and is available
            var service = await _context.Services
                .FirstOrDefaultAsync(s => s.TenantId == tenantId &&
                                         s.Name.ToLower() == serviceName.ToLower());

            if (service == null)
            {
                result.IsAllowed = false;
                result.Violations.Add($"Service '{serviceName}' is not available");
                return result;
            }

            if (!service.IsAvailable)
            {
                result.IsAllowed = false;
                result.Violations.Add($"Service '{serviceName}' is currently unavailable");
                return result;
            }

            // Check service hours
            var isServiceAvailable = await _temporalContextService.IsServiceAvailableAsync(tenantId, serviceName, evaluationTime);
            if (!isServiceAvailable)
            {
                result.IsAllowed = false;
                result.Violations.Add($"Service '{serviceName}' is not available at the requested time");

                // Suggest alternative times
                if (!string.IsNullOrEmpty(service.AvailableHours))
                {
                    result.AlternativeSuggestion = $"Service is available during: {service.AvailableHours}";
                }
            }

            // Check advance booking requirements
            if (service.RequiresAdvanceBooking && service.AdvanceBookingHours.HasValue)
            {
                var minBookingTime = DateTime.UtcNow.AddHours(service.AdvanceBookingHours.Value);
                if (evaluationTime < minBookingTime)
                {
                    result.IsAllowed = false;
                    result.Violations.Add($"Service '{serviceName}' requires {service.AdvanceBookingHours} hours advance booking");
                }
            }

            // Apply tenant-specific rules
            var rules = await GetServiceAvailabilityRulesAsync(tenantId);
            var ruleContext = new Dictionary<string, object>
            {
                { "serviceName", serviceName },
                { "requestedTime", evaluationTime },
                { "timeContext", timeContext }
            };

            var ruleResult = await ApplyRulesAsync(rules, ruleContext);
            MergeResults(result, ruleResult);

            _logger.LogInformation("Service availability evaluated: {ServiceName} for tenant {TenantId} - Allowed: {IsAllowed}",
                serviceName, tenantId, result.IsAllowed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating service availability for {ServiceName} at tenant {TenantId}", serviceName, tenantId);
            return new BusinessRuleResult { IsAllowed = false, Violations = { "Unable to evaluate service availability" } };
        }
    }

    public async Task<BusinessRuleResult> ValidateBookingConstraintsAsync(int tenantId, string guestPhone, DateTime requestedTime)
    {
        try
        {
            var result = new BusinessRuleResult();

            // Check existing bookings for conflicts
            var existingBookings = await _context.Bookings
                .Where(b => b.TenantId == tenantId && b.Phone == guestPhone)
                .Where(b => b.CheckInDate <= requestedTime.Date && b.CheckOutDate >= requestedTime.Date)
                .ToListAsync();

            if (existingBookings.Any())
            {
                result.Warnings.Add($"Guest has {existingBookings.Count} existing booking(s) for the requested date");
            }

            // Check for maximum concurrent bookings per guest
            var maxBookingsRule = await GetMaxBookingsRuleAsync(tenantId);
            if (maxBookingsRule != null && existingBookings.Count >= 3) // Default max 3 concurrent bookings
            {
                result.IsAllowed = false;
                result.Violations.Add("Maximum number of concurrent bookings exceeded");
            }

            // Check booking time constraints
            var timeRules = await GetBookingTimeConstraintRulesAsync(tenantId);
            var ruleContext = new Dictionary<string, object>
            {
                { "guestPhone", guestPhone },
                { "requestedTime", requestedTime },
                { "existingBookings", existingBookings }
            };

            var ruleResult = await ApplyRulesAsync(timeRules, ruleContext);
            MergeResults(result, ruleResult);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating booking constraints for guest {GuestPhone} at tenant {TenantId}", guestPhone, tenantId);
            return new BusinessRuleResult { IsAllowed = false, Violations = { "Unable to validate booking constraints" } };
        }
    }

    public async Task<BusinessRuleResult> CheckRoomServiceConstraintsAsync(int tenantId, string roomNumber, List<string> requestedItems)
    {
        try
        {
            var result = new BusinessRuleResult();
            var timeContext = await _temporalContextService.GetCurrentTimeContextAsync(tenantId);

            // Check if room service is available
            var roomServiceAvailable = await _temporalContextService.IsServiceAvailableAsync(tenantId, "Room Service");
            if (!roomServiceAvailable)
            {
                result.IsAllowed = false;
                result.Violations.Add("Room service is not currently available");
                return result;
            }

            // Check menu availability for current meal period
            var unavailableItems = new List<string>();
            foreach (var item in requestedItems)
            {
                var menuItem = await _context.MenuItems
                    .FirstOrDefaultAsync(m => m.TenantId == tenantId &&
                                             m.Name.ToLower().Contains(item.ToLower()) &&
                                             m.IsAvailable);

                if (menuItem == null)
                {
                    unavailableItems.Add(item);
                }
                else if (menuItem.MealType != "all" && menuItem.MealType != timeContext.MealPeriod.ToString().ToLower())
                {
                    result.Warnings.Add($"'{item}' is not typically available during {timeContext.MealPeriod}");
                }
            }

            if (unavailableItems.Any())
            {
                result.Warnings.Add($"Some items may not be available: {string.Join(", ", unavailableItems)}");
            }

            // Check room service delivery constraints
            var deliveryRules = await GetRoomServiceDeliveryRulesAsync(tenantId);
            var ruleContext = new Dictionary<string, object>
            {
                { "roomNumber", roomNumber },
                { "requestedItems", requestedItems },
                { "mealPeriod", timeContext.MealPeriod },
                { "currentTime", timeContext.CurrentTime }
            };

            var ruleResult = await ApplyRulesAsync(deliveryRules, ruleContext);
            MergeResults(result, ruleResult);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking room service constraints for room {RoomNumber} at tenant {TenantId}", roomNumber, tenantId);
            return new BusinessRuleResult { IsAllowed = true, Warnings = { "Unable to fully validate room service constraints" } };
        }
    }

    public async Task<BusinessRuleResult> EvaluateEmergencyEscalationAsync(string messageContent, int tenantId)
    {
        try
        {
            var result = new BusinessRuleResult();

            // Emergency keywords that trigger immediate escalation
            var emergencyKeywords = new[]
            {
                "emergency", "urgent", "help", "fire", "medical", "police", "ambulance",
                "injured", "hurt", "bleeding", "chest pain", "can't breathe", "overdose",
                "assault", "theft", "break in", "flood", "gas leak", "elevator stuck"
            };

            var messageWords = messageContent.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var emergencyMatches = emergencyKeywords.Where(keyword =>
                messageWords.Any(word => word.Contains(keyword))).ToList();

            if (emergencyMatches.Any())
            {
                result.RequiresEscalation = true;
                result.Violations.Add($"Emergency situation detected: {string.Join(", ", emergencyMatches)}");

                // Apply emergency escalation rules
                var emergencyRules = await GetEmergencyEscalationRulesAsync(tenantId);
                var ruleContext = new Dictionary<string, object>
                {
                    { "messageContent", messageContent },
                    { "emergencyKeywords", emergencyMatches },
                    { "detectedAt", DateTime.UtcNow }
                };

                var ruleResult = await ApplyRulesAsync(emergencyRules, ruleContext);
                MergeResults(result, ruleResult);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating emergency escalation for tenant {TenantId}", tenantId);
            return new BusinessRuleResult { RequiresEscalation = true, Violations = { "Unable to evaluate emergency status - escalating for safety" } };
        }
    }

    public async Task<List<BusinessRule>> GetActiveRulesAsync(int tenantId)
    {
        try
        {
            // This would typically load from database
            // For now, return built-in rules
            var allRules = new List<BusinessRule>();

            foreach (var ruleGroup in _builtInRules.Values)
            {
                allRules.AddRange(ruleGroup.Where(r => r.TenantId == tenantId || r.TenantId == 0)); // 0 = global rules
            }

            return allRules.Where(r => r.IsActive && IsRuleEffective(r)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active rules for tenant {TenantId}", tenantId);
            return new List<BusinessRule>();
        }
    }

    public async Task<BusinessRuleResult> ApplyRulesAsync(List<BusinessRule> rules, Dictionary<string, object> context)
    {
        var result = new BusinessRuleResult();

        foreach (var rule in rules.OrderBy(r => r.Priority))
        {
            try
            {
                var ruleApplies = EvaluateRuleConditions(rule, context);
                if (ruleApplies)
                {
                    result.TriggeredRules.Add(rule);
                    ApplyRuleActions(rule, result, context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error applying rule {RuleName}", rule.Name);
                // Continue with other rules
            }
        }

        return result;
    }

    private Dictionary<RuleType, List<BusinessRule>> InitializeBuiltInRules()
    {
        var rules = new Dictionary<RuleType, List<BusinessRule>>();

        // Service Availability Rules
        rules[RuleType.ServiceAvailability] = new List<BusinessRule>
        {
            new BusinessRule
            {
                Id = 1,
                TenantId = 0, // Global rule
                Name = "Restaurant Service Hours",
                Type = RuleType.ServiceAvailability,
                Severity = RuleSeverity.Block,
                StartTime = new TimeSpan(6, 0, 0),
                EndTime = new TimeSpan(23, 0, 0),
                Description = "Restaurant services only available 6 AM - 11 PM"
            }
        };

        // Emergency Escalation Rules
        rules[RuleType.EmergencyEscalation] = new List<BusinessRule>
        {
            new BusinessRule
            {
                Id = 2,
                TenantId = 0,
                Name = "Medical Emergency Escalation",
                Type = RuleType.EmergencyEscalation,
                Severity = RuleSeverity.Escalate,
                Description = "Immediate escalation for medical emergencies"
            }
        };

        return rules;
    }

    private bool IsRuleEffective(BusinessRule rule)
    {
        var now = DateTime.UtcNow;
        if (now < rule.EffectiveFrom) return false;
        if (rule.EffectiveTo.HasValue && now > rule.EffectiveTo.Value) return false;

        // Check day of week constraints
        if (!string.IsNullOrEmpty(rule.DaysOfWeek))
        {
            var allowedDays = rule.DaysOfWeek.Split(',').Select(int.Parse).ToList();
            var currentDay = (int)now.DayOfWeek;
            if (!allowedDays.Contains(currentDay)) return false;
        }

        // Check time constraints
        if (rule.StartTime.HasValue && rule.EndTime.HasValue)
        {
            var currentTime = now.TimeOfDay;
            if (currentTime < rule.StartTime.Value || currentTime > rule.EndTime.Value) return false;
        }

        return true;
    }

    private bool EvaluateRuleConditions(BusinessRule rule, Dictionary<string, object> context)
    {
        // Simplified rule evaluation - in production this would be more sophisticated
        switch (rule.Type)
        {
            case RuleType.ServiceAvailability:
                return EvaluateServiceAvailabilityConditions(rule, context);
            case RuleType.EmergencyEscalation:
                return EvaluateEmergencyConditions(rule, context);
            default:
                return true;
        }
    }

    private bool EvaluateServiceAvailabilityConditions(BusinessRule rule, Dictionary<string, object> context)
    {
        if (rule.StartTime.HasValue && rule.EndTime.HasValue && context.ContainsKey("requestedTime"))
        {
            var requestedTime = (DateTime)context["requestedTime"];
            var timeOfDay = requestedTime.TimeOfDay;
            return timeOfDay >= rule.StartTime.Value && timeOfDay <= rule.EndTime.Value;
        }
        return true;
    }

    private bool EvaluateEmergencyConditions(BusinessRule rule, Dictionary<string, object> context)
    {
        return context.ContainsKey("emergencyKeywords") &&
               ((List<string>)context["emergencyKeywords"]).Any();
    }

    private void ApplyRuleActions(BusinessRule rule, BusinessRuleResult result, Dictionary<string, object> context)
    {
        switch (rule.Severity)
        {
            case RuleSeverity.Block:
                result.IsAllowed = false;
                result.Violations.Add(rule.Description ?? $"Rule '{rule.Name}' prevents this action");
                break;

            case RuleSeverity.Warning:
                result.Warnings.Add(rule.Description ?? $"Rule '{rule.Name}' suggests caution");
                break;

            case RuleSeverity.Escalate:
                result.RequiresEscalation = true;
                result.Violations.Add(rule.Description ?? $"Rule '{rule.Name}' requires human intervention");
                break;

            case RuleSeverity.Info:
                result.Recommendations.Add(rule.Description ?? $"Rule '{rule.Name}' provides information");
                break;
        }
    }

    private void MergeResults(BusinessRuleResult target, BusinessRuleResult source)
    {
        if (!source.IsAllowed) target.IsAllowed = false;
        if (source.RequiresEscalation) target.RequiresEscalation = true;

        target.Violations.AddRange(source.Violations);
        target.Warnings.AddRange(source.Warnings);
        target.Recommendations.AddRange(source.Recommendations);
        target.TriggeredRules.AddRange(source.TriggeredRules);

        foreach (var kvp in source.Context)
        {
            target.Context[kvp.Key] = kvp.Value;
        }
    }

    private async Task<List<BusinessRule>> GetServiceAvailabilityRulesAsync(int tenantId)
    {
        return _builtInRules.GetValueOrDefault(RuleType.ServiceAvailability, new List<BusinessRule>())
            .Where(r => r.TenantId == tenantId || r.TenantId == 0)
            .ToList();
    }

    private async Task<BusinessRule?> GetMaxBookingsRuleAsync(int tenantId)
    {
        return _builtInRules.GetValueOrDefault(RuleType.BookingConstraint, new List<BusinessRule>())
            .FirstOrDefault(r => (r.TenantId == tenantId || r.TenantId == 0) && r.Name.Contains("Max Bookings"));
    }

    private async Task<List<BusinessRule>> GetBookingTimeConstraintRulesAsync(int tenantId)
    {
        return _builtInRules.GetValueOrDefault(RuleType.TimeConstraint, new List<BusinessRule>())
            .Where(r => r.TenantId == tenantId || r.TenantId == 0)
            .ToList();
    }

    private async Task<List<BusinessRule>> GetRoomServiceDeliveryRulesAsync(int tenantId)
    {
        return _builtInRules.GetValueOrDefault(RuleType.RoomServiceLimit, new List<BusinessRule>())
            .Where(r => r.TenantId == tenantId || r.TenantId == 0)
            .ToList();
    }

    private async Task<List<BusinessRule>> GetEmergencyEscalationRulesAsync(int tenantId)
    {
        return _builtInRules.GetValueOrDefault(RuleType.EmergencyEscalation, new List<BusinessRule>())
            .Where(r => r.TenantId == tenantId || r.TenantId == 0)
            .ToList();
    }
}