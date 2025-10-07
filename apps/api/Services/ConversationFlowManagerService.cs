using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;

namespace Hostr.Api.Services;

public interface IConversationFlowManagerService
{
    Task<ConversationFlowResult> ManageConversationFlowAsync(ConversationFlowRequest request);
    Task<FlowDecision> DetermineNextFlowStepAsync(int conversationId, string currentMessage, ConversationState state);
    Task<bool> ShouldContinueFlowAsync(ConversationFlow flow, ConversationState state);
    Task<ConversationFlow> CreateNewFlowAsync(string flowType, int conversationId, Dictionary<string, object> initialData);
    Task<ConversationFlow?> GetActiveFlowAsync(int conversationId);
    Task CompleteFlowAsync(int conversationId, string completionReason);
}


public class ConversationFlowRequest
{
    public int ConversationId { get; set; }
    public int TenantId { get; set; }
    public string CurrentMessage { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public ConversationState ConversationState { get; set; } = new();
    public TimeContext TimeContext { get; set; } = new();
    public AmbiguityResult? AmbiguityResult { get; set; }
    public List<RelevantContext> RelevantContexts { get; set; } = new();
}

public class ConversationFlowResult
{
    public FlowDecision Decision { get; set; } = new();
    public ConversationFlow? ActiveFlow { get; set; }
    public List<FlowStep> NextSteps { get; set; } = new();
    public bool FlowCompleted { get; set; }
    public bool RequiresUserInput { get; set; }
    public string NextExpectedInput { get; set; } = string.Empty;
    public Dictionary<string, object> FlowData { get; set; } = new();
    public double FlowConfidence { get; set; }
    public string ReasoningTrace { get; set; } = string.Empty;
}

public class FlowDecision
{
    public string Action { get; set; } = string.Empty; // continue, pause, complete, escalate, redirect
    public string Reasoning { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public FlowStepType NextStepType { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool RequiresHumanIntervention { get; set; }
}


public class ConversationFlowManagerService : IConversationFlowManagerService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<ConversationFlowManagerService> _logger;
    private readonly IConversationStateService _conversationStateService;
    private readonly IAmbiguityDetectionService _ambiguityDetectionService;
    private readonly IBusinessRulesEngine _businessRulesEngine;
    private readonly ITemporalContextService _temporalContextService;

    // Flow templates for different conversation types
    private static readonly Dictionary<FlowType, List<FlowStepTemplate>> FlowTemplates = new()
    {
        {
            FlowType.MultiStepBooking, new List<FlowStepTemplate>
            {
                new() { StepType = FlowStepType.Information, Title = "Booking Intent", Description = "Confirm booking request" },
                new() { StepType = FlowStepType.Question, Title = "Check-in Date", Description = "When would you like to check in?" },
                new() { StepType = FlowStepType.Question, Title = "Check-out Date", Description = "When would you like to check out?" },
                new() { StepType = FlowStepType.Question, Title = "Guest Count", Description = "How many guests will be staying?" },
                new() { StepType = FlowStepType.Question, Title = "Room Preferences", Description = "Do you have any specific room preferences?" },
                new() { StepType = FlowStepType.Confirmation, Title = "Booking Summary", Description = "Please confirm your booking details" },
                new() { StepType = FlowStepType.Action, Title = "Create Booking", Description = "Process the booking request" },
                new() { StepType = FlowStepType.Completion, Title = "Booking Complete", Description = "Provide booking confirmation" }
            }
        },
        {
            FlowType.ServiceRequest, new List<FlowStepTemplate>
            {
                new() { StepType = FlowStepType.Information, Title = "Service Request", Description = "Understand service needed" },
                new() { StepType = FlowStepType.Question, Title = "Service Type", Description = "What type of service do you need?" },
                new() { StepType = FlowStepType.Question, Title = "Urgency", Description = "How urgent is this request?" },
                new() { StepType = FlowStepType.Question, Title = "Location", Description = "Where should the service be provided?" },
                new() { StepType = FlowStepType.Confirmation, Title = "Service Summary", Description = "Please confirm service request details" },
                new() { StepType = FlowStepType.Action, Title = "Create Task", Description = "Create staff task for service" },
                new() { StepType = FlowStepType.Completion, Title = "Service Scheduled", Description = "Confirm service has been scheduled" }
            }
        },
        {
            FlowType.ComplaintResolution, new List<FlowStepTemplate>
            {
                new() { StepType = FlowStepType.Information, Title = "Complaint Received", Description = "Acknowledge the complaint" },
                new() { StepType = FlowStepType.Question, Title = "Issue Details", Description = "Can you provide more details about the issue?" },
                new() { StepType = FlowStepType.Question, Title = "When Occurred", Description = "When did this issue occur?" },
                new() { StepType = FlowStepType.Question, Title = "Previous Contact", Description = "Have you contacted us about this before?" },
                new() { StepType = FlowStepType.Confirmation, Title = "Issue Summary", Description = "Let me confirm I understand the issue correctly" },
                new() { StepType = FlowStepType.Action, Title = "Resolution Plan", Description = "Propose resolution approach" },
                new() { StepType = FlowStepType.Escalation, Title = "Manager Review", Description = "Escalate to management if needed" },
                new() { StepType = FlowStepType.Completion, Title = "Resolution Confirmed", Description = "Confirm resolution is satisfactory" }
            }
        },
        {
            FlowType.Clarification, new List<FlowStepTemplate>
            {
                new() { StepType = FlowStepType.Information, Title = "Clarification Needed", Description = "Identify what needs clarification" },
                new() { StepType = FlowStepType.Question, Title = "Clarifying Question", Description = "Ask specific clarifying question" },
                new() { StepType = FlowStepType.Confirmation, Title = "Understanding Check", Description = "Confirm understanding of clarification" },
                new() { StepType = FlowStepType.Completion, Title = "Clarification Complete", Description = "Continue with original intent" }
            }
        }
    };

    public ConversationFlowManagerService(
        HostrDbContext context,
        ILogger<ConversationFlowManagerService> logger,
        IConversationStateService conversationStateService,
        IAmbiguityDetectionService ambiguityDetectionService,
        IBusinessRulesEngine businessRulesEngine,
        ITemporalContextService temporalContextService)
    {
        _context = context;
        _logger = logger;
        _conversationStateService = conversationStateService;
        _ambiguityDetectionService = ambiguityDetectionService;
        _businessRulesEngine = businessRulesEngine;
        _temporalContextService = temporalContextService;
    }

    public async Task<ConversationFlowResult> ManageConversationFlowAsync(ConversationFlowRequest request)
    {
        try
        {
            var result = new ConversationFlowResult();

            // Step 1: Check for existing active flow
            var activeFlow = await GetActiveFlowAsync(request.ConversationId);

            if (activeFlow != null)
            {
                // Continue existing flow
                result = await ContinueExistingFlowAsync(activeFlow, request);
            }
            else
            {
                // Determine if we need to start a new flow
                var flowDecision = await ShouldStartNewFlowAsync(request);

                if (flowDecision.ShouldStart)
                {
                    // Start new flow
                    result = await StartNewFlowAsync(flowDecision.FlowType, request);
                }
                else
                {
                    // Handle as simple interaction
                    result = CreateSimpleInteractionResult(request);
                }
            }

            // Calculate flow confidence
            result.FlowConfidence = CalculateFlowConfidence(result, request);

            _logger.LogInformation("Managed conversation flow for conversation {ConversationId}, action: {Action}",
                request.ConversationId, result.Decision.Action);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error managing conversation flow for conversation {ConversationId}",
                request.ConversationId);

            return new ConversationFlowResult
            {
                Decision = new FlowDecision
                {
                    Action = "error",
                    Reasoning = "Error in flow management",
                    Confidence = 0.1,
                    RequiresHumanIntervention = true
                },
                FlowCompleted = false
            };
        }
    }

