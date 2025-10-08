using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hostr.Api.Services;

public interface IInformationGatheringService
{
    Task<BookingInformationState> ExtractInformationFromMessage(
        string message,
        string conversationHistory,
        BookingInformationState? existingState = null,
        List<(string Name, string Category)>? availableServices = null);

    Task<FollowUpDetectionResult> DetectFollowUp(
        string message,
        string recentHistory,
        string lastBotAction);

    Task<IntentDetectionResult> DetectIntent(
        string message,
        string conversationMode,
        string currentTask);

    Task<List<string>> GetMissingRequiredFields(
        BookingInformationState state,
        string serviceCategory,
        int tenantId,
        int? serviceId = null);

    Task<string> GenerateNextQuestion(
        BookingInformationState state,
        string conversationHistory);

    Task<bool> IsReadyToBook(
        BookingInformationState state,
        string serviceCategory,
        int tenantId,
        int? serviceId = null);

    Task<BookingValidationResult> ValidateBooking(
        BookingInformationState bookingInfo,
        Service? service,
        DateTime now,
        string hotelTimezone);
}

public class InformationGatheringService : IInformationGatheringService
{
    private readonly IOpenAIService _openAIService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InformationGatheringService> _logger;
    private readonly HostrDbContext _context;

    public InformationGatheringService(
        IOpenAIService openAIService,
        IConfiguration configuration,
        ILogger<InformationGatheringService> logger,
        HostrDbContext context)
    {
        _openAIService = openAIService;
        _configuration = configuration;
        _logger = logger;
        _context = context;
    }

