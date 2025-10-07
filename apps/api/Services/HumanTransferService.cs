using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface IHumanTransferService
{
    Task<TransferDetectionResult> DetectTransferRequestAsync(string messageText, Conversation conversation);
    Task<AvailableAgent?> FindBestAvailableAgentAsync(int tenantId, string department, TransferPriority priority);
    Task<TransferRouting> GetTransferRoutingAsync(TransferRequest request);
    Task<bool> InitiateTransferAsync(int conversationId, int agentId, TransferReason reason);
    Task<bool> CompleteTransferAsync(int conversationId, string transferSummary);
}

public class TransferDetectionResult
{
    public bool ShouldTransfer { get; set; }
    public double Confidence { get; set; }
    public TransferReason Reason { get; set; }
    public TransferPriority Priority { get; set; }
    public string Department { get; set; } = "General";
    public List<string> RequiredSkills { get; set; } = new();
    public string DetectionMethod { get; set; } = ""; // "keyword", "pattern", "llm"
    public string TriggerPhrase { get; set; } = "";
}

public class TransferDetectionResponse
{
    public bool ShouldTransfer { get; set; }
    public double Confidence { get; set; }
    public string Reason { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Department { get; set; } = "";
    public string Reasoning { get; set; } = "";
}

public class HumanTransferService : IHumanTransferService
{
    private readonly HostrDbContext _context;
    private readonly IOpenAIService _openAIService;
    private readonly IAgentAvailabilityService _agentAvailabilityService;
    private readonly IConversationHandoffService _conversationHandoffService;
    private readonly ILogger<HumanTransferService> _logger;

    // Keywords for detecting transfer requests
    private readonly Dictionary<TransferReason, List<string>> _transferKeywords = new()
    {
        [TransferReason.UserRequested] = new()
        {
            "speak to someone", "talk to human", "human agent", "real person",
            "customer service", "representative", "manager", "supervisor",
            "human help", "live agent", "speak to agent", "transfer me",
            "not helpful", "need human", "actual person"
        },
        [TransferReason.EmergencyHandoff] = new()
        {
            "emergency", "urgent", "immediate help", "critical", "serious problem",
            "life threatening", "medical emergency", "security issue", "fire", "ambulance"
        },
        [TransferReason.ComplexityLimit] = new()
        {
            "complicated", "complex", "detailed", "specific", "technical",
            "don't understand", "confusing", "multiple issues"
        },
        [TransferReason.SpecialistRequired] = new()
        {
            "speak to billing", "talk to billing", "billing department",
            "speak to housekeeping", "talk to housekeeping staff", "housekeeping department",
            "speak to maintenance", "talk to maintenance team", "maintenance department",
            "speak to concierge", "talk to concierge", "concierge team",
            "speak to security", "security department", "security team",
            "connect me with", "transfer me to"
        }
    };

    // Patterns for more sophisticated detection
    private readonly List<Regex> _transferPatterns = new()
    {
        new Regex(@"\b(can|could|may)\s+(i|we)\s+(speak|talk)\s+to\s+(someone|human|person|agent|representative)", RegexOptions.IgnoreCase),
        new Regex(@"\b(transfer|connect)\s+me\s+to\s+(human|agent|person|someone)", RegexOptions.IgnoreCase),
        new Regex(@"\b(this\s+)?(is\s+)?(not\s+)?(working|helping|useful)", RegexOptions.IgnoreCase),
        new Regex(@"\b(i\s+)?(need|want|require)\s+(human|real)\s+(help|assistance|person)", RegexOptions.IgnoreCase),
        new Regex(@"\b(get\s+me\s+)?(a\s+)?(real|human|live)\s+(person|agent|representative)", RegexOptions.IgnoreCase)
    };

    public HumanTransferService(
        HostrDbContext context,
        IOpenAIService openAIService,
        IAgentAvailabilityService agentAvailabilityService,
        IConversationHandoffService conversationHandoffService,
        ILogger<HumanTransferService> logger)
    {
        _context = context;
        _openAIService = openAIService;
        _agentAvailabilityService = agentAvailabilityService;
        _conversationHandoffService = conversationHandoffService;
        _logger = logger;
    }