    public async Task<FlowDecision> DetermineNextFlowStepAsync(int conversationId, string currentMessage, ConversationState state)
    {
        try
        {
            var activeFlow = await GetActiveFlowAsync(conversationId);
            if (activeFlow == null)
            {
                return new FlowDecision
                {
                    Action = "no_flow",
                    Reasoning = "No active flow found",
                    Confidence = 1.0
                };
            }

            var currentStep = GetCurrentStep(activeFlow);
            if (currentStep == null)
            {
                return new FlowDecision
                {
                    Action = "complete",
                    Reasoning = "All flow steps completed",
                    Confidence = 1.0
                };
            }

            // Analyze current message against expected step
            var messageAnalysis = await AnalyzeMessageForStep(currentMessage, currentStep);

            // Determine next action based on analysis
            return await CreateFlowDecision(activeFlow, currentStep, messageAnalysis, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining next flow step for conversation {ConversationId}", conversationId);

            return new FlowDecision
            {
                Action = "error",
                Reasoning = "Error in flow step determination",
                Confidence = 0.1,
                RequiresHumanIntervention = true
            };
        }
    }

    public async Task<bool> ShouldContinueFlowAsync(ConversationFlow flow, ConversationState state)
    {
        try
        {
            // Check flow status
            if (flow.Status != FlowStatus.Active)
                return false;

            // Check if flow has been abandoned (no activity for too long)
            var lastActivity = state.LastUpdated;
            var timeThreshold = flow.FlowType switch
            {
                FlowType.EmergencyEscalation => TimeSpan.FromMinutes(5),
                FlowType.ComplaintResolution => TimeSpan.FromMinutes(30),
                FlowType.MultiStepBooking => TimeSpan.FromHours(2),
                _ => TimeSpan.FromHours(1)
            };

            if (DateTime.UtcNow - lastActivity > timeThreshold)
            {
                await AbandonFlowAsync(flow.Id, "Timeout due to inactivity");
                return false;
            }

            // Check if user explicitly wants to exit
            if (state.Variables.TryGetValue("exit_flow", out var exitFlow) && exitFlow == "true")
            {
                return false;
            }

            // Check if flow prerequisites are still met
            return await ValidateFlowPrerequisites(flow, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if flow should continue for flow {FlowId}", flow.Id);
            return false;
        }
    }

    public async Task<ConversationFlow> CreateNewFlowAsync(string flowType, int conversationId, Dictionary<string, object> initialData)
    {
        try
        {
            if (!Enum.TryParse<FlowType>(flowType, out var parsedFlowType))
            {
                parsedFlowType = FlowType.SimpleQuery;
            }

            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId);
            if (conversation == null)
            {
                throw new InvalidOperationException($"Conversation {conversationId} not found");
            }

            var flow = new ConversationFlow
            {
                ConversationId = conversationId,
                TenantId = conversation.TenantId,
                FlowType = parsedFlowType,
                Status = FlowStatus.Active,
                CurrentStepIndex = 0,
                FlowData = JsonDocument.Parse(JsonSerializer.Serialize(initialData)),
                CollectedData = JsonDocument.Parse("{}"),
                Steps = CreateFlowSteps(parsedFlowType)
            };

            _context.ConversationFlows.Add(flow);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new flow {FlowType} for conversation {ConversationId}",
                parsedFlowType, conversationId);

            return flow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new flow {FlowType} for conversation {ConversationId}",
                flowType, conversationId);
            throw;
        }
    }

