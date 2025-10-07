using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Hostr.Api.Models;

public enum FlowType
{
    SimpleQuery = 1,
    MultiStepBooking = 2,
    ServiceRequest = 3,
    ComplaintResolution = 4,
    MenuInquiry = 5,
    EmergencyEscalation = 6,
    InformationGathering = 7,
    Clarification = 8
}

public enum FlowStatus
{
    Active = 1,
    Paused = 2,
    Completed = 3,
    Abandoned = 4,
    Escalated = 5
}

public enum FlowStepType
{
    Information = 1,
    Question = 2,
    Confirmation = 3,
    Action = 4,
    Escalation = 5,
    Completion = 6
}

public class ConversationFlow
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public int TenantId { get; set; }
    public FlowType FlowType { get; set; }
    public FlowStatus Status { get; set; }
    public int CurrentStepIndex { get; set; }
    public JsonDocument? FlowData { get; set; }
    public JsonDocument? CollectedData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string CompletionReason { get; set; } = string.Empty;
    public List<FlowStep> Steps { get; set; } = new();
    public double ProgressPercentage => Steps.Any() ? (double)CurrentStepIndex / Steps.Count * 100 : 0;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Conversation Conversation { get; set; } = null!;
}

public class FlowStep
{
    public int Id { get; set; }
    public int ConversationFlowId { get; set; }
    public int StepIndex { get; set; }
    public FlowStepType StepType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonDocument? StepData { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CollectedValue { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? ValidationRule { get; set; }

    // Navigation properties
    public virtual ConversationFlow ConversationFlow { get; set; } = null!;
}