    public async Task<BookingInformationState> ExtractInformationFromMessage(
        string message,
        string conversationHistory,
        BookingInformationState? existingState = null,
        List<(string Name, string Category)>? availableServices = null)
    {
        try
        {
            var currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var hotelTimezone = _configuration["HotelSettings:Timezone"] ?? "UTC+2";

            var existingStateJson = existingState != null
                ? JsonSerializer.Serialize(existingState, new JsonSerializerOptions { WriteIndented = false })
                : "null";

            // Build available services list for prompt
            var servicesListText = "No services provided";
            if (availableServices != null && availableServices.Any())
            {
                servicesListText = string.Join("\n", availableServices.Select(s => $"  - {s.Name} ({s.Category})"));
            }

            var prompt = $@"You are extracting booking information from a guest message.

CONTEXT:
- Current conversation history: {conversationHistory}
- Previously extracted information: {existingStateJson}
- Guest's new message: ""{message}""
- Today's date: {currentDate}
- Hotel timezone: {hotelTimezone}

AVAILABLE SERVICES (you MUST choose from this list):
{servicesListText}

TASK: Extract ALL booking-related information and return JSON:

{{
  ""serviceName"": ""exact service name from AVAILABLE SERVICES list or null if not found"",
  ""serviceCategory"": ""LOCAL_TOURS|MASSAGE|CONFERENCE_ROOM|HOUSEKEEPING_ITEMS|DINING|null"",
  ""numberOfPeople"": <number or null>,
  ""requestedDate"": ""YYYY-MM-DD or null"",
  ""requestedTime"": ""HH:MM or null"",
  ""specialRequests"": ""any special requests or null"",
  ""extractionConfidence"": 0.0-1.0,
  ""reasoning"": ""explain what you extracted and why"",
  ""citations"": {{
    ""serviceName"": ""exact quote from message or 'inferred from context'"",
    ""numberOfPeople"": ""exact quote from message or null"",
    ""requestedDate"": ""exact quote from message or null"",
    ""requestedTime"": ""exact quote from message or null""
  }},
  ""assumptions"": [""list any assumptions you made while extracting""],
  ""uncertainties"": [""list any information you're uncertain about""]
}}

REFLECTION REQUIREMENTS (CRITICAL - prevents hallucinations):

1. CITATIONS - For each extracted field, provide exact quote:
   - serviceName: Quote the exact phrase that mentioned the service OR state 'inferred from context: [explain]'
   - numberOfPeople: Quote exact phrase like 'Guest said: for 5 people' OR null if not mentioned
   - requestedDate: Quote exact phrase like 'Guest said: tomorrow' OR null
   - requestedTime: Quote exact phrase like 'Guest said: morning' OR null

2. ASSUMPTIONS - List every assumption you make:
   - Example: ""Assumed 'we' means 2 people based on typical usage""
   - Example: ""Assumed 'safari' refers to the Kruger National Park service""
   - Example: ""Assumed 'morning' means 09:00 based on hotel timezone""
   - If no assumptions, use empty array []

3. UNCERTAINTIES - Explicitly state what you DON'T know:
   - Example: ""Not certain if 'family' means 3, 4, or 5 people""
   - Example: ""Unclear if 'next week' means Monday or the guest's check-in date""
   - Example: ""Cannot determine exact service - could be either spa or massage""
   - Lower extractionConfidence (0.5-0.7) when uncertainties exist
   - If information is missing or ambiguous, state it clearly in uncertainties

4. ""I DON'T KNOW"" ENFORCEMENT:
   - If a field cannot be determined from the message, set it to null
   - Add to uncertainties: ""Need to ask guest about [missing field]""
   - NEVER guess or make up information not present in the message or conversation history
   - Confidence should be < 0.6 if multiple fields require clarification

CRITICAL SERVICE NAME RULE:
- You MUST select serviceName from the AVAILABLE SERVICES list above
- Match the guest's intent to the closest service in the list
- Use the EXACT name from the list (e.g., if guest says ""Kruger Safari"" and list has ""Kruger National Park Day Trip"", use ""Kruger National Park Day Trip"")
- If no close match exists, set serviceName to null

SMART EXTRACTION RULES:

1. PEOPLE COUNT:
   - ""me and my wife"" -> numberOfPeople: 2
   - ""my family"" -> numberOfPeople: null (ambiguous, need to ask)
   - ""just me"" -> numberOfPeople: 1
   - ""we"" -> numberOfPeople: 2
   - ""our group"" -> numberOfPeople: null (ambiguous)
   - ""5 people"" -> numberOfPeople: 5

2. DATE PARSING:
   - ""tomorrow"" -> calculate from {currentDate}
   - ""next Tuesday"" -> calculate exact date
   - ""this Friday"" -> calculate exact date
   - ""in 3 days"" -> calculate from {currentDate}
   - ""today"" -> {currentDate}

3. TIME PARSING:
   - ""morning"" -> ""09:00""
   - ""afternoon"" -> ""14:00""
   - ""evening"" -> ""18:00""
   - ""night"" -> ""20:00""
   - ""ASAP"" / ""as soon as possible"" -> null (means earliest available)
   - ""6am"" -> ""06:00""
   - ""2:30pm"" -> ""14:30""

4. HANDLING CORRECTIONS/UPDATES:
   If the message contains corrections (e.g., ""5 people... no wait, 3 people""):
   - Use the MOST RECENT value mentioned
   - Set extractionConfidence lower (0.7) to indicate correction happened

5. CONTEXT AWARENESS:
   - If previousState has serviceName and message is just ""for 5 people"":
     * Keep the existing serviceName
     * Extract numberOfPeople: 5
   - If message says ""book it"" or ""I want that"", check conversationHistory for most recent service

6. SERVICE CATEGORY CLASSIFICATION:
   - Tours, safaris, excursions -> LOCAL_TOURS
   - Massages, spa treatments -> MASSAGE
   - Meeting rooms, conference facilities -> CONFERENCE_ROOM
   - Towels, pillows, amenities -> HOUSEKEEPING_ITEMS
   - Food, meals, room service -> DINING

7. MERGE WITH EXISTING STATE:
   - If existingState has fields and new message doesn't override them, keep existing values
   - Only update fields that are explicitly mentioned in the new message

Return ONLY valid JSON, no other text.";

            var extracted = await _openAIService.GetStructuredResponseAsync<ExtractionResponse>(prompt, 0.3);
            if (extracted == null)
            {
                _logger.LogWarning("Failed to parse LLM extraction response");
                return existingState ?? new BookingInformationState();
            }

            // Build result state
            var state = existingState ?? new BookingInformationState();

            if (extracted.ServiceName != null) state.ServiceName = extracted.ServiceName;
            if (extracted.ServiceCategory != null) state.ServiceCategory = extracted.ServiceCategory;
            if (extracted.NumberOfPeople.HasValue) state.NumberOfPeople = extracted.NumberOfPeople;
            if (extracted.RequestedDate != null && DateOnly.TryParse(extracted.RequestedDate, out var date))
                state.RequestedDate = date;
            if (extracted.RequestedTime != null && TimeOnly.TryParse(extracted.RequestedTime, out var time))
                state.RequestedTime = time;
            if (extracted.SpecialRequests != null) state.SpecialRequests = extracted.SpecialRequests;

            state.ExtractionConfidence = extracted.ExtractionConfidence;
            state.ExtractionReasoning = extracted.Reasoning;
            state.LastUpdated = DateTime.UtcNow;

            // Update provided fields list
            state.ProvidedFields.Clear();
            if (state.ServiceName != null) state.ProvidedFields.Add("ServiceName");
            if (state.ServiceCategory != null) state.ProvidedFields.Add("ServiceCategory");
            if (state.NumberOfPeople.HasValue) state.ProvidedFields.Add("NumberOfPeople");
            if (state.RequestedDate.HasValue) state.ProvidedFields.Add("RequestedDate");
            if (state.RequestedTime.HasValue) state.ProvidedFields.Add("RequestedTime");

            _logger.LogInformation("Extracted booking information: {State}", JsonSerializer.Serialize(state));

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting information from message: {Message}", message);
            return existingState ?? new BookingInformationState();
        }
    }

    public async Task<FollowUpDetectionResult> DetectFollowUp(
        string message,
        string recentHistory,
        string lastBotAction)
    {
        try
        {
            var prompt = $@"You are determining if a message is a follow-up to a previous booking offer.

CONVERSATION HISTORY (last 5 messages):
{recentHistory}

CURRENT MESSAGE: ""{message}""

LAST BOT ACTION: {lastBotAction}

TASK: Determine if this is a follow-up. Return JSON:

{{
  ""isFollowUp"": true/false,
  ""referenceType"": ""pronoun|confirmation|quantity_update|null"",
  ""referencedService"": ""service name from history or null"",
  ""extractedQuantity"": <number or null>,
  ""confidence"": 0.0-1.0,
  ""reasoning"": ""explain your analysis""
}}

DETECTION RULES:

1. PRONOUN REFERENCES:
   - ""book it"", ""I want it"", ""reserve it"", ""that one"", ""this one""
   - If found, identify what ""it"" refers to from conversation history

2. CONFIRMATION PATTERNS:
   - ""yes"", ""okay"", ""sure"", ""I'll take it""
   - After bot made an offer or asked confirmation

3. QUANTITY UPDATES:
   - ""for 5 people"", ""for 3"", ""5 of us""
   - After bot described a service

4. SERVICE REFERENCES:
   - ""the first one"", ""the safari"", ""that tour""
   - Link to specific service from history

5. AMBIGUITY HANDLING:
   - If multiple services were mentioned and reference is unclear, set confidence < 0.5
   - Note the ambiguity in reasoning

Return ONLY valid JSON.";

            var result = await _openAIService.GetStructuredResponseAsync<FollowUpDetectionResult>(prompt, 0.3);

            if (result == null)
            {
                return new FollowUpDetectionResult { IsFollowUp = false, Confidence = 0 };
            }

            _logger.LogInformation("Follow-up detection: {Result}", JsonSerializer.Serialize(result));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting follow-up for message: {Message}", message);
            return new FollowUpDetectionResult { IsFollowUp = false, Confidence = 0 };
        }
    }

    public async Task<IntentDetectionResult> DetectIntent(
        string message,
        string conversationMode,
        string currentTask)
    {
        try
        {
            var prompt = $@"You are detecting if the guest wants to cancel or switch topics.

CONVERSATION CONTEXT:
Current Mode: {conversationMode}
Current Task: {currentTask}
Guest Message: ""{message}""

TASK: Analyze the message. Return JSON:

{{
  ""intent"": ""cancel|topic_switch|continue|other"",
  ""confidence"": 0.0-1.0,
  ""newTopic"": ""what they're asking about or null"",
  ""reasoning"": ""explain your analysis""
}}

CANCELLATION PATTERNS (intent=""cancel""):
- ""cancel"", ""never mind"", ""forget it"", ""don't worry""
- ""actually no"", ""changed my mind"", ""not anymore""
- ""stop"", ""abort""

TOPIC SWITCH PATTERNS (intent=""topic_switch""):
- Guest asks unrelated question while booking in progress
- Examples:
  * During tour booking, asks: ""What time is breakfast?""
  * During massage booking, asks: ""Do you have a pool?""
  * During any booking, asks: ""Can I get towels?""

CONTINUE PATTERNS (intent=""continue""):
- Answering the current question directly
- Providing requested information
- Following up on current topic

Return ONLY valid JSON.";

            var result = await _openAIService.GetStructuredResponseAsync<IntentDetectionResult>(prompt, 0.3);

            if (result == null)
            {
                return new IntentDetectionResult { Intent = "other", Confidence = 0.5 };
            }

            _logger.LogInformation("Intent detection: {Result}", JsonSerializer.Serialize(result));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting intent for message: {Message}", message);
            return new IntentDetectionResult { Intent = "other", Confidence = 0.5 };
        }
    }

    public async Task<List<string>> GetMissingRequiredFields(
        BookingInformationState state,
        string serviceCategory,
        int tenantId,
        int? serviceId = null)
    {
        try
        {
            // Normalize category to handle legacy names
            var normalizedCategory = ServiceCategoryConstants.NormalizeCategory(serviceCategory);
            _logger.LogInformation("GetMissingRequiredFields: Normalized '{Original}' → '{Normalized}'",
                serviceCategory, normalizedCategory);

            // Try to load custom requirements from business rules first
            var customRequiredFields = await LoadRequiredFieldsFromBusinessRules(tenantId, normalizedCategory, serviceId);

            // Build dynamic service requirements section
            string serviceRequirementsSection;
            if (customRequiredFields != null)
            {
                _logger.LogInformation("Using custom business rules for tenant {TenantId}, category {Category}",
                    tenantId, normalizedCategory);
                serviceRequirementsSection = $"- {normalizedCategory.ToUpper()}: {string.Join(", ", customRequiredFields)}";
            }
            else
            {
                _logger.LogInformation("Using default requirements for category {Category}", normalizedCategory);
                // Fall back to hardcoded defaults
                serviceRequirementsSection = @"- LOCAL_TOURS: serviceName, numberOfPeople, requestedDate
- MASSAGE: serviceName, numberOfPeople, requestedDate, requestedTime
- SPA: serviceName, numberOfPeople, requestedDate, requestedTime
- CONFERENCE_ROOM: requestedDate, requestedTime, numberOfPeople
- DINING: serviceName, numberOfPeople, requestedDate, requestedTime
- ACTIVITIES: serviceName, numberOfPeople, requestedDate
- HOUSEKEEPING_ITEMS: serviceName only";
            }

            var stateJson = JsonSerializer.Serialize(state);

            var prompt = $@"You are checking if we have all required information for a booking.

SERVICE REQUIREMENTS:
{serviceRequirementsSection}

CURRENT STATE:
Service Category: {normalizedCategory}
Extracted Information: {stateJson}

TASK: Return JSON:
{{
  ""missingFields"": [""field1"", ""field2""],
  ""readyToBook"": true/false,
  ""reasoning"": ""explain what's missing or why ready""
}}

RULES:
- If serviceName is vague (like ""tour"" without specific tour), count as missing
- If numberOfPeople is ambiguous or null when required, count as missing
- Optional fields (specialRequests) don't count as missing
- Use exact field names: ServiceName, NumberOfPeople, RequestedDate, RequestedTime

Return ONLY valid JSON.";

            var response = await _openAIService.GetStructuredResponseAsync<MissingFieldsResponse>(prompt, 0.3);

            if (response == null || response.MissingFields == null)
            {
                return new List<string>();
            }

            state.MissingRequiredFields = response.MissingFields.ToList();
            return response.MissingFields.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting missing fields for category: {Category}", serviceCategory);
            return new List<string>();
        }
    }

    private async Task<string[]?> LoadRequiredFieldsFromBusinessRules(
        int tenantId,
        string serviceCategory,
        int? serviceId)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantId);

