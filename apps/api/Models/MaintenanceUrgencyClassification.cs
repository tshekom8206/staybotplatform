namespace Hostr.Api.Models;

/// <summary>
/// Result of classifying a maintenance request's urgency
/// Returned by ClassifyMaintenanceUrgency() method
/// </summary>
public class MaintenanceUrgencyClassification
{
    public string UrgencyLevel { get; set; } = "NORMAL"; // EMERGENCY, URGENT, HIGH, NORMAL, LOW

    public string TaskPriority { get; set; } = "Normal"; // Urgent, High, Normal, Low

    public int TargetMinutes { get; set; } = 240; // SLA in minutes

    public bool SafetyRisk { get; set; } = false;

    public bool HabitabilityImpact { get; set; } = false;

    public bool RequiresManagerEscalation { get; set; } = false;

    public string Category { get; set; } = "General"; // Gas Leak, Plumbing, Electrical, etc.

    public string? ResponseTemplate { get; set; } // Pre-formatted template with placeholders

    public string? SafetyInstructions { get; set; } // Safety instructions for guest
}