    public async Task<TransferDetectionResult> DetectTransferRequestAsync(string messageText, Conversation conversation)
    {
        try
        {
            var result = new TransferDetectionResult();

            // Use LLM-first approach for intelligent, language-agnostic detection
            // The LLM will handle greetings, item requests, and queries in ANY language
            var llmResult = await DetectTransferByLLMAsync(messageText, conversation);

            if (llmResult.ShouldTransfer)
            {
                // LLM detected a transfer request - use it
                result = llmResult;
                _logger.LogInformation("LLM detected transfer request: {Message}, Confidence: {Confidence}, Reasoning: {Reasoning}",
                    messageText, llmResult.Confidence, llmResult.TriggerPhrase);
            }
            else
            {
                // LLM says no transfer - trust it (it's smarter than regex)
                _logger.LogInformation("LLM determined no transfer needed for message: {Message}", messageText);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting transfer request for message: {Message}", messageText);
            return new TransferDetectionResult { ShouldTransfer = false };
        }
    }

    private bool IsSimpleGreetingOrQuery(string messageText)
    {
        var lowerMessage = messageText.ToLower().Trim();

        // Simple greetings
        var simpleGreetings = new[] { "hi", "hello", "hey", "good morning", "good afternoon", "good evening", "greetings", "howdy", "yo", "hiya" };
        if (simpleGreetings.Contains(lowerMessage))
        {
            return true;
        }

        // Menu-related queries
        var menuKeywords = new[] { "menu", "food", "dining", "restaurant", "breakfast", "lunch", "dinner", "eat" };
        if (menuKeywords.Any(keyword => lowerMessage.Contains(keyword)))
        {
            return true;
        }

        // Common informational queries
        var infoPatterns = new[]
        {
            "what time", "when is", "where is", "how do i", "can i get", "do you have",
            "what is", "where can i", "how much", "what are", "i need", "i want"
        };
        if (infoPatterns.Any(pattern => lowerMessage.Contains(pattern)))
        {
            return true;
        }

        return false;
    }

    private async Task<bool> IsItemRequestAsync(string messageText, int tenantId)
    {
        try
        {
            // Get all RequestItems for this tenant (with caching consideration)
            var requestItemNames = await _context.RequestItems
                .Where(r => r.TenantId == tenantId)
                .Select(r => new { r.LlmVisibleName, r.Name })
                .ToListAsync();

            var lowerMessage = messageText.ToLower();

            // Check if message mentions any request item
            foreach (var item in requestItemNames)
            {
                if (!string.IsNullOrEmpty(item.LlmVisibleName) && lowerMessage.Contains(item.LlmVisibleName.ToLower()))
                {
                    return true;
                }
                if (!string.IsNullOrEmpty(item.Name) && lowerMessage.Contains(item.Name.ToLower()))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for item request, defaulting to false");
            return false;
        }
    }

    private TransferDetectionResult DetectTransferByKeywords(string messageText)
    {
        var lowerMessage = messageText.ToLower();

        foreach (var category in _transferKeywords)
        {
            foreach (var keyword in category.Value)
            {
                if (lowerMessage.Contains(keyword))
                {
                    var priority = category.Key == TransferReason.EmergencyHandoff ?
                        TransferPriority.Emergency : TransferPriority.Normal;

                    return new TransferDetectionResult
                    {
                        ShouldTransfer = true,
                        Confidence = 0.8,
                        Reason = category.Key,
                        Priority = priority,
                        Department = GetDepartmentForReason(category.Key),
                        RequiredSkills = GetSkillsForReason(category.Key),
                        DetectionMethod = "keyword",
                        TriggerPhrase = keyword
                    };
                }
            }
        }

        return new TransferDetectionResult { ShouldTransfer = false };
    }

    private TransferDetectionResult DetectTransferByPatterns(string messageText)
    {
        foreach (var pattern in _transferPatterns)
        {
            var match = pattern.Match(messageText);
            if (match.Success)
            {
                return new TransferDetectionResult
                {
                    ShouldTransfer = true,
                    Confidence = 0.9,
                    Reason = TransferReason.UserRequested,
                    Priority = TransferPriority.Normal,
                    Department = "General",
                    RequiredSkills = new List<string>(),
                    DetectionMethod = "pattern",
                    TriggerPhrase = match.Value
                };
            }
        }

        return new TransferDetectionResult { ShouldTransfer = false };
    }

    private async Task<TransferDetectionResult> DetectTransferByLLMAsync(string messageText, Conversation conversation)
    {
        try
        {
            var recentMessages = await _context.Messages
                .Where(m => m.ConversationId == conversation.Id)
                .OrderByDescending(m => m.CreatedAt)
                .Take(5)
                .Select(m => new { m.Body, m.Direction })
                .ToListAsync();

            var context = string.Join("\n", recentMessages.Select(m =>
                $"{(m.Direction == "Inbound" ? "Guest" : "Bot")}: {m.Body}"));

            var prompt = $@"
Analyze ONLY the latest message to determine if it EXPLICITLY requests to speak with a human agent/staff.

Latest message: {messageText}

CRITICAL: Analyze what the message ACTUALLY SAYS, not the conversation context.

The message is a transfer request ONLY IF it contains BOTH:
1. A communication action word meaning ""speak/talk/connect with""
AND
2. A reference to a human/person/agent/manager/staff

TRANSFER REQUEST EXAMPLES (contain both elements):
✓ ""I want to speak to a manager"" (speak + manager)
✓ ""Connect me with an agent"" (connect + agent)
✓ ""I need to talk to someone"" (talk + someone)
✓ ""Let me speak to a person"" (speak + person)
✓ ""Can I speak with your manager?"" (speak + manager)
✓ ""This bot isn't helping, I need a real person"" (need + person)

NOT TRANSFER REQUESTS (missing one or both elements):
✗ ""I want water"" (want + water = ITEM, not human)
✗ ""I need towels"" (need + towels = ITEM, not human)
✗ ""I want food"" (want + food = ITEM, not human)
✗ ""Show me the menu"" (no human reference)
✗ ""Yes"" (context-dependent answer, not explicit request)
✗ ""I need help"" (help ≠ human, could mean information)
✗ ""Can you help me?"" (you = bot, not transfer request)

LANGUAGE AGNOSTIC RULE:
If the message means ""I want/need [ITEM/SERVICE/FOOD]"" in ANY language → NOT a transfer
If the message means ""I want to speak/talk to [HUMAN/STAFF/MANAGER]"" in ANY language → IS a transfer

The bot may have asked ""shall I add you to queue?"" but IGNORE THIS.
Only flag transfer if the LATEST MESSAGE explicitly requests human contact.

Respond with JSON only:
{{
    ""shouldTransfer"": boolean,
    ""confidence"": number (0.0-1.0),
    ""reason"": ""UserRequested"" | ""EmergencyHandoff"" | ""ComplexityLimit"" | ""SpecialistRequired"",
    ""priority"": ""Low"" | ""Normal"" | ""High"" | ""Urgent"" | ""Emergency"",
    ""department"": ""General"" | ""FrontDesk"" | ""Housekeeping"" | ""Maintenance"" | ""Concierge"" | ""Security"",
    ""reasoning"": ""brief explanation""
}}";

            var response = await _openAIService.GetStructuredResponseAsync<TransferDetectionResponse>(prompt);

            _logger.LogInformation("LLM Transfer Detection - Message: '{Message}', ShouldTransfer: {ShouldTransfer}, Confidence: {Confidence}, Reasoning: {Reasoning}",
                messageText, response?.ShouldTransfer ?? false, response?.Confidence ?? 0, response?.Reasoning ?? "N/A");

            if (response?.ShouldTransfer == true)
            {
                return new TransferDetectionResult
                {
                    ShouldTransfer = true,
                    Confidence = response.Confidence,
                    Reason = Enum.TryParse<TransferReason>(response.Reason, out var reason) ? reason : TransferReason.UserRequested,
                    Priority = Enum.TryParse<TransferPriority>(response.Priority, out var priority) ? priority : TransferPriority.Normal,
                    Department = response.Department ?? "General",
                    DetectionMethod = "llm",
                    TriggerPhrase = response.Reasoning ?? ""
                };
            }

            return new TransferDetectionResult { ShouldTransfer = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM transfer detection");
            return new TransferDetectionResult { ShouldTransfer = false };
        }
    }

    public async Task<AvailableAgent?> FindBestAvailableAgentAsync(int tenantId, string department, TransferPriority priority)
    {
        var availableAgents = await _agentAvailabilityService.GetAvailableAgentsAsync(tenantId, department, priority);
        return availableAgents.FirstOrDefault();
    }

    public async Task<TransferRouting> GetTransferRoutingAsync(TransferRequest request)
    {
        // Use the tenant ID from the conversation
        var conversation = await _context.Conversations.FindAsync(request.ConversationId);
        if (conversation == null)
        {
            return new TransferRouting
            {
                CanTransfer = false,
                UnavailabilityReason = "Conversation not found",
                RecommendedStrategy = TransferStrategy.CreateTicket
            };
        }

        return await _agentAvailabilityService.DetermineOptimalTransferRoutingAsync(conversation.TenantId, request);
    }

    public async Task<bool> InitiateTransferAsync(int conversationId, int agentId, TransferReason reason)
    {
        try
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
                return false;

            // Step 1: Prepare comprehensive handoff context
            var handoffContext = await _conversationHandoffService.PrepareHandoffContextAsync(conversationId, reason);

            // Step 2: Use the AgentAvailabilityService to handle the assignment
            var success = await _agentAvailabilityService.AssignConversationToAgentAsync(conversationId, agentId, reason);

            if (success)
            {
                // Step 3: Update conversation status to indicate transfer
                conversation.Status = "TransferredToAgent";
                conversation.AssignedAgentId = agentId;
                conversation.TransferReason = reason.ToString();
                conversation.TransferredAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Step 4: Notify the agent with full context
                await _conversationHandoffService.NotifyAgentOfTransferAsync(agentId, handoffContext);

                _logger.LogInformation("Initiated transfer for conversation {ConversationId} to agent {AgentId} for reason {Reason}. " +
                                     "Handoff context prepared with {MessageCount} messages and {TaskCount} active tasks.",
                    conversationId, agentId, reason, handoffContext.RecentMessages.Count, handoffContext.ActiveTasks.Count);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating transfer for conversation {ConversationId}", conversationId);
            return false;
        }
    }

    public async Task<bool> CompleteTransferAsync(int conversationId, string transferSummary)
    {
        try
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
                return false;

            conversation.TransferSummary = transferSummary;
            conversation.TransferCompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Completed transfer for conversation {ConversationId}", conversationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing transfer for conversation {ConversationId}", conversationId);
            return false;
        }
    }


    private string GetDepartmentForReason(TransferReason reason)
    {
        return reason switch
        {
            TransferReason.EmergencyHandoff => "Security",
            TransferReason.SpecialistRequired => "General",
            TransferReason.ComplexityLimit => "FrontDesk",
            _ => "General"
        };
    }

    private List<string> GetSkillsForReason(TransferReason reason)
    {
        return reason switch
        {
            TransferReason.EmergencyHandoff => new List<string> { "Emergency Response", "Crisis Management" },
            TransferReason.SpecialistRequired => new List<string> { "Advanced Problem Solving" },
            TransferReason.ComplexityLimit => new List<string> { "Customer Service", "Problem Resolution" },
            _ => new List<string> { "General Support" }
        };
    }
}