            var query = _context.ServiceBusinessRules
                .Where(r => r.TenantId == tenantId &&
                           r.IsActive &&
                           r.RuleType == "required_booking_fields");

            // If serviceId is provided, look for service-specific rules first
            if (serviceId.HasValue)
            {
                var serviceSpecificRule = await query
                    .Where(r => r.ServiceId == serviceId.Value)
                    .FirstOrDefaultAsync();

                if (serviceSpecificRule != null && !string.IsNullOrEmpty(serviceSpecificRule.RuleValue))
                {
                    _logger.LogInformation("Found service-specific business rule for ServiceId {ServiceId}", serviceId.Value);
                    return serviceSpecificRule.RuleValue
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .ToArray();
                }
            }

            // Look for category-level rules by checking if RuleKey matches the service category
            var categoryRule = await query
                .Where(r => r.RuleKey.ToLower() == serviceCategory.ToLower())
                .FirstOrDefaultAsync();

            if (categoryRule != null && !string.IsNullOrEmpty(categoryRule.RuleValue))
            {
                _logger.LogInformation("Found category-level business rule for category {Category}", serviceCategory);
                return categoryRule.RuleValue
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .ToArray();
            }

            return null; // Fall back to defaults
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading business rules for tenant {TenantId}, category {Category}",
                tenantId, serviceCategory);
            return null; // Fall back to defaults on error
        }
    }

    public async Task<string> GenerateNextQuestion(
        BookingInformationState state,
        string conversationHistory)
    {
        try
        {
            var stateJson = JsonSerializer.Serialize(state);
            var missingFields = string.Join(", ", state.MissingRequiredFields);

            var prompt = $@"You are generating the next clarifying question for a booking.

CONVERSATION HISTORY:
{conversationHistory}

CURRENT BOOKING STATE:
{stateJson}

MISSING REQUIRED FIELDS:
{missingFields}

QUESTION ATTEMPTS SO FAR: {state.QuestionAttempts} / {BookingInformationState.MAX_QUESTIONS}

TASK: Generate ONE clarifying question to ask the guest.

RULES:
1. Ask for the MOST IMPORTANT missing field first:
   - Priority 1: serviceName (if specific service not identified)
   - Priority 2: numberOfPeople (for services that need it)
   - Priority 3: requestedDate
   - Priority 4: requestedTime

2. Be conversational and natural, not robotic

3. If this is attempt #{state.QuestionAttempts + 1} and {BookingInformationState.MAX_QUESTIONS} is max:
   - If last attempt, offer to connect with staff

4. Show available options when relevant for serviceName:
   - For LOCAL_TOURS: ""Which tour would you like? We offer Kruger Safari, Blyde Canyon Tour, Cultural Village Experience, and Waterfalls Tour.""
   - For MASSAGE: ""Which type of massage? We offer Traditional Massage, Hot Stone Massage, Deep Tissue Massage, and Aromatherapy.""
   - For SPA: ""Which spa service? We offer spa packages, facials, body treatments, and wellness programs.""
   - For CONFERENCE_ROOM: ""Which room would you like? We have small meeting rooms and large conference facilities.""
   - For DINING: ""Which restaurant? We have fine dining, casual dining, and private dining options.""

5. Use context from conversation history to make question more natural

Return ONLY valid JSON with format: {{ ""question"": ""your question here"" }}";

            var response = await _openAIService.GetStructuredResponseAsync<QuestionResponse>(prompt, 0.7); // Higher temp for natural language

            if (response == null || string.IsNullOrEmpty(response.Question))
            {
                return "Could you provide more details about your booking?";
            }

            _logger.LogInformation("Generated question: {Question}", response.Question);
            return response.Question;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating next question");
            return "Could you provide more details about your booking?";
        }
    }

    public async Task<bool> IsReadyToBook(
        BookingInformationState state,
        string serviceCategory,
        int tenantId,
        int? serviceId = null)
    {
        var missingFields = await GetMissingRequiredFields(state, serviceCategory, tenantId, serviceId);
        return missingFields.Count == 0;
    }

    public async Task<BookingValidationResult> ValidateBooking(
        BookingInformationState bookingInfo,
        Service? service,
        DateTime now,
        string hotelTimezone)
    {
        try
        {
            var serviceJson = service != null
                ? JsonSerializer.Serialize(new
                {
                    service.Name,
                    service.Category,
                    service.Price,
                    service.RequiresAdvanceBooking,
                    service.AvailableHours,
                    service.IsAvailable
                })
                : "null";

            var bookingJson = JsonSerializer.Serialize(bookingInfo);

            var prompt = $@"You are validating if a booking can be created.

SERVICE DETAILS:
{serviceJson}

BOOKING REQUEST:
{bookingJson}

CURRENT DATE/TIME: {now:yyyy-MM-dd HH:mm}
HOTEL TIMEZONE: {hotelTimezone}

TASK: Validate the booking. Return JSON:

{{
  ""isValid"": true/false,
  ""errorType"": ""capacity|advance_booking|availability|date_past|null"",
  ""errorMessage"": ""guest-friendly error message or null"",
  ""suggestedAlternative"": ""helpful alternative or null"",
  ""reasoning"": ""explain validation logic""
}}

VALIDATION RULES:

1. SERVICE AVAILABILITY:
   - If service is null or isAvailable = false:
     * isValid: false
     * errorType: ""availability""
     * errorMessage: ""I'm sorry, that service is currently unavailable""

2. PAST DATES:
   - If requestedDate < currentDate:
     * isValid: false
     * errorType: ""date_past""
     * errorMessage: ""That date has passed. Did you mean tomorrow or next week?""

3. ADVANCE BOOKING (if service requires it):
   - Calculate hours between now and requestedDateTime
   - If insufficient: isValid=false, errorType=""advance_booking""
   - Suggest earliest available time

4. CAPACITY (if service has max capacity and booking exceeds it):
   - isValid: false
   - errorType: ""capacity""
   - errorMessage: ""Our [service] accommodates up to [max] people""
   - suggestedAlternative: ""Would you like to book for [max] people?""

Return ONLY valid JSON.";

            var result = await _openAIService.GetStructuredResponseAsync<BookingValidationResult>(prompt, 0.3);

            if (result == null)
            {
                return new BookingValidationResult
                {
                    IsValid = true,
                    Reasoning = "Default validation - no issues found"
                };
            }

            _logger.LogInformation("Booking validation: {Result}", JsonSerializer.Serialize(result));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating booking");
            return new BookingValidationResult
            {
                IsValid = true,
                Reasoning = "Validation error, defaulting to valid"
            };
        }
    }

    private ServiceRequirements GetServiceRequirements(string category)
    {
        var requirements = _configuration.GetSection($"ServiceRequirements:{category}").Get<ServiceRequirements>();
        return requirements ?? new ServiceRequirements();
    }

    // Helper classes for JSON deserialization
    private class ExtractionResponse
    {
        public string? ServiceName { get; set; }
        public string? ServiceCategory { get; set; }
        public int? NumberOfPeople { get; set; }
        public string? RequestedDate { get; set; }
        public string? RequestedTime { get; set; }
        public string? SpecialRequests { get; set; }
        public double ExtractionConfidence { get; set; }
        public string? Reasoning { get; set; }

        // Reflection fields for hallucination prevention
        public Dictionary<string, string?>? Citations { get; set; }
        public string[]? Assumptions { get; set; }
        public string[]? Uncertainties { get; set; }
    }

    private class MissingFieldsResponse
    {
        public string[] MissingFields { get; set; } = Array.Empty<string>();
        public bool ReadyToBook { get; set; }
        public string? Reasoning { get; set; }
    }

    private class QuestionResponse
    {
        public string Question { get; set; } = string.Empty;
    }
}
