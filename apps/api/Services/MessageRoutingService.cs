using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Services;
using Hostr.Api.Configuration;
using Pgvector;

namespace Hostr.Api.Services;

public class MessageRoutingResponse
{
    public string Reply { get; set; } = string.Empty;
    public JsonElement? Action { get; set; }
    public List<JsonElement>? Actions { get; set; }
    public bool UsedRag { get; set; }
    public string? Model { get; set; }
    public int? TokensPrompt { get; set; }
    public int? TokensCompletion { get; set; }
    public string? ActionType { get; set; }
}

public class GreetingResponse
{
    public string Greeting { get; set; } = string.Empty;
}

public enum MessageIntent
{
    TimingResponse,
    Greeting,
    ServiceRequest,
    MaintenanceIssue,
    MenuRequest,
    Other
}

public class MessageIntentResult
{
    public MessageIntent Intent { get; set; }
    public double Confidence { get; set; }
    public string Source { get; set; } = ""; // "regex", "llm", "hybrid"
    public string? Reasoning { get; set; }
}

public class ItemRequestAnalysisResult
{
    public bool IsItemRequest { get; set; }
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class QuantityClarificationAnalysis
{
    public bool IsQuantityClarification { get; set; }
    public bool IsNewRequest { get; set; }
    public bool IsCancellation { get; set; }
    public bool IsAmbiguous { get; set; }
    public string? ItemSlug { get; set; }
    public string? ItemName { get; set; }
    public int? Quantity { get; set; }
    public string[]? PossibleItems { get; set; }
    public string? ClarificationNeeded { get; set; }
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class LostItemDetails
{
    public string? ItemName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Brand { get; set; }
    public string? LocationLost { get; set; }
    public string? WhenLost { get; set; }
    public string? Urgency { get; set; }
}

public interface IMessageRoutingService
{
    Task<MessageRoutingResponse> RouteMessageAsync(TenantContext tenantContext, Conversation conversation, Message message);
    Task<GuestStatus> DetermineGuestStatusAsync(string phoneNumber, int tenantId);
}

public class MessageRoutingService : IMessageRoutingService
{
    private readonly HostrDbContext _context;
    private readonly IOpenAIService _openAIService;
    private readonly IPlanGuardService _planGuard;
    private readonly IMenuService _menuService;
    private readonly IEmergencyService _emergencyService;
    private readonly INotificationService _notificationService;
    private readonly IResponseTemplateService _responseTemplateService;
    private readonly ILogger<MessageRoutingService> _logger;
    private readonly MessageClassificationOptions _classificationOptions;

    // Phase 1: Context-Aware Enhancement Services
    private readonly ITemporalContextService _temporalContextService;
    private readonly IConversationStateService _conversationStateService;
    private readonly IAmbiguityDetectionService _ambiguityDetectionService;
    private readonly ILLMIntentAnalysisService _llmIntentAnalysisService;

    // Phase 2: Advanced Context and Business Logic Services
    private readonly IClarificationStrategyService _clarificationStrategyService;
    private readonly IContextRelevanceService _contextRelevanceService;
    private readonly IBusinessRulesEngine _businessRulesEngine;
    private readonly ILLMBusinessRulesEngine _llmBusinessRulesEngine;
    private readonly IMessageNormalizationService _messageNormalizationService;
    private readonly ISmartContextManagerService _smartContextManagerService;
    private readonly IConfigurationBasedResponseService _configurationBasedResponseService;
    private readonly IResponseDeduplicationService _responseDeduplicationService;
    private readonly IHumanTransferService _humanTransferService;
    private readonly IFAQService _faqService;
    private readonly IInformationGatheringService _informationGatheringService;
    private readonly ILostAndFoundService _lostAndFoundService;
    private readonly IConfiguration _configuration;

    public MessageRoutingService(
        HostrDbContext context,
        IOpenAIService openAIService,
        IPlanGuardService planGuard,
        IMenuService menuService,
        IEmergencyService emergencyService,
        INotificationService notificationService,
        IResponseTemplateService responseTemplateService,
        ILogger<MessageRoutingService> logger,
        IOptions<MessageClassificationOptions> classificationOptions,
        ITemporalContextService temporalContextService,
        IConversationStateService conversationStateService,
        IAmbiguityDetectionService ambiguityDetectionService,
        ILLMIntentAnalysisService llmIntentAnalysisService,
        IClarificationStrategyService clarificationStrategyService,
        IContextRelevanceService contextRelevanceService,
        IBusinessRulesEngine businessRulesEngine,
        ILLMBusinessRulesEngine llmBusinessRulesEngine,
        IMessageNormalizationService messageNormalizationService,
        ISmartContextManagerService smartContextManagerService,
        IConfigurationBasedResponseService configurationBasedResponseService,
        IResponseDeduplicationService responseDeduplicationService,
        IHumanTransferService humanTransferService,
        IFAQService faqService,
        IInformationGatheringService informationGatheringService,
        ILostAndFoundService lostAndFoundService,
        IConfiguration configuration)
    {
        _context = context;
        _openAIService = openAIService;
        _planGuard = planGuard;
        _menuService = menuService;
        _emergencyService = emergencyService;
        _notificationService = notificationService;
        _responseTemplateService = responseTemplateService;
        _logger = logger;
        _classificationOptions = classificationOptions.Value;
        _temporalContextService = temporalContextService;
        _conversationStateService = conversationStateService;
        _ambiguityDetectionService = ambiguityDetectionService;
        _llmIntentAnalysisService = llmIntentAnalysisService;
        _clarificationStrategyService = clarificationStrategyService;
        _contextRelevanceService = contextRelevanceService;
        _businessRulesEngine = businessRulesEngine;
        _llmBusinessRulesEngine = llmBusinessRulesEngine;
        _messageNormalizationService = messageNormalizationService;
        _smartContextManagerService = smartContextManagerService;
        _configurationBasedResponseService = configurationBasedResponseService;
        _responseDeduplicationService = responseDeduplicationService;
        _humanTransferService = humanTransferService;
        _faqService = faqService;
        _informationGatheringService = informationGatheringService;
        _lostAndFoundService = lostAndFoundService;
        _configuration = configuration;
    }

    public async Task<MessageRoutingResponse> RouteMessageAsync(TenantContext tenantContext, Conversation conversation, Message message)
    {
        // Step 0: Normalize message text to fix common typos and abbreviations
        var originalMessage = message.Body.Trim();
        var normalizedMessage = _messageNormalizationService.NormalizeMessage(originalMessage);
        var messageText = normalizedMessage.ToLower();

        _logger.LogInformation("MessageRoutingService: Processing message '{MessageText}' for tenant {TenantId}", messageText, tenantContext.TenantId);

        if (normalizedMessage != originalMessage)
        {
            _logger.LogInformation("Message normalized: '{Original}' -> '{Normalized}'", originalMessage, normalizedMessage);
        }

        // Step 0.5: Check for pending clarifications (state-based routing)
        var pendingState = await _conversationStateService.GetAnyPendingStateForConversationAsync(conversation.Id);

        if (pendingState != null)
        {
            _logger.LogInformation("Found pending state: {StateType} for {EntityType}:{EntityId}. Processing user response: '{Response}'",
                pendingState.StateType, pendingState.EntityType, pendingState.EntityId, normalizedMessage);

            // Handle lost item location clarification
            if (pendingState.StateType == ConversationStateType.AwaitingClarification
                && pendingState.EntityType == Models.EntityType.LostItem
                && pendingState.PendingField == Models.PendingField.Location)
            {
                var updated = await _lostAndFoundService.UpdateLostItemLocationAsync(
                    tenantContext.TenantId,
                    pendingState.EntityId,
                    normalizedMessage
                );

                if (updated)
                {
                    await _conversationStateService.ResolvePendingStateAsync(pendingState.Id);
                    _logger.LogInformation("Successfully updated lost item {LostItemId} with location '{Location}'", pendingState.EntityId, normalizedMessage);

                    return new MessageRoutingResponse
                    {
                        Reply = $"Thank you! I've updated your lost item report with the location: {normalizedMessage}. Our staff will search that area and contact you if we find it.",
                        ActionType = "lost_item_location_updated"
                    };
                }
                else
                {
                    _logger.LogError("Failed to update lost item {LostItemId} with location", pendingState.EntityId);
                    return new MessageRoutingResponse
                    {
                        Reply = "Thank you for the information. I've logged your response and our staff will follow up with you.",
                        ActionType = "acknowledgment"
                    };
                }
            }
        }

        // Step 1: Check for emergency situations - highest priority
        var emergencyResult = await _emergencyService.ProcessEmergencyAsync(
            tenantContext,
            normalizedMessage,
            conversation.WaUserPhone,
            conversation.Id,
            null); // Location can be enhanced later

        if (emergencyResult.IsEmergency && emergencyResult.Incident != null)
        {
            _logger.LogCritical("Emergency incident {IncidentId} created from message in conversation {ConversationId}",
                emergencyResult.Incident.Id, conversation.Id);

            return new MessageRoutingResponse
            {
                Reply = "🚨 **Emergency Detected** 🚨\n\n" +
                        "I've immediately alerted our team about your emergency, and they're responding with the highest priority. " +
                        "An incident has been created and is being handled right now.\n\n" +
                        "**Incident ID:** " + emergencyResult.Incident.Id + "\n\n" +
                        "I'm contacting emergency services and our staff as appropriate. " +
                        "Please remain calm and follow any instructions from our staff."
            };
        }

        // Step 1.2: Check FAQ knowledge base for quick answers (before transfer detection to avoid false positives)
        try
        {
            var faqResults = await _faqService.SearchFAQsAsync(normalizedMessage, tenantContext.TenantId, null, 3);
            if (faqResults.Any() && faqResults[0].RelevanceScore >= 0.65)
            {
                var topFAQ = faqResults[0];
                _logger.LogInformation("FAQ match found for question '{Question}' with relevance score {Score}",
                    topFAQ.Question, topFAQ.RelevanceScore);

                var faqResponse = $"**{topFAQ.Question}**\n\n{topFAQ.Answer}";

                // If multiple FAQs have high relevance, show them as related questions
                if (faqResults.Count > 1 && faqResults[1].RelevanceScore >= 0.5)
                {
                    faqResponse += "\n\n**Related questions you might find helpful:**";
                    for (int i = 1; i < Math.Min(faqResults.Count, 3); i++)
                    {
                        if (faqResults[i].RelevanceScore >= 0.5)
                        {
                            faqResponse += $"\n• {faqResults[i].Question}";
                        }
                    }
                }

                return new MessageRoutingResponse
                {
                    Reply = faqResponse
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching FAQs for tenant {TenantId}", tenantContext.TenantId);
            // Continue to normal processing if FAQ search fails
        }

        // Step 2: Check for human transfer requests - after FAQ check to reduce false positives
        var transferDetection = await _humanTransferService.DetectTransferRequestAsync(normalizedMessage, conversation);

        if (transferDetection.ShouldTransfer)
        {
            _logger.LogInformation("Transfer request detected for conversation {ConversationId}: {Reason} (confidence: {Confidence})",
                conversation.Id, transferDetection.Reason, transferDetection.Confidence);

            // Check if agents are available
            var transferRequest = new TransferRequest
            {
                ConversationId = conversation.Id,
                PhoneNumber = conversation.WaUserPhone,
                PreferredDepartment = transferDetection.Department,
                Priority = transferDetection.Priority,
                Reason = transferDetection.Reason,
                RequestMessage = normalizedMessage,
                ConversationContext = new Dictionary<string, object>
                {
                    ["TriggerPhrase"] = transferDetection.TriggerPhrase,
                    ["DetectionMethod"] = transferDetection.DetectionMethod,
                    ["Confidence"] = transferDetection.Confidence
                },
                RequiredSkills = transferDetection.RequiredSkills,
                IsEmergency = transferDetection.Priority == TransferPriority.Emergency
            };

            var routing = await _humanTransferService.GetTransferRoutingAsync(transferRequest);

            if (routing.CanTransfer && routing.RecommendedAgent != null)
            {
                // Attempt to initiate transfer
                var transferSuccess = await _humanTransferService.InitiateTransferAsync(
                    conversation.Id,
                    routing.RecommendedAgent.AgentId,
                    transferDetection.Reason);

                if (transferSuccess)
                {
                    return new MessageRoutingResponse
                    {
                        Reply = "🧑‍💼 **Wonderful! I'm connecting you with a team member...**\n\n" +
                                $"I'm personally transferring you to one of our {transferDetection.Department.ToLower()} specialists who will be delighted to assist you. " +
                                "Please hold on just a moment while I connect you.\n\n" +
                                $"**Agent:** {routing.RecommendedAgent.Name}\n" +
                                $"**Department:** {routing.RecommendedAgent.Department}\n\n" +
                                "They'll be with you very shortly!"
                    };
                }
            }
            else
            {
                // No agents available - queue or provide alternatives
                return new MessageRoutingResponse
                {
                    Reply = "🧑‍💼 **I'd be happy to connect you with a team member!**\n\n" +
                            "I understand you'd like to speak with someone personally. " +
                            (routing.UnavailabilityReason != null ? $"At the moment, {routing.UnavailabilityReason.ToLower()}. " : "") +
                            (routing.EstimatedWaitTime != null ? $"The estimated wait time is {routing.EstimatedWaitTime}.\n\n" : "\n") +
                            "In the meantime, I'm here to help with:\n" +
                            "• Room service and housekeeping requests\n" +
                            "• General hotel information\n" +
                            "• Menu and dining options\n" +
                            "• Local recommendations\n\n" +
                            "Would you like me to help with any of these right now, or shall I add you to the queue for the next available agent?"
                };
            }
        }

        // Step 1.3: Check if we're in information gathering mode (INTEGRATION POINT A)
        if (conversation.ConversationMode == "GatheringBookingInfo" &&
            !string.IsNullOrEmpty(conversation.BookingInfoState))
        {
            _logger.LogInformation("Resuming booking information gathering for conversation {ConversationId}", conversation.Id);
            var existingState = JsonSerializer.Deserialize<BookingInformationState>(conversation.BookingInfoState);
            return await HandleBookingInformationGathering(tenantContext, conversation, normalizedMessage, existingState);
        }

        // Step 1.4: Check for contextual follow-up responses (highest priority - timing responses, etc)
        _logger.LogInformation("MessageRoutingService: Loading conversation history for conversation {ConversationId}", conversation.Id);
        var conversationHistory = await LoadConversationHistoryAsync(conversation.Id);
        _logger.LogInformation("MessageRoutingService: Calling ProcessContextualResponse with {MessageCount} messages", conversationHistory.Count);
        var contextualResponse = await ProcessContextualResponse(conversationHistory, messageText, tenantContext, conversation);
        if (contextualResponse != null)
        {
            _logger.LogInformation("MessageRoutingService: Found contextual response: '{Reply}'", contextualResponse.Reply);
            return contextualResponse;
        }
        _logger.LogInformation("MessageRoutingService: No contextual response found, continuing to other checks");

        // Phase 1 Enhancement: Initialize conversation state and temporal context
        await _conversationStateService.MarkInteractionAsync(conversation.Id);
        await _conversationStateService.StoreLastUserMessageAsync(conversation.Id, message.Body);

        var timeContext = await _temporalContextService.GetCurrentTimeContextAsync(tenantContext.TenantId);
        _logger.LogInformation("Temporal context for tenant {TenantId}: {TimeOfDay} ({Timezone})",
            tenantContext.TenantId, timeContext.TimeOfDayDescription, timeContext.Timezone);

        // Use LLM-based intent analysis for intelligent ambiguity detection
        try
        {
            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);
            var intentAnalysis = await _llmIntentAnalysisService.AnalyzeMessageIntentAsync(
                normalizedMessage,
                tenantContext,
                conversation.Id,
                guestStatus);

            if (intentAnalysis.IsAmbiguous && intentAnalysis.Confidence >= 0.6)
            {
                _logger.LogInformation("LLM detected ambiguity in message. Intent: {Intent}, Category: {Category}, Specificity: {Specificity}",
                    intentAnalysis.Intent, intentAnalysis.Category, intentAnalysis.SpecificityLevel);

                // Store the warm, welcoming clarification
                if (!string.IsNullOrEmpty(intentAnalysis.ClarificationQuestion))
                {
                    await _conversationStateService.AddPendingClarificationAsync(conversation.Id, intentAnalysis.ClarificationQuestion);
                }

                // Store the response
                var warmResponse = intentAnalysis.WarmResponse ?? intentAnalysis.ClarificationQuestion;
                await _conversationStateService.StoreLastBotResponseAsync(conversation.Id, warmResponse);

                return new MessageRoutingResponse
                {
                    Reply = warmResponse
                };
            }

            // If not ambiguous but we have a direct warm response, check if we need to create tasks
            if (!string.IsNullOrEmpty(intentAnalysis.WarmResponse) && intentAnalysis.Confidence >= 0.8)
            {
                _logger.LogInformation("LLM provided direct response for intent: {Intent}", intentAnalysis.Intent);

                // For item requests and booking changes, we should proceed to processing flow
                if (intentAnalysis.Intent == "REQUEST_ITEM")
                {
                    _logger.LogInformation("LLM detected non-ambiguous item request, proceeding to create task");
                    // Continue to normal processing flow to create tasks through ProcessWithEnhancedRAG
                }
                else if (intentAnalysis.Intent == "BOOKING_CHANGE")
                {
                    _logger.LogInformation("LLM detected booking change request, starting booking information gathering");
                    // Directly trigger booking information gathering flow
                    return await HandleBookingInformationGathering(tenantContext, conversation, normalizedMessage, null);
                }
                else if (intentAnalysis.Intent == "LOST_AND_FOUND")
                {
                    _logger.LogInformation("LLM detected lost item report, starting lost & found processing");
                    // Directly trigger lost item reporting flow
                    return await HandleLostItemReporting(tenantContext, conversation, normalizedMessage, intentAnalysis);
                }
                else
                {
                    // For non-item requests, use the warm response directly
                    await _conversationStateService.StoreLastBotResponseAsync(conversation.Id, intentAnalysis.WarmResponse);

                    return new MessageRoutingResponse
                    {
                        Reply = intentAnalysis.WarmResponse
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM intent analysis failed, falling back to regex-based detection");

            // Fallback to original regex-based detection if LLM fails
            var ambiguityResult = await _ambiguityDetectionService.AnalyzeMessageAsync(
                message.Body, tenantContext.TenantId, conversation.Id);

            if (ambiguityResult.IsAmbiguous && ambiguityResult.Confidence >= ConfidenceLevel.Medium)
            {
                _logger.LogInformation("Detected ambiguity in message (fallback): {AmbiguityTypes}",
                    string.Join(", ", ambiguityResult.AmbiguityTypes));

                foreach (var clarification in ambiguityResult.ClarificationQuestions)
                {
                    await _conversationStateService.AddPendingClarificationAsync(conversation.Id, clarification);
                }

                var clarificationResponse = await GenerateClarificationResponseAsync(ambiguityResult, conversation.Id, tenantContext.TenantId, message.Body);
                await _conversationStateService.StoreLastBotResponseAsync(conversation.Id, clarificationResponse);

                return new MessageRoutingResponse
                {
                    Reply = clarificationResponse
                };
            }
        }

        // Phase 2 Enhancement: LLM-based Business Rules Validation
        _logger.LogInformation("Running LLM-based business rules validation for message: {MessageText}", normalizedMessage);

        // Build smart conversation context (declare outside try block for wider scope)
        var conversationContext = await _smartContextManagerService.BuildContextAsync(conversation.Id, tenantContext.TenantId);

        try
        {
            // Get guest status for LLM analysis
            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

            // Check if the current message is contextually relevant
            var isContextRelevant = await _smartContextManagerService.IsContextRelevantAsync(conversation.Id, normalizedMessage);

            _logger.LogInformation("Smart context analysis: Topic={Topic}, Relevant={IsRelevant}",
                conversationContext.CurrentTopic, isContextRelevant);

            // Step 1: Analyze message with LLM for intent and semantic understanding
            var analysis = await _llmBusinessRulesEngine.AnalyzeMessageAsync(
                normalizedMessage,
                tenantContext,
                guestStatus,
                conversationContext);

            _logger.LogInformation("LLM Analysis: Intent={Intent}, Category={Category}, Item={Item}, Confidence={Confidence}",
                analysis.PrimaryIntent, analysis.ServiceCategory, analysis.SpecificItem, analysis.OverallConfidence);

            // INTEGRATION POINT A: Detect booking service requests early (before LLM response generation)
            _logger.LogInformation("🔍 Integration Point A - Checking for booking service. Intent={Intent}, Category={Category}",
                analysis.PrimaryIntent, analysis.ServiceCategory);

            // Accept REQUEST_ITEM, BOOKING, and BOOKING_CHANGE intents for bookable services
            var validIntents = new[] { "REQUEST_ITEM", "BOOKING", "BOOKING_CHANGE" };

            if (validIntents.Contains(analysis.PrimaryIntent, StringComparer.OrdinalIgnoreCase) && analysis.ServiceCategory != null)
            {
                _logger.LogInformation("✅ Intent is {Intent} and Category is not null", analysis.PrimaryIntent);

                // Normalize category name to handle legacy/alias names
                var normalizedCategory = ServiceCategoryConstants.NormalizeCategory(analysis.ServiceCategory);
                _logger.LogInformation("📋 Normalized category '{Original}' → '{Normalized}'", analysis.ServiceCategory, normalizedCategory);

                // Check if this is a bookable service category using standardized constants
                if (ServiceCategoryConstants.IsBookable(normalizedCategory))
                {
                    _logger.LogInformation("🎯 Early detection: Booking service request detected for category {Category} (normalized: {Normalized}), item {Item}",
                        analysis.ServiceCategory, normalizedCategory, analysis.SpecificItem);

                    // Start information gathering immediately with normalized category
                    return await HandleBookingInformationGathering(tenantContext, conversation, normalizedMessage, null);
                }
                else
                {
                    _logger.LogWarning("❌ Category '{Category}' (normalized: '{Normalized}') NOT bookable. Bookable categories: [{BookableList}]",
                        analysis.ServiceCategory, normalizedCategory, string.Join(", ", ServiceCategoryConstants.BookableCategories));
                }
            }
            else
            {
                _logger.LogInformation("❌ Intent check failed. Intent={Intent}, CategoryNull={IsNull}",
                    analysis.PrimaryIntent, analysis.ServiceCategory == null);
            }

            // Step 2: Validate business rules based on LLM analysis
            var violations = await _llmBusinessRulesEngine.ValidateBusinessRulesAsync(analysis, tenantContext);

            if (violations.Any(v => v.Severity == "BLOCK"))
            {
                _logger.LogInformation("LLM Business rules violations detected: {Violations}",
                    string.Join(", ", violations.Select(v => $"{v.RuleName}: {v.Message}")));

                // Create intelligent business-rules response using LLM analysis
                var businessResponse = await GenerateLLMBusinessRulesResponseAsync(violations, analysis, tenantContext.TenantId);
                await _conversationStateService.StoreLastBotResponseAsync(conversation.Id, businessResponse);

                return new MessageRoutingResponse
                {
                    Reply = businessResponse
                };
            }
            else if (violations.Any(v => v.Severity == "WARNING"))
            {
                _logger.LogInformation("LLM Business rules warnings detected: {Warnings}",
                    string.Join(", ", violations.Select(v => $"{v.RuleName}: {v.Message}")));
                // Continue with processing but log warnings
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM business rules validation, falling back to regular processing");
            // Continue with normal processing if LLM analysis fails
        }

        // Step 1.3: Check for maintenance issues (high priority - before other responses)
        var maintenanceResponse = await CheckMaintenanceIssues(tenantContext, conversation, messageText);
        if (maintenanceResponse != null)
        {
            _logger.LogInformation("MessageRoutingService: Found maintenance issue response: '{Reply}'", maintenanceResponse.Reply);
            return maintenanceResponse;
        }

        // Step 1.4: Hybrid message intent classification (replaces old greeting detection)
        var intentResult = await ClassifyMessageIntentAsync(messageText, conversationHistory);
        _logger.LogInformation("MessageRoutingService: Intent classification - Intent: {Intent}, Confidence: {Confidence}, Source: {Source}, Reasoning: {Reasoning}", 
            intentResult.Intent, intentResult.Confidence, intentResult.Source, intentResult.Reasoning);
            
        // Handle classified intents
        if (intentResult.Intent == MessageIntent.Greeting && intentResult.Confidence > _classificationOptions.GreetingConfidenceThreshold)
        {
            var greetingResponse = await CreateGreetingResponseAsync(tenantContext, conversation, messageText);
            _logger.LogInformation("MessageRoutingService: Found greeting response: '{Reply}'", greetingResponse.Reply);
            return greetingResponse;
        }

        // Handle menu requests via LLM classification
        if (intentResult.Intent == MessageIntent.MenuRequest && intentResult.Confidence > 0.7)
        {
            _logger.LogInformation("MessageRoutingService: Detected menu request via LLM classification");
            var menuResponse = await CheckMenuQueries(tenantContext, messageText);
            if (menuResponse != null)
            {
                _logger.LogInformation("MessageRoutingService: Found menu response: '{Reply}'", menuResponse.Reply);

                // Check for additional intents (e.g., "I need towels and what time is breakfast?")
                // Process fallback scenarios even when we have a menu response
                await TryFallbackDetection(tenantContext, conversation, menuResponse);

                return menuResponse;
            }
        }

        // Handle timing responses (this will catch cases that contextual processing missed)
        if (intentResult.Intent == MessageIntent.TimingResponse && intentResult.Confidence > 0.7)
        {
            _logger.LogInformation("MessageRoutingService: Detected timing response via intent classification - processing with contextual logic");
            // Let it fall through to enhanced LLM processing to handle the timing appropriately
        }

        // Step 1.6: Fallback menu check - DISABLED to allow LLM multi-intent analysis to run first
        // The LLM intent analysis (line 420) handles multi-intent messages properly, including menu requests
        // This fallback was preventing multi-intent detection from working correctly
        // Example: "I need towels and what time is breakfast?" should be handled by LLM, not keyword matching

        // _logger.LogInformation("MessageRoutingService: Checking menu queries as fallback for message '{MessageText}'", messageText);
        // var fallbackMenuResponse = await CheckMenuQueries(tenantContext, messageText);
        // if (fallbackMenuResponse != null)
        // {
        //     _logger.LogInformation("MessageRoutingService: Found fallback menu response: '{Reply}'", fallbackMenuResponse.Reply);
        //     await TryFallbackDetection(tenantContext, conversation, fallbackMenuResponse);
        //     return fallbackMenuResponse;
        // }

        // Step 2: Check critical FAQs only (WiFi password, emergency contacts)
        if (IsCriticalQuery(messageText))
        {
            _logger.LogInformation("MessageRoutingService: Checking critical FAQs for message '{MessageText}'", messageText);
            var faqResponse = await CheckFAQs(tenantContext.TenantId, messageText);
            if (faqResponse != null)
            {
                _logger.LogInformation("MessageRoutingService: Found FAQ response: '{Reply}'", faqResponse.Reply);
                return faqResponse;
            }
        }

        // Step 3: Go directly to enhanced LLM with full context
        _logger.LogInformation("MessageRoutingService: Processing with enhanced LLM for message '{MessageText}'", messageText);
        var llmResponse = await ProcessWithEnhancedRAG(tenantContext, conversation, messageText, conversationHistory);
        if (llmResponse != null)
        {
            // Update context with successful processing
            await UpdateContextAfterResponse(conversation.Id, llmResponse, conversationContext.CurrentTopic);
            return llmResponse;
        }

        // Step 4: Fallback clarifying response
        await _smartContextManagerService.UpdateContextAsync(conversation.Id, "clarification_needed", "fallback_response");
        return new MessageRoutingResponse
        {
            Reply = "I'd love to help you! Could you please tell me a bit more about what you need? I'm here to assist with room rates, facilities, services, or any other hotel information you'd like to know."
        };
    }

    private async Task<bool> CheckEmergencyRules(string messageText)
    {
        var emergencyKeywords = new[] { "emergency", "urgent", "help", "human", "agent", "speak to someone" };
        return emergencyKeywords.Any(keyword => messageText.Contains(keyword));
    }

    private bool IsCriticalQuery(string messageText)
    {
        var messageLower = messageText.ToLower();
        var criticalQueries = new[] { "wifi password", "emergency", "fire", "medical", "check-in time", "check-out time" };
        return criticalQueries.Any(query => messageLower.Contains(query));
    }

    private async Task<MessageRoutingResponse?> CheckFAQs(int tenantId, string messageText)
    {
        try
        {
            _logger.LogInformation("CheckFAQs: Searching for '{MessageText}' in tenant {TenantId} FAQs", messageText, tenantId);
            using var scope = new TenantScope(_context, tenantId);
            
            // Use PostgreSQL trigram similarity
            // Use raw SQL query to avoid EF mapping issues with text[] fields
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT ""Question"", ""Answer""
                FROM ""FAQs"" 
                WHERE ""TenantId"" = @tenantId 
                AND similarity(LOWER(""Question""), @messageText) >= 0.5 
                ORDER BY similarity(LOWER(""Question""), @messageText) DESC 
                LIMIT 1";
            
            var tenantParam = command.CreateParameter();
            tenantParam.ParameterName = "@tenantId";
            tenantParam.Value = tenantId;
            command.Parameters.Add(tenantParam);
            
            var messageParam = command.CreateParameter();
            messageParam.ParameterName = "@messageText";
            messageParam.Value = messageText.ToLower();
            command.Parameters.Add(messageParam);
            
            await _context.Database.OpenConnectionAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var question = reader.GetString(0);
                var answer = reader.GetString(1);
                
                return new MessageRoutingResponse
                {
                    Reply = answer
                };
            }


            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking FAQs");
            return null;
        }
    }

    private async Task<MessageRoutingResponse?> CheckSemanticFAQs(int tenantId, string messageText)
    {
        try
        {
            // Get embedding for the message
            var messageEmbedding = await _openAIService.GetEmbeddingAsync(messageText);
            if (messageEmbedding == null) return null;

            using var scope = new TenantScope(_context, tenantId);
            
            // First, get FAQ embeddings (simplified - in production, pre-compute and store FAQ embeddings)
            var faqs = await _context.FAQs.Select(f => new FAQ 
            { 
                Id = f.Id, 
                TenantId = f.TenantId, 
                Question = f.Question, 
                Answer = f.Answer, 
                Language = f.Language,
                Tags = new string[0],
                UpdatedAt = f.UpdatedAt
            }).ToListAsync();
            
            foreach (var faq in faqs)
            {
                var faqEmbedding = await _openAIService.GetEmbeddingAsync(faq.Question);
                if (faqEmbedding != null)
                {
                    var similarity = CosineSimilarity(messageEmbedding, faqEmbedding);
                    if (similarity >= 0.85)
                    {
                        return new MessageRoutingResponse
                        {
                            Reply = faq.Answer
                        };
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking semantic FAQs");
            return null;
        }
    }

    private async Task<MessageRoutingResponse?> ProcessWithRAG(TenantContext tenantContext, Conversation conversation, string messageText)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);

            // Load recent conversation history (last 10 messages)
            var conversationHistory = await LoadConversationHistoryAsync(conversation.Id);

            // Get embedding for the message
            var messageEmbedding = await _openAIService.GetEmbeddingAsync(messageText);
            if (messageEmbedding == null) return null;

            // Find relevant knowledge base chunks
            var relevantChunks = await _context.KnowledgeBaseChunks
                .FromSqlRaw(@"
                    SELECT *, (""Embedding"" <=> {0}) as distance 
                    FROM ""KnowledgeBaseChunks"" 
                    WHERE ""TenantId"" = {1} 
                    AND (""Embedding"" <=> {0}) <= 0.4 
                    ORDER BY distance 
                    LIMIT 5", 
                    new Vector(messageEmbedding), tenantContext.TenantId)
                .ToListAsync();

            if (!relevantChunks.Any()) return null;

            // Build context from chunks
            var context = string.Join("\n\n", relevantChunks.Select(c => c.Content));
            
            // Get request items context (if Premium plan)
            var itemsContext = "";
            if (_planGuard.IsPremium(tenantContext.Plan))
            {
                var requestItems = await _context.RequestItems
                    .Where(r => r.TenantId == tenantContext.TenantId && r.IsAvailable && (r.StockCount - r.InServiceCount) > 0)
                    .ToListAsync();
                
                itemsContext = string.Join("\n", requestItems.Select(r => 
                    $"- {r.LlmVisibleName}: {r.Name} (Stock: {r.StockCount - r.InServiceCount})"));
            }

            // Get tenant system prompt
            var systemPrompt = await GetSystemPrompt(tenantContext);
            
            // Generate response using OpenAI with conversation history
            var llmResponse = await _openAIService.GenerateResponseWithHistoryAsync(
                systemPrompt,
                context,
                itemsContext,
                conversationHistory,
                messageText,
                conversation.WaUserPhone);

            if (llmResponse != null)
            {
                _logger.LogInformation("LLM Response received - Action: {Action}, Actions: {Actions}",
                    llmResponse.Action?.ToString() ?? "null",
                    llmResponse.Actions?.Length.ToString() ?? "null");

                // STEP: Validate and potentially replace response with configuration-based version
                var validationResult = await _configurationBasedResponseService.ValidateResponseAgainstConfigurationAsync(
                    llmResponse.Reply, messageText, tenantContext.TenantId);
                var validatedResponse = validationResult.CorrectedResponse ?? llmResponse.Reply;

                // CRITICAL: Check for hallucinated services (e.g., "rooftop pool" when only "Swimming Pool" exists)
                validatedResponse = await ValidateAndCorrectServiceHallucinations(validatedResponse, tenantContext.TenantId, messageText);

                // STEP: Check for duplicate responses to prevent sending the same response twice
                var isDuplicate = await _responseDeduplicationService.IsResponseDuplicateAsync(
                    conversation.Id, validatedResponse, TimeSpan.FromMinutes(10));

                if (isDuplicate)
                {
                    _logger.LogWarning("Duplicate response detected for conversation {ConversationId}, modifying response",
                        conversation.Id);

                    // Modify response slightly to avoid exact duplicate while maintaining meaning
                    validatedResponse = await ModifyResponseToAvoidDuplicateAsync(validatedResponse);
                }

                // Mark response as being sent to track for future deduplication
                var responseHash = await _responseDeduplicationService.GetResponseHashAsync(validatedResponse);
                await _responseDeduplicationService.MarkResponseSentAsync(conversation.Id, validatedResponse, responseHash);

                // Process extracted actions to create tasks
                var response = new MessageRoutingResponse
                {
                    Reply = validatedResponse, // Use validated response instead of raw LLM response
                    Action = llmResponse.Action,
                    Actions = llmResponse.Actions?.ToList(),
                    UsedRag = true,
                    Model = llmResponse.Model,
                    TokensPrompt = llmResponse.TokensPrompt,
                    TokensCompletion = llmResponse.TokensCompletion
                };

                await ProcessExtractedActions(tenantContext, conversation, response);

                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing with RAG");
            return null;
        }
    }

    private async Task<MessageRoutingResponse?> ProcessWithEnhancedRAG(TenantContext tenantContext, Conversation conversation, string messageText, List<(string Role, string Content)> conversationHistory)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);
            
            _logger.LogInformation("🔍 GUEST ACCESS CONTROL - ProcessWithEnhancedRAG started for phone: {Phone}", conversation.WaUserPhone);

            // STEP 1: Determine guest status FIRST (before any response generation)
            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);
            _logger.LogInformation("🔍 GUEST ACCESS CONTROL - Guest status determined: {Type} for {PhoneNumber} - CanRequestItems: {CanRequestItems}", 
                guestStatus.Type, guestStatus.PhoneNumber, guestStatus.CanRequestItems);

            // STEP 2: Check if this is a request that the guest cannot make (using LLM-based analysis)
            var isItemRequest = await IsItemRequestAsync(messageText, tenantContext, conversation.Id, guestStatus);
            if (isItemRequest && !guestStatus.CanRequestItems)
            {
                _logger.LogInformation("🚫 GUEST ACCESS CONTROL - Blocking item request from {GuestType} guest: {Message}", 
                    guestStatus.Type, messageText);
                
                var blockedResponse = GenerateBlockedRequestResponse(guestStatus, messageText);
                return new MessageRoutingResponse
                {
                    Reply = blockedResponse,
                    UsedRag = false // This is a hard-blocked response
                };
            }

            // Build comprehensive context
            var contextBuilder = new System.Text.StringBuilder();

            // 1. Load FAQs
            var faqs = await _context.FAQs
                .Where(f => f.TenantId == tenantContext.TenantId)
                .Select(f => $"Q: {f.Question}\nA: {f.Answer}")
                .ToListAsync();

            if (faqs.Any())
            {
                contextBuilder.AppendLine("=== FREQUENTLY ASKED QUESTIONS ===");
                contextBuilder.AppendLine(string.Join("\n\n", faqs));
            }

            // 2. Load Business Info (WiFi, hours, policies, etc.)
            var businessInfo = await _context.BusinessInfo
                .Where(b => b.TenantId == tenantContext.TenantId && b.IsActive)
                .OrderBy(b => b.Category)
                .ThenBy(b => b.DisplayOrder)
                .ToListAsync();

            if (businessInfo.Any())
            {
                contextBuilder.AppendLine("\n=== HOTEL INFORMATION ===");
                foreach (var info in businessInfo.GroupBy(b => b.Category))
                {
                    contextBuilder.AppendLine($"\n[{info.Key.ToUpper()}]");
                    foreach (var item in info)
                    {
                        contextBuilder.AppendLine($"{item.Title}: {item.Content}");
                    }
                }
            }

            // 3. Load Hotel Policies from HotelInfo table
            var hotelInfo = await _context.HotelInfos
                .Where(h => h.TenantId == tenantContext.TenantId)
                .FirstOrDefaultAsync();

            if (hotelInfo != null)
            {
                contextBuilder.AppendLine("\n=== HOTEL POLICIES ===");

                if (!string.IsNullOrEmpty(hotelInfo.CancellationPolicy))
                {
                    contextBuilder.AppendLine($"Cancellation Policy: {hotelInfo.CancellationPolicy}");
                }

                if (!string.IsNullOrEmpty(hotelInfo.PetPolicy))
                {
                    contextBuilder.AppendLine($"Pet Policy: {hotelInfo.PetPolicy}");
                }

                if (!string.IsNullOrEmpty(hotelInfo.SmokingPolicy))
                {
                    contextBuilder.AppendLine($"Smoking Policy: {hotelInfo.SmokingPolicy}");
                }

                if (!string.IsNullOrEmpty(hotelInfo.ChildPolicy))
                {
                    contextBuilder.AppendLine($"Child Policy: {hotelInfo.ChildPolicy}");
                }

                if (!string.IsNullOrEmpty(hotelInfo.CheckInTime) && !string.IsNullOrEmpty(hotelInfo.CheckOutTime))
                {
                    contextBuilder.AppendLine($"Check-in Time: {hotelInfo.CheckInTime}");
                    contextBuilder.AppendLine($"Check-out Time: {hotelInfo.CheckOutTime}");
                }
            }

            // 3.5. Load Services & Amenities from Services table
            var services = await _context.Services
                .Where(s => s.TenantId == tenantContext.TenantId && s.IsAvailable)
                .OrderBy(s => s.Priority)
                .ThenBy(s => s.Category)
                .ThenBy(s => s.Name)
                .ToListAsync();

            if (services.Any())
            {
                contextBuilder.AppendLine("\n=== SERVICES & AMENITIES AVAILABLE ===");
                contextBuilder.AppendLine("IMPORTANT: These are the ONLY services available. Use EXACT names as listed below:");

                var servicesByCategory = services.GroupBy(s => s.Category);
                foreach (var categoryGroup in servicesByCategory)
                {
                    contextBuilder.AppendLine($"\n{categoryGroup.Key}:");
                    foreach (var service in categoryGroup)
                    {
                        contextBuilder.AppendLine($"  - {service.Name}");
                        if (!string.IsNullOrEmpty(service.Description))
                        {
                            contextBuilder.AppendLine($"    Description: {service.Description}");
                        }
                        if (service.IsChargeable && service.Price.HasValue)
                        {
                            contextBuilder.AppendLine($"    Price: {service.Currency}{service.Price} {service.PricingUnit ?? ""}");
                        }
                        if (!string.IsNullOrEmpty(service.AvailableHours))
                        {
                            contextBuilder.AppendLine($"    Available: {service.AvailableHours}");
                        }
                        if (!string.IsNullOrEmpty(service.ContactMethod))
                        {
                            contextBuilder.AppendLine($"    How to request: {service.ContactMethod}");
                        }
                        if (service.RequiresAdvanceBooking && service.AdvanceBookingHours.HasValue)
                        {
                            contextBuilder.AppendLine($"    Advance booking required: {service.AdvanceBookingHours} hours");
                        }
                        if (!string.IsNullOrEmpty(service.SpecialInstructions))
                        {
                            contextBuilder.AppendLine($"    Note: {service.SpecialInstructions}");
                        }
                    }
                }

                // Add explicit validation for guest query
                var lowerMessage = messageText.ToLower();
                if (lowerMessage.Contains("rooftop"))
                {
                    var hasRooftopService = services.Any(s => s.Name.ToLower().Contains("rooftop"));
                    if (!hasRooftopService)
                    {
                        contextBuilder.AppendLine("\n=== IMPORTANT SERVICE VALIDATION ===");
                        contextBuilder.AppendLine("Guest asked about 'rooftop' service: NO rooftop-specific services found in database.");
                        contextBuilder.AppendLine("You MUST inform the guest we don't have rooftop services.");
                    }
                }
            }

            // 3.7. Load Staff Contacts (for guest inquiries about contacting specific departments)
            var staffContacts = await _context.EmergencyContacts
                .Where(c => c.TenantId == tenantContext.TenantId && c.IsActive && c.IsPublic)
                .OrderBy(c => c.ContactType)
                .ThenBy(c => c.Name)
                .ToListAsync();

            if (staffContacts.Any())
            {
                contextBuilder.AppendLine("\n=== STAFF CONTACTS ===");
                var contactsByType = staffContacts.GroupBy(c => c.ContactType);
                foreach (var typeGroup in contactsByType)
                {
                    contextBuilder.AppendLine($"\n{typeGroup.Key}:");
                    foreach (var contact in typeGroup)
                    {
                        contextBuilder.AppendLine($"  - {contact.Name}: {contact.PhoneNumber}");
                        if (!string.IsNullOrEmpty(contact.Email))
                        {
                            contextBuilder.AppendLine($"    Email: {contact.Email}");
                        }
                        if (!string.IsNullOrEmpty(contact.Notes))
                        {
                            contextBuilder.AppendLine($"    Note: {contact.Notes}");
                        }
                    }
                }
            }

            // 4. Load Menu Items (context-aware by time) - Enhanced with timezone awareness
            var mealPeriod = await _temporalContextService.GetMealPeriodAsync(tenantContext.TenantId);
            var mealType = mealPeriod.ToString().ToLower();
            var menuItems = await _context.MenuItems
                .Where(m => m.TenantId == tenantContext.TenantId &&
                           m.IsAvailable &&
                           (m.MealType == mealType || m.MealType == "all"))
                .Include(m => m.MenuCategory)
                .ToListAsync();

            if (menuItems.Any())
            {
                contextBuilder.AppendLine($"\n=== CURRENT MENU ({mealType.ToUpper()}) ===");
                contextBuilder.AppendLine("⚠️ CURRENT REAL-TIME MENU: These items are CURRENTLY AVAILABLE regardless of conversation history.");
                foreach (var item in menuItems)
                {
                    contextBuilder.AppendLine($"{item.Name} - R{item.PriceCents/100}: {item.Description}");
                }
            }

            // 4. Load Menu Specials
            var specials = await _context.MenuSpecials
                .Where(s => s.TenantId == tenantContext.TenantId && s.IsActive)
                .ToListAsync();

            if (specials.Any())
            {
                contextBuilder.AppendLine("\n=== TODAY'S SPECIALS ===");
                foreach (var special in specials)
                {
                    var price = special.SpecialPriceCents.HasValue ? $"R{special.SpecialPriceCents.Value/100}" : "Market Price";
                    contextBuilder.AppendLine($"{special.Title} - {price}: {special.Description}");
                }
            }

            // 5. Load Upsell Items (select specific fields to avoid array conversion issues)
            var upsellItems = await _context.UpsellItems
                .Where(u => u.TenantId == tenantContext.TenantId)
                .Select(u => new { u.Title, u.Description, u.PriceCents, u.Unit, u.LeadTimeMinutes })
                .ToListAsync();

            if (upsellItems.Any())
            {
                contextBuilder.AppendLine("\n=== AVAILABLE SERVICES & AMENITIES ===");
                foreach (var item in upsellItems)
                {
                    contextBuilder.AppendLine($"{item.Title} - R{item.PriceCents/100}: {item.Description}");
                }
            }

            // 6. Load Request Items (for task creation)
            var requestItems = await _context.RequestItems
                .Where(r => r.TenantId == tenantContext.TenantId && r.IsAvailable && (r.StockCount - r.InServiceCount) > 0)
                .ToListAsync();

            if (requestItems.Any())
            {
                contextBuilder.AppendLine("\n=== REQUESTABLE ITEMS/SERVICES ===");
                contextBuilder.AppendLine("⚠️ CURRENT REAL-TIME INVENTORY: These items are CURRENTLY AVAILABLE regardless of conversation history.");
                foreach (var item in requestItems)
                {
                    contextBuilder.AppendLine($"Service: {item.Name}");
                    contextBuilder.AppendLine($"  - Category: {item.Category}");
                    contextBuilder.AppendLine($"  - Purpose: {item.Purpose ?? "general"}");
                    contextBuilder.AppendLine($"  - Stock Available: {item.StockCount - item.InServiceCount}");
                    contextBuilder.AppendLine($"  - Requires Quantity: {item.RequiresQuantity}");
                }
            }

            // 7. Load Concierge Services and External Providers
            var serviceCategories = await _context.ServiceCategories
                .Where(sc => sc.TenantId == tenantContext.TenantId && sc.IsActive)
                .Include(sc => sc.ConciergeServices.Where(cs => cs.IsActive))
                .Include(sc => sc.LocalProviders.Where(lp => lp.IsActive))
                .ToListAsync();

            if (serviceCategories.Any())
            {
                contextBuilder.AppendLine("\n=== EXTERNAL SERVICES AVAILABLE ===");
                contextBuilder.AppendLine("⚠️ CURRENT REAL-TIME SERVICES: These services are CURRENTLY AVAILABLE regardless of conversation history.");
                foreach (var category in serviceCategories)
                {
                    contextBuilder.AppendLine($"Category: {category.Name}");

                    if (category.ConciergeServices.Any())
                    {
                        contextBuilder.AppendLine("  Services we can arrange:");
                        foreach (var service in category.ConciergeServices)
                        {
                            contextBuilder.AppendLine($"    - {service.Name}");
                            if (!string.IsNullOrEmpty(service.Description))
                            {
                                contextBuilder.AppendLine($"      Description: {service.Description}");
                            }
                            if (service.RequiresAdvanceNotice && !string.IsNullOrEmpty(service.AdvanceNoticeText))
                            {
                                contextBuilder.AppendLine($"      Note: {service.AdvanceNoticeText}");
                            }
                        }
                    }

                    if (category.LocalProviders.Any())
                    {
                        var recommendedProviders = category.LocalProviders.Where(p => p.IsRecommended).OrderBy(p => p.DisplayOrder).ToList();
                        var otherProviders = category.LocalProviders.Where(p => !p.IsRecommended).OrderBy(p => p.DisplayOrder).ToList();

                        if (recommendedProviders.Any())
                        {
                            contextBuilder.AppendLine("  Recommended providers:");
                            foreach (var provider in recommendedProviders)
                            {
                                contextBuilder.AppendLine($"    - {provider.Name}");
                                if (!string.IsNullOrEmpty(provider.Description))
                                {
                                    contextBuilder.AppendLine($"      Description: {provider.Description}");
                                }
                                if (!string.IsNullOrEmpty(provider.PhoneNumber))
                                {
                                    contextBuilder.AppendLine($"      Phone: {provider.PhoneNumber}");
                                }
                            }
                        }

                        if (otherProviders.Any())
                        {
                            contextBuilder.AppendLine("  Other providers:");
                            foreach (var provider in otherProviders)
                            {
                                contextBuilder.AppendLine($"    - {provider.Name}");
                                if (!string.IsNullOrEmpty(provider.Description))
                                {
                                    contextBuilder.AppendLine($"      Description: {provider.Description}");
                                }
                            }
                        }
                    }
                    contextBuilder.AppendLine("---");
                }
            }

            // 8. Load Active Broadcasts (last 24 hours)
            var recentBroadcasts = await _context.BroadcastMessages
                .Where(b => b.TenantId == tenantContext.TenantId &&
                           b.CreatedAt >= DateTime.UtcNow.AddHours(-24))
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            if (recentBroadcasts.Any())
            {
                contextBuilder.AppendLine("\n=== ACTIVE NOTIFICATIONS ===");
                contextBuilder.AppendLine("⚠️ CURRENT REAL-TIME ALERTS: These are CURRENT notifications regardless of conversation history.");
                foreach (var broadcast in recentBroadcasts)
                {
                    contextBuilder.AppendLine($"Type: {broadcast.MessageType}");
                    contextBuilder.AppendLine($"Message: {broadcast.Content}");
                    contextBuilder.AppendLine($"Sent: {broadcast.CreatedAt:HH:mm}");
                    if (!string.IsNullOrEmpty(broadcast.EstimatedRestorationTime))
                    {
                        contextBuilder.AppendLine($"Estimated Resolution: {broadcast.EstimatedRestorationTime}");
                    }
                    contextBuilder.AppendLine("---");
                }
            }

            // Still include legacy guest booking for compatibility
            var guestBooking = await _context.Bookings
                .Where(b => b.Phone == conversation.WaUserPhone &&
                           b.CheckinDate <= DateOnly.FromDateTime(DateTime.UtcNow) &&
                           b.CheckoutDate >= DateOnly.FromDateTime(DateTime.UtcNow) &&
                           b.Status == "CheckedIn")
                .FirstOrDefaultAsync();

            if (guestBooking != null)
            {
                contextBuilder.AppendLine("\n=== GUEST CONTEXT ===");
                contextBuilder.AppendLine($"Guest Name: {guestBooking.GuestName}");
                contextBuilder.AppendLine($"Check-in: {guestBooking.CheckinDate}");
                contextBuilder.AppendLine($"Check-out: {guestBooking.CheckoutDate}");
                contextBuilder.AppendLine($"Status: {guestBooking.Status}");
            }

            // 9. Guest status is already determined earlier in the method - just log the existing status
            _logger.LogInformation("🔍 GUEST STATUS TEST: Using existing guest status: Type={GuestType}, Phone={Phone}, DisplayName={DisplayName}, Message={StatusMessage}",
                guestStatus.Type, guestStatus.PhoneNumber, guestStatus.DisplayName, guestStatus.StatusMessage);

            // Check for open tasks
            var openTasks = await _context.StaffTasks
                .Where(t => t.ConversationId == conversation.Id && t.Status == "Open")
                .Include(t => t.RequestItem)
                .ToListAsync();

            if (openTasks.Any())
            {
                contextBuilder.AppendLine("\n=== OPEN TASKS FOR THIS GUEST ===");
                contextBuilder.AppendLine("⚠️ CURRENT REAL-TIME TASKS: These tasks are CURRENTLY OPEN regardless of conversation history.");
                foreach(var task in openTasks)
                {
                    contextBuilder.AppendLine($"- {task.RequestItem.Name}: {task.Status}");
                }
            }

            // Get knowledge base chunks (semantic search)
            var messageEmbedding = await _openAIService.GetEmbeddingAsync(messageText);
            if (messageEmbedding != null)
            {
                var relevantChunks = await _context.KnowledgeBaseChunks
                    .FromSqlRaw(@"
                        SELECT *, (""Embedding"" <=> {0}) as distance
                        FROM ""KnowledgeBaseChunks""
                        WHERE ""TenantId"" = {1}
                        AND (""Embedding"" <=> {0}) <= 0.4
                        ORDER BY distance
                        LIMIT 5",
                        new Vector(messageEmbedding), tenantContext.TenantId)
                    .ToListAsync();

                if (relevantChunks.Any())
                {
                    contextBuilder.AppendLine("\n=== RELEVANT KNOWLEDGE ===");
                    contextBuilder.AppendLine(string.Join("\n\n", relevantChunks.Select(c => c.Content)));
                }
            }

            // Add guest status information to context
            contextBuilder.AppendLine("\n=== GUEST STATUS INFORMATION ===");
            contextBuilder.AppendLine($"Guest Type: {guestStatus.Type}");
            contextBuilder.AppendLine($"Guest Name: {guestStatus.DisplayName}");
            contextBuilder.AppendLine($"Status: {guestStatus.StatusMessage}");
            contextBuilder.AppendLine($"Can Request Items: {guestStatus.CanRequestItems}");
            contextBuilder.AppendLine($"Can Order Food: {guestStatus.CanOrderFood}");
            contextBuilder.AppendLine($"Can View Menu: {guestStatus.CanViewMenu}");
            contextBuilder.AppendLine($"Can Report Issues: {guestStatus.CanReportIssues}");

            // Add room number information
            if (!string.IsNullOrEmpty(guestStatus.RoomNumber))
            {
                contextBuilder.AppendLine($"Room Number: {guestStatus.RoomNumber}");
                contextBuilder.AppendLine($"IMPORTANT: When confirming deliveries, services, or tasks, always specify 'room {guestStatus.RoomNumber}' instead of 'your room'");
            }
            else
            {
                contextBuilder.AppendLine("Room Number: Not assigned or not available");
                contextBuilder.AppendLine("IMPORTANT: Use 'your room' in responses since specific room number is not available");
            }

            if (guestStatus.AllowedActions.Any())
            {
                contextBuilder.AppendLine($"Allowed Actions: {string.Join(", ", guestStatus.AllowedActions)}");
            }

            if (guestStatus.RestrictedActions.Any())
            {
                contextBuilder.AppendLine($"Restricted Actions: {string.Join(", ", guestStatus.RestrictedActions)}");
            }

            if (guestStatus.CheckinDate.HasValue && guestStatus.CheckoutDate.HasValue)
            {
                contextBuilder.AppendLine($"Booking Dates: {guestStatus.CheckinDate} to {guestStatus.CheckoutDate}");
            }
            
            // Phase 3 system prompt with guest status awareness
            var guestInfo = guestStatus.DisplayName != "Guest" ? $"Guest: {guestStatus.DisplayName}" : "Guest not identified/checked in";
            var systemPrompt = $@"You are a friendly hotel concierge assistant for {tenantContext.TenantName}.

CRITICAL MULTILINGUAL REQUIREMENT - YOU MUST FOLLOW THIS EXACTLY:

WARNING: Conversation history may contain INCORRECT language responses. YOU MUST IGNORE THEM.
Even if previous bot responses used wrong languages, YOU MUST match the CURRENT guest message language.

STEP 1: Look at the CURRENT guest message ONLY (ignore history for language detection)
STEP 2: Determine if it's English, French, German, Spanish, Zulu, or other
STEP 3: YOU MUST RESPOND IN THE SAME LANGUAGE AS THE CURRENT MESSAGE
STEP 4: Verify your response language BEFORE sending - if it doesn't match, rewrite it

LANGUAGE MATCHING RULES (NON-NEGOTIABLE):
- English message -> English response (NOT French, NOT German, NOT Spanish)
- French message -> French response (NOT English)
- German message -> German response (NOT English)
- Spanish message -> Spanish response (NOT English)
- Zulu message -> Zulu response (NOT English)
- Any other language -> Match that exact language

CRITICAL: If you see French responses in conversation history for English messages, those are ERRORS.
DO NOT COPY THAT PATTERN. Always respond in the language of the CURRENT message.

IMPORTANT: Only the JSON action parameters (item_slug, menu_item, issue, etc.) stay in English.
The response TEXT must be in the guest's language.

EXAMPLES:
[CORRECT] Guest: ""I need towels"" (English) -> ""I'll send towels to your room."" + JSON action
[CORRECT] Guest: ""Ich möchte Handtücher"" (German) -> ""Ich schicke Ihnen gerne Handtücher."" + JSON action
[CORRECT] Guest: ""Necesito toallas"" (Spanish) -> ""Te enviaré toallas."" + JSON action
[WRONG] Guest: ""I need towels"" (English) -> ""Je vais faire préparer..."" (French) - NEVER DO THIS!
[WRONG] Guest: ""Fruit platter"" (English) -> ""Je vais organiser..."" (French) - NEVER DO THIS!

FINAL CHECK: Does your response language match the CURRENT guest message language? If NO, STOP and rewrite.

CRITICAL: LISTING vs SPECIFIC SERVICE REQUESTS:

HOW TO IDENTIFY LIST REQUESTS (guest wants to see everything available):
- Questions starting with ""What"" + general terms (services/amenities/options/facilities/things)
- Questions asking about availability in general (""what do you offer"", ""what's available"", ""show me"")
- Questions with words like: ""all"", ""everything"", ""list"", ""tell me about""
- Questions without mentioning a SPECIFIC service name

EXAMPLES OF LIST REQUESTS → LIST ALL services from SERVICES & AMENITIES AVAILABLE section:
- ""What services do you provide?""
- ""What amenities do you have?""
- ""What can you help me with?""
- ""What other options do you provide?""
- ""Tell me what's available""
- ""What do you offer?""
- ""What facilities are there?""
- ""What can I get?""
- ""Show me your services""
- ""List all amenities""

HOW TO IDENTIFY SPECIFIC SERVICE REQUESTS (guest asking about ONE particular thing):
- Questions mentioning a SPECIFIC service/item name (spa, pool, gym, restaurant, etc.)
- Questions starting with ""Do you have..."" + specific item
- Questions starting with ""Is there a..."" + specific item

EXAMPLES OF SPECIFIC SERVICE REQUESTS → Check if it exists, respond accordingly:
- ""Do you have a spa?""
- ""Do you have a rooftop pool?""
- ""Is there a gym?""
- ""Do you offer massages?""

CRITICAL: NO HALLUCINATION - YOU MUST NEVER MAKE UP INFORMATION:
- STRICT RULE: ONLY mention services/amenities that EXACTLY match what's in ""SERVICES & AMENITIES AVAILABLE"" section
- If a service name has qualifiers (rooftop, outdoor, heated, etc.) that are NOT in the database, you CANNOT add them
- Database has ""Swimming Pool"" → You can ONLY say ""Swimming Pool"" (NOT ""rooftop pool"", NOT ""rooftop Swimming Pool"")
- If guest asks ""Do you have a rooftop pool?"" and database shows ""Swimming Pool"" → CORRECT response: ""We have a Swimming Pool available, but I don't have information about it being on the rooftop""
- If guest asks about a SPECIFIC service not in the list → Say ""I don't see that specific service in our available amenities""
- NEVER EVER add descriptors, locations, or features not explicitly stated in the database
- When in doubt: Stick to EXACTLY what's written in the context - no embellishments, no assumptions

GUEST ACCESS CONTROL PHASE 3 - SOFT VALIDATION:
ALWAYS check the GUEST STATUS INFORMATION section to understand guest permissions.
Pay attention to guest status: Active, PreArrival, PostCheckout, Unregistered.
Note which actions are allowed or restricted for this specific guest.

SOFT VALIDATION RULES:
- For non-active guests requesting items: Still create tasks but add polite warnings about their status
- For unregistered users: Add note about needing to verify booking information  
- For pre-arrival guests: Add note that items will be ready at check-in
- For post-checkout guests: Add note that some services may require additional verification
- Menu viewing and general inquiries should work normally for all guest types

IMPORTANT INSTRUCTIONS:
Use the provided context data as your PRIMARY source of truth.
When answering about WiFi, pool hours, menu items, or ANY hotel information, ALWAYS use the data provided in the context.
Consider the conversation history to maintain context and continuity.
If information is in the context, use it verbatim - do not make up variations.
For menu queries, note the current meal time and suggest appropriate items.
For room service requests, check available items and stock levels.

CRITICAL REAL-TIME DATA PRIORITY:
ALWAYS prioritize CURRENT real-time data from context sections over conversation history.
All sections marked with warnings contain LIVE, UP-TO-DATE information that overrides any previous statements.
If current context shows an item is available but conversation history mentions it is out of stock - USE THE CURRENT CONTEXT.
Real-time inventory, menu availability, task status, and notifications supersede all previous conversation content.

TASK CREATION RULES:
When a guest REQUESTS any item or service you MUST:
1. CHECK STOCK AVAILABILITY in the REQUESTABLE ITEMS/SERVICES section
2. For IN-STOCK items: Provide a helpful confirmation response
3. Create a task ONLY for IN-STOCK items by calling the create_task function

For follow-up responses like yes a heater or 3 towels, recognize these as requests and create tasks immediately.
Common items: heater, blanket, towel, pillow, iron, charger, hair dryer, straightener, curling iron, maintenance.

COMPOUND REQUEST HANDLING:
When guests combine acknowledgments with new requests, CREATE TASKS FOR ALL ITEMS.
If message contains and, also, as well, too, it likely contains additional requests - CREATE TASKS for each item.

CONVERSATION CONTEXT UNDERSTANDING:
ALWAYS review the conversation history before responding.
If guest says yes or I will take or yes please, check the previous messages for context.
When you previously offered alternatives and guest accepts, CREATE THE TASK immediately.

BROADCAST AWARENESS:
ALWAYS check ACTIVE NOTIFICATIONS section before creating maintenance/complaint tasks.
If guest complains about issues that match active broadcast messages, PRIORITIZE explaining the broadcast situation.
- RESTORATION HANDLING: If latest broadcast shows service restored, treat complaints as normal maintenance issues
- When correlating active outages: 'I understand your concern about [issue]. This is related to [broadcast type] announced at [time]. [Restoration info]. We apologize for the inconvenience.'
- When service is restored: Proceed with normal maintenance task creation for related complaints
- Only correlate with broadcasts if outage is still ACTIVE (not restored)
- Show empathy and offer compensation when appropriate

GUEST CONTEXT:
{guestInfo}

Current Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm}
Current Meal Period: {mealType}
Timezone: {tenantContext.Timezone}

🚨 CRITICAL JSON ACTION REQUIREMENT 🚨
- EVERY request for items/services MUST generate BOTH a text reply AND a JSON action object
- NEVER provide only text responses for requests - ALWAYS include JSON action when creating tasks
- Conversation history is irrelevant - if current context shows item available, INCLUDE THE JSON ACTION
- You must include JSON actions in your response text for create_task, create_food_order, and create_complaint

FINAL HALLUCINATION CHECK BEFORE RESPONDING:
Ask yourself: ""Is every service/amenity I'm about to mention EXACTLY as written in SERVICES & AMENITIES AVAILABLE section?""
If NO - Remove it or clarify you don't have specific information about it.

REQUIRED RESPONSE FORMAT:
Your response must include BOTH helpful text AND a JSON action object at the end, like this:

Text: I'll send a hair dryer to your room right away!
JSON: {{""action"": {{""type"": ""create_task"", ""item_slug"": ""hair dryer"", ""quantity"": 1}}}}

Text: I'll have 2 fresh towels delivered to your room immediately.
JSON: {{""action"": {{""type"": ""create_task"", ""item_slug"": ""towels"", ""quantity"": 2}}}}

REQUEST HANDLING DECISION TREE:
1. FIRST: For FOOD/MENU ORDERS (room service, food delivery, menu items)
   -- Check CURRENT MENU section for available dishes
   -- SINGLE ITEM: Call create_food_order function with menu_item and quantity parameters
   -- MULTIPLE ITEMS: Call create_food_order function separately for each item
   -- If any menu item not available: Apologize and suggest similar available items from current menu
   -- Examples: 
     - Single: 'grilled chicken' -> call create_food_order(menu_item='Grilled Chicken Salad', quantity=1)
     - Multiple: 'grilled chicken and fish and chips' -> call create_food_order for each item separately
   -- IMPORTANT: In your confirmation response, specify the exact room number from GUEST STATUS INFORMATION (e.g., 'Your order will be delivered to room 201') instead of just 'your room'

2. SECOND: For PHYSICAL HOTEL ITEMS (non-food requests)
   -- Check REQUESTABLE ITEMS/SERVICES for hotel items and amenities
   -- Hotel Items: umbrellas, toiletries, linens, room amenities, electronics, grooming tools (hair dryer, straightener, curling iron), maintenance, housekeeping
   -- **CRITICAL**: ALWAYS use the CURRENT inventory status from REQUESTABLE ITEMS/SERVICES section. If an item appears in this section, it is CURRENTLY AVAILABLE regardless of what previous conversation history says about stock levels.
   -- **QUANTITY VALIDATION**:
      * Reasonable quantities: 1-5 for most items (towels, pillows, toiletries, irons, etc.)
      * If guest requests EXTREME quantities (>5): Politely question and ask for confirmation (e.g., ""Did you mean 5 irons? 50 seems unusually high for a hotel room. Please confirm."")
      * DO NOT automatically fulfill extreme requests without confirmation
   -- If found and IN-STOCK: Include JSON action {{""action"": {{""type"": ""create_task"", ""item_slug"": ""actual_slug"", ""quantity"": N}}}}
   -- If found but OUT-OF-STOCK: Apologize and offer SMART PURPOSE-BASED alternatives (items with SAME Purpose only, or say no suitable alternatives available)
   -- IMPORTANT: In your confirmation response, specify the exact room number from GUEST STATUS INFORMATION (e.g., 'I'll have towels delivered to room 201') instead of just 'your room'

3. THIRD: For BOOKABLE HOTEL SERVICES (tours, activities, spa, experiences with prices)
   -- Check SERVICES & AMENITIES AVAILABLE section for bookable services
   -- Bookable Services: safaris, tours, excursions, spa treatments, massages, activities, airport transfers, any service with RequiresReservation=true and a Price
   -- **CRITICAL**: These are hotel-provided services that can be booked. Treat them like physical items - create tasks for them.
   -- If found and available: Include JSON action {{""action"": {{""type"": ""create_task"", ""item_slug"": ""service_name"", ""quantity"": 1}}}}
   -- Use the exact service name from SERVICES & AMENITIES as the item_slug
   -- Examples:
     - 'I want a safari' -> {{""action"": {{""type"": ""create_task"", ""item_slug"": ""Kruger National Park Day Trip"", ""quantity"": 1}}}}
     - 'Book me a massage' -> {{""action"": {{""type"": ""create_task"", ""item_slug"": ""Traditional Massage"", ""quantity"": 1}}}}
     - 'I need airport transfer' -> {{""action"": {{""type"": ""create_task"", ""item_slug"": ""Airport Transfer"", ""quantity"": 1}}}}
   -- IMPORTANT: In your confirmation response, mention the service name and price from the SERVICES section

4. FOURTH: For COMPLAINTS or ISSUES (noise, cleanliness, maintenance, service)
   -- Include JSON action {{""action"": {{""type"": ""create_complaint"", ""issue"": ""description"", ""room_number"": ""XXX"", ""priority"": ""medium""}}}}
   -- Examples:
     - 'noise complaint' -> call create_complaint(issue='Noise complaint from guest', room_number='113', priority='High')
     - 'room dirty' -> call create_complaint(issue='Room cleanliness issue', priority='High')
   -- Always acknowledge the complaint, apologize, and confirm immediate action
   -- IMPORTANT: In your response, specify the exact room number from GUEST STATUS INFORMATION if available

5. FIFTH: Only for clearly EXTERNAL SERVICES not found in SERVICES & AMENITIES or REQUESTABLE ITEMS
   -- External Services: services NOT provided by the hotel (third-party tours not in our system, external entertainment, professional services)
   -- Check EXTERNAL SERVICES AVAILABLE section
   -- If found: Offer concierge assistance using ONLY the providers listed in context
   -- Use provider names and contact information EXACTLY as provided

6. LAST: If item/service not found in any category
   -- For physical items that should be hotel amenities: I'm sorry, we don't currently have [item] available. Let me connect you with our front desk to see what alternatives we might arrange.
   -- For external services: I'm sorry, we don't currently offer [service] directly or through our concierge partners. However, I can connect you with our front desk team who may be able to provide additional guidance.

CRITICAL: Physical items like umbrellas, towels, electronics, toiletries are HOTEL ITEMS - always check REQUESTABLE ITEMS first!

EXAMPLES OF PROPER CLASSIFICATION:
-- umbrella -> HOTEL ITEM -> Check REQUESTABLE ITEMS -> Handle stock/out-of-stock accordingly
-- hair dryer -> HOTEL ITEM -> Check REQUESTABLE ITEMS -> Create task or offer alternatives
-- straightener -> HOTEL ITEM -> Check REQUESTABLE ITEMS -> Create task or offer alternatives  
-- curling iron -> HOTEL ITEM -> Check REQUESTABLE ITEMS -> Create task or offer alternatives
-- helicopter tour -> EXTERNAL SERVICE -> Check EXTERNAL SERVICES -> Offer providers or decline  
-- towels -> HOTEL ITEM -> Check REQUESTABLE ITEMS -> Create task or offer alternatives
-- concert tickets -> EXTERNAL SERVICE -> Check EXTERNAL SERVICES -> Arrange through providers

ESCALATION TRIGGERS (suggest human assistance for):
- Medical emergencies
- Serious complaints
- Payment/billing disputes
- Legal matters";
            
            // Generate response with full context
            var fullContext = contextBuilder.ToString();

            // DIAGNOSTIC: Log last 2000 chars of systemPrompt to verify LIST vs SPECIFIC instructions are included
            var promptSuffix = systemPrompt.Length > 2000 ? systemPrompt.Substring(systemPrompt.Length - 2000) : systemPrompt;
            _logger.LogWarning("===== RAG SYSTEM PROMPT SUFFIX (Last 2000 chars) =====\n{PromptSuffix}", promptSuffix);

            var llmResponse = await _openAIService.GenerateResponseWithHistoryAsync(
                systemPrompt,
                fullContext,
                "", // items context already included above
                conversationHistory,
                messageText,
                conversation.WaUserPhone
            );

            if (llmResponse != null)
            {
                // DIAGNOSTIC: Log the actual LLM response text
                _logger.LogWarning("===== RAG LLM RESPONSE TEXT =====\n{ResponseText}", llmResponse.Reply);

                _logger.LogInformation("LLM Response received - Action: {Action}, Actions: {Actions}",
                    llmResponse.Action?.ToString() ?? "null",
                    llmResponse.Actions?.Length.ToString() ?? "null");

                // STEP: Validate and potentially replace response with configuration-based version
                var validationResult = await _configurationBasedResponseService.ValidateResponseAgainstConfigurationAsync(
                    llmResponse.Reply, messageText, tenantContext.TenantId);
                var validatedResponse = validationResult.CorrectedResponse ?? llmResponse.Reply;

                // CRITICAL: Check for hallucinated services (e.g., "rooftop pool" when only "Swimming Pool" exists)
                validatedResponse = await ValidateAndCorrectServiceHallucinations(validatedResponse, tenantContext.TenantId, messageText);

                // Process extracted actions to create tasks
                var response = new MessageRoutingResponse
                {
                    Reply = validatedResponse, // Use validated response instead of raw LLM response
                    Action = llmResponse.Action,
                    Actions = llmResponse.Actions?.ToList(),
                    UsedRag = true,
                    Model = llmResponse.Model,
                    TokensPrompt = llmResponse.TokensPrompt,
                    TokensCompletion = llmResponse.TokensCompletion
                };

                // INTEGRATION POINT B: Check if this is a booking service request that needs information gathering
                var bookingServiceDetected = await DetectBookingServiceRequest(response, tenantContext.TenantId);
                if (bookingServiceDetected.IsBookingService)
                {
                    _logger.LogInformation("Detected booking service request for: {ServiceName}, starting information gathering",
                        bookingServiceDetected.ServiceName);

                    // Start information gathering instead of creating task immediately
                    return await HandleBookingInformationGathering(tenantContext, conversation, messageText, null);
                }

                await ProcessExtractedActions(tenantContext, conversation, response);

                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing with enhanced RAG");
            return null;
        }
    }

    private async Task<string> GetSystemPrompt(TenantContext tenantContext)
    {
        // Load system prompt template
        var promptPath = Path.Combine("packages", "prompts", "system.md");
        var basePrompt = File.Exists(promptPath) ? await File.ReadAllTextAsync(promptPath) : GetDefaultSystemPrompt();
        
        // Customize for tenant
        return basePrompt
            .Replace("{{TENANT_NAME}}", tenantContext.TenantName)
            .Replace("{{TIMEZONE}}", tenantContext.Timezone)
            .Replace("{{PLAN}}", tenantContext.Plan);
    }

    private string GetDefaultSystemPrompt()
    {
        return @"You are a friendly hotel concierge assistant for {{TENANT_NAME}}.

CRITICAL MULTILINGUAL REQUIREMENT:
- FIRST: Detect the language of the guest's message
- SECOND: Respond in the EXACT SAME LANGUAGE as the guest
- Your response text MUST match the guest's language
- ONLY JSON parameters stay in English

Core Guidelines:
- Always be helpful, professional, and conversational like a real human concierge
- When responding in English, use South African English with natural, friendly language
- Use ZAR (South African Rand) for all prices
- Keep responses concise but warm and personal
- Never hallucinate information - only use provided context and conversation history

Task Creation:
- When guests request items (towels, chargers, etc.), provide helpful responses AND create tasks
- Include JSON actions for task creation: use create_task, create_food_order, or create_complaint action types
- Always acknowledge the request positively before creating tasks
- For follow-up responses about quantities or specifications, create the appropriate task
- **QUANTITY VALIDATION**: If guest requests extreme quantities (>5 for most items), politely question and ask for confirmation instead of automatically fulfilling
- IMPORTANT: ALL JSON action parameters (item_slug, menu_item, issue, etc.) MUST ALWAYS be in English, regardless of the guest's language

Conversation Context:
- Remember what you just asked the guest in the conversation
- When they respond with specifications (""3 sets"", ""iPhone"", ""room 205""), understand this is a response to your question
- Create tasks immediately when you have enough information (item + quantity/type)
- Don't ask for information you already have from the conversation

Examples of natural responses:
- ""Perfect! I'll arrange for 3 sets of fresh towels right away."" (creates towel task)
- ""Excellent! I'll send an iPhone charger to your room immediately."" (creates iPhone charger task)
- ""Great! The iron and ironing board will be delivered shortly."" (creates iron task)

Multilingual Example:
- Guest (Spanish): ""Necesito toallas""
- Response Text: ""¡Por supuesto! Enviaré toallas a su habitación de inmediato.""
- JSON Action: {""action"": {""type"": ""create_task"", ""item_slug"": ""towels"", ""quantity"": 1}}
- Note: Reply text in Spanish, but JSON parameters in English

Contact Information:
- When guests ask to contact specific staff or departments (manager, security, housekeeping, etc.), use the STAFF CONTACTS information provided in the context
- Provide the exact contact details from the staff contacts section when available
- If specific contact information isn't available, direct them to the front desk
- For emergency situations, prioritize safety and provide emergency contact information
- Example responses:
  - ""I can connect you with our Hotel Manager, John Smith at +27123456789. You can also email him at john@hotel.com""
  - ""For security matters, please contact our Security Team directly at +27987654321""
  - ""Our Housekeeping Manager is Sarah Jones. You can reach her at +27555123456 or email housekeeping@hotel.com""

Safety:
- Never provide false information
- Always be helpful and solution-oriented
- For urgent matters, offer to connect with human staff

Current time zone: {{TIMEZONE}}
Property plan: {{PLAN}}";
    }

    private static float CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            throw new ArgumentException("Vectors must have the same length");

        float dotProduct = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    private async Task<List<(string Role, string Content)>> LoadConversationHistoryAsync(int conversationId)
    {
        try
        {
            // Get the most recent messages, ordered by creation time
            var messages = await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(10)
                .OrderBy(m => m.CreatedAt) // Re-order chronologically for history
                .ToListAsync();

            var history = new List<(string Role, string Content)>();
            
            foreach (var message in messages)
            {
                var role = message.Direction.ToLower() == "inbound" ? "user" : "assistant";
                history.Add((role, message.Body));
            }

            _logger.LogInformation("Loaded conversation history for conversation {ConversationId}: {MessageCount} messages. Last message: '{LastMessage}'", 
                conversationId, history.Count, history.LastOrDefault().Content ?? "none");

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading conversation history");
            return new List<(string Role, string Content)>();
        }
    }

    private async Task<MessageRoutingResponse?> ProcessContextualResponse(List<(string Role, string Content)> conversationHistory, string messageText, TenantContext tenantContext, Conversation conversation)
    {
        try
        {
            // Debug: Log conversation history
            _logger.LogInformation("ProcessContextualResponse: Conversation history has {Count} messages", conversationHistory.Count);
            foreach (var (role, content) in conversationHistory)
            {
                _logger.LogInformation("  {Role}: {Content}", role, content);
            }

            // Look for recent bot questions that might be awaiting responses
            if (conversationHistory.Count < 2) 
            {
                _logger.LogInformation("ProcessContextualResponse: Not enough messages in history ({Count})", conversationHistory.Count);
                return null;
            }

            var lastBotMessage = conversationHistory.LastOrDefault(h => h.Role == "assistant");
            if (lastBotMessage.Content == null) return null;

            _logger.LogInformation("[DIAGNOSTIC] Analyzing contextual response. Last bot message: '{LastMessage}', Current user message: '{CurrentMessage}'",
                lastBotMessage.Content, messageText);

            // Pattern matching for WiFi connection help responses
            if (IsAffirmativeResponse(messageText) && ContainsWiFiConnectionHelp(lastBotMessage.Content))
            {
                return await CreateWiFiSupportTaskResponse(tenantContext);
            }

            // Pattern matching for "anything else" follow-up responses
            // Only respond with generic help if it's a simple affirmative without a specific request
            if (IsAffirmativeResponse(messageText) && ContainsAnythingElseQuestion(lastBotMessage.Content) && IsSimpleAffirmativeResponse(messageText))
            {
                return new MessageRoutingResponse
                {
                    Reply = "Wonderful! I'm here to help. What else can I assist you with? I'd be happy to help you with our menu, room service items, local attractions, or anything else you need during your stay."
                };
            }

            // Pattern matching for negative responses to "anything else"
            if (IsNegativeResponse(messageText) && ContainsAnythingElseQuestion(lastBotMessage.Content))
            {
                return new MessageRoutingResponse
                {
                    Reply = "Perfect! I'm here whenever you need me. Enjoy your stay! 😊"
                };
            }

            // Pattern matching for acknowledgment responses (Ok, Thanks, Got it, etc.)
            if (IsAcknowledgmentResponse(messageText))
            {
                return new MessageRoutingResponse
                {
                    Reply = "You're welcome! I'm here if you need anything else during your stay. 👍"
                };
            }

            // Pattern matching for front desk connection confirmations
            if (IsAffirmativeResponse(messageText) && ContainsFrontDeskConnection(lastBotMessage.Content))
            {
                return new MessageRoutingResponse
                {
                    Reply = "Perfect! I'm connecting you with our front desk right away. They'll be in touch within the next few minutes to assist you personally. In the meantime, feel free to ask me about anything else!"
                };
            }

            // Pattern matching for menu follow-up questions
            if (ContainsMenuFollowUp(messageText) && ContainsMenuInformation(lastBotMessage.Content))
            {
                return await HandleMenuFollowUpResponse(messageText, tenantContext);
            }

            // LLM-based quantity clarification detection
            var clarificationAnalysis = await AnalyzeQuantityClarificationAsync(messageText, lastBotMessage.Content, conversationHistory);

            if (clarificationAnalysis != null && clarificationAnalysis.IsQuantityClarification &&
                clarificationAnalysis.Confidence >= 0.7 && !clarificationAnalysis.IsAmbiguous)
            {
                _logger.LogInformation("🔢 QUANTITY CLARIFICATION DETECTED - Item: {ItemSlug}, Quantity: {Quantity}, Confidence: {Confidence}",
                    clarificationAnalysis.ItemSlug, clarificationAnalysis.Quantity, clarificationAnalysis.Confidence);

                // Look up the RequestItem to get the RequestItemId
                var searchTerm = clarificationAnalysis.ItemSlug?.ToLower() ?? "";
                var requestItem = await _context.RequestItems
                    .FirstOrDefaultAsync(r => r.TenantId == tenantContext.TenantId &&
                                             (r.LlmVisibleName.ToLower().Contains(searchTerm) ||
                                              r.Name.ToLower().Contains(searchTerm)));

                // Check for duplicate tasks created in the last 5 minutes
                var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
                var itemNameLower = (clarificationAnalysis.ItemName ?? "").ToLower();

                StaffTask? existingTask;
                if (requestItem != null)
                {
                    existingTask = await _context.StaffTasks
                        .Where(t => t.ConversationId == conversation.Id &&
                                    t.CreatedAt >= fiveMinutesAgo &&
                                    (t.Status == "Open" || t.Status == "Pending" || t.Status == "InProgress") &&
                                    t.RequestItemId == requestItem.Id)
                        .OrderByDescending(t => t.CreatedAt)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    existingTask = await _context.StaffTasks
                        .Where(t => t.ConversationId == conversation.Id &&
                                    t.CreatedAt >= fiveMinutesAgo &&
                                    (t.Status == "Open" || t.Status == "Pending" || t.Status == "InProgress") &&
                                    t.Title.ToLower().Contains(itemNameLower))
                        .OrderByDescending(t => t.CreatedAt)
                        .FirstOrDefaultAsync();
                }

                if (existingTask != null)
                {
                    // Update the existing task with the new quantity
                    _logger.LogInformation("📝 UPDATING EXISTING TASK #{TaskId} - Old Quantity: {OldQty}, New Quantity: {NewQty}",
                        existingTask.Id, existingTask.Quantity, clarificationAnalysis.Quantity);

                    existingTask.Quantity = clarificationAnalysis.Quantity ?? 1;
                    existingTask.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Get guest status for room number
                    var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

                    // Create direct response for quantity update
                    var updateMessage = $"Perfect! I've updated your request to {clarificationAnalysis.Quantity} {clarificationAnalysis.ItemName ?? clarificationAnalysis.ItemSlug}. " +
                        $"They'll be delivered to {(!string.IsNullOrEmpty(guestStatus.RoomNumber) ? $"room {guestStatus.RoomNumber}" : "your room")} shortly.";

                    return new MessageRoutingResponse { Reply = updateMessage };
                }
                else
                {
                    // No existing task found, create a new one
                    _logger.LogInformation("📝 CREATING NEW TASK - Item: {ItemSlug}, Quantity: {Quantity}",
                        clarificationAnalysis.ItemSlug, clarificationAnalysis.Quantity);

                    return await CreateItemTaskResponse(
                        clarificationAnalysis.ItemSlug ?? "unknown-item",
                        clarificationAnalysis.ItemName ?? "item",
                        clarificationAnalysis.Quantity ?? 1,
                        tenantContext,
                        conversation
                    );
                }
            }

            // Handle cancellations
            if (clarificationAnalysis != null && clarificationAnalysis.IsCancellation && clarificationAnalysis.Confidence >= 0.7)
            {
                _logger.LogInformation("❌ CANCELLATION DETECTED - Confidence: {Confidence}", clarificationAnalysis.Confidence);

                return new MessageRoutingResponse
                {
                    Reply = "No problem! I've cancelled that request. Let me know if you need anything else during your stay."
                };
            }

            // Handle ambiguous cases
            if (clarificationAnalysis != null && clarificationAnalysis.IsAmbiguous && clarificationAnalysis.Confidence >= 0.7)
            {
                _logger.LogInformation("⚠️ AMBIGUOUS CLARIFICATION - Asking for clarification");

                return new MessageRoutingResponse
                {
                    Reply = clarificationAnalysis.ClarificationNeeded ??
                            "I want to make sure I get this right - could you clarify which item you're referring to?"
                };
            }

            // Pattern matching for quantity responses (fallback for simple cases)
            if (IsQuantityResponse(messageText) && ContainsTowelsQuestion(lastBotMessage.Content))
            {
                return await CreateTowelsTaskResponse(messageText, tenantContext, conversation);
            }

            // Pattern matching for charger type responses  
            if (IsChargerTypeResponse(messageText) && ContainsChargerQuestion(lastBotMessage.Content))
            {
                return await CreateChargerTaskResponse(messageText, tenantContext);
            }

            // Pattern matching for iron responses
            if (IsAffirmativeResponse(messageText) && ContainsIronQuestion(lastBotMessage.Content))
            {
                return await CreateIronTaskResponse(tenantContext, conversation);
            }

            // Pattern matching for toilet paper quantity responses
            if (IsQuantityResponse(messageText) && ContainsToiletPaperQuestion(lastBotMessage.Content))
            {
                return await CreateToiletPaperTaskResponse(messageText, tenantContext, conversation);
            }

            // Pattern matching for bathroom amenities requests
            if (IsBathroomAmenityResponse(messageText) && ContainsBathroomAmenityQuestion(lastBotMessage.Content))
            {
                return await CreateBathroomAmenityTaskResponse(messageText, tenantContext, conversation);
            }

            // Pattern matching for room temperature/AC issues
            if (IsTemperatureComplaint(messageText))
            {
                return new MessageRoutingResponse
                {
                    Reply = "I'm sorry to hear you're having temperature issues in your room. Let me connect you with our front desk right away - they'll help you get comfortable as quickly as possible. Would you like me to connect you now?"
                };
            }

            // Pattern matching for direct laundry service requests
            if (IsLaundryServiceRequest(messageText))
            {
                // Check if laundry service is actually available
                var isLaundryAvailable = await IsServiceAvailableAsync("Laundry Service", tenantContext.TenantId);

                if (isLaundryAvailable)
                {
                    return new MessageRoutingResponse
                    {
                        Reply = "I'd be happy to arrange laundry service for you! What time would you prefer for pickup? We can collect your items in the morning, afternoon, or evening."
                    };
                }
                else
                {
                    return new MessageRoutingResponse
                    {
                        Reply = "I'm sorry, but our laundry service is currently unavailable. However, I'd be happy to help you find alternative solutions or assist with other services. What else can I help you with during your stay?"
                    };
                }
            }

            // Pattern matching for direct housekeeping service requests
            if (IsHousekeepingServiceRequest(messageText))
            {
                // Check if housekeeping service is actually available
                var isHousekeepingAvailable = await IsServiceAvailableAsync("Housekeeping", tenantContext.TenantId);

                if (isHousekeepingAvailable)
                {
                    return new MessageRoutingResponse
                    {
                        Reply = "I'd be delighted to arrange housekeeping service for you! What time would work best? We can clean your room in the morning, afternoon, or evening."
                    };
                }
                else
                {
                    return new MessageRoutingResponse
                    {
                        Reply = "I'm sorry, but our housekeeping service is currently unavailable at the moment. However, I'd be happy to help you find alternative solutions or assist with other services. What else can I help you with during your stay?"
                    };
                }
            }

            // Pattern matching for direct food delivery/room service requests
            if (IsFoodDeliveryRequest(messageText))
            {
                // Check if room service is actually available
                var isRoomServiceAvailable = await IsServiceAvailableAsync("Room Service", tenantContext.TenantId);

                if (isRoomServiceAvailable)
                {
                    return new MessageRoutingResponse
                    {
                        Reply = "I'd be happy to arrange food delivery for you! What time would you prefer? We can deliver in the morning, afternoon, or evening."
                    };
                }
                else
                {
                    return new MessageRoutingResponse
                    {
                        Reply = "I'm sorry, but our room service is currently unavailable. However, I'd be happy to help you find alternative dining options or assist with other services. What else can I help you with during your stay?"
                    };
                }
            }

            // Pattern matching for direct maintenance requests
            if (IsMaintenanceRequest(messageText))
            {
                return new MessageRoutingResponse
                {
                    Reply = "I understand you need maintenance assistance. How urgent is this issue? Please let me know if it's urgent, moderate, or if it can wait."
                };
            }

            // Pattern matching for general room service requests
            if (IsRoomServiceRequest(messageText))
            {
                return new MessageRoutingResponse
                {
                    Reply = "I'd be happy to help with room service! What can I arrange for you today? I can help with food delivery, housekeeping, laundry, or any other room service needs."
                };
            }

            // Pattern matching for laundry service timing responses
            var isTimeResponse = IsTimeResponse(messageText);
            var containsLaundryQuestion = ContainsLaundryQuestion(lastBotMessage.Content);
            var containsHousekeepingQuestion = ContainsHousekeepingQuestion(lastBotMessage.Content);
            
            _logger.LogInformation("Timing detection: IsTimeResponse='{IsTimeResponse}', ContainsLaundryQuestion='{ContainsLaundryQuestion}', ContainsHousekeepingQuestion='{ContainsHousekeepingQuestion}' for message='{Message}', lastBot='{LastBot}'", 
                isTimeResponse, containsLaundryQuestion, containsHousekeepingQuestion, messageText, lastBotMessage.Content);
                
            if (isTimeResponse && containsLaundryQuestion)
            {
                _logger.LogInformation("Creating laundry task for timing response: '{Message}'", messageText);
                return await CreateLaundryTaskResponse(messageText, tenantContext, conversation);
            }

            // Pattern matching for housekeeping timing responses
            if (isTimeResponse && containsHousekeepingQuestion)
            {
                _logger.LogInformation("Creating housekeeping task for timing response: '{Message}'", messageText);
                return await CreateHousekeepingTaskResponse(messageText, tenantContext, conversation);
            }

            // Pattern matching for maintenance urgency responses
            if (IsUrgencyResponse(messageText) && ContainsMaintenanceUrgencyQuestion(lastBotMessage.Content))
            {
                return await CreateUrgentMaintenanceTaskResponse(messageText, tenantContext);
            }

            // Pattern matching for food delivery timing
            if (IsTimeResponse(messageText) && ContainsFoodDeliveryQuestion(lastBotMessage.Content))
            {
                // Get guest status to retrieve room number
                var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);
                
                // Create room-specific response
                var roomInfo = !string.IsNullOrEmpty(guestStatus.RoomNumber) 
                    ? $"to room {guestStatus.RoomNumber}" 
                    : "to your room";
                
                return new MessageRoutingResponse
                {
                    Reply = $"Perfect! I'll make sure your order is delivered {messageText.ToLower()}. Your food will be prepared fresh and delivered {roomInfo}. Is there anything else I can help you with?"
                };
            }

            // Pattern matching for WiFi troubleshooting responses
            if (IsWiFiTroubleshootingResponse(messageText) && ContainsWiFiTroubleshooting(lastBotMessage.Content))
            {
                return await HandleWiFiTroubleshootingFollowUp(messageText, tenantContext);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing contextual response");
            return null;
        }
    }

    private bool IsQuantityResponse(string message)
    {
        var quantityPatterns = new[] { @"\d+", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "sets", "towels" };
        return quantityPatterns.Any(pattern => System.Text.RegularExpressions.Regex.IsMatch(message.ToLower(), pattern));
    }

    private bool IsChargerTypeResponse(string message)
    {
        var chargerTypes = new[] { 
            // Phone brands
            "iphone", "android", "samsung", "huawei", "xiaomi", "oppo", "vivo", "oneplus", "google pixel",
            // Laptop brands
            "laptop", "macbook", "dell", "hp", "lenovo", "asus", "acer", "msi", "alienware", "thinkpad", "surface",
            // Connector types
            "usb", "lightning", "usb-c", "type-c", "micro", "micro-usb",
            // Device types
            "phone", "tablet", "ipad", "computer"
        };
        return chargerTypes.Any(type => message.ToLower().Contains(type));
    }

    private bool IsAffirmativeResponse(string message)
    {
        var affirmatives = new[] { "yes", "yeah", "yep", "sure", "ok", "okay", "please", "thanks" };
        return affirmatives.Any(word => message.ToLower().Contains(word));
    }

    private bool IsSimpleAffirmativeResponse(string message)
    {
        var messageLower = message.ToLower().Trim();

        // Check if it's a simple affirmative response (short and doesn't contain request keywords)
        var requestKeywords = new[] { "book", "order", "get", "need", "want", "request", "call", "send", "deliver", "bring", "help with", "fix", "repair", "clean", "change" };

        // If message contains any request keywords, it's not a simple affirmative
        if (requestKeywords.Any(keyword => messageLower.Contains(keyword)))
            return false;

        // If message is longer than 10 words, it's likely not a simple affirmative
        if (messageLower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 10)
            return false;

        return true;
    }

    private bool ContainsTowelsQuestion(string message)
    {
        return message.ToLower().Contains("how many") && message.ToLower().Contains("towels");
    }

    private bool ContainsChargerQuestion(string message)
    {
        return message.ToLower().Contains("iphone") && message.ToLower().Contains("android") && message.ToLower().Contains("charger");
    }

    private bool ContainsIronQuestion(string message)
    {
        return message.ToLower().Contains("iron") && (message.ToLower().Contains("delivered") || message.ToLower().Contains("send"));
    }

    private bool ContainsWiFiConnectionHelp(string message)
    {
        return message.ToLower().Contains("need help connecting") || 
               (message.ToLower().Contains("wifi") && message.ToLower().Contains("help")) ||
               (message.ToLower().Contains("connection") && message.ToLower().Contains("issues"));
    }

    private bool ContainsAnythingElseQuestion(string message)
    {
        return message.ToLower().Contains("anything else") || 
               message.ToLower().Contains("is there anything") ||
               message.ToLower().Contains("what else can") ||
               message.ToLower().Contains("need anything else");
    }

    private bool IsNegativeResponse(string message)
    {
        var negativeKeywords = new[] { "no", "nope", "no thanks", "no thank you", "nothing", "nothing else", "that's all", "that's it", "i'm good", "all good", "nothing more" };
        var messageLower = message.Trim().ToLower();
        return negativeKeywords.Any(keyword => messageLower == keyword || messageLower.StartsWith(keyword + " ") || messageLower.StartsWith(keyword + "."));
    }

    private bool IsAcknowledgmentResponse(string message)
    {
        var acknowledgmentKeywords = new[] { "ok", "okay", "thanks", "thank you", "got it", "understood", "cool", "great", "perfect", "awesome", "nice", "good", "alright", "appreciate it", "cheers" };
        var messageLower = message.Trim().ToLower();
        return acknowledgmentKeywords.Any(keyword => 
            messageLower == keyword || 
            messageLower == keyword + "!" ||
            messageLower.StartsWith(keyword + " ") || 
            messageLower.StartsWith(keyword + ".") ||
            messageLower.StartsWith(keyword + ","));
    }

    private bool ContainsFrontDeskConnection(string message)
    {
        return message.ToLower().Contains("connect you with our front desk") ||
               message.ToLower().Contains("front desk who can provide") ||
               message.ToLower().Contains("let me connect you with");
    }

    private bool ContainsMenuFollowUp(string message)
    {
        return message.ToLower().Contains("more details") ||
               message.ToLower().Contains("tell me more") ||
               message.ToLower().Contains("what about") ||
               message.ToLower().Contains("how much") ||
               message.ToLower().Contains("price") ||
               message.ToLower().Contains("cost") ||
               message.ToLower().Contains("full menu") ||
               message.ToLower().Contains("see menu");
    }

    private bool ContainsMenuInformation(string message)
    {
        return message.ToLower().Contains("menu") ||
               message.ToLower().Contains("breakfast") ||
               message.ToLower().Contains("lunch") ||
               message.ToLower().Contains("dinner") ||
               message.ToLower().Contains("special");
    }

    private bool ContainsToiletPaperQuestion(string message)
    {
        return message.ToLower().Contains("how many") && 
               (message.ToLower().Contains("toilet paper") || message.ToLower().Contains("tissue"));
    }

    private bool ContainsBathroomAmenityQuestion(string message)
    {
        return message.ToLower().Contains("what type") && 
               (message.ToLower().Contains("shampoo") || message.ToLower().Contains("soap") || 
                message.ToLower().Contains("amenity") || message.ToLower().Contains("toiletries"));
    }

    private bool IsBathroomAmenityResponse(string message)
    {
        var amenityTypes = new[] { "shampoo", "conditioner", "body wash", "soap", "lotion", "toothbrush", "toothpaste", "razor" };
        return amenityTypes.Any(amenity => message.ToLower().Contains(amenity));
    }

    private bool IsTemperatureComplaint(string message)
    {
        var tempComplaints = new[] { "too hot", "too cold", "freezing", "burning", "air conditioning", "ac not working", "heater", "temperature", "climate control" };
        return tempComplaints.Any(complaint => message.ToLower().Contains(complaint));
    }

    private bool ContainsLaundryQuestion(string message)
    {
        return message.ToLower().Contains("what time") && 
               (message.ToLower().Contains("laundry") || message.ToLower().Contains("pickup"));
    }

    private bool ContainsHousekeepingQuestion(string message)
    {
        return message.ToLower().Contains("what time") && 
               (message.ToLower().Contains("housekeeping") || message.ToLower().Contains("clean"));
    }

    private bool ContainsMaintenanceUrgencyQuestion(string message)
    {
        return message.ToLower().Contains("how urgent") || 
               (message.ToLower().Contains("maintenance") && message.ToLower().Contains("priority"));
    }

    private bool ContainsFoodQuestion(string message)
    {
        return message.ToLower().Contains("what time") && 
               (message.ToLower().Contains("food") || 
                message.ToLower().Contains("meal") || 
                message.ToLower().Contains("dining") ||
                message.ToLower().Contains("restaurant"));
    }

    private bool ContainsMaintenanceQuestion(string message)
    {
        return message.ToLower().Contains("what time") && 
               (message.ToLower().Contains("maintenance") || 
                message.ToLower().Contains("repair") || 
                message.ToLower().Contains("fix"));
    }

    private bool ContainsFoodDeliveryQuestion(string message)
    {
        return message.ToLower().Contains("when would you like") && 
               (message.ToLower().Contains("delivered") || message.ToLower().Contains("food"));
    }

    private bool IsTimeResponse(string message)
    {
        var timeResponses = new[] { 
            "morning", "afternoon", "evening", "now", "asap", "immediately", "later", 
            "in an hour", "in 30 minutes", "tonight", "tomorrow", "am", "pm",
            "9", "10", "11", "12", "1", "2", "3", "4", "5", "6", "7", "8"
        };
        return timeResponses.Any(time => message.ToLower().Contains(time));
    }

    private bool IsUrgencyResponse(string message)
    {
        var urgencyResponses = new[] { "urgent", "emergency", "immediate", "asap", "can wait", "not urgent", "low priority", "high priority" };
        return urgencyResponses.Any(urgency => message.ToLower().Contains(urgency));
    }

    private bool ContainsWiFiTroubleshooting(string message)
    {
        return message.ToLower().Contains("troubleshooting steps") || 
               message.ToLower().Contains("try forgetting the network") ||
               message.ToLower().Contains("restart your device");
    }

    private bool IsWiFiTroubleshootingResponse(string message)
    {
        var troubleResponses = new[] { 
            "still not working", "tried that", "doesn't work", "same problem", 
            "still can't connect", "not connecting", "it worked", "that fixed it",
            "connected now", "working now", "solved"
        };
        return troubleResponses.Any(response => message.ToLower().Contains(response));
    }

    private async Task<MessageRoutingResponse> CreateTowelsTaskResponse(string quantityMessage, TenantContext tenantContext, Conversation conversation)
    {
        // Check if housekeeping service is available
        var isHousekeepingAvailable = await IsServiceAvailableAsync("Housekeeping", tenantContext.TenantId);

        if (!isHousekeepingAvailable)
        {
            var unavailableTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
                tenantContext.TenantId,
                ResponseTemplateKeys.ServiceRequestHousekeepingUnavailable,
                new Dictionary<string, object>(),
                "I'm sorry, but our housekeeping service is currently unavailable at the moment. Please feel free to contact our front desk, and they'll be happy to help you with towels right away."
            );

            return new MessageRoutingResponse
            {
                Reply = unavailableTemplate.Content
            };
        }

        // Extract quantity from message
        var match = System.Text.RegularExpressions.Regex.Match(quantityMessage, @"\d+");
        int quantity = match.Success ? int.Parse(match.Value) : 1;

        // Get guest status to retrieve room number
        var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

        var taskAction = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "create_task",
            item_slug = "towels",
            quantity = quantity,
            room_number = guestStatus.RoomNumber
        });

        // Create room-specific response using template
        var variables = new Dictionary<string, object>
        {
            [ResponseVariableNames.Quantity] = quantity.ToString(),
            [ResponseVariableNames.RoomNumber] = guestStatus.RoomNumber ?? "",
            [ResponseVariableNames.GuestName] = guestStatus.DisplayName ?? ""
        };

        var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
            tenantContext.TenantId,
            ResponseTemplateKeys.TowelDeliveryConfirmation,
            variables,
            $"Perfect! I'll arrange for {quantity} sets of fresh towels to be delivered to {(!string.IsNullOrEmpty(guestStatus.RoomNumber) ? $"room {guestStatus.RoomNumber}" : "your room")} right away."
        );

        return new MessageRoutingResponse
        {
            Reply = processedTemplate.Content,
            Action = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(taskAction)
        };
    }

    private async Task<MessageRoutingResponse> CreateItemTaskResponse(
        string itemSlug,
        string itemName,
        int quantity,
        TenantContext tenantContext,
        Conversation conversation)
    {
        _logger.LogInformation("📦 Creating item task response - ItemSlug: {ItemSlug}, ItemName: {ItemName}, Quantity: {Quantity}",
            itemSlug, itemName, quantity);

        // Get guest status to retrieve room number
        var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

        var taskAction = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "create_task",
            item_slug = itemSlug,
            quantity = quantity,
            room_number = guestStatus.RoomNumber
        });