    public async Task<ConversationFlow?> GetActiveFlowAsync(int conversationId)
    {
        try
        {
            return await _context.ConversationFlows
                .Include(f => f.Steps)
                .FirstOrDefaultAsync(f => f.ConversationId == conversationId && f.Status == FlowStatus.Active);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active flow for conversation {ConversationId}", conversationId);
            return null;
        }
    }

    public async Task CompleteFlowAsync(int conversationId, string completionReason)
    {
        try
        {
            var activeFlow = await GetActiveFlowAsync(conversationId);
            if (activeFlow != null)
            {
                activeFlow.Status = FlowStatus.Completed;
                activeFlow.CompletedAt = DateTime.UtcNow;
                activeFlow.CompletionReason = completionReason;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Completed flow for conversation {ConversationId}: {Reason}",
                    conversationId, completionReason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing flow for conversation {ConversationId}", conversationId);
        }
    }

    private async Task<ConversationFlowResult> ContinueExistingFlowAsync(ConversationFlow flow, ConversationFlowRequest request)
    {
        var result = new ConversationFlowResult { ActiveFlow = flow };

        // Check if flow should continue
        if (!await ShouldContinueFlowAsync(flow, request.ConversationState))
        {
            await CompleteFlowAsync(request.ConversationId, "Flow discontinued");
            result.FlowCompleted = true;
            result.Decision = new FlowDecision
            {
                Action = "complete",
                Reasoning = "Flow was discontinued",
                Confidence = 1.0
            };
            return result;
        }

        // Process current step
        var currentStep = GetCurrentStep(flow);
        if (currentStep == null)
        {
            await CompleteFlowAsync(request.ConversationId, "All steps completed");
            result.FlowCompleted = true;
            result.Decision = new FlowDecision
            {
                Action = "complete",
                Reasoning = "All flow steps completed",
                Confidence = 1.0
            };
            return result;
        }

        // Analyze user's response for current step
        var stepAnalysis = await AnalyzeMessageForStep(request.CurrentMessage, currentStep);

        // Update step based on analysis
        await UpdateStepWithResponse(currentStep, stepAnalysis, request.CurrentMessage);

        // Determine next action
        result.Decision = await CreateFlowDecision(flow, currentStep, stepAnalysis, request.ConversationState);

        // Move to next step if current step is complete
        if (stepAnalysis.IsStepComplete)
        {
            await MoveToNextStep(flow);
        }

        // Get next steps
        result.NextSteps = GetNextSteps(flow, 3);
        result.RequiresUserInput = DetermineIfUserInputRequired(currentStep);
        result.NextExpectedInput = GetExpectedInputDescription(currentStep);
        result.FlowData = GetFlowDataDictionary(flow);

        return result;
    }

    private async Task<ConversationFlowResult> StartNewFlowAsync(FlowType flowType, ConversationFlowRequest request)
    {
        var initialData = new Dictionary<string, object>
        {
            { "intent", request.Intent },
            { "startTime", DateTime.UtcNow },
            { "phoneNumber", request.PhoneNumber },
            { "tenantId", request.TenantId }
        };

        var flow = await CreateNewFlowAsync(flowType.ToString(), request.ConversationId, initialData);

        var result = new ConversationFlowResult
        {
            ActiveFlow = flow,
            FlowCompleted = false,
            NextSteps = GetNextSteps(flow, 3),
            Decision = new FlowDecision
            {
                Action = "start",
                Reasoning = $"Started new {flowType} flow",
                Confidence = 0.8,
                NextStepType = flow.Steps.FirstOrDefault()?.StepType ?? FlowStepType.Information
            }
        };

        var firstStep = flow.Steps.FirstOrDefault();
        if (firstStep != null)
        {
            result.RequiresUserInput = DetermineIfUserInputRequired(firstStep);
            result.NextExpectedInput = GetExpectedInputDescription(firstStep);
        }

        result.FlowData = GetFlowDataDictionary(flow);

        return result;
    }

    private ConversationFlowResult CreateSimpleInteractionResult(ConversationFlowRequest request)
    {
        return new ConversationFlowResult
        {
            FlowCompleted = true,
            Decision = new FlowDecision
            {
                Action = "simple_response",
                Reasoning = "Single-turn interaction, no flow needed",
                Confidence = 1.0,
                NextStepType = FlowStepType.Information
            },
            RequiresUserInput = false
        };
    }

    private async Task<FlowStartDecision> ShouldStartNewFlowAsync(ConversationFlowRequest request)
    {
        // Analyze intent complexity and requirements
        var intentAnalysis = AnalyzeIntentComplexity(request.Intent, request.CurrentMessage);

        // Check for ambiguity that requires clarification flow
        if (request.AmbiguityResult != null && request.AmbiguityResult.HasAmbiguity)
        {
            return new FlowStartDecision
            {
                ShouldStart = true,
                FlowType = FlowType.Clarification,
                Reasoning = "Ambiguity detected requiring clarification flow"
            };
        }

        // Check for multi-step processes
        if (intentAnalysis.RequiresMultipleSteps)
        {
            var flowType = DetermineFlowType(request.Intent, intentAnalysis);
            return new FlowStartDecision
            {
                ShouldStart = true,
                FlowType = flowType,
                Reasoning = $"Multi-step process detected: {intentAnalysis.Reasoning}"
            };
        }

        return new FlowStartDecision
        {
            ShouldStart = false,
            FlowType = FlowType.SimpleQuery,
            Reasoning = "Simple query requiring single response"
        };
    }

    private IntentComplexityAnalysis AnalyzeIntentComplexity(string intent, string message)
    {
        var analysis = new IntentComplexityAnalysis();

        // Analyze for booking-related complexity
        if (intent.Contains("booking") || intent.Contains("reservation"))
        {
            analysis.RequiresMultipleSteps = true;
            analysis.Reasoning = "Booking requests typically require multiple pieces of information";
            analysis.EstimatedSteps = 5;
        }
        // Analyze for service requests
        else if (intent.Contains("service") || intent.Contains("request"))
        {
            analysis.RequiresMultipleSteps = !IsSimpleServiceRequest(message);
            analysis.Reasoning = analysis.RequiresMultipleSteps ?
                "Service request requires details collection" :
                "Simple service request";
            analysis.EstimatedSteps = analysis.RequiresMultipleSteps ? 4 : 1;
        }
        // Analyze for complaints
        else if (intent.Contains("complaint") || intent.Contains("issue"))
        {
            analysis.RequiresMultipleSteps = true;
            analysis.Reasoning = "Complaint resolution requires structured information gathering";
            analysis.EstimatedSteps = 6;
        }
        // Simple queries
        else
        {
            analysis.RequiresMultipleSteps = false;
            analysis.Reasoning = "Simple query or information request";
            analysis.EstimatedSteps = 1;
        }

        return analysis;
    }

    private bool IsSimpleServiceRequest(string message)
    {
        var simpleRequestPatterns = new[]
        {
            "towel", "pillow", "blanket", "water", "coffee",
            "ice", "soap", "shampoo", "toilet paper"
        };

        return simpleRequestPatterns.Any(pattern =>
            message.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private FlowType DetermineFlowType(string intent, IntentComplexityAnalysis analysis)
    {
        return intent.ToLower() switch
        {
            var i when i.Contains("booking") || i.Contains("reservation") => FlowType.MultiStepBooking,
            var i when i.Contains("service") || i.Contains("request") => FlowType.ServiceRequest,
            var i when i.Contains("complaint") || i.Contains("issue") => FlowType.ComplaintResolution,
            var i when i.Contains("menu") || i.Contains("food") => FlowType.MenuInquiry,
            var i when i.Contains("emergency") => FlowType.EmergencyEscalation,
            _ => FlowType.InformationGathering
        };
    }

    private List<FlowStep> CreateFlowSteps(FlowType flowType)
    {
        if (!FlowTemplates.TryGetValue(flowType, out var templates))
        {
            templates = FlowTemplates[FlowType.SimpleQuery];
        }

        return templates.Select((template, index) => new FlowStep
        {
            StepIndex = index,
            StepType = template.StepType,
            Title = template.Title,
            Description = template.Description,
            IsRequired = template.IsRequired,
            IsCompleted = false
        }).ToList();
    }

    private FlowStep? GetCurrentStep(ConversationFlow flow)
    {
        return flow.Steps
            .Where(s => s.StepIndex >= flow.CurrentStepIndex)
            .OrderBy(s => s.StepIndex)
            .FirstOrDefault(s => !s.IsCompleted);
    }

    private async Task<StepAnalysis> AnalyzeMessageForStep(string message, FlowStep step)
    {
        var analysis = new StepAnalysis();

        switch (step.StepType)
        {
            case FlowStepType.Question:
                analysis = await AnalyzeQuestionResponse(message, step);
                break;
            case FlowStepType.Confirmation:
                analysis = AnalyzeConfirmationResponse(message);
                break;
            case FlowStepType.Information:
                analysis = AnalyzeInformationStep(message, step);
                break;
            default:
                analysis.IsStepComplete = true;
                analysis.Confidence = 1.0;
                break;
        }

        return analysis;
    }

    private async Task<StepAnalysis> AnalyzeQuestionResponse(string message, FlowStep step)
    {
        var analysis = new StepAnalysis();

        // Determine question type based on step title
        var questionType = DetermineQuestionType(step.Title);

        switch (questionType)
        {
            case "date":
                analysis = AnalyzeDateResponse(message);
                break;
            case "number":
                analysis = AnalyzeNumberResponse(message);
                break;
            case "choice":
                analysis = AnalyzeChoiceResponse(message, step);
                break;
            default:
                analysis = AnalyzeTextResponse(message);
                break;
        }

        return analysis;
    }

    private StepAnalysis AnalyzeConfirmationResponse(string message)
    {
        var confirmationPatterns = new[] { "yes", "confirm", "correct", "right", "ok", "okay", "sure" };
        var rejectionPatterns = new[] { "no", "wrong", "incorrect", "cancel", "stop" };

        var isConfirmation = confirmationPatterns.Any(p =>
            message.Contains(p, StringComparison.OrdinalIgnoreCase));
        var isRejection = rejectionPatterns.Any(p =>
            message.Contains(p, StringComparison.OrdinalIgnoreCase));

        return new StepAnalysis
        {
            IsStepComplete = isConfirmation || isRejection,
            Confidence = isConfirmation || isRejection ? 0.9 : 0.3,
            ExtractedValue = isConfirmation ? "confirmed" : (isRejection ? "rejected" : "unclear"),
            ValidationResult = isConfirmation || isRejection ? "valid" : "needs_clarification"
        };
    }

    private StepAnalysis AnalyzeInformationStep(string message, FlowStep step)
    {
        return new StepAnalysis
        {
            IsStepComplete = true,
            Confidence = 0.8,
            ExtractedValue = message,
            ValidationResult = "valid"
        };
    }

    private string DetermineQuestionType(string stepTitle)
    {
        return stepTitle.ToLower() switch
        {
            var title when title.Contains("date") => "date",
            var title when title.Contains("count") || title.Contains("number") => "number",
            var title when title.Contains("preference") || title.Contains("type") => "choice",
            _ => "text"
        };
    }

    private StepAnalysis AnalyzeDateResponse(string message)
    {
        // Simple date parsing - in production, use more sophisticated date parsing
        var datePatterns = new[]
        {
            @"\d{1,2}\/\d{1,2}\/\d{2,4}",
            @"\d{1,2}-\d{1,2}-\d{2,4}",
            @"(today|tomorrow|yesterday)",
            @"(monday|tuesday|wednesday|thursday|friday|saturday|sunday)"
        };

        var hasDatePattern = datePatterns.Any(pattern =>
            System.Text.RegularExpressions.Regex.IsMatch(message, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));

        return new StepAnalysis
        {
            IsStepComplete = hasDatePattern,
            Confidence = hasDatePattern ? 0.8 : 0.2,
            ExtractedValue = message,
            ValidationResult = hasDatePattern ? "valid" : "needs_clarification"
        };
    }

    private StepAnalysis AnalyzeNumberResponse(string message)
    {
        var numberMatch = System.Text.RegularExpressions.Regex.Match(message, @"\d+");
        var hasNumber = numberMatch.Success;

        return new StepAnalysis
        {
            IsStepComplete = hasNumber,
            Confidence = hasNumber ? 0.9 : 0.1,
            ExtractedValue = hasNumber ? numberMatch.Value : message,
            ValidationResult = hasNumber ? "valid" : "needs_clarification"
        };
    }

    private StepAnalysis AnalyzeChoiceResponse(string message, FlowStep step)
    {
        // This would be enhanced with actual choice options from step data
        return new StepAnalysis
        {
            IsStepComplete = !string.IsNullOrWhiteSpace(message),
            Confidence = 0.7,
            ExtractedValue = message,
            ValidationResult = "valid"
        };
    }

    private StepAnalysis AnalyzeTextResponse(string message)
    {
        var isSubstantive = message.Length > 5 && !string.IsNullOrWhiteSpace(message);

        return new StepAnalysis
        {
            IsStepComplete = isSubstantive,
            Confidence = isSubstantive ? 0.8 : 0.3,
            ExtractedValue = message,
            ValidationResult = isSubstantive ? "valid" : "needs_more_detail"
        };
    }

    private async Task<FlowDecision> CreateFlowDecision(ConversationFlow flow, FlowStep currentStep, StepAnalysis analysis, ConversationState state)
    {
        var decision = new FlowDecision();

        if (analysis.IsStepComplete && analysis.ValidationResult == "valid")
        {
            decision.Action = "continue";
            decision.Reasoning = "Step completed successfully, continuing to next step";
            decision.Confidence = analysis.Confidence;
            decision.NextStepType = GetNextStepType(flow, currentStep);
        }
        else if (analysis.ValidationResult == "needs_clarification")
        {
            decision.Action = "clarify";
            decision.Reasoning = "Response needs clarification";
            decision.Confidence = 0.7;
            decision.NextStepType = FlowStepType.Question;
        }
        else
        {
            decision.Action = "repeat";
            decision.Reasoning = "Step not completed, repeating question";
            decision.Confidence = 0.5;
            decision.NextStepType = currentStep.StepType;
        }

        return decision;
    }

    private FlowStepType GetNextStepType(ConversationFlow flow, FlowStep currentStep)
    {
        var nextStep = flow.Steps
            .Where(s => s.StepIndex > currentStep.StepIndex)
            .OrderBy(s => s.StepIndex)
            .FirstOrDefault();

        return nextStep?.StepType ?? FlowStepType.Completion;
    }

    private async Task UpdateStepWithResponse(FlowStep step, StepAnalysis analysis, string message)
    {
        if (analysis.IsStepComplete)
        {
            step.IsCompleted = true;
            step.CompletedAt = DateTime.UtcNow;
            step.CollectedValue = analysis.ExtractedValue;
            await _context.SaveChangesAsync();
        }
    }

    private async Task MoveToNextStep(ConversationFlow flow)
    {
        flow.CurrentStepIndex++;
        await _context.SaveChangesAsync();
    }

    private List<FlowStep> GetNextSteps(ConversationFlow flow, int count)
    {
        return flow.Steps
            .Where(s => s.StepIndex >= flow.CurrentStepIndex)
            .OrderBy(s => s.StepIndex)
            .Take(count)
            .ToList();
    }

    private bool DetermineIfUserInputRequired(FlowStep step)
    {
        return step.StepType == FlowStepType.Question || step.StepType == FlowStepType.Confirmation;
    }

    private string GetExpectedInputDescription(FlowStep step)
    {
        return step.StepType switch
        {
            FlowStepType.Question => step.Description,
            FlowStepType.Confirmation => "Please confirm (yes/no)",
            _ => "Continue conversation"
        };
    }

    private Dictionary<string, object> GetFlowDataDictionary(ConversationFlow flow)
    {
        var data = new Dictionary<string, object>
        {
            { "flowType", flow.FlowType.ToString() },
            { "currentStep", flow.CurrentStepIndex },
            { "totalSteps", flow.Steps.Count },
            { "progress", flow.ProgressPercentage },
            { "status", flow.Status.ToString() }
        };

        // Add collected step data
        foreach (var step in flow.Steps.Where(s => s.IsCompleted && !string.IsNullOrEmpty(s.CollectedValue)))
        {
            data[$"step_{step.StepIndex}_{step.Title.Replace(" ", "_").ToLower()}"] = step.CollectedValue!;
        }

        return data;
    }

    private async Task<bool> ValidateFlowPrerequisites(ConversationFlow flow, ConversationState state)
    {
        // Check business hours for certain flow types
        if (flow.FlowType == FlowType.ServiceRequest)
        {
            var timeContext = await _temporalContextService.GetCurrentTimeContextAsync(flow.TenantId);
            // Allow service requests outside business hours but log for follow-up
            return true;
        }

        return true;
    }

    private async Task AbandonFlowAsync(int flowId, string reason)
    {
        var flow = await _context.ConversationFlows.FindAsync(flowId);
        if (flow != null)
        {
            flow.Status = FlowStatus.Abandoned;
            flow.CompletionReason = reason;
            flow.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private double CalculateFlowConfidence(ConversationFlowResult result, ConversationFlowRequest request)
    {
        double confidence = result.Decision.Confidence;

        // Adjust based on flow state
        if (result.ActiveFlow != null)
        {
            var progressFactor = result.ActiveFlow.ProgressPercentage / 100.0 * 0.2;
            confidence += progressFactor;
        }

        // Adjust based on context quality
        if (request.RelevantContexts.Any())
        {
            var contextQuality = request.RelevantContexts.Average(c => c.RelevanceScore) * 0.1;
            confidence += contextQuality;
        }

        return Math.Min(confidence, 1.0);
    }
}

// Helper classes
public class FlowStepTemplate
{
    public FlowStepType StepType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
}

public class FlowStartDecision
{
    public bool ShouldStart { get; set; }
    public FlowType FlowType { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class IntentComplexityAnalysis
{
    public bool RequiresMultipleSteps { get; set; }
    public int EstimatedSteps { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class StepAnalysis
{
    public bool IsStepComplete { get; set; }
    public double Confidence { get; set; }
    public string ExtractedValue { get; set; } = string.Empty;
    public string ValidationResult { get; set; } = string.Empty;
}