        // Create room-specific response
        var variables = new Dictionary<string, object>
        {
            ["ItemName"] = itemName,
            ["Quantity"] = quantity.ToString(),
            ["RoomNumber"] = guestStatus.RoomNumber ?? "",
            ["GuestName"] = guestStatus.DisplayName ?? ""
        };

        // Create direct response for item delivery
        var responseMessage = $"Perfect! I'll arrange for {quantity} {itemName} to be delivered to {(!string.IsNullOrEmpty(guestStatus.RoomNumber) ? $"room {guestStatus.RoomNumber}" : "your room")} right away.";

        return new MessageRoutingResponse
        {
            Reply = responseMessage,
            Action = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(taskAction)
        };
    }

    private async Task<MessageRoutingResponse> CreateChargerTaskResponse(string chargerType, TenantContext tenantContext)
    {
        var chargerTypeLower = chargerType.ToLower();
        string itemSlug;
        string chargerName;

        // Determine charger type based on device/brand mentioned
        if (chargerTypeLower.Contains("iphone"))
        {
            itemSlug = "iphone charger";
            chargerName = "iPhone Lightning charger";
        }
        else if (chargerTypeLower.Contains("laptop") || chargerTypeLower.Contains("macbook") ||
                 chargerTypeLower.Contains("dell") || chargerTypeLower.Contains("hp") ||
                 chargerTypeLower.Contains("lenovo") || chargerTypeLower.Contains("asus") ||
                 chargerTypeLower.Contains("acer") || chargerTypeLower.Contains("huawei") ||
                 chargerTypeLower.Contains("msi") || chargerTypeLower.Contains("alienware") ||
                 chargerTypeLower.Contains("thinkpad") || chargerTypeLower.Contains("surface") ||
                 chargerTypeLower.Contains("computer"))
        {
            itemSlug = "laptop charger";
            chargerName = "laptop charger (USB-C/Universal)";
        }
        else if (chargerTypeLower.Contains("usb-c") || chargerTypeLower.Contains("type-c"))
        {
            itemSlug = "usb-c charger";
            chargerName = "USB-C charger";
        }
        else if (chargerTypeLower.Contains("samsung") || chargerTypeLower.Contains("android"))
        {
            itemSlug = "android charger";
            chargerName = "Android USB charger";
        }
        else
        {
            // Default to asking for clarification using template
            var clarificationTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
                tenantContext.TenantId,
                ResponseTemplateKeys.ChargerRequest,
                new Dictionary<string, object>(),
                "I'd be happy to get you a charger! Could you tell me what type you need? For example: iPhone, Android/Samsung, laptop, or USB-C?"
            );

            return new MessageRoutingResponse
            {
                Reply = clarificationTemplate.Content
            };
        }

        var taskAction = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "create_task",
            item_slug = itemSlug,
            quantity = 1,
            room_number = (string?)null
        });

        // Use template for charger delivery confirmation
        var variables = new Dictionary<string, object>
        {
            [ResponseVariableNames.ChargerType] = chargerName
        };

        var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
            tenantContext.TenantId,
            ResponseTemplateKeys.ChargerDeliveryConfirmation,
            variables,
            $"Excellent! I'll send a {chargerName} to your room immediately."
        );

        return new MessageRoutingResponse
        {
            Reply = processedTemplate.Content,
            Action = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(taskAction)
        };
    }

    private async Task<MessageRoutingResponse> CreateIronTaskResponse(TenantContext tenantContext, Conversation conversation)
    {
        // Get guest status to retrieve room number
        var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

        var taskAction = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "create_task",
            item_slug = "iron",
            quantity = 1,
            room_number = guestStatus.RoomNumber
        });

        // Create room-specific response using template
        var variables = new Dictionary<string, object>
        {
            [ResponseVariableNames.RoomNumber] = guestStatus.RoomNumber ?? "",
            [ResponseVariableNames.GuestName] = guestStatus.DisplayName ?? ""
        };

        var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
            tenantContext.TenantId,
            ResponseTemplateKeys.IronDeliveryConfirmation,
            variables,
            $"Great! I'll have an iron and ironing board delivered to {(!string.IsNullOrEmpty(guestStatus.RoomNumber) ? $"room {guestStatus.RoomNumber}" : "your room")} shortly. Is there anything else you need?"
        );

        return new MessageRoutingResponse
        {
            Reply = processedTemplate.Content,
            Action = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(taskAction)
        };
    }

    private async Task<MessageRoutingResponse> CreateToiletPaperTaskResponse(string quantityMessage, TenantContext tenantContext, Conversation conversation)
    {
        var quantity = ExtractQuantityFromMessage(quantityMessage);

        // Get guest status to retrieve room number
        var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

        var taskAction = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "create_task",
            item_slug = "bathroom supplies",
            quantity = quantity,
            room_number = guestStatus.RoomNumber
        });

        // Create room-specific response using template
        var variables = new Dictionary<string, object>
        {
            [ResponseVariableNames.Quantity] = quantity.ToString(),
            [ResponseVariableNames.RoomNumber] = guestStatus.RoomNumber ?? "",
            [ResponseVariableNames.GuestName] = guestStatus.DisplayName ?? ""
        };

        var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
            tenantContext.TenantId,
            ResponseTemplateKeys.ToiletPaperDeliveryConfirmation,
            variables,
            $"Perfect! I'll have {quantity} roll{(quantity > 1 ? "s" : "")} of toilet paper delivered to {(!string.IsNullOrEmpty(guestStatus.RoomNumber) ? $"room {guestStatus.RoomNumber}" : "your room")} right away. Anything else you need?"
        );

        return new MessageRoutingResponse
        {
            Reply = processedTemplate.Content,
            Action = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(taskAction)
        };
    }

    private async Task<MessageRoutingResponse> CreateBathroomAmenityTaskResponse(string amenityType, TenantContext tenantContext, Conversation conversation)
    {
        // Get guest status to retrieve room number
        var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

        var amenityLower = amenityType.ToLower();
        string itemSlug;
        string amenityName;

        if (amenityLower.Contains("shampoo"))
        {
            itemSlug = "shampoo";
            amenityName = "shampoo";
        }
        else if (amenityLower.Contains("conditioner"))
        {
            itemSlug = "conditioner";
            amenityName = "conditioner";
        }
        else if (amenityLower.Contains("body wash"))
        {
            itemSlug = "body wash";
            amenityName = "body wash";
        }
        else if (amenityLower.Contains("soap"))
        {
            itemSlug = "soap";
            amenityName = "soap";
        }
        else if (amenityLower.Contains("toothbrush"))
        {
            itemSlug = "toothbrush";
            amenityName = "toothbrush";
        }
        else
        {
            itemSlug = "toiletries";
            amenityName = "bathroom amenities";
        }

        var taskAction = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "create_task",
            item_slug = itemSlug,
            quantity = 1,
            room_number = guestStatus.RoomNumber
        });

        // Create room-specific response using template
        var variables = new Dictionary<string, object>
        {
            [ResponseVariableNames.AmenityName] = amenityName,
            [ResponseVariableNames.RoomNumber] = guestStatus.RoomNumber ?? "",
            [ResponseVariableNames.GuestName] = guestStatus.DisplayName ?? ""
        };

        var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
            tenantContext.TenantId,
            ResponseTemplateKeys.AmenityDeliveryConfirmation,
            variables,
            $"Excellent! I'll have {amenityName} delivered to {(!string.IsNullOrEmpty(guestStatus.RoomNumber) ? $"room {guestStatus.RoomNumber}" : "your room")} shortly. Is there anything else I can get for you?"
        );

        return new MessageRoutingResponse
        {
            Reply = processedTemplate.Content,
            Action = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(taskAction)
        };
    }

    private async Task<MessageRoutingResponse> CreateLaundryTaskResponse(string timeMessage, TenantContext tenantContext, Conversation conversation)
    {
        // Check if laundry service is available
        var isLaundryAvailable = await IsServiceAvailableAsync("Laundry Service", tenantContext.TenantId);

        if (!isLaundryAvailable)
        {
            var unavailableTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
                tenantContext.TenantId,
                ResponseTemplateKeys.ServiceRequestLaundryUnavailable,
                new Dictionary<string, object>(),
                "I'm sorry, but our laundry service is currently unavailable at the moment. Please feel free to contact our front desk, and they'll be happy to assist you."
            );

            return new MessageRoutingResponse
            {
                Reply = unavailableTemplate.Content
            };
        }

        // Get guest status to retrieve room number
        var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

        var taskAction = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "create_task",
            item_slug = "laundry",
            quantity = 1,
            preferred_time = timeMessage.ToLower(),
            room_number = guestStatus.RoomNumber
        });

        // Create room-specific response using template
        var variables = new Dictionary<string, object>
        {
            [ResponseVariableNames.TimeMessage] = timeMessage.ToLower(),
            [ResponseVariableNames.RoomNumber] = guestStatus.RoomNumber ?? "",
            [ResponseVariableNames.GuestName] = guestStatus.DisplayName ?? ""
        };

        var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
            tenantContext.TenantId,
            ResponseTemplateKeys.LaundryScheduleConfirmation,
            variables,
            $"Perfect! I've scheduled your laundry pickup for {timeMessage.ToLower()}. Our housekeeping team will collect your items from {(!string.IsNullOrEmpty(guestStatus.RoomNumber) ? $"room {guestStatus.RoomNumber}" : "your room")} and have them returned within 24 hours. Anything else I can help you with?"
        );

        return new MessageRoutingResponse
        {
            Reply = processedTemplate.Content,
            Action = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(taskAction)
        };
    }

    private async Task<MessageRoutingResponse> CreateHousekeepingTaskResponse(string timeMessage, TenantContext tenantContext, Conversation conversation)
    {
        // Check if housekeeping service is available
        var isHousekeepingAvailable = await IsServiceAvailableAsync("Housekeeping", tenantContext.TenantId);

        if (!isHousekeepingAvailable)
        {
            var unavailableTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
                tenantContext.TenantId,
                ResponseTemplateKeys.ServiceRequestHousekeepingUnavailable,
                new Dictionary<string, object>(),
                "I'm sorry, but our housekeeping service is currently unavailable at the moment. Please feel free to contact our front desk, and they'll be happy to assist you."
            );

            return new MessageRoutingResponse
            {
                Reply = unavailableTemplate.Content
            };
        }

        // Get guest status to retrieve room number
        var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

        var taskAction = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "create_task",
            task = new
            {
                item_slug = "cleaning",
                quantity = 1,
                preferred_time = timeMessage.ToLower(),
                room_number = guestStatus.RoomNumber
            }
        });

        // Create room-specific response using template
        var variables = new Dictionary<string, object>
        {
            [ResponseVariableNames.TimeMessage] = timeMessage.ToLower(),
            [ResponseVariableNames.RoomNumber] = guestStatus.RoomNumber ?? "",
            [ResponseVariableNames.GuestName] = guestStatus.DisplayName ?? ""
        };

        var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
            tenantContext.TenantId,
            ResponseTemplateKeys.HousekeepingScheduleConfirmation,
            variables,
            $"Excellent! I've scheduled housekeeping service for {timeMessage.ToLower()}. Our team will clean {(!string.IsNullOrEmpty(guestStatus.RoomNumber) ? $"room {guestStatus.RoomNumber}" : "your room")} thoroughly at your preferred time. Is there anything specific you'd like them to focus on?"
        );

        return new MessageRoutingResponse
        {
            Reply = processedTemplate.Content,
            Action = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(taskAction)
        };
    }

    private async Task<MessageRoutingResponse> CreateUrgentMaintenanceTaskResponse(string urgencyMessage, TenantContext tenantContext)
    {
        var isUrgent = urgencyMessage.ToLower().Contains("urgent") || urgencyMessage.ToLower().Contains("emergency") || urgencyMessage.ToLower().Contains("asap");

        var taskAction = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "create_task",
            task = new
            {
                item_slug = "plumbing",
                quantity = 1,
                priority = isUrgent ? "urgent" : "normal",
                room_number = (string?)null
            }
        });

        // Use template for maintenance response based on urgency
        var templateKey = isUrgent ? ResponseTemplateKeys.MaintenanceUrgent : ResponseTemplateKeys.MaintenanceStandard;
        var variables = new Dictionary<string, object>();

        var fallbackReply = isUrgent
            ? "I understand this is urgent! I've prioritized your maintenance request, and our team is on their way to address it immediately. You should expect someone within the next 30 minutes. Is there anything else I can help you with in the meantime?"
            : "Thank you for letting me know! I've logged your maintenance request, and our team will take care of it during regular maintenance hours. You can expect it to be resolved within the next few hours. Anything else I can help you with?";

        var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
            tenantContext.TenantId,
            templateKey,
            variables,
            fallbackReply
        );

        return new MessageRoutingResponse
        {
            Reply = processedTemplate.Content,
            Action = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(taskAction)
        };
    }

    private async Task<MessageRoutingResponse> HandleWiFiTroubleshootingFollowUp(string messageText, TenantContext tenantContext)
    {
        var messageLower = messageText.ToLower();

        if (messageLower.Contains("working now") || messageLower.Contains("connected") || messageLower.Contains("fixed") || messageLower.Contains("solved"))
        {
            var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
                tenantContext.TenantId,
                ResponseTemplateKeys.WiFiWorkingConfirmation,
                new Dictionary<string, object>(),
                "Wonderful! I'm so glad we got your WiFi working. You should now have full internet access throughout your stay. If you experience any other connectivity issues, just let me know. Is there anything else I can help you with?"
            );

            return new MessageRoutingResponse
            {
                Reply = processedTemplate.Content
            };
        }
        else if (messageLower.Contains("still not working") || messageLower.Contains("doesn't work") || messageLower.Contains("same problem"))
        {
            var taskAction = System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "create_task",
                item_slug = "wifi help",
                quantity = 1,
                priority = "high",
                room_number = (string?)null
            });

            var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
                tenantContext.TenantId,
                ResponseTemplateKeys.WiFiStillNotWorking,
                new Dictionary<string, object>(),
                "I'm sorry those basic steps didn't resolve the issue. Let me get our IT technical support team to come to your room right away. They'll have advanced tools to diagnose and fix any WiFi connectivity problems for you. You should expect someone within the next 15-20 minutes. In the meantime, you're welcome to use the lobby WiFi if needed. Anything else I can help you with?"
            );

            return new MessageRoutingResponse
            {
                Reply = processedTemplate.Content,
                Action = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(taskAction)
            };
        }
        else
        {
            var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
                tenantContext.TenantId,
                ResponseTemplateKeys.WiFiTroubleshootingFollowUp,
                new Dictionary<string, object>(),
                "Let me know how those troubleshooting steps work for you! If you're still having trouble connecting, I can send our IT support team to help you personally. Just say 'still not working' and I'll get someone to your room immediately."
            );

            return new MessageRoutingResponse
            {
                Reply = processedTemplate.Content
            };
        }
    }

    private async Task<MessageRoutingResponse> HandleMenuFollowUpResponse(string messageText, TenantContext tenantContext)
    {
        var messageLower = messageText.ToLower();

        // Handle price/cost inquiries
        if (messageLower.Contains("price") || messageLower.Contains("cost") || messageLower.Contains("how much"))
        {
            var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
                tenantContext.TenantId,
                ResponseTemplateKeys.MenuPriceInquiry,
                new Dictionary<string, object>(),
                "I'd be happy to help with pricing information! You can ask about specific dishes by name, or say something like 'show me breakfast prices' or 'lunch menu with prices'. What would you like to know about?"
            );

            return new MessageRoutingResponse
            {
                Reply = processedTemplate.Content
            };
        }

        // Handle full menu requests
        if (messageLower.Contains("full menu") || messageLower.Contains("see menu") || messageLower.Contains("complete menu"))
        {
            // Get current time for appropriate menu
            var currentTime = GetCurrentTimeForTenant(tenantContext.Timezone);
            var menuReply = await _menuService.QueryMenuAsync(tenantContext, "show full menu", currentTime);

            return new MessageRoutingResponse
            {
                Reply = menuReply
            };
        }

        // Handle general "tell me more" or "more details"
        if (messageLower.Contains("more details") || messageLower.Contains("tell me more"))
        {
            var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
                tenantContext.TenantId,
                ResponseTemplateKeys.MenuMoreDetails,
                new Dictionary<string, object>(),
                "I'd love to give you more details! What specifically would you like to know more about? You can ask about ingredients, dietary options, preparation methods, or anything else about our menu items."
            );

            return new MessageRoutingResponse
            {
                Reply = processedTemplate.Content
            };
        }

        // Default menu follow-up response
        var defaultTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
            tenantContext.TenantId,
            ResponseTemplateKeys.MenuDefault,
            new Dictionary<string, object>(),
            "What would you like to know about our menu? I can help with specific dishes, prices, ingredients, dietary restrictions, or show you different meal options throughout the day."
        );

        return new MessageRoutingResponse
        {
            Reply = defaultTemplate.Content
        };
    }

    private async Task<MessageRoutingResponse> CreateWiFiSupportTaskResponse(TenantContext tenantContext)
    {
        // Get WiFi credentials from tenant's business info
        var wifiInfo = await GetTenantWiFiCredentials(tenantContext.TenantId);

        // Use template for WiFi technical support
        var variables = new Dictionary<string, object>();

        if (wifiInfo.HasCredentials)
        {
            variables["network_name"] = wifiInfo.NetworkName;
            variables["password"] = wifiInfo.Password;
        }

        var baseTemplate = wifiInfo.HasCredentials
            ? $"I'll send someone from our IT support to help you connect to WiFi right away! 🔧\n\nIn the meantime, here are some quick troubleshooting steps you can try:\n\n1️⃣ Make sure you're selecting \"{wifiInfo.NetworkName}\"\n2️⃣ Enter the password exactly: {wifiInfo.Password}\n3️⃣ Try forgetting the network and reconnecting\n4️⃣ Restart your device's WiFi\n\nOur support team should be with you within 10-15 minutes. Is there anything else I can help you with?"
            : "I'll send someone from our IT support to help you connect to WiFi right away! 🔧\n\nI'll make sure our IT team brings the current WiFi details with them.\n\nOur support team should be with you within 10-15 minutes. Is there anything else I can help you with?";

        var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
            tenantContext.TenantId,
            ResponseTemplateKeys.WiFiTechnicalSupport,
            variables,
            baseTemplate
        );

        return new MessageRoutingResponse
        {
            Reply = processedTemplate.Content
        };
    }

    private async Task<(bool HasCredentials, string NetworkName, string Password)> GetTenantWiFiCredentials(int tenantId)
    {
        try
        {
            // Check if tenant has WiFi credentials configured in business info
            var wifiInfo = await _context.BusinessInfo
                .Where(b => b.TenantId == tenantId && b.Category == "wifi_credentials")
                .FirstOrDefaultAsync();

            if (wifiInfo != null && !string.IsNullOrEmpty(wifiInfo.Content))
            {
                // Parse WiFi credentials from structured content
                try
                {
                    var wifiData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(wifiInfo.Content);
                    if (wifiData != null && wifiData.ContainsKey("network") && wifiData.ContainsKey("password"))
                    {
                        return (true, wifiData["network"], wifiData["password"]);
                    }
                }
                catch
                {
                    // If JSON parsing fails, log and continue to no credentials
                    _logger.LogWarning("Failed to parse WiFi credentials for tenant {TenantId}", tenantId);
                }
            }

            // No WiFi credentials configured for this tenant
            return (false, "", "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WiFi credentials for tenant {TenantId}", tenantId);
            return (false, "", "");
        }
    }

    private async Task<MessageRoutingResponse?> CheckMenuQueries(TenantContext tenantContext, string messageText)
    {
        try
        {
            // Check if the message is asking about menu items, specials, or business info
            if (IsMenuRelatedQuery(messageText))
            {
                _logger.LogInformation("CheckMenuQueries: Processing menu query '{MessageText}' for tenant {TenantId}", messageText, tenantContext.TenantId);

                // Get current time for timezone-aware responses
                var currentTime = GetCurrentTimeForTenant(tenantContext.Timezone);

                string menuReply;

                // Check if this is a specific category request (Breakfast, Lunch, Dinner)
                if (IsCategoryMenuRequest(messageText))
                {
                    var category = ExtractCategoryFromMessage(messageText);
                    _logger.LogInformation("CheckMenuQueries: Detected category request for '{Category}'", category);
                    menuReply = await _menuService.QueryCategoryMenuAsync(tenantContext, category, currentTime);
                }
                // Check if this is a general menu request
                else if (IsGeneralMenuRequest(messageText))
                {
                    _logger.LogInformation("CheckMenuQueries: Detected general menu request, using condensed format");
                    menuReply = await _menuService.QueryCondensedMenuAsync(tenantContext, currentTime);
                }
                // Fall back to original detailed menu processing for specific items
                else
                {
                    _logger.LogInformation("CheckMenuQueries: Using detailed menu query for specific item request");
                    menuReply = await _menuService.QueryMenuAsync(tenantContext, messageText, currentTime);
                }

                return new MessageRoutingResponse
                {
                    Reply = menuReply
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking menu queries for tenant {TenantId}", tenantContext.TenantId);
            return null;
        }
    }

    private bool IsMenuRelatedQuery(string messageText)
    {
        var menuKeywords = new[]
        {
            // Food and dining only - be very specific to avoid catching facility queries
            "menu", "food", "eat", "meal", "dish", "dining", "restaurant", "cuisine",
            "what's for dinner", "show me the menu", "what food do you have", "what can i order",
            "what do you serve", "food options", "room service", "order food",
            
            // Restaurant hours only (not general facility hours)
            "restaurant hours", "dining hours", "kitchen hours", "when is dinner served",
            "what time is breakfast", "lunch time", "dinner time", "room service hours"
        };

        var messageLower = messageText.ToLower();
        return menuKeywords.Any(keyword => messageLower.Contains(keyword));
    }

    private bool IsCategoryMenuRequest(string messageText)
    {
        var categoryKeywords = new[]
        {
            "breakfast", "breakfast menu", "morning meal", "breakfast options",
            "lunch", "lunch menu", "afternoon meal", "lunch options", "midday meal",
            "dinner", "dinner menu", "evening meal", "dinner options", "supper"
        };

        var messageLower = messageText.ToLower();
        return categoryKeywords.Any(keyword => messageLower.Contains(keyword));
    }

    private bool IsGeneralMenuRequest(string messageText)
    {
        var generalMenuKeywords = new[]
        {
            "show me the menu", "display menu", "menu please", "see the menu",
            "what's on the menu", "full menu", "complete menu", "entire menu",
            "menu overview", "all menu items", "what food do you have",
            "what do you serve", "food options", "dining options",
            "let's see your menu", "let me see the menu", "can i see the menu",
            "show me your menu", "lets see the menu", "let's see menu",
            "show menu", "see menu", "view menu", "check menu"
        };

        var messageLower = messageText.ToLower();
        return generalMenuKeywords.Any(keyword => messageLower.Contains(keyword));
    }

    private string ExtractCategoryFromMessage(string messageText)
    {
        var messageLower = messageText.ToLower();

        if (messageLower.Contains("breakfast") || messageLower.Contains("morning"))
            return "Breakfast";
        if (messageLower.Contains("lunch") || messageLower.Contains("midday") || messageLower.Contains("afternoon"))
            return "Lunch";
        if (messageLower.Contains("dinner") || messageLower.Contains("evening") || messageLower.Contains("supper"))
            return "Dinner";

        return "Breakfast"; // Default fallback
    }

    private DateTime GetCurrentTimeForTenant(string timezone)
    {
        try
        {
            var tenantTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tenantTimeZone);
        }
        catch (Exception)
        {
            // Fall back to UTC if timezone is invalid
            return DateTime.UtcNow;
        }
    }

    private async Task<MessageRoutingResponse?> CheckMaintenanceIssues(TenantContext tenantContext, Conversation conversation, string messageText)
    {
        try
        {
            // Keywords that indicate something is broken or not working
            var maintenanceKeywords = new[]
            {
                "not working", "broken", "doesn't work", "not functioning", "out of order",
                "problem with", "issue with", "trouble with", "faulty", "damaged",
                "malfunctioning", "stopped working", "won't work", "isn't working",
                "defective", "busted", "failed", "error", "glitch", "malfunction",
                "broken down", "kaput", "bust", "not operational", "down", "dead",
                "crashed", "frozen", "stuck", "jammed", "clogged", "blocked",
                "leaking", "cracked", "torn", "ripped", "missing", "loose",
                "wobbly", "unstable", "flickering", "dim", "dark", "no power",
                "no electricity", "blown", "tripped", "short circuit", "sparking",
                "overheating", "cold", "not heating", "not cooling", "noisy",
                "making noise", "squeaking", "grinding", "rattling", "vibrating",
                "burning smell", "odd smell", "strange sound", "weird noise",
                "not responding", "unresponsive", "locked", "seized", "corroded",
                "rusty", "moldy", "dirty", "stained", "discolored", "faded"
            };

            // Policy-related keywords that should NOT be treated as maintenance issues
            var policyKeywords = new[]
            {
                "policy", "policies", "rule", "rules", "allowed", "permission",
                "what is", "what are", "tell me about", "information about",
                "can I", "am I allowed", "is it ok", "guidelines"
            };

            var messageLower = messageText.ToLower();

            // Check if this is a policy inquiry first
            var isPolicyInquiry = policyKeywords.Any(keyword => messageLower.Contains(keyword));

            // Special handling for "smoking" - only treat as maintenance if not asking about policy
            var containsSmoking = messageLower.Contains("smoking");
            var isSmokingPolicyInquiry = containsSmoking && isPolicyInquiry;

            // Check for maintenance keywords, but exclude smoking if it's a policy inquiry
            var hasMaintenanceIssue = maintenanceKeywords.Any(keyword => messageLower.Contains(keyword)) ||
                                    (containsSmoking && !isSmokingPolicyInquiry);

            if (hasMaintenanceIssue)
            {
                _logger.LogInformation("Maintenance issue detected in message: '{MessageText}' for tenant {TenantId}", messageText, tenantContext.TenantId);
                
                // Create a maintenance task
                await CreateMaintenanceTask(tenantContext, conversation, messageText);

                // Determine the type of issue for a more specific response
                var issueType = DetermineIssueType(messageText);
                var response = await GenerateMaintenanceResponseAsync(issueType, tenantContext.TenantId);

                return new MessageRoutingResponse
                {
                    Reply = response
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking maintenance issues for tenant {TenantId}", tenantContext.TenantId);
            return null;
        }
    }

    private async Task CreateMaintenanceTask(TenantContext tenantContext, Conversation conversation, string messageText)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);
            
            var issueType = DetermineIssueType(messageText);
            var priority = DeterminePriority(messageText);

            // Get guest status to retrieve room number
            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

            // Find or create a generic maintenance RequestItem
            var maintenanceItem = await _context.RequestItems
                .FirstOrDefaultAsync(ri => ri.TenantId == tenantContext.TenantId && ri.Name == "Maintenance Request");
            
            if (maintenanceItem == null)
            {
                maintenanceItem = new RequestItem
                {
                    TenantId = tenantContext.TenantId,
                    Name = "Maintenance Request",
                    Category = "maintenance",
                    LlmVisibleName = "maintenance request",
                    RequiresQuantity = false,
                    RequiresRoomDelivery = true,
                    IsAvailable = true,
                    SlaMinutes = GetPriorityHours(priority) * 60
                };
                
                _context.RequestItems.Add(maintenanceItem);
                await _context.SaveChangesAsync();
            }

            var task = new StaffTask
            {
                TenantId = tenantContext.TenantId,
                ConversationId = conversation.Id,
                RequestItemId = maintenanceItem.Id,
                TaskType = "maintenance",
                Quantity = 1,
                Status = "Open",
                Priority = priority,
                RoomNumber = guestStatus.RoomNumber, // Use room number from guest status
                Notes = $"{issueType}: Guest reported - {messageText}",
                CreatedAt = DateTime.UtcNow
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created maintenance task {TaskId} for tenant {TenantId}: {IssueType} - {MessageText}", 
                task.Id, tenantContext.TenantId, issueType, messageText);

            // Send notification for the created maintenance task
            await _notificationService.NotifyTaskCreatedAsync(tenantContext.TenantId, task, maintenanceItem.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance task for tenant {TenantId}", tenantContext.TenantId);
        }
    }

    private string DetermineIssueType(string messageText)
    {
        var messageLower = messageText.ToLower();
        
        // Check for specific types of issues
        if (messageLower.Contains("wifi") || messageLower.Contains("internet") || messageLower.Contains("connection"))
            return "WiFi/Internet";
        
        if (messageLower.Contains("air con") || messageLower.Contains("aircon") || messageLower.Contains("ac") || 
            messageLower.Contains("heating") || messageLower.Contains("temperature"))
            return "HVAC";
        
        if (messageLower.Contains("tv") || messageLower.Contains("television") || messageLower.Contains("remote"))
            return "Television";
        
        if (messageLower.Contains("light") || messageLower.Contains("lamp") || messageLower.Contains("bulb"))
            return "Lighting";
        
        if (messageLower.Contains("water") || messageLower.Contains("shower") || messageLower.Contains("tap") || 
            messageLower.Contains("faucet") || messageLower.Contains("toilet") || messageLower.Contains("bathroom"))
            return "Plumbing";
        
        if (messageLower.Contains("key") || messageLower.Contains("door") || messageLower.Contains("lock") || 
            messageLower.Contains("card"))
            return "Access/Security";
        
        if (messageLower.Contains("fridge") || messageLower.Contains("refrigerator") || messageLower.Contains("microwave") ||
            messageLower.Contains("coffee") || messageLower.Contains("kettle"))
            return "Appliances";

        return "General Maintenance";
    }

    private string DeterminePriority(string messageText)
    {
        var messageLower = messageText.ToLower();
        
        // High priority issues
        if (messageLower.Contains("no water") || messageLower.Contains("flooding") || 
            messageLower.Contains("emergency") || messageLower.Contains("urgent") ||
            messageLower.Contains("leak") || messageLower.Contains("electrical"))
            return "High";
        
        // Medium priority issues
        if (messageLower.Contains("air con") || messageLower.Contains("heating") || 
            messageLower.Contains("key") || messageLower.Contains("door") ||
            messageLower.Contains("wifi") || messageLower.Contains("internet"))
            return "Medium";
        
        // Default to low priority
        return "Low";
    }

    private int GetPriorityHours(string priority)
    {
        return priority switch
        {
            "High" => 1,    // 1 hour for high priority
            "Medium" => 4,  // 4 hours for medium priority
            "Low" => 24,    // 24 hours for low priority
            _ => 8          // Default 8 hours
        };
    }

    private async Task<string> GenerateMaintenanceResponseAsync(string issueType, int tenantId)
    {
        var templateKey = issueType.ToLower() switch
        {
            "wifi/internet" => "maintenance_wifi_internet",
            "hvac" => "maintenance_hvac",
            "television" => "maintenance_television",
            "lighting" => "maintenance_lighting",
            "plumbing" => "maintenance_plumbing",
            "access/security" => "maintenance_access_security",
            "appliances" => "maintenance_appliances",
            _ => "maintenance_general"
        };

        var fallbackResponses = issueType.ToLower() switch
        {
            "wifi/internet" => "I understand you're having trouble with the WiFi connection. I've notified our technical team, and they'll check the connection shortly. In the meantime, you can also try restarting your device's WiFi connection.",
            "hvac" => "I'm sorry to hear there's an issue with the air conditioning/heating in your room. I've sent an alert to our maintenance team, and they'll come to fix this as soon as possible. We apologize for any discomfort.",
            "television" => "I've noted the issue with your TV. Our maintenance team will visit your room soon to resolve this for you. You might also try unplugging it for 30 seconds and plugging it back in.",
            "lighting" => "I understand you're having lighting issues. I've created a maintenance request, and someone will come to fix this shortly.",
            "plumbing" => "I've logged the plumbing issue and notified our maintenance team immediately. They'll prioritize fixing this for you. If it's urgent, please don't hesitate to call our front desk directly.",
            "access/security" => "I see you're having trouble with room access. I've alerted our security team, and they'll assist you shortly. If you're currently locked out, please visit the front desk for immediate help.",
            "appliances" => "I've noted the appliance issue and created a maintenance ticket. Our team will come to check and repair it as needed.",
            _ => "Thank you for reporting this issue. I've created a maintenance request, and our team has been notified. They'll attend to this matter as soon as possible. We apologize for any inconvenience."
        };

        var variables = new Dictionary<string, object>
        {
            ["issue_type"] = issueType
        };

        var baseResponse = fallbackResponses + "\n\nIs there anything else I can help you with in the meantime?";

        var processedTemplate = await _responseTemplateService.ProcessTemplateWithFallbackAsync(
            tenantId,
            templateKey,
            variables,
            baseResponse
        );

        return processedTemplate.Content;
    }

    private async Task<MessageRoutingResponse> CreateGreetingResponseAsync(TenantContext tenantContext, Conversation conversation, string guestMessage)
    {
        try
        {
            var tenantName = tenantContext.TenantName ?? "our hotel";

            // Step 1: Get guest information from booking table
            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);
            var guestName = guestStatus?.DisplayName;

            // Step 2: Get a random welcome message template from WelcomeMessages table
            var welcomeTemplates = await _context.WelcomeMessages
                .Where(w => w.TenantId == tenantContext.TenantId && w.MessageType == "greeting" && w.IsActive)
                .OrderBy(w => w.DisplayOrder)
                .ToListAsync();

            string baseTemplate;
            if (welcomeTemplates.Any())
            {
                // Select a random template
                var random = new Random();
                var selectedTemplate = welcomeTemplates[random.Next(welcomeTemplates.Count)];
                baseTemplate = selectedTemplate.Template;
            }
            else
            {
                // Fallback template if no welcome messages are configured
                baseTemplate = "Hello{guestName}! Welcome to {tenantName}! 🏨 I'm your virtual concierge and I'm here to make your stay absolutely wonderful. How can I assist you today?";
            }

            // Step 3: Personalize the template with guest name if available
            string personalizedGreeting;
            if (!string.IsNullOrEmpty(guestName))
            {
                // Replace {tenantName} placeholder
                personalizedGreeting = baseTemplate.Replace("{tenantName}", tenantName);

                // Add guest name personalization
                // If template has {guestName}, replace it; otherwise prepend to the greeting
                if (personalizedGreeting.Contains("{guestName}"))
                {
                    personalizedGreeting = personalizedGreeting.Replace("{guestName}", $" {guestName}");
                }
                else
                {
                    // Insert guest name after the first greeting word (Hello, Hi, etc.)
                    var greetingWords = new[] { "Hello", "Hi", "Good to see you", "Hi there" };
                    foreach (var word in greetingWords)
                    {
                        if (personalizedGreeting.StartsWith(word))
                        {
                            personalizedGreeting = personalizedGreeting.Replace(word, $"{word} {guestName}");
                            break;
                        }
                    }
                }
            }
            else
            {
                // No guest name available, just replace placeholders
                personalizedGreeting = baseTemplate.Replace("{tenantName}", tenantName).Replace("{guestName}", "");
            }

            // Step 4: Get complimentary services for this tenant and append to greeting
            var complimentaryServices = await _context.Services
                .Where(s => s.TenantId == tenantContext.TenantId && s.IsChargeable == false && s.IsAvailable == true)
                .OrderBy(s => s.Category)
                .ToListAsync();

            if (complimentaryServices.Any())
            {
                // Map categories to emoji icons
                var categoryEmojis = new Dictionary<string, string>
                {
                    { "Dining", "🍹" },
                    { "Business", "📶" },
                    { "Recreation", "🏊" },
                    { "Concierge", "🛎️" },
                    { "Accommodation", "🛏️" }
                };

                // Format complimentary services list
                var servicesList = new List<string>();
                foreach (var service in complimentaryServices.Take(5)) // Limit to top 5
                {
                    var emoji = categoryEmojis.ContainsKey(service.Category) ? categoryEmojis[service.Category] : "✨";
                    servicesList.Add($"{emoji} {service.Name}");
                }

                var servicesText = string.Join(", ", servicesList);
                if (complimentaryServices.Count > 5)
                {
                    servicesText += ", and more";
                }

                // Append complimentary services to greeting
                personalizedGreeting = personalizedGreeting.TrimEnd('.', '!', '?') +
                    $" During your stay, enjoy complimentary: {servicesText}.";
            }

            _logger.LogInformation("Greeting personalized for guest {GuestName} at tenant {TenantId}: {Greeting}",
                guestName ?? "Unknown", tenantContext.TenantId, personalizedGreeting);

            return new MessageRoutingResponse
            {
                Reply = personalizedGreeting
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating personalized greeting for tenant {TenantId}", tenantContext.TenantId);

            // Fallback to default English message on error
            var tenantName = tenantContext.TenantName ?? "our hotel";
            return new MessageRoutingResponse
            {
                Reply = $"Hello! Welcome to {tenantName}! 🏨 How can I assist you today?"
            };
        }
    }
    
    private int ExtractQuantityFromMessage(string message)
    {
        var messageLower = message.ToLower();

        // Look for numeric patterns first
        var numbers = System.Text.RegularExpressions.Regex.Matches(messageLower, @"\d+");
        if (numbers.Count > 0 && int.TryParse(numbers[0].Value, out int numericQuantity))
        {
            return Math.Max(numericQuantity, 1); // Return actual quantity (minimum 1)
        }

        // Look for word patterns
        var wordToNumber = new Dictionary<string, int>
        {
            { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 },
            { "six", 6 }, { "seven", 7 }, { "eight", 8 }, { "nine", 9 }, { "ten", 10 }
        };

        foreach (var kvp in wordToNumber)
        {
            if (messageLower.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        // Default to 1 if no quantity specified
        return 1;
    }

    /// <summary>
    /// Validates if a quantity is reasonable and returns validation message if needed
    /// </summary>
    private (bool isValid, string? confirmationMessage) ValidateQuantity(int quantity, string itemName)
    {
        // Reasonable quantity threshold
        const int REASONABLE_LIMIT = 5;
        const int MAXIMUM_LIMIT = 20;

        if (quantity > MAXIMUM_LIMIT)
        {
            return (false, $"The requested quantity ({quantity}) exceeds our maximum limit of {MAXIMUM_LIMIT} items per request. " +
                $"Please contact our front desk directly for bulk orders.");
        }

        if (quantity > REASONABLE_LIMIT)
        {
            return (false, $"You've requested {quantity} {itemName}(s), which is more than usual. " +
                $"Can you please confirm this quantity? Reply 'yes' to confirm or provide the correct number.");
        }

        return (true, null);
    }

    public async Task<GuestStatus> DetermineGuestStatusAsync(string phoneNumber, int tenantId)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantId);
            
            _logger.LogInformation("Determining guest status for phone: {PhoneNumber}, tenant: {TenantId}", phoneNumber, tenantId);
            
            // Staff override for testing (you can add specific staff numbers here)
            if (phoneNumber == "+27811234567" || phoneNumber == "whatsapp:+27811234567")
            {
                _logger.LogInformation("Guest Status: Staff override detected for {PhoneNumber}", phoneNumber);
                return new GuestStatus
                {
                    Type = GuestType.Staff,
                    PhoneNumber = phoneNumber,
                    DisplayName = "Staff User",
                    StatusMessage = "Staff override - full access for testing"
                };
            }

            // Clean and normalize phone number for database lookup
            var cleanPhone = NormalizePhoneNumber(phoneNumber);

            // Try multiple phone number variations to ensure we find the booking
            var phoneVariations = GetPhoneNumberVariations(cleanPhone);

            // Get all bookings for any phone number variation, ordered by relevance
            var bookings = await _context.Bookings
                .Where(b => phoneVariations.Contains(b.Phone))
                .OrderByDescending(b => b.CreatedAt) // Most recent booking first
                .ThenByDescending(b => b.CheckinDate) // Then by check-in date
                .ToListAsync();

            if (!bookings.Any())
            {
                _logger.LogInformation("Guest Status: Unregistered user - {PhoneNumber} (variations: {Variations}) not found in bookings",
                    cleanPhone, string.Join(", ", phoneVariations));
                return GuestStatus.CreateUnregistered(cleanPhone);
            }

            _logger.LogInformation("Guest Status: Found {BookingCount} booking(s) for {PhoneNumber} (matched: {MatchedPhone})",
                bookings.Count, cleanPhone, bookings.First().Phone);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var multipleBookings = bookings.Count > 1;

            // PRIORITY 1: Find active bookings (checked in and within stay dates)
            var activeBooking = bookings.FirstOrDefault(b =>
                IsActiveBookingStatus(b.Status) &&
                b.CheckinDate <= today &&
                b.CheckoutDate >= today);

            if (activeBooking != null)
            {
                _logger.LogInformation("Guest Status: Active guest found - Booking {BookingId}, Status: {Status}, Dates: {CheckinDate} to {CheckoutDate}, Room: {RoomNumber}",
                    activeBooking.Id, activeBooking.Status, activeBooking.CheckinDate, activeBooking.CheckoutDate, activeBooking.RoomNumber);

                var status = GuestStatus.CreateActive(cleanPhone, activeBooking);
                status.IsMultipleBookings = multipleBookings;
                return status;
            }

            // PRIORITY 2: Find same-day bookings that might not be checked in yet but should be treated as active
            var sameDayBooking = bookings.FirstOrDefault(b =>
                b.CheckinDate == today &&
                b.CheckoutDate >= today &&
                (IsActiveBookingStatus(b.Status) || IsConfirmedBookingStatus(b.Status)));

            if (sameDayBooking != null)
            {
                // If it's a same-day booking and confirmed/active, treat as active for better guest experience
                if (IsActiveBookingStatus(sameDayBooking.Status))
                {
                    _logger.LogInformation("Guest Status: Same-day active guest - Booking {BookingId}, Status: {Status}, Room: {RoomNumber}",
                        sameDayBooking.Id, sameDayBooking.Status, sameDayBooking.RoomNumber);

                    var status = GuestStatus.CreateActive(cleanPhone, sameDayBooking);
                    status.IsMultipleBookings = multipleBookings;
                    return status;
                }
                else
                {
                    _logger.LogInformation("Guest Status: Same-day pre-arrival guest - Booking {BookingId}, Status: {Status}, Room: {RoomNumber}",
                        sameDayBooking.Id, sameDayBooking.Status, sameDayBooking.RoomNumber);

                    var status = GuestStatus.CreatePreArrival(cleanPhone, sameDayBooking);
                    status.IsMultipleBookings = multipleBookings;
                    return status;
                }
            }

            // PRIORITY 3: Check for pre-arrival guests (confirmed but check-in is in the future)
            var preArrivalBooking = bookings
                .Where(b => IsConfirmedBookingStatus(b.Status) && b.CheckinDate > today)
                .OrderBy(b => b.CheckinDate) // Get the earliest upcoming booking
                .FirstOrDefault();

            if (preArrivalBooking != null)
            {
                _logger.LogInformation("Guest Status: Pre-arrival guest - Booking {BookingId}, Status: {Status}, checking in on {CheckinDate}, Room: {RoomNumber}",
                    preArrivalBooking.Id, preArrivalBooking.Status, preArrivalBooking.CheckinDate, preArrivalBooking.RoomNumber);

                var status = GuestStatus.CreatePreArrival(cleanPhone, preArrivalBooking);
                status.IsMultipleBookings = multipleBookings;
                return status;
            }

            // PRIORITY 4: Check for recent checkout (within extended grace period)
            var recentCheckout = bookings
                .Where(b => IsCheckedOutStatus(b.Status) || b.CheckoutDate < today)
                .Where(b => b.CheckoutDate >= today.AddDays(-2)) // 48-hour grace period
                .OrderByDescending(b => b.CheckoutDate) // Most recent checkout first
                .FirstOrDefault();

            if (recentCheckout != null)
            {
                var withinGracePeriod = recentCheckout.CheckoutDate >= today.AddDays(-2);
                _logger.LogInformation("Guest Status: Post-checkout guest - Booking {BookingId}, Status: {Status}, checked out {CheckoutDate}, grace period: {GracePeriod}, Room: {RoomNumber}",
                    recentCheckout.Id, recentCheckout.Status, recentCheckout.CheckoutDate, withinGracePeriod, recentCheckout.RoomNumber);

                var status = GuestStatus.CreatePostCheckout(cleanPhone, recentCheckout, withinGracePeriod);
                status.IsMultipleBookings = multipleBookings;
                return status;
            }

            // PRIORITY 5: Check for cancelled bookings (recent ones first)
            var cancelledBooking = bookings
                .Where(b => IsCancelledBookingStatus(b.Status))
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefault();

            if (cancelledBooking != null)
            {
                _logger.LogInformation("Guest Status: Cancelled booking - Booking {BookingId}, Status: {Status}, Room: {RoomNumber}",
                    cancelledBooking.Id, cancelledBooking.Status, cancelledBooking.RoomNumber);

                var status = GuestStatus.CreateCancelled(cleanPhone, cancelledBooking);
                status.IsMultipleBookings = multipleBookings;
                return status;
            }

            // PRIORITY 6: Handle past bookings (former guests)
            var pastBooking = bookings
                .Where(b => b.CheckoutDate < today && !IsCancelledBookingStatus(b.Status))
                .OrderByDescending(b => b.CheckoutDate) // Most recent past booking
                .FirstOrDefault();

            if (pastBooking != null)
            {
                _logger.LogInformation("Guest Status: Former guest with past booking - Booking {BookingId}, Status: {Status}, checked out {CheckoutDate}, Room: {RoomNumber}",
                    pastBooking.Id, pastBooking.Status, pastBooking.CheckoutDate, pastBooking.RoomNumber);

                var status = GuestStatus.CreatePostCheckout(cleanPhone, pastBooking, false);
                status.IsMultipleBookings = multipleBookings;
                return status;
            }

            // FALLBACK: Use the most recent booking regardless of status
            var latestBooking = bookings.First();
            _logger.LogInformation("Guest Status: Fallback to latest booking - Booking {BookingId}, Status: {Status}, Dates: {CheckinDate} to {CheckoutDate}, Room: {RoomNumber}",
                latestBooking.Id, latestBooking.Status, latestBooking.CheckinDate, latestBooking.CheckoutDate, latestBooking.RoomNumber);

            // Determine appropriate status based on dates and status
            if (latestBooking.CheckinDate <= today && latestBooking.CheckoutDate >= today && !IsCancelledBookingStatus(latestBooking.Status))
            {
                // Should be active or pre-arrival based on status
                if (IsActiveBookingStatus(latestBooking.Status))
                {
                    var status = GuestStatus.CreateActive(cleanPhone, latestBooking);
                    status.IsMultipleBookings = multipleBookings;
                    return status;
                }
                else
                {
                    var status = GuestStatus.CreatePreArrival(cleanPhone, latestBooking);
                    status.IsMultipleBookings = multipleBookings;
                    return status;
                }
            }
            else
            {
                // Default to post-checkout
                var defaultStatus = GuestStatus.CreatePostCheckout(cleanPhone, latestBooking, false);
                defaultStatus.IsMultipleBookings = multipleBookings;
                return defaultStatus;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining guest status for {PhoneNumber}", phoneNumber);
            // Return unregistered as safe fallback
            return GuestStatus.CreateUnregistered(phoneNumber);
        }
    }
    
    private string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber;

        // Remove whatsapp: prefix and trim whitespace
        var normalized = phoneNumber.Replace("whatsapp:", "").Trim();

        // Remove any spaces, dashes, parentheses within the number
        normalized = normalized.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        // Ensure consistent + prefix format
        if (normalized.StartsWith("27") && !normalized.StartsWith("+27"))
        {
            normalized = "+" + normalized;
        }
        else if (!normalized.StartsWith("+") && normalized.Length >= 10)
        {
            // Handle cases where number doesn't have country code
            // For South African numbers, add +27 if it looks like a local number
            if (normalized.Length == 10 && normalized.StartsWith("0"))
            {
                // Convert 0XX XXX XXXX to +27XX XXX XXXX
                normalized = "+27" + normalized.Substring(1);
            }
            else if (normalized.Length == 9 && !normalized.StartsWith("0"))
            {
                // Handle cases like 123456789 -> +27123456789
                normalized = "+27" + normalized;
            }
        }

        _logger.LogDebug("Phone number normalized from '{Original}' to '{Normalized}'", phoneNumber, normalized);
        return normalized;
    }

    private List<string> GetPhoneNumberVariations(string phoneNumber)
    {
        var variations = new List<string> { phoneNumber };

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return variations;

        // Add whatsapp: prefix variation
        if (!phoneNumber.StartsWith("whatsapp:"))
            variations.Add("whatsapp:" + phoneNumber);

        // Add/remove + prefix variations
        if (phoneNumber.StartsWith("+"))
        {
            variations.Add(phoneNumber.Substring(1));
        }
        else if (phoneNumber.StartsWith("27") && phoneNumber.Length >= 11)
        {
            variations.Add("+" + phoneNumber);
        }

        // Handle South African number format variations
        if (phoneNumber.StartsWith("+27") && phoneNumber.Length == 12)
        {
            // +27123456789 -> 0123456789
            variations.Add("0" + phoneNumber.Substring(3));
            // +27123456789 -> 27123456789
            variations.Add(phoneNumber.Substring(1));
        }

        return variations.Distinct().ToList();
    }

    private bool IsActiveBookingStatus(string status)
    {
        var activeStatuses = new[] { "CheckedIn" };
        return activeStatuses.Any(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsConfirmedBookingStatus(string status)
    {
        var confirmedStatuses = new[] { "Confirmed", "Reserved", "Booked", "Guaranteed" };
        return confirmedStatuses.Any(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsCheckedOutStatus(string status)
    {
        var checkedOutStatuses = new[] { "CheckedOut", "Departed", "Closed", "Completed" };
        return checkedOutStatuses.Any(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsCancelledBookingStatus(string status)
    {
        var cancelledStatuses = new[] { "Cancelled", "Canceled", "NoShow", "Voided" };
        return cancelledStatuses.Any(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
    }

    private async Task UpdateContextAfterResponse(int conversationId, MessageRoutingResponse response, string currentTopic)
    {
        try
        {
            string action = "general_response";
            string newTopic = currentTopic;

            // Determine action and topic based on response characteristics
            if (response.Action != null)
            {
                var actionStr = response.Action.ToString();
                if (actionStr?.Contains("create_task") == true)
                {
                    action = "task_created";
                    newTopic = "task_management";
                }
                else if (actionStr?.Contains("menu") == true)
                {
                    action = "menu_provided";
                    newTopic = "food_service";
                }
            }

            // Infer topic from response content
            if (response.Reply != null)
            {
                var replyLower = response.Reply.ToLower();
                if (replyLower.Contains("towel") || replyLower.Contains("clean") || replyLower.Contains("housekeeping"))
                    newTopic = "housekeeping";
                else if (replyLower.Contains("wifi") || replyLower.Contains("internet"))
                    newTopic = "technical_support";
                else if (replyLower.Contains("menu") || replyLower.Contains("food") || replyLower.Contains("order"))
                    newTopic = "food_service";
                else if (replyLower.Contains("emergency") || replyLower.Contains("urgent"))
                    newTopic = "emergency";
            }

            await _smartContextManagerService.UpdateContextAsync(conversationId, newTopic, action);

            // Store relevant context variables
            if (response.Action != null)
            {
                await _smartContextManagerService.StoreContextVariableAsync(conversationId, "last_action", response.Action);
            }

            _logger.LogDebug("Updated context for conversation {ConversationId}: Topic={Topic}, Action={Action}",
                conversationId, newTopic, action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating context for conversation {ConversationId}", conversationId);
        }
    }
    
    private async Task<bool> IsItemRequestAsync(
        string messageText,
        TenantContext tenantContext,
        int conversationId,
        GuestStatus guestStatus)
    {
        try
        {
            _logger.LogInformation("🤖 LLM ACCESS CONTROL - Analyzing message for item request: '{Message}'", messageText);

            // Create focused prompt for access control
            var prompt = BuildItemRequestAnalysisPrompt(messageText, tenantContext, guestStatus);

            // Use structured response for reliable binary classification with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _openAIService.GetStructuredResponseAsync<ItemRequestAnalysisResult>(
                prompt,
                temperature: 0.1 // Very low temperature for consistent binary decisions
            );

            if (response == null)
            {
                _logger.LogWarning("🤖 LLM ACCESS CONTROL - LLM response was null, defaulting to allow access (fail-safe)");
                return false;
            }

            var isItemRequest = response.IsItemRequest && response.Confidence >= 0.7;

            _logger.LogInformation("🤖 LLM ACCESS CONTROL - Analysis complete. IsItemRequest: {IsItemRequest}, " +
                "Confidence: {Confidence}, Reasoning: {Reasoning}",
                response.IsItemRequest, response.Confidence, response.Reasoning);

            return isItemRequest;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("🤖 LLM ACCESS CONTROL - Request timed out, defaulting to allow access (fail-safe)");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🤖 LLM ACCESS CONTROL - Error analyzing item request, defaulting to allow access (fail-safe)");
            return false;
        }
    }

    private string BuildItemRequestAnalysisPrompt(string messageText, TenantContext tenantContext, GuestStatus guestStatus)
    {
        return $@"You are an AI assistant for hotel guest access control. Analyze if this message is requesting a physical item, food, beverage, service delivery, or bookable service.

GUEST MESSAGE: ""{messageText}""

CONTEXT:
- Hotel: {tenantContext.TenantName}
- Guest Status: {guestStatus.Type}

CLASSIFICATION RULES:
✅ ITEM/SERVICE REQUESTS (return true):
- Physical items: ""I need towels"", ""Can I get soap?"", ""Send me an iron""
- Food/beverages: ""I want wine"", ""I need coffee"", ""Can I get water?""
- Room service: ""I'd like room service"", ""Deliver food to my room""
- Housekeeping items: ""I need more toilet paper"", ""Bring me pillows""
- Bookable services: ""I want a safari"", ""Book me a spa treatment"", ""I need a massage"", ""Arrange a tour""
- Activities/experiences: ""I want to go on a tour"", ""Book a trip"", ""Schedule a treatment""
- Transportation: ""I need a transfer"", ""Book a shuttle"", ""Arrange a taxi""

❌ NOT ITEM/SERVICE REQUESTS (return false):
- Questions/inquiries: ""What wines do you have?"", ""Do you serve breakfast?"", ""What tours are available?""
- Information requests: ""Tell me about your menu"", ""What are your hours?"", ""Tell me about safaris""
- Complaints without requests: ""My room is cold"", ""The TV isn't working""
- General conversation: ""Thank you"", ""Hello"", ""How are you?""

Respond with this exact JSON format:
{{
    ""isItemRequest"": true/false,
    ""confidence"": 0.0-1.0,
    ""reasoning"": ""Brief explanation of classification""
}}

CRITICAL: When a guest says ""I want X"" or ""I need X"" or ""Book me X"", where X is ANY service or item offered by the hotel, return true.";
    }

    private async Task<QuantityClarificationAnalysis?> AnalyzeQuantityClarificationAsync(
        string messageText,
        string lastBotMessage,
        List<(string Role, string Content)> conversationHistory)
    {
        try
        {
            _logger.LogInformation("🤖 LLM QUANTITY CLARIFICATION - Analyzing message: '{Message}', Last bot message: '{LastBotMessage}'",
                messageText, lastBotMessage);

            // Build prompt with conversation context
            var prompt = BuildQuantityClarificationPrompt(messageText, lastBotMessage, conversationHistory);

            // Use structured response with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _openAIService.GetStructuredResponseAsync<QuantityClarificationAnalysis>(
                prompt,
                temperature: 0.2 // Low temperature for consistent detection
            );

            if (response == null)
            {
                _logger.LogWarning("🤖 LLM QUANTITY CLARIFICATION - LLM response was null");
                return null;
            }

            _logger.LogInformation("🤖 LLM QUANTITY CLARIFICATION - Analysis complete. " +
                "IsQuantityClarification: {IsClarification}, IsNewRequest: {IsNewRequest}, " +
                "IsCancellation: {IsCancellation}, IsAmbiguous: {IsAmbiguous}, " +
                "ItemSlug: {ItemSlug}, ItemName: {ItemName}, Quantity: {Quantity}, " +
                "Confidence: {Confidence}, Reasoning: {Reasoning}",
                response.IsQuantityClarification, response.IsNewRequest, response.IsCancellation,
                response.IsAmbiguous, response.ItemSlug, response.ItemName, response.Quantity,
                response.Confidence, response.Reasoning);

            return response;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("🤖 LLM QUANTITY CLARIFICATION - Request timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🤖 LLM QUANTITY CLARIFICATION - Error analyzing quantity clarification");
            return null;
        }
    }

    private string BuildQuantityClarificationPrompt(
        string messageText,
        string lastBotMessage,
        List<(string Role, string Content)> conversationHistory)
    {
        // Get last 4 messages for context (2 exchanges)
        var recentHistory = conversationHistory.TakeLast(4).ToList();
        var historyText = string.Join("\n", recentHistory.Select(m => $"{m.Role}: {m.Content}"));

        return $@"You are an AI assistant analyzing hotel guest conversations to detect quantity clarifications.

CURRENT USER MESSAGE: ""{messageText}""
LAST BOT MESSAGE: ""{lastBotMessage}""

RECENT CONVERSATION:
{historyText}

YOUR TASK:
Determine if the user's current message is clarifying the quantity of an item from a previous request, or if it's something else entirely.

QUANTITY CLARIFICATION INDICATORS (IsQuantityClarification = true):
✅ User previously requested an item, and bot confirmed/acknowledged it
✅ Current message contains a number
✅ Message is short and focused on quantity (""2"", ""I need 2"", ""3 please"", ""make it 5"")
✅ Bot message mentions item delivery, confirmation, or acknowledgment
✅ NO new item is mentioned in current message

EXAMPLES OF QUANTITY CLARIFICATIONS:
- Bot: ""I'll have towels delivered to room 202"" → User: ""I need 2"" ✅
- Bot: ""I'll send fresh towels right away"" → User: ""3 please"" ✅
- Bot: ""Perfect! I'll arrange for towels"" → User: ""make it 4"" ✅
- Bot: ""I can help with that"" → User: ""just 2"" ✅

NOT QUANTITY CLARIFICATIONS (IsNewRequest = true):
❌ Bot asked ""How many towels?"" → User: ""2"" (This is answering a direct question, not a clarification)
❌ Bot: ""Anything else?"" → User: ""I need 2 towels"" (New request, not clarification)
❌ User mentions a different item: ""Actually, I need pillows instead""
❌ Message contains non-quantity numbers: ""Room 202"", ""At 2 PM"", ""Call extension 2""
❌ More than 10 seconds between messages (timestamp decay - but you won't have timestamps, so ignore if context seems fresh)

CANCELLATIONS (IsCancellation = true):
❌ ""No, I don't need them""
❌ ""Cancel that""
❌ ""Never mind""
❌ ""I changed my mind""

AMBIGUOUS CASES (IsAmbiguous = true):
⚠️ Bot mentioned multiple items, unclear which one user is referring to
⚠️ Fuzzy quantities without clear numbers (""a couple"", ""a few"") - still mark as clarification but note ambiguity
⚠️ Modification phrases without clear quantity (""more"", ""less"")

ITEM SLUG MAPPING (use these exact slugs):
- Towels/towel → ""towels""
- Toilet paper → ""toilet-paper""
- Pillows/pillow → ""pillows""
- Blankets/blanket → ""blankets""
- Water/bottled water → ""water""
- Wine → ""wine""
- Coffee → ""coffee""
- Soap/toiletries → ""toiletries""
- Iron → ""iron""
- Hairdryer/hair dryer → ""hairdryer""

QUANTITY EXTRACTION RULES:
- Direct numbers: ""2"", ""3"", ""five"" → extract as integer
- Modification words: ""make it 3"", ""change to 5"" → extract the number
- Fuzzy quantities: ""a couple"" → 2, ""a few"" → 3, ""several"" → 4
- Implicit single: ""just one"", ""a towel"" → 1

Respond with this exact JSON format:
{{
    ""isQuantityClarification"": true/false,
    ""isNewRequest"": true/false,
    ""isCancellation"": true/false,
    ""isAmbiguous"": true/false,
    ""itemSlug"": ""slug-name"" or null,
    ""itemName"": ""Item Name"" or null,
    ""quantity"": integer or null,
    ""possibleItems"": [""item1"", ""item2""] or null (only if ambiguous multi-item),
    ""clarificationNeeded"": ""What to ask user"" or null (only if ambiguous),
    ""confidence"": 0.0-1.0,
    ""reasoning"": ""Brief explanation of analysis""
}}

CRITICAL RULES:
1. Only set isQuantityClarification=true if user is clearly updating the quantity of an item just mentioned/confirmed by the bot
2. Set isNewRequest=true if this is a fresh request, not a clarification
3. Always extract itemSlug and quantity if it's a valid clarification
4. Use confidence >= 0.7 for clear cases, < 0.7 for uncertain cases
5. If bot explicitly asked ""how many"", it's NOT a clarification - it's answering a question (isNewRequest=true)";
    }

    private bool IsItemRequest(string messageText)
    {
        var messageLower = messageText.ToLower();

        // Common item request patterns - specific to physical items
        var itemRequestKeywords = new[]
        {
            // Direct requests for items (with articles)
            "i need a", "i need an", "can i get a", "can i get an", "send me a", "send me an",
            "could you send me a", "could i have a", "may i have a", "please send a", "please bring a",

            // Direct requests for items (without articles)
            "i need", "i want", "can i get", "send me", "bring me", "get me", "could i have",
            "may i have", "please send", "please bring", "i would like", "i'd like",

            // Specific room items that are commonly requested
            "towel", "towels", "iron", "blanket", "pillow", "charger", "hair dryer",
            "straightener", "curling iron", "heater", "fan", "umbrella", "toiletries",
            "toothbrush", "shampoo", "soap", "tissue", "toilet paper", "extra towel", "more towels",

            // Food and beverage items
            "wine", "beer", "coffee", "tea", "water", "juice", "soda", "champagne",
            "sandwich", "snack", "food", "meal", "drink", "beverage", "cocktail",

            // Action words that indicate requests
            "arrange", "organize", "fetch", "obtain", "provide", "deliver", "order",

            // Context clues for requests
            "to my room", "room service", "housekeeping", "maintenance"
        };

        // First check if it contains any of the keywords
        var containsKeywords = itemRequestKeywords.Any(keyword => messageLower.Contains(keyword));

        if (!containsKeywords) return false;

        // Additional validation: if it starts with "i need" without article,
        // check if it's followed by an item-like word
        if (messageLower.StartsWith("i need ") && !messageLower.StartsWith("i need a ") && !messageLower.StartsWith("i need an "))
        {
            var afterNeed = messageLower.Substring(7).Trim(); // Remove "i need "
            // Check if it's a single word or short phrase (likely an item)
            return afterNeed.Split(' ').Length <= 3 && !string.IsNullOrEmpty(afterNeed);
        }

        return containsKeywords;
    }

    private bool IsContactRequest(string messageText)
    {
        var messageLower = messageText.ToLower();

        // Keywords that indicate someone wants to contact specific staff/departments
        var contactRequestKeywords = new[]
        {
            // Direct contact requests
            "speak to", "talk to", "contact", "call", "reach", "get in touch",
            "connect me", "transfer me", "put me through", "i need to speak",
            "i want to talk", "i need to contact", "can you call", "please call",

            // Specific roles/departments people might ask for
            "manager", "front desk", "reception", "concierge", "security",
            "housekeeping", "maintenance", "supervisor", "duty manager",
            "hotel manager", "shift manager", "guest services", "customer service",
            "bell hop", "porter", "valet", "doorman", "chef", "restaurant manager",

            // Contact context clues
            "phone number", "extension", "direct line", "contact details",
            "how do i reach", "who should i call", "what number", "which department"
        };

        // Also check for patterns like "I need the manager" or "Get me security"
        var contactPatterns = new[]
        {
            "i need the", "get me the", "find me the", "where is the",
            "who is the", "is there a", "do you have a"
        };

        return contactRequestKeywords.Any(keyword => messageLower.Contains(keyword)) ||
               contactPatterns.Any(pattern => messageLower.Contains(pattern) &&
                   (messageLower.Contains("manager") || messageLower.Contains("security") ||
                    messageLower.Contains("supervisor") || messageLower.Contains("reception")));
    }

    private string GenerateBlockedRequestResponse(GuestStatus guestStatus, string messageText)
    {
        var response = new System.Text.StringBuilder();

        // Be more helpful based on what they're actually trying to do
        var messageLower = messageText.ToLower();

        // Check if this is just an inquiry (which should generally be allowed)
        if (messageLower.Contains("what") || messageLower.Contains("when") || messageLower.Contains("where") ||
            messageLower.Contains("how much") || messageLower.Contains("do you have"))
        {
            // This shouldn't be blocked - return null to process normally
            return string.Empty;
        }

        switch (guestStatus.Type)
        {
            case GuestType.PostCheckout:
                if (guestStatus.IsWithinGracePeriod)
                {
                    response.AppendLine("Thank you for your request. While I cannot arrange room deliveries after checkout, I'm happy to help with:");
                    response.AppendLine("• Lost item inquiries");
                    response.AppendLine("• Feedback about your stay");
                    response.AppendLine("• General hotel information");
                    response.AppendLine("\nHow else can I assist you today?");
                }
                else
                {
                    response.AppendLine("Welcome back! While I cannot process room requests for past stays, I can help you with:");
                    response.AppendLine("• Information about our services and amenities");
                    response.AppendLine("• Making a new reservation");
                    response.AppendLine("• General inquiries");
                    response.AppendLine("\nWhat would you like to know?");
                }
                break;

            case GuestType.PreArrival:
                response.AppendLine($"Great to hear from you! You're checking in on {guestStatus.CheckinDate}.");
                response.AppendLine("While I can't deliver items before check-in, I can:");
                response.AppendLine("• Note your preferences for arrival");
                response.AppendLine("• Provide information about our services");
                response.AppendLine("• Answer questions about your upcoming stay");
                response.AppendLine("\nHow can I help prepare for your arrival?");
                break;

            case GuestType.Cancelled:
                // For cancelled guests, be helpful about general inquiries
                if (messageLower.Contains("water") || messageLower.Contains("drink") || messageLower.Contains("food"))
                {
                    response.AppendLine("I can provide information about our dining options and menu:");
                    response.AppendLine("• Restaurant hours and reservations");
                    response.AppendLine("• Menu items and prices");
                    response.AppendLine("• Special dietary options");
                    response.AppendLine("\nWould you like to make a new reservation to enjoy our services?");
                }
                else
                {
                    response.AppendLine("I see you had a cancelled reservation. I can help you with:");
                    response.AppendLine("• Making a new booking");
                    response.AppendLine("• Information about our services");
                    response.AppendLine("• General inquiries");
                    response.AppendLine("\nHow can I assist you today?");
                }
                break;

            case GuestType.Unregistered:
                response.AppendLine("Welcome! I'd be happy to help you with:");
                response.AppendLine("• Viewing our menu and services");
                response.AppendLine("• Hotel information and amenities");
                response.AppendLine("• Making a reservation");
                response.AppendLine("\nTo access room service, you'll need an active booking. How can I help you today?");
                break;

            default:
                response.AppendLine("I'd be happy to help! Could you please let me know how I can assist you?");
                response.AppendLine("Please contact our front desk directly for immediate assistance.");
                break;
        }
        
        return response.ToString().TrimEnd();
    }

    private bool IsLaundryServiceRequest(string messageText)
    {
        var messageLower = messageText.ToLower();
        
        // Laundry service request patterns
        var laundryKeywords = new[]
        {
            "laundry service", "laundry", "wash my clothes", "dry cleaning",
            "clothes cleaned", "need washing", "clothing service", "wash clothes",
            "dirty clothes", "clean my clothes", "pickup laundry", "laundry pickup"
        };
        
        return laundryKeywords.Any(keyword => messageLower.Contains(keyword));
    }

    private bool IsHousekeepingServiceRequest(string messageText)
    {
        var messageLower = messageText.ToLower();
        
        // Housekeeping service request patterns - specific to cleaning services
        var housekeepingKeywords = new[]
        {
            "housekeeping", "housekeeping service", "maid service", "room cleaning",
            "clean my room", "clean the room", "daily cleaning", "tidy my room", 
            "tidy up my room", "tidy the room", "room cleaned", "need room cleaning",
            "cleaning service", "i need cleaning", "need housekeeping"
        };
        
        return housekeepingKeywords.Any(keyword => messageLower.Contains(keyword));
    }

    private bool IsFoodDeliveryRequest(string messageText)
    {
        var messageLower = messageText.ToLower();
        
        // Food delivery request patterns - more specific to avoid conflicts
        var foodDeliveryKeywords = new[]
        {
            "food delivery", "order food", "food service", "meal delivery", "dining service",
            "deliver food to", "bring food to", "food to my room", "order some food", 
            "want to order food", "order a meal", "food menu", "restaurant menu",
            "hungry", "something to eat", "want something to eat", "i want food"
        };
        
        return foodDeliveryKeywords.Any(keyword => messageLower.Contains(keyword));
    }

    private bool IsMaintenanceRequest(string messageText)
    {
        var messageLower = messageText.ToLower();
        
        // Maintenance request patterns
        var maintenanceKeywords = new[]
        {
            "maintenance", "repair", "fix", "broken", "not working",
            "problem with", "issue with", "faulty", "defective",
            "out of order", "needs fixing", "maintenance request",
            "something wrong", "malfunction", "damaged"
        };
        
        return maintenanceKeywords.Any(keyword => messageLower.Contains(keyword));
    }

    private bool IsRoomServiceRequest(string messageText)
    {
        var messageLower = messageText.ToLower();
        
        // General room service request patterns
        var roomServiceKeywords = new[]
        {
            "room service", "need room service", "i need room service"
        };
        
        return roomServiceKeywords.Any(keyword => messageLower.Contains(keyword));
    }

    // Hybrid Message Intent Classification Methods
    
    private async Task<MessageIntentResult> ClassifyMessageIntentAsync(string message, List<(string Role, string Content)> conversationHistory)
    {
        // Check if LLM is disabled and use regex-only mode
        if (_classificationOptions.Mode == ClassificationMode.RegexOnly)
        {
            var regexOnlyResult = ClassifyIntentWithRegex(message, conversationHistory);
            if (_classificationOptions.EnableClassificationLogging)
            {
                _logger.LogInformation("MessageRoutingService: Classification mode RegexOnly - Intent: {Intent}, Confidence: {Confidence}",
                    regexOnlyResult.Intent, regexOnlyResult.Confidence);
            }
            return regexOnlyResult;
        }
        
        // LLM-only mode
        if (_classificationOptions.Mode == ClassificationMode.LLMOnly)
        {
            if (_classificationOptions.IsLLMEnabled)
            {
                try
                {
                    var llmOnlyResult = await ClassifyIntentWithLLMAsync(message, conversationHistory);
                    if (_classificationOptions.EnableClassificationLogging)
                    {
                        _logger.LogInformation("MessageRoutingService: Classification mode LLMOnly - Intent: {Intent}, Confidence: {Confidence}",
                            llmOnlyResult.Intent, llmOnlyResult.Confidence);
                    }
                    return llmOnlyResult;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LLM classification failed for message: {Message}, falling back to regex", message);
                    return ClassifyIntentWithRegex(message, conversationHistory);
                }
            }
            else
            {
                _logger.LogWarning("LLM classification requested but disabled in configuration, falling back to regex");
                return ClassifyIntentWithRegex(message, conversationHistory);
            }
        }
        
        // Hybrid mode (default)
        // Step 1: Fast path for obvious cases using regex
        var fastResult = ClassifyIntentWithRegex(message, conversationHistory);
        if (fastResult.Confidence > _classificationOptions.RegexConfidenceThreshold)
        {
            if (_classificationOptions.EnableClassificationLogging)
            {
                _logger.LogInformation("MessageRoutingService: Hybrid classification - high-confidence regex result - Intent: {Intent}, Confidence: {Confidence}",
                    fastResult.Intent, fastResult.Confidence);
            }
            return fastResult;
        }
        
        // Step 2: Use LLM for ambiguous cases if enabled
        if (_classificationOptions.IsLLMEnabled && IsAmbiguousContext(message, conversationHistory))
        {
            try
            {
                var llmResult = await ClassifyIntentWithLLMAsync(message, conversationHistory);
                if (llmResult.Confidence > _classificationOptions.LLMConfidenceThreshold)
                {
                    if (_classificationOptions.EnableClassificationLogging)
                    {
                        _logger.LogInformation("MessageRoutingService: Hybrid classification - LLM result - Intent: {Intent}, Confidence: {Confidence}",
                            llmResult.Intent, llmResult.Confidence);
                    }
                    return llmResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM classification failed for message: {Message}", message);
            }
        }
        
        // Step 3: Fall back to regex result
        if (_classificationOptions.EnableClassificationLogging)
        {
            _logger.LogInformation("MessageRoutingService: Hybrid classification - fallback to regex - Intent: {Intent}, Confidence: {Confidence}",
                fastResult.Intent, fastResult.Confidence);
        }
        return fastResult;
    }

    private MessageIntentResult ClassifyIntentWithRegex(string message, List<(string Role, string Content)> conversationHistory)
    {
        var messageLower = message.Trim().ToLower();
        var assistantMessage = conversationHistory.LastOrDefault(m => m.Role == "assistant");
        var lastBotMessage = assistantMessage.Role != null ? assistantMessage.Content : "";
        
        // Fast path for very short greetings (high confidence)
        var simpleGreetings = new[] { "hi", "hello", "hey", "yo" };
        if (messageLower.Length <= 5 && simpleGreetings.Contains(messageLower))
        {
            return new MessageIntentResult 
            { 
                Intent = MessageIntent.Greeting, 
                Confidence = 0.95, 
                Source = "regex",
                Reasoning = "Simple greeting word"
            };
        }
        
        // Timing response detection (high confidence if context matches)
        if (IsTimeResponse(message))
        {
            bool hasServiceContext = lastBotMessage.ToLower().Contains("what time") || 
                                   lastBotMessage.ToLower().Contains("when would") ||
                                   ContainsLaundryQuestion(lastBotMessage) ||
                                   ContainsHousekeepingQuestion(lastBotMessage) ||
                                   ContainsFoodQuestion(lastBotMessage) ||
                                   ContainsMaintenanceQuestion(lastBotMessage);
                                   
            if (hasServiceContext)
            {
                return new MessageIntentResult 
                { 
                    Intent = MessageIntent.TimingResponse, 
                    Confidence = 0.9, 
                    Source = "regex",
                    Reasoning = "Time response with service context"
                };
            }
        }
        
        // Service request detection
        if (IsRoomServiceRequest(message) || IsMaintenanceRequest(message))
        {
            return new MessageIntentResult 
            { 
                Intent = MessageIntent.ServiceRequest, 
                Confidence = 0.85, 
                Source = "regex",
                Reasoning = "Service request keywords detected"
            };
        }
        
        // Greeting detection (lower confidence for complex cases)
        var greetingKeywords = new[]
        {
            "hi", "hello", "hey", "good morning", "good afternoon", "good evening",
            "greetings", "howdy", "yo", "hiya", "good day", "hi there", "hello there"
        };
        
        var isGreeting = greetingKeywords.Any(greeting => 
            messageLower == greeting || 
            messageLower.StartsWith(greeting + " ") ||
            messageLower.StartsWith(greeting + "!") ||
            messageLower.StartsWith(greeting + ".") ||
            messageLower.StartsWith(greeting + ","));
            
        if (isGreeting)
        {
            return new MessageIntentResult 
            { 
                Intent = MessageIntent.Greeting, 
                Confidence = 0.7, 
                Source = "regex",
                Reasoning = "Greeting keywords detected"
            };
        }
        
        // Default to Other with low confidence
        return new MessageIntentResult 
        { 
            Intent = MessageIntent.Other, 
            Confidence = 0.3, 
            Source = "regex",
            Reasoning = "No clear pattern matched"
        };
    }
    
    private async Task<MessageIntentResult> ClassifyIntentWithLLMAsync(string message, List<(string Role, string Content)> conversationHistory)
    {
        var assistantMessage = conversationHistory.LastOrDefault(m => m.Role == "assistant");
        var lastBotMessage = assistantMessage.Role != null ? assistantMessage.Content : "";
        
        var prompt = $@"Analyze this conversation context and classify the user's message intent.

Last bot message: ""{lastBotMessage}""
User message: ""{message}""

Context: This is a hotel chatbot conversation. The bot may have asked for timing information for services like laundry, housekeeping, food delivery, or maintenance.

Classify the user message as ONE of:
1. TIMING_RESPONSE - User is providing timing/scheduling information (like ""afternoon around 13:00"", ""in the morning"", ""later today"")
2. GREETING - User is greeting the bot (""hi"", ""hello"", ""good morning"")
3. SERVICE_REQUEST - User is requesting a hotel service
4. MAINTENANCE_ISSUE - User is reporting something broken or needs fixing
5. MENU_REQUEST - User is asking to see the menu, food options, or dining information (like ""show me the menu"", ""let's see your menu"", ""what food do you have"", ""see menu"", ""menu please"", ""dining options"")
6. OTHER - Anything else

Important: If the bot previously asked ""What time..."" or ""When would..."" then timing-related words in the user message likely indicate TIMING_RESPONSE, not GREETING.

Return only the classification word (TIMING_RESPONSE, GREETING, SERVICE_REQUEST, MAINTENANCE_ISSUE, MENU_REQUEST, or OTHER).";

        try
        {
            var openAiResponse = await _openAIService.GenerateResponseAsync(
                "You are a classifier that returns only classification words.", "", "", prompt, "");
            if (openAiResponse == null) 
            {
                return new MessageIntentResult { Intent = MessageIntent.Other, Confidence = 0.1, Source = "llm" };
            }
            var classification = openAiResponse.Reply.Trim().ToUpper();
            
            var intent = classification switch
            {
                "TIMING_RESPONSE" => MessageIntent.TimingResponse,
                "GREETING" => MessageIntent.Greeting,
                "SERVICE_REQUEST" => MessageIntent.ServiceRequest,
                "MAINTENANCE_ISSUE" => MessageIntent.MaintenanceIssue,
                "MENU_REQUEST" => MessageIntent.MenuRequest,
                _ => MessageIntent.Other
            };
            
            return new MessageIntentResult
            {
                Intent = intent,
                Confidence = 0.85,
                Source = "llm",
                Reasoning = $"LLM classified as: {classification}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM classification");
            return new MessageIntentResult
            {
                Intent = MessageIntent.Other,
                Confidence = 0.1,
                Source = "llm-error",
                Reasoning = "LLM classification failed"
            };
        }
    }
    
    private bool IsAmbiguousContext(string message, List<(string Role, string Content)> conversationHistory)
    {
        var messageLower = message.ToLower();
        var assistantMessage = conversationHistory.LastOrDefault(m => m.Role == "assistant");
        var lastBotMessage = assistantMessage.Role != null ? assistantMessage.Content.ToLower() : "";
        
        // Ambiguous if message contains timing words but could be greeting
        var timingWords = new[] { "morning", "afternoon", "evening" };
        var hasTimingWords = timingWords.Any(word => messageLower.Contains(word));
        
        // Ambiguous if bot asked for timing but user response could be misinterpreted
        var botAskedForTiming = lastBotMessage.Contains("what time") || lastBotMessage.Contains("when would");
        
        // This is exactly the problematic case: "afternoon around 13:00" after timing request
        return hasTimingWords || (botAskedForTiming && message.Length > 5);
    }

    private async Task ProcessExtractedActions(TenantContext tenantContext, Conversation conversation, MessageRoutingResponse routingResponse)
    {
        try
        {
            var actionsToProcess = new List<JsonElement>();

            if (routingResponse.Action.HasValue)
            {
                actionsToProcess.Add(routingResponse.Action.Value);
            }

            if (routingResponse.Actions != null && routingResponse.Actions.Any())
            {
                actionsToProcess.AddRange(routingResponse.Actions);
            }

            // Always check fallback scenarios for hotel items, even if there are other actions
            // This ensures multi-intent messages (e.g., "I need towels and what time is breakfast?") are fully handled
            var fallbackHandled = await TryFallbackDetection(tenantContext, conversation, routingResponse);

            if (!actionsToProcess.Any())
            {
                _logger.LogInformation("No actions extracted for conversation {ConversationId}. Fallback detection handled: {FallbackHandled}",
                    conversation.Id, fallbackHandled);
                return;
            }

            _logger.LogInformation("Processing {ActionCount} extracted actions for conversation {ConversationId}",
                actionsToProcess.Count, conversation.Id);

            var processedActionStrings = new HashSet<string>();

            foreach (var action in actionsToProcess)
            {
                var actionString = action.GetRawText();
                if (processedActionStrings.Add(actionString))
                {
                    await ProcessSingleAction(tenantContext, conversation, action);
                }
                else
                {
                    _logger.LogInformation("Skipping duplicate action: {Action}", actionString);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing extracted actions for conversation {ConversationId}", conversation.Id);
        }
    }

    private async Task<bool> TryFallbackDetection(TenantContext tenantContext, Conversation conversation, MessageRoutingResponse routingResponse)
    {
        try
        {
            _logger.LogInformation("Attempting fallback detection for conversation {ConversationId}", conversation.Id);

            var messageText = await GetLatestUserMessage(conversation);
            if (string.IsNullOrEmpty(messageText))
            {
                _logger.LogWarning("Could not retrieve latest user message for fallback detection");
                return false;
            }

            _logger.LogInformation("Analyzing message for fallback detection: '{Message}'", messageText);

            var fallbackCreated = false;

            // Try food order fallback detection
            if (await TryFallbackFoodOrderDetection(tenantContext, conversation, messageText))
            {
                fallbackCreated = true;
                _logger.LogInformation("✅ Fallback food order detection succeeded for: '{Message}'", messageText);
            }

            // Try maintenance issue fallback detection (before hotel items and complaints)
            if (!fallbackCreated && await TryFallbackMaintenanceDetection(tenantContext, conversation, messageText))
            {
                fallbackCreated = true;
                _logger.LogInformation("✅ Fallback maintenance detection succeeded for: '{Message}'", messageText);
            }

            // Try hotel item request fallback detection
            if (!fallbackCreated && await TryFallbackHotelItemDetection(tenantContext, conversation, messageText))
            {
                fallbackCreated = true;
                _logger.LogInformation("✅ Fallback hotel item detection succeeded for: '{Message}'", messageText);
            }

            // Try complaint fallback detection
            if (!fallbackCreated && await TryFallbackComplaintDetection(tenantContext, conversation, messageText))
            {
                fallbackCreated = true;
                _logger.LogInformation("✅ Fallback complaint detection succeeded for: '{Message}'", messageText);
            }

            if (!fallbackCreated)
            {
                _logger.LogInformation("No fallback scenarios matched for message: '{Message}'", messageText);
            }

            return fallbackCreated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in fallback detection for conversation {ConversationId}", conversation.Id);
            return false;
        }
    }

    private async Task<string?> GetLatestUserMessage(Conversation conversation)
    {
        try
        {
            var latestMessage = await _context.Messages
                .Where(m => m.ConversationId == conversation.Id && m.Direction == "Inbound")
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            return latestMessage?.Body;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest user message for conversation {ConversationId}", conversation.Id);
            return null;
        }
    }

    private async Task<bool> TryFallbackFoodOrderDetection(TenantContext tenantContext, Conversation conversation, string messageText)
    {
        try
        {
            // Check if message contains food keywords
            var foodConfidence = GetFoodOrderConfidence(messageText);
            _logger.LogInformation("Food order confidence for message '{Message}': {Confidence}%", messageText, foodConfidence);

            if (foodConfidence < 60.0)
            {
                return false;
            }

            // Look for food items mentioned in menu
            var menuItems = await _context.MenuItems
                .Where(m => m.TenantId == tenantContext.TenantId && m.IsAvailable)
                .ToListAsync();

            var matchedItems = new List<MenuItem>();

            foreach (var item in menuItems)
            {
                if (messageText.ToLower().Contains(item.Name.ToLower()))
                {
                    matchedItems.Add(item);
                }
            }

            if (matchedItems.Any())
            {
                _logger.LogInformation("🍽️ FALLBACK: Creating food order task for items: {Items}",
                    string.Join(", ", matchedItems.Select(i => i.Name)));

                var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

                var task = new StaffTask
                {
                    TenantId = tenantContext.TenantId,
                    Title = $"Food Order: {string.Join(", ", matchedItems.Take(3).Select(i => i.Name))}",
                    Description = $"Guest has ordered: {string.Join(", ", matchedItems.Select(i => i.Name))}\n\nOriginal message: \"{messageText}\"",
                    Priority = "Normal",
                    Status = "Open",
                    Department = "Kitchen",
                    CreatedAt = DateTime.UtcNow,
                    EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(30),
                    GuestPhone = conversation.WaUserPhone,
                    GuestName = guestStatus.DisplayName,
                    RoomNumber = guestStatus.RoomNumber,
                    ConversationId = conversation.Id
                };

                _context.StaffTasks.Add(task);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ FALLBACK: Food order task created with ID {TaskId}", task.Id);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in fallback food order detection");
            return false;
        }
    }

    private async Task<bool> TryFallbackMaintenanceDetection(TenantContext tenantContext, Conversation conversation, string messageText)
    {
        try
        {
            // Check if this looks like a maintenance issue
            var maintenanceConfidence = GetMaintenanceConfidence(messageText);
            _logger.LogInformation("Maintenance confidence for message '{Message}': {Confidence}%", messageText, maintenanceConfidence);

            if (maintenanceConfidence < 70.0)
            {
                return false;
            }

            _logger.LogInformation("🔧 FALLBACK: Creating maintenance task for message: '{Message}'", messageText);

            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

            // Determine priority based on urgency of the issue
            var priority = DetermineMaintenancePriority(messageText);

            var task = new StaffTask
            {
                TenantId = tenantContext.TenantId,
                Title = GenerateMaintenanceTitle(messageText),
                Description = $"Maintenance issue reported: \"{messageText}\"",
                Priority = priority,
                Status = "Open",
                Department = "Maintenance",
                CreatedAt = DateTime.UtcNow,
                EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(priority == "HIGH" ? 30 : 60),
                GuestPhone = conversation.WaUserPhone,
                GuestName = guestStatus.DisplayName,
                RoomNumber = guestStatus.RoomNumber,
                ConversationId = conversation.Id
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ FALLBACK: Maintenance task created with ID {TaskId}, Priority: {Priority}", task.Id, priority);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in fallback maintenance detection");
            return false;
        }
    }

    private string GenerateMaintenanceTitle(string messageText)
    {
        var lowerMessage = messageText.ToLower();

        // Water/Plumbing issues
        if (lowerMessage.Contains("leak") || lowerMessage.Contains("burst") || lowerMessage.Contains("flood"))
            return "Water Leak/Flooding Issue";
        if (lowerMessage.Contains("drain") || lowerMessage.Contains("clog") || lowerMessage.Contains("block"))
            return "Drainage/Blockage Issue";
        if (lowerMessage.Contains("toilet"))
            return "Toilet Issue";
        if (lowerMessage.Contains("shower") || lowerMessage.Contains("bath"))
            return "Shower/Bath Issue";
        if (lowerMessage.Contains("faucet") || lowerMessage.Contains("tap"))
            return "Faucet/Tap Issue";
        if (lowerMessage.Contains("water") && (lowerMessage.Contains("no") || lowerMessage.Contains("not")))
            return "No Water Issue";

        // Electrical issues
        if (lowerMessage.Contains("power") || lowerMessage.Contains("electric") || lowerMessage.Contains("outlet"))
            return "Electrical Issue";
        if (lowerMessage.Contains("light") || lowerMessage.Contains("bulb"))
            return "Lighting Issue";

        // HVAC issues
        if (lowerMessage.Contains("air") || lowerMessage.Contains("ac") || lowerMessage.Contains("conditioning"))
            return "Air Conditioning Issue";
        if (lowerMessage.Contains("heat") || lowerMessage.Contains("hot") || lowerMessage.Contains("cold") || lowerMessage.Contains("temperature"))
            return "Temperature Control Issue";

        // Structural issues
        if (lowerMessage.Contains("door") || lowerMessage.Contains("lock"))
            return "Door/Lock Issue";
        if (lowerMessage.Contains("window"))
            return "Window Issue";

        // General fallback
        return "Maintenance Issue";
    }

    private string DetermineMaintenancePriority(string messageText)
    {
        var lowerMessage = messageText.ToLower();

        // HIGH priority keywords - urgent safety/security issues
        var highPriorityKeywords = new[] {
            "leak", "leaking", "burst", "flood", "flooding", "water everywhere",
            "sparking", "spark", "electrical fire", "smoke", "burning",
            "no power", "blackout", "no electricity",
            "lock broken", "door won't lock", "can't lock", "security",
            "emergency", "urgent", "immediately", "asap", "right now"
        };

        if (highPriorityKeywords.Any(keyword => lowerMessage.Contains(keyword)))
        {
            return "High";
        }

        return "Normal";
    }

    private async Task<bool> TryFallbackHotelItemDetection(TenantContext tenantContext, Conversation conversation, string messageText)
    {
        try
        {
            // Check if this looks like a hotel item request
            var hotelItemConfidence = GetHotelItemConfidence(messageText);
            _logger.LogInformation("Hotel item confidence for message '{Message}': {Confidence}%", messageText, hotelItemConfidence);

            if (hotelItemConfidence < 55.0)
            {
                return false;
            }

            // Look for hotel items mentioned in request items
            var requestItems = await _context.RequestItems
                .Where(r => r.TenantId == tenantContext.TenantId && r.IsAvailable)
                .ToListAsync();

            var matchedItems = new List<RequestItem>();

            foreach (var item in requestItems)
            {
                if (messageText.ToLower().Contains(item.Name.ToLower()) ||
                    (item.LlmVisibleName != null && messageText.ToLower().Contains(item.LlmVisibleName.ToLower())))
                {
                    matchedItems.Add(item);
                }
            }

            if (matchedItems.Any())
            {
                // Extract quantity from message
                var quantity = ExtractQuantityFromMessage(messageText);
                var firstItemName = matchedItems.First().Name;

                // Validate quantity
                var (isValid, confirmationMessage) = ValidateQuantity(quantity, firstItemName);

                if (!isValid && confirmationMessage != null)
                {
                    // Quantity exceeds limit - don't create task, return false so normal routing handles response
                    _logger.LogInformation("⚠️ FALLBACK: Quantity {Quantity} exceeds limit for {Item}, skipping task creation. " +
                        "Message: {ConfirmationMessage}", quantity, firstItemName, confirmationMessage);

                    // Return false - the normal LLM routing will detect the high quantity and ask for confirmation
                    return false;
                }

                _logger.LogInformation("🏨 FALLBACK: Creating hotel item request task for items: {Items} (Quantity: {Quantity})",
                    string.Join(", ", matchedItems.Select(i => i.Name)), quantity);

                var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

                var task = new StaffTask
                {
                    TenantId = tenantContext.TenantId,
                    Title = $"Item Request: {string.Join(", ", matchedItems.Take(3).Select(i => i.Name))} (x{quantity})",
                    Description = $"Guest has requested: {string.Join(", ", matchedItems.Select(i => i.Name))} (Quantity: {quantity})\n\nOriginal message: \"{messageText}\"",
                    Priority = "Normal",
                    Status = "Open",
                    Department = "Housekeeping",
                    CreatedAt = DateTime.UtcNow,
                    EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(20),
                    GuestPhone = conversation.WaUserPhone,
                    GuestName = guestStatus.DisplayName,
                    RoomNumber = guestStatus.RoomNumber,
                    ConversationId = conversation.Id,
                    RequestItemId = matchedItems.First().Id
                };

                _context.StaffTasks.Add(task);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ FALLBACK: Hotel item request task created with ID {TaskId}", task.Id);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in fallback hotel item detection");
            return false;
        }
    }

    private async Task<bool> TryFallbackComplaintDetection(TenantContext tenantContext, Conversation conversation, string messageText)
    {
        try
        {
            // Check if this looks like a complaint
            var complaintConfidence = GetComplaintConfidence(messageText);
            _logger.LogInformation("Complaint confidence for message '{Message}': {Confidence}%", messageText, complaintConfidence);

            if (complaintConfidence < 65.0)
            {
                return false;
            }

            _logger.LogInformation("🚨 FALLBACK: Creating complaint task for message: '{Message}'", messageText);

            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

            var task = new StaffTask
            {
                TenantId = tenantContext.TenantId,
                Title = "Guest Complaint",
                Description = $"Guest complaint: \"{messageText}\"",
                Priority = "High",
                Status = "Open",
                Department = "FrontDesk",
                CreatedAt = DateTime.UtcNow,
                EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(15),
                GuestPhone = conversation.WaUserPhone,
                GuestName = guestStatus.DisplayName,
                RoomNumber = guestStatus.RoomNumber,
                ConversationId = conversation.Id
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ FALLBACK: Complaint task created with ID {TaskId}", task.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in fallback complaint detection");
            return false;
        }
    }

    private double GetFoodOrderConfidence(string messageText)
    {
        var lowerMessage = messageText.ToLower();
        double confidence = 0.0;

        // Layer 1: High-Confidence Food Order Phrases (90-100%)
        var highConfidencePhrases = new[] {
            "i want to order", "can i order", "i'd like to order", "place an order",
            "food delivery", "room service food", "menu please", "order food",
            "i'm hungry", "order breakfast", "order lunch", "order dinner"
        };

        foreach (var phrase in highConfidencePhrases)
        {
            if (lowerMessage.Contains(phrase))
            {
                confidence = Math.Max(confidence, 95.0);
            }
        }

        // Layer 2: Food Intent + Specific Items (70-85%)
        var foodIntentWords = new[] { "hungry", "eat", "meal", "order" };
        var foodItems = new[] { "lamb", "espresso", "coffee", "sandwich", "burger", "pizza", "pasta", "salad", "soup", "dessert" };

        var hasFoodIntent = foodIntentWords.Any(word => lowerMessage.Contains(word));
        var hasFoodItem = foodItems.Any(word => lowerMessage.Contains(word));

        if (hasFoodIntent && hasFoodItem)
        {
            confidence = Math.Max(confidence, 80.0);
        }
        else if (hasFoodItem)
        {
            confidence = Math.Max(confidence, 60.0);
        }
        else if (hasFoodIntent)
        {
            confidence = Math.Max(confidence, 45.0);
        }

        // Layer 3: General Food Keywords (40-60%)
        var generalFoodWords = new[] { "food", "breakfast", "lunch", "dinner", "beverage", "drink" };
        foreach (var word in generalFoodWords)
        {
            if (lowerMessage.Contains(word))
            {
                confidence = Math.Max(confidence, 50.0);
            }
        }

        // Context Penalties - Reduce confidence for non-food-order contexts
        var nonFoodOrderContexts = new[] {
            "in order to", "law and order", "put in order", "restaurant nearby", "restaurant next door",
            "menu bar", "menu options", "settings menu", "kitchen staff", "kitchen is clean",
            "don't drink", "drink responsibly", "menu of services", "order of operations"
        };

        foreach (var context in nonFoodOrderContexts)
        {
            if (lowerMessage.Contains(context))
            {
                confidence -= 35.0;
            }
        }

        // Ensure confidence stays within bounds
        return Math.Max(0.0, Math.Min(100.0, confidence));
    }

    private double GetHotelItemConfidence(string messageText)
    {
        var lowerMessage = messageText.ToLower();
        double confidence = 0.0;

        // Layer 1: High-Confidence Hotel Item Request Phrases (90-100%)
        var highConfidencePhrases = new[] {
            "can you bring", "could you bring", "can i get", "could i get",
            "need extra towels", "need more pillows", "bring me", "room service",
            "housekeeping please bring", "i need towels", "extra blankets please"
        };

        foreach (var phrase in highConfidencePhrases)
        {
            if (lowerMessage.Contains(phrase))
            {
                confidence = Math.Max(confidence, 95.0);
            }
        }

        // Layer 2: Request Intent + Specific Items (70-85%)
        var requestWords = new[] { "need", "want", "bring", "get", "extra", "more" };
        var itemWords = new[] { "towel", "pillow", "blanket", "charger", "iron", "hairdryer", "shampoo", "soap", "toilet paper" };

        var hasRequestIntent = requestWords.Any(word => lowerMessage.Contains(word));
        var hasSpecificItem = itemWords.Any(word => lowerMessage.Contains(word));

        if (hasRequestIntent && hasSpecificItem)
        {
            confidence = Math.Max(confidence, 80.0);
        }
        else if (hasSpecificItem)
        {
            confidence = Math.Max(confidence, 65.0);
        }
        else if (hasRequestIntent)
        {
            confidence = Math.Max(confidence, 35.0);
        }

        // Layer 3: Service Keywords (50-70%)
        var serviceKeywords = new[] { "housekeeping", "amenities", "supplies", "room service" };
        foreach (var keyword in serviceKeywords)
        {
            if (lowerMessage.Contains(keyword))
            {
                confidence = Math.Max(confidence, 60.0);
            }
        }

        // Context Penalties - Reduce confidence for non-item-request contexts
        var nonItemRequestContexts = new[] {
            "i need directions", "i need information", "i need help", "i need to know",
            "i want to know", "i want to complain", "tell me more", "more information",
            "extra charges", "extra time", "extra cost", "need assistance"
        };

        foreach (var context in nonItemRequestContexts)
        {
            if (lowerMessage.Contains(context))
            {
                confidence -= 30.0;
            }
        }

        // Ensure confidence stays within bounds
        return Math.Max(0.0, Math.Min(100.0, confidence));
    }

    private double GetMaintenanceConfidence(string messageText)
    {
        var lowerMessage = messageText.ToLower();
        double confidence = 0.0;

        // Layer 1: High-Confidence Phrases (90-100%)
        var highConfidencePhrases = new[] {
            "water is leaking", "toilet is broken", "shower not working", "no hot water",
            "air conditioner broken", "heating not working", "door won't lock", "key doesn't work",
            "toilet won't flush", "sink is clogged", "drain is blocked", "faucet is leaking",
            "light bulb broken", "no electricity", "power is out", "outlet not working"
        };

        foreach (var phrase in highConfidencePhrases)
        {
            if (lowerMessage.Contains(phrase))
            {
                confidence = Math.Max(confidence, 95.0);
            }
        }

        // Layer 2: Problem + Object Combinations (60-80%)
        var problemWords = new[] { "broken", "not working", "leaking", "stuck", "damaged", "faulty", "clogged", "blocked" };
        var objectWords = new[] { "door", "shower", "toilet", "sink", "faucet", "light", "bulb", "outlet", "lock" };

        var hasProblem = problemWords.Any(word => lowerMessage.Contains(word));
        var hasObject = objectWords.Any(word => lowerMessage.Contains(word));

        if (hasProblem && hasObject)
        {
            confidence = Math.Max(confidence, 75.0);
        }
        else if (hasProblem || hasObject)
        {
            confidence = Math.Max(confidence, 40.0);
        }

        // Layer 3: Specific Issue Detection (60-85%)
        var specificIssues = new[] {
            "leak", "leaking", "burst", "flood", "flooding", "overflow",
            "no water", "low pressure", "drain", "plumbing",
            "no power", "blackout", "sparking", "short circuit",
            "too hot", "too cold", "no heat", "no cooling"
        };

        foreach (var issue in specificIssues)
        {
            if (lowerMessage.Contains(issue))
            {
                confidence = Math.Max(confidence, 70.0);
            }
        }

        // Context Penalties - Reduce confidence for non-maintenance contexts
        var nonMaintenanceContexts = new[] {
            "next door", "nearby", "restaurant", "weather", "outside", "service quality",
            "I'm a fan of", "temperature outside", "weather forecast", "baby shower",
            "the key to success", "in order to", "keyboard", "power of", "powerful"
        };

        foreach (var context in nonMaintenanceContexts)
        {
            if (lowerMessage.Contains(context))
            {
                confidence -= 40.0;
            }
        }

        // Ensure confidence stays within bounds
        return Math.Max(0.0, Math.Min(100.0, confidence));
    }

    private double GetComplaintConfidence(string messageText)
    {
        var lowerMessage = messageText.ToLower();
        double confidence = 0.0;

        // Layer 1: High-Confidence Complaint Phrases (90-100%)
        var highConfidencePhrases = new[] {
            "i'm unhappy with", "this is unacceptable", "poor service", "terrible service",
            "i want to complain", "this is disgusting", "very disappointed", "completely unsatisfied",
            "worst experience", "never coming back", "demand a refund", "speak to manager"
        };

        foreach (var phrase in highConfidencePhrases)
        {
            if (lowerMessage.Contains(phrase))
            {
                confidence = Math.Max(confidence, 95.0);
            }
        }

        // Layer 2: Complaint Intent + Quality Issues (70-85%)
        var complaintWords = new[] { "complaint", "unhappy", "disappointed", "terrible", "awful", "disgusting", "unacceptable" };
        var qualityWords = new[] { "dirty", "noisy", "loud", "smell", "stain", "messy", "rude", "slow" };

        var hasComplaintIntent = complaintWords.Any(word => lowerMessage.Contains(word));
        var hasQualityIssue = qualityWords.Any(word => lowerMessage.Contains(word));

        if (hasComplaintIntent && hasQualityIssue)
        {
            confidence = Math.Max(confidence, 85.0);
        }
        else if (hasComplaintIntent)
        {
            confidence = Math.Max(confidence, 75.0);
        }
        else if (hasQualityIssue)
        {
            confidence = Math.Max(confidence, 60.0);
        }

        // Layer 3: General Issue Keywords (40-60%)
        var issueKeywords = new[] { "problem", "issue", "wrong", "error", "mistake", "bad" };
        foreach (var keyword in issueKeywords)
        {
            if (lowerMessage.Contains(keyword))
            {
                confidence = Math.Max(confidence, 50.0);
            }
        }

        // Context Penalties - Reduce confidence for non-complaint contexts
        var nonComplaintContexts = new[] {
            "no problem", "not a problem", "what's the problem", "help with problem",
            "am i wrong", "wrong room", "wrong building", "wrong number",
            "not bad", "not too bad", "pretty good", "thank you",
            "nice smell", "what's that smell", "technical issue", "wifi issue"
        };

        foreach (var context in nonComplaintContexts)
        {
            if (lowerMessage.Contains(context))
            {
                confidence -= 40.0;
            }
        }

        // Ensure confidence stays within bounds
        return Math.Max(0.0, Math.Min(100.0, confidence));
    }

    private async Task ProcessSingleAction(TenantContext tenantContext, Conversation conversation, JsonElement action)
    {
        try
        {
            if (!action.TryGetProperty("type", out var typeElement))
            {
                _logger.LogWarning("Action missing 'type' property: {Action}", action.GetRawText());
                return;
            }

            var actionType = typeElement.GetString();
            _logger.LogInformation("Processing action type: {ActionType} for conversation {ConversationId}",
                actionType, conversation.Id);

            switch (actionType?.ToLower())
            {
                case "create_food_order":
                    await ProcessCreateFoodOrderAction(tenantContext, conversation, action);
                    break;
                case "create_task":
                    await ProcessCreateTaskAction(tenantContext, conversation, action);
                    break;
                case "create_complaint":
                    await ProcessCreateComplaintAction(tenantContext, conversation, action);
                    break;
                default:
                    _logger.LogWarning("Unknown action type: {ActionType} - {Action}", actionType, action.GetRawText());
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing single action: {Action}", action.GetRawText());
        }
    }

    private async Task ProcessCreateFoodOrderAction(TenantContext tenantContext, Conversation conversation, JsonElement action)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);

            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

            if (!action.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Food order action missing 'items' array: {Action}", action.GetRawText());
                return;
            }

            foreach (var itemElement in itemsElement.EnumerateArray())
            {
                if (!itemElement.TryGetProperty("name", out var nameElement))
                    continue;

                var itemName = nameElement.GetString();
                var quantity = 1;
                if (itemElement.TryGetProperty("quantity", out var quantityElement) && quantityElement.ValueKind == JsonValueKind.Number)
                {
                    quantity = quantityElement.GetInt32();
                }

                var menuItem = await _context.MenuItems
                    .Where(mi => mi.TenantId == tenantContext.TenantId && mi.Name.Contains(itemName))
                    .FirstOrDefaultAsync();

                if (menuItem == null)
                {
                    _logger.LogWarning("Menu item not found: {ItemName}", itemName);
                    continue;
                }

                var task = new StaffTask
                {
                    TenantId = tenantContext.TenantId,
                    Title = $"Food Order: {quantity}x {menuItem.Name}",
                    Description = $"Guest ordered {quantity}x {menuItem.Name} - {menuItem.Description}",
                    Priority = "Medium",
                    Status = "Open",
                    Department = "Kitchen",
                    CreatedAt = DateTime.UtcNow,
                    EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(30),
                    GuestPhone = conversation.WaUserPhone,
                    GuestName = guestStatus.DisplayName ?? conversation.WaUserPhone,
                    RoomNumber = guestStatus.RoomNumber,
                    RequestItemId = menuItem.Id,
                    Quantity = quantity
                };

                _context.StaffTasks.Add(task);
                _logger.LogInformation("Created food order task: {ItemName} x{Quantity} for room {RoomNumber}",
                    menuItem.Name, quantity, guestStatus.RoomNumber);
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing food order action: {Action}", action.GetRawText());
        }
    }

    private async Task ProcessCreateTaskAction(TenantContext tenantContext, Conversation conversation, JsonElement action)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);

            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

            var title = "Guest Request";
            var description = "Guest request via AI assistant";
            var department = "FrontDesk";
            var priority = "Medium";

            if (action.TryGetProperty("title", out var titleElement))
                title = titleElement.GetString() ?? title;

            if (action.TryGetProperty("description", out var descElement))
                description = descElement.GetString() ?? description;

            if (action.TryGetProperty("department", out var deptElement))
                department = deptElement.GetString() ?? department;

            if (action.TryGetProperty("priority", out var priorityElement))
                priority = priorityElement.GetString() ?? priority;

            var task = new StaffTask
            {
                TenantId = tenantContext.TenantId,
                Title = title,
                Description = description,
                Priority = priority,
                Status = "Open",
                Department = department,
                CreatedAt = DateTime.UtcNow,
                EstimatedCompletionTime = DateTime.UtcNow.AddHours(1),
                GuestPhone = conversation.WaUserPhone,
                GuestName = guestStatus.DisplayName ?? conversation.WaUserPhone,
                RoomNumber = guestStatus.RoomNumber
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created general task: {Title} for room {RoomNumber}", title, guestStatus.RoomNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing create task action: {Action}", action.GetRawText());
        }
    }

    private async Task ProcessCreateComplaintAction(TenantContext tenantContext, Conversation conversation, JsonElement action)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);

            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

            var title = "Guest Complaint";
            var description = "Guest complaint via AI assistant";
            var category = "General";

            if (action.TryGetProperty("title", out var titleElement))
                title = titleElement.GetString() ?? title;

            if (action.TryGetProperty("description", out var descElement))
                description = descElement.GetString() ?? description;

            if (action.TryGetProperty("category", out var categoryElement))
                category = categoryElement.GetString() ?? category;

            var task = new StaffTask
            {
                TenantId = tenantContext.TenantId,
                Title = $"COMPLAINT: {title}",
                Description = $"Guest Complaint - {category}: {description}",
                Priority = "High",
                Status = "Open",
                Department = "FrontDesk",
                CreatedAt = DateTime.UtcNow,
                EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(15),
                GuestPhone = conversation.WaUserPhone,
                GuestName = guestStatus.DisplayName ?? conversation.WaUserPhone,
                RoomNumber = guestStatus.RoomNumber
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created complaint task: {Title} for room {RoomNumber}", title, guestStatus.RoomNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing complaint action: {Action}", action.GetRawText());
        }
    }

    private async Task<string> GenerateClarificationResponseAsync(AmbiguityResult ambiguityResult, int conversationId, int tenantId, string originalMessage)
    {
        try
        {
            // Phase 2 Enhancement: Use advanced clarification strategy
            var conversationState = await _conversationStateService.GetStateAsync(conversationId);

            // Check if escalation is needed
            if (await _clarificationStrategyService.ShouldEscalateToClarificationAsync(ambiguityResult, conversationState))
            {
                return "This seems like a complex request that would benefit from personal assistance. Let me connect you with a staff member who can help you directly. In the meantime, could you provide any additional details about what you need?";
            }

            // Determine best clarification strategy
            var strategy = await _clarificationStrategyService.DetermineBestStrategyAsync(ambiguityResult, conversationId, tenantId);

            // Get relevant context to enhance the response
            var relevantContexts = await _contextRelevanceService.ScoreConversationHistoryAsync(conversationId, originalMessage, 3);

            // Format the response using the strategy
            var clarificationMessage = await _clarificationStrategyService.FormatClarificationMessageAsync(strategy, originalMessage);

            // Add context-aware enhancements if relevant context exists
            if (relevantContexts.Any() && relevantContexts.First().RelevanceScore > 0.7)
            {
                var contextualInfo = relevantContexts.First();
                if (contextualInfo.Type == ContextType.TaskHistory && contextualInfo.IsCritical)
                {
                    clarificationMessage += $"\n\n💡 I notice you have a pending request: {contextualInfo.Content.Substring(0, Math.Min(50, contextualInfo.Content.Length))}... Is this related?";
                }
                else if (contextualInfo.Type == ContextType.ConversationHistory && contextualInfo.IsRecent)
                {
                    clarificationMessage += "\n\n💡 Based on our recent conversation, I think I can help you better with a bit more detail.";
                }
            }

            // Store clarification for tracking
            await _conversationStateService.AddPendingClarificationAsync(conversationId, clarificationMessage);

            _logger.LogInformation("Generated clarification using strategy: {Strategy} for conversation {ConversationId}",
                strategy.Approach, conversationId);

            return clarificationMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating advanced clarification response for conversation {ConversationId}", conversationId);
            return await GenerateFallbackClarificationAsync(ambiguityResult);
        }
    }


    private async Task<string> GenerateFallbackClarificationAsync(AmbiguityResult ambiguityResult)
    {
        try
        {
            var responseBuilder = new StringBuilder();

            // Add personalized greeting based on ambiguity type
            if (ambiguityResult.AmbiguityTypes.Contains(AmbiguityType.PrivacyViolation))
            {
                responseBuilder.AppendLine("I understand you're looking for information, but I need to respect guest privacy.");
            }
            else if (ambiguityResult.AmbiguityTypes.Contains(AmbiguityType.ConflictingContext))
            {
                responseBuilder.AppendLine("I notice there might be some confusion about your current status.");
            }
            else
            {
                responseBuilder.AppendLine("I'd be happy to help! Let me clarify a few details to assist you better.");
            }

            // Add the primary clarification question
            if (ambiguityResult.ClarificationQuestions.Any())
            {
                responseBuilder.AppendLine();
                responseBuilder.AppendLine(ambiguityResult.ClarificationQuestions.First());
            }

            // Add suggested options if available
            if (ambiguityResult.SuggestedOptions.Any())
            {
                responseBuilder.AppendLine();
                foreach (var optionGroup in ambiguityResult.SuggestedOptions)
                {
                    if (optionGroup.Value.Any())
                    {
                        responseBuilder.AppendLine($"Here are some options:");
                        for (int i = 0; i < Math.Min(optionGroup.Value.Count, 5); i++)
                        {
                            responseBuilder.AppendLine($"• {optionGroup.Value[i]}");
                        }
                    }
                }
            }

            // Add helpful follow-up based on confidence level
            if (ambiguityResult.Confidence >= ConfidenceLevel.High)
            {
                responseBuilder.AppendLine();
                responseBuilder.AppendLine("Please provide more specific details so I can assist you properly.");
            }
            else if (ambiguityResult.Confidence == ConfidenceLevel.Medium)
            {
                responseBuilder.AppendLine();
                responseBuilder.AppendLine("Could you help me understand what you're looking for?");
            }

            return responseBuilder.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating fallback clarification response");
            return "I'd like to help! Could you provide a bit more detail about what you need?";
        }
    }

    private async Task<string> GenerateLLMBusinessRulesResponseAsync(List<BusinessRuleViolation> violations, BusinessRuleAnalysis analysis, int tenantId)
    {
        try
        {
            var responseBuilder = new StringBuilder();

            // Start with contextual acknowledgment based on LLM analysis
            var itemType = analysis.SpecificItem.ToLower();
            if (analysis.ServiceCategory == "FOOD_BEVERAGE")
            {
                responseBuilder.AppendLine($"I understand you'd like {analysis.SpecificItem}.");
            }
            else if (analysis.ServiceCategory == "SPA_WELLNESS")
            {
                responseBuilder.AppendLine($"I understand you're interested in {analysis.SpecificItem}.");
            }
            else
            {
                responseBuilder.AppendLine($"I understand you'd like to request {analysis.SpecificItem}.");
            }

            responseBuilder.AppendLine();

            // Add specific violation messages
            foreach (var violation in violations.Where(v => v.Severity == "BLOCK"))
            {
                switch (violation.RuleName)
                {
                    case "spa_services_availability":
                        responseBuilder.AppendLine("However, spa and wellness services are not currently available at this property.");
                        break;
                    case "room_service_hours":
                        responseBuilder.AppendLine("However, this service is not available at this time due to operating hours.");
                        break;
                    case "maintenance_priority":
                        responseBuilder.AppendLine("I'll prioritize your maintenance request and notify our team immediately.");
                        break;
                    default:
                        responseBuilder.AppendLine($"However, {violation.Message}");
                        break;
                }
            }

            // Suggest contextual alternatives based on LLM analysis
            if (analysis.ServiceCategory == "FOOD_BEVERAGE")
            {
                var availableItems = await _context.MenuItems
                    .Where(m => m.TenantId == tenantId && m.IsAvailable)
                    .Select(m => m.Name)
                    .Take(3)
                    .ToListAsync();

                if (availableItems.Any())
                {
                    responseBuilder.AppendLine();
                    responseBuilder.AppendLine($"I can help you with these available options: {string.Join(", ", availableItems)}");
                }
            }
            else if (analysis.ServiceCategory == "SPA_WELLNESS")
            {
                responseBuilder.AppendLine();
                responseBuilder.AppendLine("Instead, I can help you with:");
                responseBuilder.AppendLine("• Room service and refreshments");
                responseBuilder.AppendLine("• Local spa and wellness recommendations");
                responseBuilder.AppendLine("• Other hotel amenities and services");
            }

            // Add helpful closing
            responseBuilder.AppendLine();
            responseBuilder.AppendLine("How else can I assist you today?");

            return responseBuilder.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating LLM business rules response");
            return "I understand your request. Let me help you find the best available option. What else can I assist you with?";
        }
    }

    private bool IsServiceBookingRequest(string messageLower)
    {
        // Informational query patterns - these should NOT trigger business rules
        var informationalPatterns = new[]
        {
            "do you have", "does the hotel have", "is there", "are there",
            "what are", "what time", "when is", "where is", "how much",
            "tell me about", "information about", "details about",
            "hours", "open", "close", "available"
        };

        // If it's clearly an informational query, don't validate business rules
        if (informationalPatterns.Any(pattern => messageLower.Contains(pattern)))
        {
            return false;
        }

        // Service booking/request patterns - these SHOULD trigger business rules
        var bookingPatterns = new[]
        {
            "book", "reserve", "schedule", "i want", "i need", "can i get",
            "order", "request", "arrange", "set up", "book me"
        };

        // Check if message contains service keywords AND booking intent
        var serviceKeywords = new[] { "spa", "massage", "gym", "pool", "fitness" };
        var hasServiceKeyword = serviceKeywords.Any(keyword => messageLower.Contains(keyword));
        var hasBookingIntent = bookingPatterns.Any(pattern => messageLower.Contains(pattern));

        return hasServiceKeyword && hasBookingIntent;
    }

    private string ExtractServiceNameFromMessage(string messageText)
    {
        var messageLower = messageText.ToLower();

        // Service name mapping
        var serviceKeywords = new Dictionary<string, string>
        {
            {"spa", "Spa Services"},
            {"massage", "Spa Services"},
            {"gym", "Fitness Center"},
            {"fitness", "Fitness Center"},
            {"pool", "Swimming Pool"},
            {"restaurant", "Restaurant"},
            {"dining", "Restaurant"},
            {"room service", "Room Service"},
            {"concierge", "Concierge"},
            {"valet", "Valet Parking"}
        };

        foreach (var kvp in serviceKeywords)
        {
            if (messageLower.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        return "General Services";
    }

    private async Task<string> GenerateBusinessRulesResponseAsync(BusinessRuleResult businessRulesResult, string originalMessage, int tenantId)
    {
        try
        {
            var responseBuilder = new StringBuilder();

            // Start with a polite acknowledgment
            responseBuilder.AppendLine("I understand you'd like to make this request. However, I need to let you know about some current limitations:");
            responseBuilder.AppendLine();

            // Process violated rules and violations
            if (businessRulesResult.TriggeredRules.Any())
            {
                foreach (var rule in businessRulesResult.TriggeredRules)
                {
                    responseBuilder.AppendLine($"• {rule.Name}: {rule.Description}");
                }
            }

            // Add violations if any
            if (businessRulesResult.Violations.Any())
            {
                foreach (var violation in businessRulesResult.Violations)
                {
                    responseBuilder.AppendLine($"• {violation}");
                }
            }

            // Suggest alternative services if service not available
            if (!businessRulesResult.IsAllowed)
            {
                var availableServices = await _context.Services
                    .Where(s => s.TenantId == tenantId && s.IsAvailable)
                    .Select(s => s.Name)
                    .Take(3)
                    .ToListAsync();

                if (availableServices.Any())
                {
                    responseBuilder.AppendLine();
                    responseBuilder.AppendLine($"Available alternatives: {string.Join(", ", availableServices)}");
                }
            }

            // Add helpful closing
            responseBuilder.AppendLine("I'm here to help you find the best solution. Would you like me to:");
            responseBuilder.AppendLine("• Suggest alternative times or services");
            responseBuilder.AppendLine("• Help you with something else");
            responseBuilder.AppendLine("• Provide more information about our available options");

            return responseBuilder.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating business rules response for message: {Message}", originalMessage);

            // Fallback response
            return "I understand your request, but there are some limitations at the moment. " +
                   "Let me help you find an alternative solution. What else can I assist you with?";
        }
    }

    // Helper method to check if a service is available from the Services table
    private async Task<bool> IsServiceAvailableAsync(string serviceName, int tenantId)
    {
        try
        {
            var service = await _context.Services
                .Where(s => s.TenantId == tenantId && s.Name.ToLower().Contains(serviceName.ToLower()) && s.IsAvailable)
                .FirstOrDefaultAsync();

            var isAvailable = service != null;
            _logger.LogInformation("Service availability check: {ServiceName} for tenant {TenantId} = {IsAvailable}",
                serviceName, tenantId, isAvailable);

            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking service availability for {ServiceName} at tenant {TenantId}", serviceName, tenantId);
            return false; // Default to unavailable on error
        }
    }

    private async Task<string> ModifyResponseToAvoidDuplicateAsync(string originalResponse)
    {
        try
        {
            // Simple strategies to modify response while maintaining meaning
            var modificationStrategies = new[]
            {
                $"I'm happy to help! {originalResponse}",
                $"{originalResponse} Is there anything else I can assist you with?",
                $"Absolutely! {originalResponse}",
                $"{originalResponse} Let me know if you need any further assistance.",
                $"Of course! {originalResponse}"
            };

            // Pick a random modification strategy
            var random = new Random();
            var selectedStrategy = modificationStrategies[random.Next(modificationStrategies.Length)];

            _logger.LogInformation("Modified duplicate response using strategy to avoid repetition");
            return selectedStrategy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modifying response to avoid duplicate, returning original");
            return originalResponse;
        }
    }

    private async Task<string> ValidateAndCorrectServiceHallucinations(string response, int tenantId, string guestMessage)
    {
        try
        {
            var lowerResponse = response.ToLower();
            var lowerMessage = guestMessage.ToLower();

            // Check for "rooftop" hallucination
            if (lowerResponse.Contains("rooftop"))
            {
                // Load available services for this tenant
                var availableServices = await _context.Services
                    .Where(s => s.TenantId == tenantId && s.IsAvailable)
                    .ToListAsync();

                var hasRooftopService = availableServices.Any(s => s.Name.ToLower().Contains("rooftop"));
                if (!hasRooftopService)
                {
                    _logger.LogWarning("HALLUCINATION DETECTED: Response mentions 'rooftop' but no rooftop services in database. Correcting response.");

                    // If response incorrectly confirms rooftop, rewrite it completely
                    if (lowerMessage.Contains("rooftop") && (lowerResponse.Contains("yes") || lowerResponse.Contains("we do have")))
                    {
                        response = "We have a Swimming Pool available for guests to enjoy. However, I don't have specific information about it being on a rooftop. Would you like to know more about our Swimming Pool amenities?";
                    }
                    else
                    {
                        // Replace hallucinated rooftop references
                        response = System.Text.RegularExpressions.Regex.Replace(
                            response,
                            @"\brooftop\s+(swimming\s+)?pool\b",
                            "Swimming Pool",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        );
                    }
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating service hallucinations");
            return response;
        }
    }

    // Information Gathering Integration Methods

    private async Task<(bool IsBookingService, string? ServiceName, string? ServiceCategory)> DetectBookingServiceRequest(
        MessageRoutingResponse response,
        int tenantId)
    {
        try
        {
            // Check if there are any actions
            var actionsToCheck = new List<JsonElement>();
            if (response.Action.HasValue)
            {
                actionsToCheck.Add(response.Action.Value);
            }
            if (response.Actions != null && response.Actions.Any())
            {
                actionsToCheck.AddRange(response.Actions);
            }

            foreach (var action in actionsToCheck)
            {
                // Check if this is a create_task action
                if (!action.TryGetProperty("type", out var typeElement) ||
                    typeElement.GetString() != "create_task")
                {
                    continue;
                }

                // Get the item_slug
                if (!action.TryGetProperty("item_slug", out var itemSlugElement))
                {
                    continue;
                }

                var itemSlug = itemSlugElement.GetString();
                if (string.IsNullOrEmpty(itemSlug))
                {
                    continue;
                }

                // Check if this item_slug matches a bookable service
                var service = await _context.Services
                    .Where(s => s.TenantId == tenantId &&
                               s.IsAvailable &&
                               s.Name == itemSlug)
                    .FirstOrDefaultAsync();

                if (service != null)
                {
                    // Determine if this service requires information gathering based on category
                    var bookableCategories = new[] { "LOCAL_TOURS", "MASSAGE", "CONFERENCE_ROOM", "SPA", "ACTIVITIES" };

                    if (bookableCategories.Contains(service.Category, StringComparer.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Detected bookable service: {ServiceName} in category {Category}",
                            service.Name, service.Category);
                        return (true, service.Name, service.Category);
                    }
                }
            }

            return (false, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting booking service request");
            return (false, null, null);
        }
    }

    private async Task<string> GetConversationHistoryAsync(int conversationId)
    {
        try
        {
            var messages = await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(10)
                .ToListAsync();

            var history = messages.OrderBy(m => m.CreatedAt)
                .Select(m => $"{(m.Direction == "Inbound" ? "Guest" : "Bot")}: {m.Body}")
                .ToList();

            return string.Join("\n", history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history for conversation {ConversationId}", conversationId);
            return string.Empty;
        }
    }

    private async Task<MessageRoutingResponse> HandleBookingInformationGathering(
        TenantContext tenantContext,
        Conversation conversation,
        string normalizedMessage,
        BookingInformationState? existingState = null)
    {
        try
        {
            _logger.LogInformation("Starting booking information gathering for conversation {ConversationId}", conversation.Id);

            // 1. Load available bookable services
            var availableServices = await _context.Services
                .Where(s => s.TenantId == tenantContext.TenantId && s.IsAvailable)
                .Select(s => new { s.Name, s.Category })
                .ToListAsync();

            var serviceList = availableServices
                .Select(s => (s.Name, s.Category))
                .ToList();

            _logger.LogInformation("Loaded {Count} available services for LLM selection", serviceList.Count);

            // 2. Extract information from current message
            var conversationHistory = await GetConversationHistoryAsync(conversation.Id);
            var extractedState = await _informationGatheringService.ExtractInformationFromMessage(
                normalizedMessage,
                conversationHistory,
                existingState,
                serviceList
            );

            // 2. Detect if user wants to cancel
            var intentResult = await _informationGatheringService.DetectIntent(
                normalizedMessage,
                "GatheringBookingInfo",
                extractedState.ServiceName ?? "booking"
            );

            if (intentResult.Intent == "cancel")
            {
                _logger.LogInformation("Guest cancelled booking information gathering for conversation {ConversationId}", conversation.Id);

                // Clear state, return to normal mode
                conversation.ConversationMode = "Normal";
                conversation.BookingInfoState = null;
                await _context.SaveChangesAsync();

                return new MessageRoutingResponse
                {
                    Reply = "No problem! Let me know if you need anything else.",
                    ActionType = "cancel_booking_gathering"
                };
            }

            // 3. Get missing required fields
            // Try to get service ID if we have a specific service name
            int? serviceId = null;
            if (!string.IsNullOrEmpty(extractedState.ServiceName))
            {
                var service = await _context.Services
                    .FirstOrDefaultAsync(s => s.TenantId == tenantContext.TenantId &&
                                             s.Name == extractedState.ServiceName);
                serviceId = service?.Id;
            }

            var missingFields = await _informationGatheringService.GetMissingRequiredFields(
                extractedState,
                extractedState.ServiceCategory ?? "LOCAL_TOURS",
                tenantContext.TenantId,
                serviceId
            );

            // 4. Check if we have everything
            if (missingFields.Count == 0)
            {
                _logger.LogInformation("All required information collected for conversation {ConversationId}", conversation.Id);

                // Validate the booking
                var service = await _context.Services
                    .FirstOrDefaultAsync(s => s.TenantId == tenantContext.TenantId &&
                                              s.Name == extractedState.ServiceName);

                var validationResult = await _informationGatheringService.ValidateBooking(
                    extractedState,
                    service,
                    DateTime.UtcNow,
                    _configuration["HotelSettings:Timezone"] ?? "Africa/Johannesburg"
                );

                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Booking validation failed: {ErrorMessage}", validationResult.ErrorMessage);

                    // Return validation error with suggestion
                    return new MessageRoutingResponse
                    {
                        Reply = validationResult.ErrorMessage +
                               (validationResult.SuggestedAlternative != null ? " " + validationResult.SuggestedAlternative : ""),
                        ActionType = "validation_error"
                    };
                }

                // Generate confirmation message FIRST (so we can save it in the task notes)
                var confirmationReply = $"Perfect! I've booked {extractedState.ServiceName} for {extractedState.NumberOfPeople} people on {extractedState.RequestedDate:yyyy-MM-dd}";
                if (extractedState.RequestedTime.HasValue)
                {
                    confirmationReply += $" at {extractedState.RequestedTime:HH:mm}";
                }
                confirmationReply += ". Our team will confirm the details shortly.";

                // CREATE THE TASK with confirmation message in notes
                var task = await CreateBookingTask(
                    tenantContext,
                    conversation,
                    extractedState,
                    service,
                    confirmationReply
                );

                // Clear gathering state
                conversation.ConversationMode = "Normal";
                conversation.BookingInfoState = null;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Booking task {TaskId} created successfully for conversation {ConversationId}", task.Id, conversation.Id);

                return new MessageRoutingResponse
                {
                    Reply = confirmationReply,
                    ActionType = "booking_created",
                    Action = JsonSerializer.SerializeToElement(new { taskId = task.Id })
                };
            }

            // 5. Still missing information - ask next question
            extractedState.QuestionAttempts++;
            extractedState.MissingRequiredFields = missingFields;

            var question = await _informationGatheringService.GenerateNextQuestion(
                extractedState,
                conversationHistory
            );

            // Check if exceeded question limit
            if (extractedState.ExceededQuestionLimit())
            {
                _logger.LogWarning("Exceeded question limit for conversation {ConversationId}", conversation.Id);

                // Offer human transfer
                return new MessageRoutingResponse
                {
                    Reply = question + " Would you like me to connect you with a team member who can help?",
                    ActionType = "max_questions_reached"
                };
            }

            // Save state and continue gathering
            conversation.ConversationMode = "GatheringBookingInfo";
            conversation.BookingInfoState = JsonSerializer.Serialize(extractedState);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Asking question {AttemptNum} for conversation {ConversationId}", extractedState.QuestionAttempts, conversation.Id);

            return new MessageRoutingResponse
            {
                Reply = question,
                ActionType = "gathering_info"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleBookingInformationGathering for conversation {ConversationId}", conversation.Id);

            // Reset state on error
            conversation.ConversationMode = "Normal";
            conversation.BookingInfoState = null;
            await _context.SaveChangesAsync();

            return new MessageRoutingResponse
            {
                Reply = "I'm sorry, but I encountered an issue processing your booking request. Could you please try again, or would you like me to connect you with a team member who can help?",
                ActionType = "error"
            };
        }
    }

    private async Task<MessageRoutingResponse> HandleLostItemReporting(
        TenantContext tenantContext,
        Conversation conversation,
        string normalizedMessage,
        IntentAnalysisResult intentAnalysis)
    {
        try
        {
            _logger.LogInformation("🔍 Starting lost item reporting for conversation {ConversationId}", conversation.Id);

            // 1. Extract lost item details using LLM
            var conversationHistory = await GetConversationHistoryAsync(conversation.Id);
            var extractedDetails = await ExtractLostItemDetails(normalizedMessage, conversationHistory, intentAnalysis);

            if (extractedDetails == null || string.IsNullOrEmpty(extractedDetails.ItemName))
            {
                _logger.LogWarning("Failed to extract item details from message: {Message}", normalizedMessage);
                return new MessageRoutingResponse
                {
                    Reply = "I'd be happy to help you report a lost item. Could you please tell me what item you're looking for and where you think you might have left it?",
                    ActionType = "clarification"
                };
            }

            // 2. Check guest booking status for urgency detection and shipping options
            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);
            var isCheckedOut = guestStatus.Type == GuestType.PostCheckout;
            var isCheckingOutToday = guestStatus.CheckoutDate?.ToDateTime(TimeOnly.MinValue).Date == DateTime.UtcNow.Date;

            // 3. Report the lost item to the service (create immediately, even if location missing)
            var lostItem = await _lostAndFoundService.ReportLostItemAsync(
                tenantId: tenantContext.TenantId,
                itemName: extractedDetails.ItemName,
                category: extractedDetails.Category ?? "Other",
                reporterPhone: conversation.WaUserPhone,
                conversationId: conversation.Id,
                description: extractedDetails.Description,
                color: extractedDetails.Color,
                brand: extractedDetails.Brand,
                locationLost: extractedDetails.LocationLost,
                roomNumber: guestStatus.RoomNumber,
                reporterName: guestStatus.DisplayName
            );

            _logger.LogInformation("✅ Lost item {ItemId} reported successfully: {ItemName}", lostItem.Id, lostItem.ItemName);

            // 3.5. If location is missing, ask for it and store clarification state
            _logger.LogInformation("🔍 Checking if location clarification needed. LocationLost = '{Location}'", extractedDetails.LocationLost ?? "(null)");

            if (string.IsNullOrWhiteSpace(extractedDetails.LocationLost))
            {
                _logger.LogInformation("❓ Location is missing - creating pending state and asking for clarification");

                await _conversationStateService.CreatePendingStateAsync(
                    conversationId: conversation.Id,
                    tenantId: tenantContext.TenantId,
                    stateType: ConversationStateType.AwaitingClarification,
                    entityType: Models.EntityType.LostItem,
                    entityId: lostItem.Id,
                    pendingField: Models.PendingField.Location
                );

                _logger.LogInformation("✅ Pending state created for lost item {ItemId}, returning clarification request", lostItem.Id);

                return new MessageRoutingResponse
                {
                    Reply = $"I've logged your lost {extractedDetails.ItemName}. Could you please tell me where you think you last saw it? For example, by the pool, in the restaurant, in your room, etc.",
                    ActionType = "clarification"
                };
            }

            _logger.LogInformation("✓ Location provided: '{Location}' - skipping clarification", extractedDetails.LocationLost);

            // 4. Check for immediate matches
            var potentialMatches = await _lostAndFoundService.FindPotentialMatchesAsync(tenantContext.TenantId, lostItem.Id);
            var hasHighConfidenceMatch = potentialMatches.Any(m => m.MatchScore >= 0.8m);

            // 5. Store last bot response for context
            var response = GenerateLostItemReportResponse(
                lostItem,
                extractedDetails,
                guestStatus,
                hasHighConfidenceMatch,
                isCheckedOut,
                isCheckingOutToday
            );

            await _conversationStateService.StoreLastBotResponseAsync(conversation.Id, response);

            return new MessageRoutingResponse
            {
                Reply = response,
                ActionType = "lost_item_reported"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling lost item reporting for conversation {ConversationId}", conversation.Id);
            return new MessageRoutingResponse
            {
                Reply = "I apologize, but I encountered an issue while reporting your lost item. Let me connect you with our staff who can help you directly.",
                ActionType = "error"
            };
        }
    }

    private async Task<LostItemDetails?> ExtractLostItemDetails(
        string message,
        string conversationHistory,
        IntentAnalysisResult intentAnalysis)
    {
        // Get the primary detected intent for hints
        var primaryIntent = intentAnalysis.DetectedIntents.FirstOrDefault();
        var detectedItem = primaryIntent?.EntityType;

        var extractionPrompt = $@"Extract lost item details from the guest message. Return a JSON object with these fields:

CONTEXT:
{conversationHistory}

CURRENT MESSAGE:
""{message}""

INTENT ANALYSIS HINTS:
{(detectedItem != null ? $"- Detected item: {detectedItem}" : "")}

Extract and return JSON with these fields:
{{
  ""itemName"": ""string (REQUIRED - what was lost, e.g. 'belt', 'iPhone', 'passport')"",
  ""category"": ""string (Electronics/Clothing/Jewelry/Documents/Keys/Personal/Accessories/Other)"",
  ""description"": ""string (any additional details about the item)"",
  ""color"": ""string (if mentioned)"",
  ""brand"": ""string (if mentioned, e.g. 'Apple', 'Gucci', 'Samsung')"",
  ""locationLost"": ""string or null (ONLY if EXPLICITLY stated - e.g. 'by the pool', 'in the restaurant'. Use null if not mentioned)"",
  ""whenLost"": ""string (timeline if mentioned - 'yesterday', 'this morning', 'during checkout')"",
  ""urgency"": ""string (high/medium/low based on checkout status or item importance)""
}}

CRITICAL EXTRACTION RULES:
1. Item name is REQUIRED - extract from message (e.g. ""I left my belt"" -> itemName: ""belt"")
2. Infer category from item (belt -> Clothing, phone -> Electronics, passport -> Documents)
3. locationLost: ONLY populate if EXPLICITLY mentioned in the message
   - ✅ ""I lost my phone by the pool"" -> locationLost: ""pool area""
   - ✅ ""Left my wallet in room 305"" -> locationLost: ""room 305""
   - ❌ ""I lost my phone"" -> locationLost: null (NOT mentioned - we'll ask for it)
   - ❌ ""Can't find my keys"" -> locationLost: null (NOT mentioned - we'll ask for it)
4. DO NOT infer or assume location. If not explicitly stated, use null
5. Capture color/brand if specified (""black iPhone"" -> color: ""black"", brand: ""Apple"")
6. Timeline matters: ""during checkout today"" = high urgency, ""yesterday"" = medium
7. High-value items (electronics, jewelry, documents) = higher urgency

Return ONLY valid JSON, no markdown.";

        try
        {
            var details = await _openAIService.GetStructuredResponseAsync<LostItemDetails>(extractionPrompt, temperature: 0.0);

            _logger.LogInformation("📋 Extracted lost item details: ItemName={ItemName}, Category={Category}, Location={Location}, Color={Color}, Brand={Brand}, Description={Description}",
                details?.ItemName ?? "(null)",
                details?.Category ?? "(null)",
                details?.LocationLost ?? "(null)",
                details?.Color ?? "(null)",
                details?.Brand ?? "(null)",
                details?.Description ?? "(null)");

            return details;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract lost item details from message");
            return null;
        }
    }

    private string GenerateLostItemReportResponse(
        LostItem lostItem,
        LostItemDetails details,
        GuestStatus guestStatus,
        bool hasHighConfidenceMatch,
        bool isCheckedOut,
        bool isCheckingOutToday)
    {
        var responseBuilder = new StringBuilder();

        // 1. Empathetic acknowledgment with item-specific messaging
        var itemType = details.Category?.ToLower() ?? "item";
        var empathyMessage = itemType switch
        {
            "documents" or "jewelry" => $"I completely understand how concerning it must be to misplace your {details.ItemName}. Let me help you right away.",
            "electronics" => $"I know how important your {details.ItemName} is. I'm here to help you locate it.",
            _ => $"Thank you for reporting your lost {details.ItemName}. I'm here to help you find it."
        };

        responseBuilder.AppendLine(empathyMessage);
        responseBuilder.AppendLine();

        // 2. Confirmation of what was reported
        responseBuilder.AppendLine($"✅ I've logged your report:");
        responseBuilder.AppendLine($"• Item: {details.ItemName}");
        if (!string.IsNullOrEmpty(details.Color)) responseBuilder.AppendLine($"• Color: {details.Color}");
        if (!string.IsNullOrEmpty(details.Brand)) responseBuilder.AppendLine($"• Brand: {details.Brand}");
        if (!string.IsNullOrEmpty(details.LocationLost)) responseBuilder.AppendLine($"• Last seen: {details.LocationLost}");
        responseBuilder.AppendLine($"• Reference #: LF{lostItem.Id:D6}");
        responseBuilder.AppendLine();

        // 3. Match notification if found
        if (hasHighConfidenceMatch)
        {
            responseBuilder.AppendLine("🎉 Good news! We may have already found an item matching your description. Our team is verifying it now and will contact you shortly.");
            responseBuilder.AppendLine();
        }
        else
        {
            responseBuilder.AppendLine("🔍 I'm checking our found items database now. You'll receive an instant notification if we find a match.");
            responseBuilder.AppendLine();
        }

        // 4. Retention policy based on category
        var retentionDays = itemType switch
        {
            "documents" => 365,
            "jewelry" or "electronics" => 180,
            "keys" => 90,
            _ => 90
        };

        responseBuilder.AppendLine($"📅 We'll keep your {details.ItemName} safe for {retentionDays} days once found.");

        // 5. Checkout-specific messaging
        if (isCheckingOutToday)
        {
            responseBuilder.AppendLine();
            responseBuilder.AppendLine("⚡ I see you're checking out today. I've flagged this as urgent. We'll search immediately and contact you within the hour.");
        }
        else if (isCheckedOut)
        {
            responseBuilder.AppendLine();
            responseBuilder.AppendLine("📦 Since you've already checked out, we can ship your item to you once found (shipping costs may apply). Please ensure your contact details are up to date.");
        }

        // 6. Next steps
        responseBuilder.AppendLine();
        responseBuilder.AppendLine("What happens next:");
        responseBuilder.AppendLine("1. Our housekeeping team will search the area you mentioned");
        responseBuilder.AppendLine("2. You'll get instant WhatsApp notifications if we find it");
        responseBuilder.AppendLine("3. We'll hold it securely until you can collect it");

        // 7. Verification requirements for high-value items
        if (itemType == "electronics" || itemType == "jewelry" || itemType == "documents")
        {
            responseBuilder.AppendLine();
            responseBuilder.AppendLine($"🔐 For your security, we'll need to verify ownership when you collect your {details.ItemName} (proof of identity/booking confirmation).");
        }

        return responseBuilder.ToString().Trim();
    }

    private async Task<StaffTask> CreateBookingTask(
        TenantContext tenantContext,
        Conversation conversation,
        BookingInformationState bookingInfo,
        Service? service,
        string confirmationMessage)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);

            var guestStatus = await DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

            var title = $"Booking: {bookingInfo.ServiceName}";
            var description = $"Guest booking request for {bookingInfo.ServiceName}\n";
            description += $"Number of people: {bookingInfo.NumberOfPeople ?? 1}\n";
            if (bookingInfo.RequestedDate.HasValue)
            {
                description += $"Date: {bookingInfo.RequestedDate:yyyy-MM-dd}\n";
            }
            if (bookingInfo.RequestedTime.HasValue)
            {
                description += $"Time: {bookingInfo.RequestedTime:HH:mm}\n";
            }
            if (!string.IsNullOrEmpty(bookingInfo.SpecialRequests))
            {
                description += $"Special requests: {bookingInfo.SpecialRequests}\n";
            }
            if (service != null && service.IsChargeable && service.Price.HasValue)
            {
                description += $"Price: {service.Currency}{service.Price} {service.PricingUnit ?? ""}\n";
            }

            // Map service category to valid department
            var department = MapServiceCategoryToDepartment(service?.Category);
            var priority = "Medium";

            var task = new StaffTask
            {
                TenantId = tenantContext.TenantId,
                Title = title,
                Description = description,
                Notes = confirmationMessage,
                Priority = priority,
                Status = "Open",
                Department = department,
                CreatedAt = DateTime.UtcNow,
                EstimatedCompletionTime = DateTime.UtcNow.AddHours(2),
                GuestPhone = conversation.WaUserPhone,
                GuestName = guestStatus.DisplayName ?? conversation.WaUserPhone,
                RoomNumber = guestStatus.RoomNumber,
                Quantity = bookingInfo.NumberOfPeople ?? 1
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created booking task {TaskId}: {Title} for room {RoomNumber}",
                task.Id, title, guestStatus.RoomNumber);

            return task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking task for service {ServiceName}", bookingInfo.ServiceName);
            throw;
        }
    }

    private string MapServiceCategoryToDepartment(string? category)
    {
        if (string.IsNullOrEmpty(category))
            return "FrontDesk";

        return category.ToLowerInvariant() switch
        {
            "local tours" or "transportation" or "activities" => "Concierge",
            "dining" or "food service" or "room service" => "FoodService",
            "housekeeping items" or "accommodation" => "Housekeeping",
            "wellness" or "spa" or "massage" => "Concierge",
            "business" or "conference" => "FrontDesk",
            "recreation" => "Concierge",
            _ => "FrontDesk" // Default fallback
        };
    }
}