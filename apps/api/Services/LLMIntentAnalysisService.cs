using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;
using System.Text;

namespace Hostr.Api.Services;

public interface ILLMIntentAnalysisService
{
    Task<IntentAnalysisResult> AnalyzeMessageIntentAsync(
        string message,
        TenantContext tenantContext,
        int conversationId,
        GuestStatus guestStatus);
}

public class DetectedIntent
{
    public string Intent { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SpecificityLevel { get; set; } = string.Empty;
    public List<string> AvailableOptions { get; set; } = new();
    public string EntityType { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int? RequestedQuantity { get; set; }
}

public class IntentAnalysisResult
{
    public bool IsAmbiguous { get; set; }
    public string Intent { get; set; } = string.Empty; // PRIMARY intent for backward compatibility
    public string Category { get; set; } = string.Empty; // PRIMARY category for backward compatibility
    public string SpecificityLevel { get; set; } = string.Empty; // SPECIFIC, VAGUE, UNCLEAR
    public List<string> AvailableOptions { get; set; } = new();
    public string ClarificationQuestion { get; set; } = string.Empty;
    public string WarmResponse { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    // NEW: Support for multiple intents
    public List<DetectedIntent> DetectedIntents { get; set; } = new();
    public bool HasMultipleIntents => DetectedIntents.Count > 1;
}

public class LLMIntentAnalysisService : ILLMIntentAnalysisService
{
    private readonly HostrDbContext _context;
    private readonly IOpenAIService _openAIService;
    private readonly IMenuService _menuService;
    private readonly ITenantCacheService _tenantCache;
    private readonly IUpsellRecommendationService _upsellService;
    private readonly ILogger<LLMIntentAnalysisService> _logger;

    public LLMIntentAnalysisService(
        HostrDbContext context,
        IOpenAIService openAIService,
        IMenuService menuService,
        ITenantCacheService tenantCache,
        IUpsellRecommendationService upsellService,
        ILogger<LLMIntentAnalysisService> logger)
    {
        _context = context;
        _openAIService = openAIService;
        _menuService = menuService;
        _tenantCache = tenantCache;
        _upsellService = upsellService;
        _logger = logger;
    }

    public async Task<IntentAnalysisResult> AnalyzeMessageIntentAsync(
        string message,
        TenantContext tenantContext,
        int conversationId,
        GuestStatus guestStatus)
    {
        try
        {
            // 1. Build comprehensive hotel context
            var hotelContext = await BuildHotelContextAsync(tenantContext.TenantId);

            // 2. Get conversation history for context
            var conversationHistory = await GetRecentConversationHistoryAsync(conversationId);

            // 3. Get guest context
            var guestContext = await BuildGuestContextAsync(conversationId, guestStatus);

            // 4. Create the analysis prompt
            var analysisPrompt = BuildIntentAnalysisPrompt(
                message,
                hotelContext,
                conversationHistory,
                guestContext);

            // DIAGNOSTIC: Log the full prompt and hotel context
            _logger.LogWarning("===== INTENT ANALYSIS DIAGNOSTIC =====");
            _logger.LogWarning("Message: {Message}", message);
            _logger.LogWarning("Available Services in Context: {Services}", JsonSerializer.Serialize(hotelContext.AvailableServices));
            _logger.LogWarning("Full Prompt Being Sent to OpenAI:\n{Prompt}", analysisPrompt);
            _logger.LogWarning("=======================================");

            // 5. Call OpenAI for intent analysis
            var analysisResponse = await _openAIService.GetStructuredResponseAsync<IntentAnalysisResponse>(
                analysisPrompt,
                temperature: 0.3 // Lower temperature for more consistent analysis
            );

            // 6. Process and enrich the response with warm hospitality
            var result = await ProcessAnalysisResponseAsync(
                analysisResponse,
                hotelContext,
                message,
                guestContext);

            _logger.LogInformation(
                "Intent analysis completed for message: '{Message}'. " +
                "Intent: {Intent}, IsAmbiguous: {IsAmbiguous}, Confidence: {Confidence}",
                message, result.Intent, result.IsAmbiguous, result.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing message intent for conversation {ConversationId}", conversationId);

            // Fallback to simple analysis
            return GetFallbackAnalysis(message);
        }
    }

    private async Task<HotelConfigurationContext> BuildHotelContextAsync(int tenantId)
    {
        var context = new HotelConfigurationContext
        {
            TenantId = tenantId
        };

        // Get menu items and categories
        var menuCategories = await _context.MenuCategories
            .Where(mc => mc.TenantId == tenantId && mc.IsActive)
            .Include(mc => mc.MenuItems.Where(mi => mi.IsAvailable))
            .ToListAsync();

        context.MenuCategories = menuCategories.Select(mc => new
        {
            Name = mc.Name,
            Items = mc.MenuItems.Select(mi => new
            {
                mi.Name,
                mi.Description,
                Price = mi.PriceCents,
                mi.Currency,
                mi.Allergens,
                mi.IsVegetarian,
                mi.IsVegan,
                mi.IsGlutenFree
            }).ToList()
        }).ToList();

        // Get available services
        var services = await _context.Services
            .Where(s => s.TenantId == tenantId && s.IsAvailable)
            .Select(s => new
            {
                s.Name,
                s.Description,
                s.Category,
                OperatingHours = s.AvailableHours,
                s.ContactInfo,
                RequiresReservation = s.RequiresAdvanceBooking,
                s.Price
            })
            .ToListAsync();

        context.AvailableServices = services;

        // Get request items (towels, toiletries, etc.)
        var requestItems = await _context.RequestItems
            .Where(ri => ri.TenantId == tenantId)
            .Select(ri => new
            {
                ri.Name,
                ri.Category,
                ri.Department,
                ri.Purpose,
                ri.IsAvailable
            })
            .ToListAsync();

        context.RequestItems = requestItems;

        // Get business info (operating hours, policies from BusinessInfo table)
        var businessInfos = await _context.BusinessInfo
            .Where(bi => bi.TenantId == tenantId && bi.IsActive)
            .ToListAsync();

        if (businessInfos.Any())
        {
            context.BusinessHours = businessInfos.Select(bi => new
            {
                Category = bi.Category,
                Title = bi.Title,
                Content = bi.Content,
                Tags = bi.Tags
            }).ToList();
        }

        // Get configured departments
        var departments = await _context.TenantDepartments
            .Where(td => td.TenantId == tenantId && td.IsActive)
            .Select(td => new
            {
                Name = td.DepartmentName,
                td.Description,
                td.ContactInfo,
                td.WorkingHours
            })
            .ToListAsync();

        context.Departments = departments;

        // Get hotel information
        var hotelInfo = await _context.HotelInfos
            .Where(hi => hi.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (hotelInfo != null)
        {
            context.HotelName = $"{hotelInfo.City} {hotelInfo.Category}";
            context.HotelType = hotelInfo.Category; // luxury, premium, comfort, etc.
            context.Amenities = !string.IsNullOrEmpty(hotelInfo.Features)
                ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(hotelInfo.Features) ?? new List<string>()
                : new List<string>();

            // Add check-in/check-out times if available
            context.CheckInTime = hotelInfo.CheckInTime;
            context.CheckOutTime = hotelInfo.CheckOutTime;

            // Load hotel policies
            context.SmokingPolicy = hotelInfo.SmokingPolicy;
            context.PetPolicy = hotelInfo.PetPolicy;
            context.CancellationPolicy = hotelInfo.CancellationPolicy;
            context.ChildPolicy = hotelInfo.ChildPolicy;
        }

        return context;
    }

    private async Task<string> GetRecentConversationHistoryAsync(int conversationId)
    {
        var recentMessages = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .Select(m => new { m.Direction, m.Body })
            .ToListAsync();

        if (!recentMessages.Any())
            return "No previous conversation history.";

        var history = new StringBuilder();
        foreach (var msg in recentMessages.AsEnumerable().Reverse())
        {
            var speaker = msg.Direction == "Inbound" ? "Guest" : "Hotel";
            history.AppendLine($"{speaker}: {msg.Body}");
        }

        return history.ToString();
    }

    private async Task<string> GenerateGreetingResponseAsync(
        HotelConfigurationContext hotelContext,
        GuestContextInfo guestContext,
        int conversationId)
    {
        try
        {
            // Get tenant ID from conversation
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                return "Hello! How may I assist you today?";
            }

            var tenantId = conversation.TenantId;

            // Get the actual tenant name from Tenants table
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId);
            var tenantName = tenant?.Name ?? "our property";

            // Fetch greeting templates from WelcomeMessages table
            var welcomeTemplates = await _context.WelcomeMessages
                .Where(w => w.TenantId == tenantId && w.MessageType == "greeting" && w.IsActive)
                .OrderBy(w => w.DisplayOrder)
                .ToListAsync();

            string baseTemplate;
            if (welcomeTemplates.Any())
            {
                // Select a template based on conversation ID for rotation
                var templateIndex = conversationId % welcomeTemplates.Count;
                var selectedTemplate = welcomeTemplates[templateIndex];
                baseTemplate = selectedTemplate.Template;
            }
            else
            {
                // Fallback template if no welcome messages are configured
                baseTemplate = "Hello{guestName}! Welcome to {tenantName}! 🏨 I'm your virtual concierge and I'm here to make your stay absolutely wonderful. How can I assist you today?";
            }

            // Personalize the template
            string personalizedGreeting = baseTemplate.Replace("{tenantName}", tenantName);

            // Add guest name personalization if available
            if (!string.IsNullOrEmpty(guestContext.GuestName))
            {
                // Replace {guestName} placeholder
                if (personalizedGreeting.Contains("{guestName}"))
                {
                    personalizedGreeting = personalizedGreeting.Replace("{guestName}", $" {guestContext.GuestName}");
                }
                else
                {
                    // Insert guest name after the first greeting word (Hello, Hi, etc.)
                    var greetingWords = new[] { "Hello", "Hi", "Good to see you", "Hi there" };
                    foreach (var word in greetingWords)
                    {
                        if (personalizedGreeting.StartsWith(word))
                        {
                            personalizedGreeting = personalizedGreeting.Replace(word, $"{word} {guestContext.GuestName}");
                            break;
                        }
                    }
                }
            }
            else
            {
                // Remove {guestName} placeholder if no guest name available
                personalizedGreeting = personalizedGreeting.Replace("{guestName}", "");
            }

            // Add high-value paid services highlight (non-intrusive upsell)
            var highValueServices = await _upsellService.GetTopHighValueServicesAsync(tenantId, limit: 2);
            if (highValueServices.Any())
            {
                var serviceList = string.Join(" and ", highValueServices.Select(s =>
                    $"{s.Name} (R{s.Price})"));
                personalizedGreeting += $"\n\n✨ Don't miss our popular services: {serviceList}";
            }

            return personalizedGreeting;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating greeting response for conversation {ConversationId}", conversationId);
            return "Hello! How may I assist you today?";
        }
    }

    private async Task<GuestContextInfo> BuildGuestContextAsync(int conversationId, GuestStatus guestStatus)
    {
        var guestInfo = new GuestContextInfo
        {
            Status = guestStatus.ToString(),
            ConversationId = conversationId
        };

        // Get booking information by phone number
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation != null)
        {
            // Try to find booking by phone number
            var booking = await _context.Bookings
                .Where(b => b.TenantId == conversation.TenantId &&
                           b.Phone == conversation.WaUserPhone)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            if (booking != null)
            {
                guestInfo.RoomNumber = booking.RoomNumber;
                guestInfo.CheckInDate = booking.CheckinDate.ToDateTime(TimeOnly.MinValue);
                guestInfo.CheckOutDate = booking.CheckoutDate.ToDateTime(TimeOnly.MinValue);
                guestInfo.GuestName = booking.GuestName;
            }
        }

        // Get recent requests
        var recentTasks = await _context.StaffTasks
            .Where(t => t.ConversationId == conversationId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(3)
            .Select(t => new { t.Title, t.Status })
            .ToListAsync();

        guestInfo.RecentRequests = recentTasks;

        return guestInfo;
    }

    private string BuildIntentAnalysisPrompt(
        string message,
        HotelConfigurationContext hotelContext,
        string conversationHistory,
        GuestContextInfo guestContext)
    {
        var prompt = $@"You are analyzing a guest message for {hotelContext.HotelName ?? "a hospitality property"}.
Your job is ONLY to analyze the intent and identify what the guest is asking for - DO NOT generate responses.

HOTEL CONFIGURATION:
{JsonSerializer.Serialize(hotelContext, new JsonSerializerOptions { WriteIndented = true })}

GUEST CONTEXT:
- Status: {guestContext.Status}
- Room: {guestContext.RoomNumber ?? "Not specified"}
- Guest Name: {guestContext.GuestName ?? "Not specified"}

GUEST MESSAGE: ""{message}""

MULTI-INTENT DETECTION:
If the guest message contains MULTIPLE requests or questions (e.g., ""I need towels and also what time is breakfast?""), you MUST extract ALL intents separately.

Analyze this message and respond with the following JSON structure:
{{
    ""intents"": [
        {{
            ""intent"": ""REQUEST_ITEM | REQUEST_SERVICE | INQUIRY | COMPLAINT | BOOKING_CHANGE | GREETING | LOST_AND_FOUND | OTHER"",
            ""category"": ""BEVERAGE | FOOD | HOUSEKEEPING | MAINTENANCE | AMENITIES | DINING | LOST_ITEMS | OTHER"",
            ""specificityLevel"": ""SPECIFIC | VAGUE | UNCLEAR"",
            ""availableOptions"": [""List ONLY the actual available options from hotel configuration that match the request""],
            ""entityType"": ""The specific entity being requested (e.g., 'towels', 'breakfast time', 'pool')"",
            ""originalText"": ""The exact portion of the message related to this intent"",
            ""confidence"": 0.0-1.0,
            ""requestedQuantity"": <number or null>,
            ""citations"": {{
                ""intent"": ""Quote the exact phrase that triggered this intent classification"",
                ""category"": ""Quote or explain how you determined the category"",
                ""entity"": ""Quote the exact phrase for the entity or 'inferred from context'""
            }},
            ""assumptions"": [""List any assumptions made for this intent""],
            ""uncertainties"": [""List any uncertainties about this intent classification""]
        }}
    ],
    ""isAmbiguous"": true/false,
    ""ambiguityReason"": ""Why is this ambiguous (if applicable)"",
    ""clarificationNeeded"": {{
        ""required"": true/false,
        ""type"": ""OPTION_SELECTION | QUANTITY | TIME | DETAILS | NONE"",
        ""question"": ""Warm clarification question if needed - CRITICAL: NEVER use vague language like 'and more', 'etc.', 'such as', or similar phrases. List ALL available options explicitly from the configuration.""
    }}
}}

REFLECTION REQUIREMENTS (CRITICAL - prevents hallucinations):

1. CITATIONS - For each intent, provide exact quotes from the guest message:
   - intent: Quote the EXACT phrase that indicates REQUEST_ITEM vs INQUIRY vs other intent types
     * Example: Guest said ""I need towels"" -> intent citation: ""I need towels""
     * Example: Guest said ""What services do you have?"" -> intent citation: ""What services do you have?""
   - category: Explain how you determined HOUSEKEEPING vs DINING vs other category
     * Example: ""Determined HOUSEKEEPING from the word 'towels'""
     * Example: ""Inferred DINING from context: breakfast is a meal service""
   - entity: Quote the exact entity mentioned OR state 'inferred from context'
     * Example: Guest said ""extra pillows"" -> entity citation: ""extra pillows""
     * Example: Guest said ""it"" referring to pool -> entity citation: ""inferred from context: pool mentioned earlier""

2. ASSUMPTIONS - List every assumption made during intent classification:
   - Example: ""Assumed 'help' is a general INQUIRY rather than a specific request""
   - Example: ""Assumed REQUEST_ITEM because guest used 'I need' language pattern""
   - Example: ""Assumed HOUSEKEEPING category because towels typically fall under housekeeping""
   - If no assumptions needed, use empty array []

3. UNCERTAINTIES - Explicitly state classification doubts:
   - Example: ""Uncertain if this is INQUIRY or REQUEST_SERVICE - message is ambiguous""
   - Example: ""Not certain which service category - could be DINING or ROOM_SERVICE""
   - Example: ""Cannot determine exact entity - guest said 'something to drink' which is vague""
   - Lower confidence (0.5-0.7) when uncertainties exist
   - If uncertain, set isAmbiguous: true and provide ambiguityReason

4. ""I DON'T KNOW"" ENFORCEMENT FOR HOTEL CONFIGURATION:
   - If guest asks about ""rooftop pool"" but ONLY ""Swimming Pool"" exists:
     * citations.entity: ""rooftop pool"" (what guest asked for)
     * assumptions: [""Guest may be asking about the regular swimming pool""]
     * uncertainties: [""Hotel configuration does not show a 'rooftop' pool specifically - only outdoor pool available""]
     * availableOptions: [""Swimming Pool""] (what actually exists)
   - If guest asks about service not in configuration:
     * availableOptions: [] (empty - nothing matches)
     * uncertainties: [""Requested service not found in hotel configuration""]
     * confidence: < 0.5
   - NEVER invent or assume services that aren't explicitly in the hotel configuration

5. CONFIDENCE SCORING WITH REFLECTION:
   - High confidence (0.8-1.0): Clear intent, exact match in configuration, no uncertainties
   - Medium confidence (0.6-0.8): Intent clear but minor assumptions needed
   - Low confidence (0.3-0.6): Significant uncertainties, ambiguous request, or no configuration match
   - Very low confidence (< 0.3): Cannot determine intent with any certainty

CRITICAL ANALYSIS RULES:
1. In availableOptions, list ONLY items/services that ACTUALLY exist in the hotel configuration
2. If the guest asks about something specific (like ""rooftop pool""), check if it exists EXACTLY as described
3. If ""Swimming Pool"" exists but guest asks ""rooftop swimming pool"", the available option is ONLY ""Swimming Pool""
4. DO NOT infer, assume, or add details not present in the configuration
5. For ambiguous requests (like 'water' when multiple types exist), set isAmbiguous=true
6. If guest specifies exact match (like 'still water' matches 'Still Water'), it is NOT ambiguous
7. If nothing matches the request, return empty availableOptions array
8. Extract requestedQuantity from the message (e.g., ""50 towels"" -> 50, ""I need towels"" -> null)
9. If quantity > 5, set clarificationNeeded.type = ""QUANTITY"" and ask for confirmation
10. Vague single-word requests like ""help"", ""info"", ""assistance"" should be classified as INQUIRY, not REQUEST_ITEM
11. GREETING intent: Simple greetings, salutations, or conversational openers should be classified as GREETING
    - Examples: ""hi"", ""hello"", ""hey"", ""good morning"", ""greetings"", ""👋"", ""hi there"", ""hola"", ""bonjour"", ""guten tag""
    - Include multilingual greetings and emoji-based greetings
    - Do NOT classify questions like ""how are you doing?"" as GREETING if they seem like service inquiries

EXAMPLES:

SINGLE-INTENT EXAMPLES:
1. Guest: ""water"", Config has: [""Sparkling Water"", ""Still Water""] -> intents: [{{intent: REQUEST_ITEM, entityType: ""water"", availableOptions: [""Sparkling Water"", ""Still Water""]}}], isAmbiguous: true
2. Guest: ""still water"", Config has: [""Still Water""] -> intents: [{{intent: REQUEST_ITEM, entityType: ""still water"", availableOptions: [""Still Water""]}}], isAmbiguous: false
3. Guest: ""help"" -> intents: [{{intent: INQUIRY, entityType: ""assistance"", originalText: ""help""}}], clarificationNeeded.question: ""I'd be happy to assist you! What can I help you with today?""
4. Guest: ""Hi"" -> intents: [{{intent: GREETING, entityType: ""greeting"", originalText: ""Hi""}}], isAmbiguous: false

MULTI-INTENT EXAMPLES (CRITICAL):
5. Guest: ""I need towels and also what time is breakfast?"" -> intents: [
   {{intent: REQUEST_ITEM, category: HOUSEKEEPING, entityType: ""towels"", originalText: ""I need towels"", availableOptions: [""Towels""]}},
   {{intent: INQUIRY, category: DINING, entityType: ""breakfast time"", originalText: ""what time is breakfast?""}}
]
6. Guest: ""Can I get water and check what amenities you have?"" -> intents: [
   {{intent: REQUEST_ITEM, category: BEVERAGE, entityType: ""water"", originalText: ""Can I get water""}},
   {{intent: INQUIRY, category: AMENITIES, entityType: ""amenities list"", originalText: ""check what amenities you have""}}
]
7. Guest: ""My AC is not working and I need extra pillows"" -> intents: [
   {{intent: COMPLAINT, category: MAINTENANCE, entityType: ""air conditioning"", originalText: ""My AC is not working""}},
   {{intent: REQUEST_ITEM, category: HOUSEKEEPING, entityType: ""pillows"", originalText: ""I need extra pillows""}}
]

CRITICAL AMENITY/SERVICE INQUIRY EXAMPLES (MUST CLASSIFY CORRECTLY):
8. Guest: ""What services do you provide?"" -> intents: [{{intent: INQUIRY, category: AMENITIES, entityType: ""services list"", originalText: ""What services do you provide?""}}]
9. Guest: ""What other services do you provide?"" -> intents: [{{intent: INQUIRY, category: AMENITIES, entityType: ""services list"", originalText: ""What other services do you provide?""}}]
10. Guest: ""What amenities do you have?"" -> intents: [{{intent: INQUIRY, category: AMENITIES, entityType: ""amenities list"", originalText: ""What amenities do you have?""}}]
11. Guest: ""What other amenities do you provide?"" -> intents: [{{intent: INQUIRY, category: AMENITIES, entityType: ""amenities list"", originalText: ""What other amenities do you provide?""}}]
12. Guest: ""List the amenities you provide"" -> intents: [{{intent: INQUIRY, category: AMENITIES, entityType: ""amenities list"", originalText: ""List the amenities you provide""}}]
13. Guest: ""What else do you offer?"" -> intents: [{{intent: INQUIRY, category: AMENITIES, entityType: ""services list"", originalText: ""What else do you offer?""}}]
14. Guest: ""Tell me what's available"" -> intents: [{{intent: INQUIRY, category: AMENITIES, entityType: ""services list"", originalText: ""Tell me what's available""}}]
15. Guest: ""What can you help me with?"" -> intents: [{{intent: INQUIRY, category: AMENITIES, entityType: ""services list"", originalText: ""What can you help me with?""}}]

LOST & FOUND EXAMPLES (CRITICAL - must detect lost item reports):
16. Guest: ""I think i left my belt"" -> intents: [{{intent: LOST_AND_FOUND, category: LOST_ITEMS, entityType: ""belt"", originalText: ""I think i left my belt"", citations: {{intent: ""I think i left"", entity: ""belt""}}, assumptions: [""Assumed LOST_AND_FOUND from 'left' indicating item misplaced""], uncertainties: []}}]
17. Guest: ""I lost my phone in the pool area"" -> intents: [{{intent: LOST_AND_FOUND, category: LOST_ITEMS, entityType: ""phone"", originalText: ""I lost my phone in the pool area"", citations: {{intent: ""I lost"", entity: ""phone"", location: ""pool area""}}}}]
18. Guest: ""I can't find my wallet"" -> intents: [{{intent: LOST_AND_FOUND, category: LOST_ITEMS, entityType: ""wallet"", originalText: ""I can't find my wallet"", citations: {{intent: ""can't find"", entity: ""wallet""}}}}]
19. Guest: ""I forgot my sunglasses in the restaurant"" -> intents: [{{intent: LOST_AND_FOUND, category: LOST_ITEMS, entityType: ""sunglasses"", originalText: ""I forgot my sunglasses in the restaurant"", citations: {{intent: ""I forgot"", entity: ""sunglasses"", location: ""restaurant""}}}}]
20. Guest: ""Did anyone find a black iPhone?"" -> intents: [{{intent: LOST_AND_FOUND, category: LOST_ITEMS, entityType: ""iPhone"", originalText: ""Did anyone find a black iPhone?"", citations: {{intent: ""Did anyone find"", entity: ""black iPhone""}}}}]
21. Guest: ""I left my passport in room 305"" -> intents: [{{intent: LOST_AND_FOUND, category: LOST_ITEMS, entityType: ""passport"", originalText: ""I left my passport in room 305"", citations: {{intent: ""I left"", entity: ""passport"", location: ""room 305""}}}}]

LOST & FOUND TRIGGER PHRASES (look for these):
- ""I lost..."", ""I left..."", ""I forgot..."", ""I can't find..."", ""I misplaced...""
- ""Did anyone find..."", ""Has anyone seen..."", ""Looking for my...""
- ""Missing..."", ""Can't locate..."", ""Where is my...""

IMPORTANT: When detecting LOST_AND_FOUND:
- Extract: WHAT (item), WHERE (location if mentioned), WHEN (timeline if mentioned)
- Color/Brand details in citations
- Urgency: checkout today = urgent

Focus on ACCURATE ANALYSIS based ONLY on the actual configuration data provided.";

        return prompt;
    }

    /// <summary>
    /// CRITICAL ANTI-HALLUCINATION METHOD
    /// Validates LLM-provided available options against actual hotel configuration
    /// Filters out any hallucinated items that don't exist in the database
    /// </summary>
    private List<string> ValidateAvailableOptions(List<string> llmOptions, HotelConfigurationContext hotelContext, string intentType)
    {
        _logger.LogWarning("===== VALIDATE AVAILABLE OPTIONS CALLED =====");
        _logger.LogWarning("LLM Options Count: {Count}, Options: {Options}", llmOptions?.Count ?? 0, string.Join(", ", llmOptions ?? new List<string>()));

        if (llmOptions == null || llmOptions.Count == 0)
            return new List<string>();

        try
        {
            // Get all valid item/service names from hotel configuration
            var validNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add RequestItems names
            if (hotelContext.RequestItems != null)
            {
                // First serialize to JSON, then deserialize - handles anonymous types
                var requestItemsJson = JsonSerializer.Serialize(hotelContext.RequestItems);
                var requestItems = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(requestItemsJson);
                if (requestItems != null)
                {
                    foreach (var item in requestItems)
                    {
                        if (item.ContainsKey("Name"))
                        {
                            validNames.Add(item["Name"].GetString() ?? "");
                        }
                    }
                }
            }

            // Add Services names
            if (hotelContext.AvailableServices != null)
            {
                // First serialize to JSON, then deserialize - handles anonymous types
                var servicesJson = JsonSerializer.Serialize(hotelContext.AvailableServices);
                var services = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(servicesJson);
                if (services != null)
                {
                    foreach (var service in services)
                    {
                        if (service.ContainsKey("Name"))
                        {
                            validNames.Add(service["Name"].GetString() ?? "");
                        }
                    }
                }
            }

            // Filter LLM options to only include items that exist in hotel configuration
            var validatedOptions = llmOptions
                .Where(option => validNames.Contains(option))
                .ToList();

            // Log if hallucinations were detected
            var hallucinatedOptions = llmOptions.Except(validatedOptions, StringComparer.OrdinalIgnoreCase).ToList();
            if (hallucinatedOptions.Any())
            {
                _logger.LogWarning(
                    "ANTI-HALLUCINATION: Removed {Count} hallucinated items from LLM response: {Items}. " +
                    "Valid items: {ValidItems}",
                    hallucinatedOptions.Count,
                    string.Join(", ", hallucinatedOptions),
                    string.Join(", ", validatedOptions));
            }

            return validatedOptions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating available options, returning original list");
            return llmOptions; // Fallback to original list if validation fails
        }
    }

    private async Task<IntentAnalysisResult> ProcessAnalysisResponseAsync(
        IntentAnalysisResponse aiResponse,
        HotelConfigurationContext hotelContext,
        string originalMessage,
        GuestContextInfo guestContext)
    {
        var result = new IntentAnalysisResult
        {
            IsAmbiguous = aiResponse.IsAmbiguous
        };

        // Convert ExtractedIntent list to DetectedIntent list
        if (aiResponse.Intents != null && aiResponse.Intents.Count > 0)
        {
            _logger.LogWarning("===== PROCESSING {Count} INTENTS FROM LLM =====", aiResponse.Intents.Count);
            foreach (var i in aiResponse.Intents)
            {
                _logger.LogWarning("LLM Intent: {Intent}, AvailableOptions from LLM: {Options}",
                    i.Intent, string.Join(", ", i.AvailableOptions ?? new List<string>()));
            }

            result.DetectedIntents = aiResponse.Intents.Select(i => new DetectedIntent
            {
                Intent = i.Intent,
                Category = i.Category,
                SpecificityLevel = i.SpecificityLevel,
                // CRITICAL ANTI-HALLUCINATION: Validate availableOptions against actual hotel configuration
                // Filter out any items that don't exist in the database
                AvailableOptions = ValidateAvailableOptions(i.AvailableOptions ?? new List<string>(), hotelContext, i.Intent),
                EntityType = i.EntityType ?? "",
                OriginalText = i.OriginalText ?? "",
                Confidence = i.Confidence,
                RequestedQuantity = i.RequestedQuantity
            }).ToList();

            _logger.LogWarning("===== AFTER VALIDATION =====");
            foreach (var di in result.DetectedIntents)
            {
                _logger.LogWarning("DetectedIntent: {Intent}, AvailableOptions AFTER validation: {Options}",
                    di.Intent, string.Join(", ", di.AvailableOptions));
            }

            // Set primary intent/category for backward compatibility (use first intent)
            var primaryIntent = result.DetectedIntents.First();
            result.Intent = primaryIntent.Intent;
            result.Category = primaryIntent.Category;
            result.SpecificityLevel = primaryIntent.SpecificityLevel;
            result.AvailableOptions = primaryIntent.AvailableOptions;
            result.Confidence = primaryIntent.Confidence;
        }

        // Generate warm response based on detected intents
        if (result.DetectedIntents.Count > 1)
        {
            _logger.LogWarning("===== RESPONSE PATH: MULTI-INTENT =====");
            // MULTI-INTENT: Handle each intent and combine responses
            result.WarmResponse = await GenerateMultiIntentResponseAsync(
                result.DetectedIntents,
                hotelContext,
                guestContext,
                originalMessage);
        }
        else if (result.DetectedIntents.Count == 1)
        {
            // SINGLE INTENT: Use existing logic
            var intent = result.DetectedIntents.First();

            _logger.LogWarning("===== RESPONSE PATH: SINGLE INTENT =====");
            _logger.LogWarning("Intent: {Intent}, Category: {Category}, AvailableOptions Count: {Count}",
                intent.Intent, intent.Category, intent.AvailableOptions.Count);
            _logger.LogWarning("AvailableOptions: {Options}", string.Join(", ", intent.AvailableOptions));
            _logger.LogWarning("IsAmbiguous: {IsAmbiguous}", result.IsAmbiguous);

            if (intent.AvailableOptions.Count > 0)
            {
                // CRITICAL: Check for ambiguity before generating response
                // If ambiguous with multiple options, ask for clarification instead
                if (result.IsAmbiguous && intent.AvailableOptions.Count > 1 &&
                    (intent.Intent == "REQUEST_ITEM" || intent.Intent == "REQUEST_SERVICE" || intent.Intent == "REQUEST_SERVICE"))
                {
                    _logger.LogWarning("===== TAKING CLARIFICATION PATH (lines 583-597) =====");
                    // CRITICAL ANTI-HALLUCINATION: Always generate clarification from AvailableOptions
                    // NEVER trust LLM's question as it may hallucinate items not in the database
                    var optionsList = string.Join(", ", intent.AvailableOptions.Select((opt, idx) =>
                        idx == intent.AvailableOptions.Count - 1 && intent.AvailableOptions.Count > 1
                            ? $"or {opt}"
                            : opt));
                    var clarificationQuestion = $"I'd be happy to help! We have {optionsList}. Which one would you prefer?";

                    result.ClarificationQuestion = clarificationQuestion;
                    result.WarmResponse = clarificationQuestion;

                    _logger.LogInformation(
                        "Ambiguous {Intent} detected with {Count} options. Asking clarification: '{Question}'",
                        intent.Intent, intent.AvailableOptions.Count, clarificationQuestion);
                }
                else
                {
                    // Not ambiguous or only one option - generate normal response
                    var serviceDetails = await GetServiceDetailsAsync(intent.AvailableOptions, hotelContext);
                    result.WarmResponse = await GenerateWarmResponseAsync(
                        originalMessage,
                        serviceDetails,
                        hotelContext.HotelName ?? "our property",
                        hotelContext.TenantId,
                        guestContext.ConversationId,
                        intent.Category);
                }
            }
            else if (intent.Intent == "GREETING")
            {
                result.WarmResponse = await GenerateGreetingResponseAsync(
                    hotelContext,
                    guestContext,
                    guestContext.ConversationId);
            }
            else if (!string.IsNullOrEmpty(DetectAndGeneratePolicyResponse(originalMessage, hotelContext)))
            {
                var policyResponse = DetectAndGeneratePolicyResponse(originalMessage, hotelContext);
                result.WarmResponse = policyResponse;
                result.Intent = "INQUIRY";
                result.Metadata["policyType"] = "detected";
            }
            else if (intent.Intent == "INQUIRY" && aiResponse.ClarificationNeeded?.Required == true)
            {
                result.WarmResponse = aiResponse.ClarificationNeeded.Question;
                result.ClarificationQuestion = aiResponse.ClarificationNeeded.Question;
            }
            else if (intent.Intent == "INQUIRY")
            {
                // Check if this is an amenities/services list inquiry
                if (intent.Category == "AMENITIES" ||
                    intent.EntityType?.Contains("amenities") == true ||
                    intent.EntityType?.Contains("services") == true ||
                    intent.EntityType?.Contains("list") == true)
                {
                    // Generate proper amenities list response
                    result.WarmResponse = await GenerateInquiryResponseAsync(intent, hotelContext);
                }
                else
                {
                    // Generic inquiry response for other inquiries
                    result.WarmResponse = "I'd be happy to assist you! How may I help you today? You can ask about our services, amenities, room service, or any other questions you may have.";
                }
            }
            else if (intent.Intent == "GREETING")
            {
                result.WarmResponse = "Hello! Welcome to our hotel. How may I assist you today?";
            }
            else
            {
                result.WarmResponse = $"I'm sorry, we don't currently offer that service at our property. " +
                    $"However, I'd be happy to help you with other requests or provide information about our available amenities and services.";
            }
        }

        // Add metadata
        result.Metadata["originalMessage"] = originalMessage;
        result.Metadata["guestStatus"] = guestContext.Status;
        result.Metadata["propertyType"] = hotelContext.HotelType ?? "Hotel";
        result.Metadata["timestamp"] = DateTime.UtcNow;
        result.Metadata["multiIntent"] = result.HasMultipleIntents;
        result.Metadata["intentCount"] = result.DetectedIntents.Count;

        return result;
    }

    private async Task<string> GetServiceDetailsAsync(List<string> serviceNames, HotelConfigurationContext hotelContext)
    {
        // Extract full service details from hotelContext.AvailableServices
        var services = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
            hotelContext.AvailableServices?.ToString() ?? "[]");

        var matchingServices = services?.Where(s =>
            s.ContainsKey("Name") && serviceNames.Contains(s["Name"].ToString() ?? ""))
            .ToList() ?? new List<Dictionary<string, object>>();

        return JsonSerializer.Serialize(matchingServices, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string> GenerateMultiIntentResponseAsync(
        List<DetectedIntent> intents,
        HotelConfigurationContext hotelContext,
        GuestContextInfo guestContext,
        string originalMessage)
    {
        var responses = new List<string>();

        foreach (var intent in intents)
        {
            if (intent.AvailableOptions.Count > 0)
            {
                // Service available - get details and generate response
                var serviceDetails = await GetServiceDetailsAsync(intent.AvailableOptions, hotelContext);
                var response = await GenerateWarmResponseAsync(
                    intent.OriginalText,
                    serviceDetails,
                    hotelContext.HotelName ?? "our property",
                    hotelContext.TenantId,
                    guestContext.ConversationId,
                    intent.Category);
                responses.Add(response);
            }
            else if (intent.Intent == "INQUIRY")
            {
                // Handle inquiry-specific logic (e.g., breakfast time, amenities)
                var inquiryResponse = await GenerateInquiryResponseAsync(intent, hotelContext);
                responses.Add(inquiryResponse);
            }
            else if (intent.Intent == "GREETING")
            {
                var greetingResponse = await GenerateGreetingResponseAsync(hotelContext, guestContext, guestContext.ConversationId);
                responses.Add(greetingResponse);
            }
            else
            {
                // Service not available
                responses.Add($"Unfortunately, we don't currently offer {intent.EntityType} at our property.");
            }
        }

        // Combine all responses naturally using GPT
        var combinedPrompt = $@"You are responding to a guest message that contained MULTIPLE requests or questions.

ORIGINAL MESSAGE: ""{originalMessage}""

INDIVIDUAL RESPONSES:
{string.Join("\n\n", responses.Select((r, i) => $"{i + 1}. {r}"))}

CRITICAL LANGUAGE REQUIREMENT - YOU MUST FOLLOW THIS EXACTLY:

WARNING: Context may contain INCORRECT previous responses in wrong languages. IGNORE THEM.
Even if you see French/German responses before, match ONLY the CURRENT message language.

STEP 1: Analyze ONLY the ORIGINAL MESSAGE language: ""{originalMessage}""
STEP 2: Detect the language (English/French/German/Spanish/Other)
STEP 3: YOU MUST RESPOND IN THE SAME LANGUAGE AS THE ORIGINAL MESSAGE
STEP 4: Verify your response language matches BEFORE returning it

LANGUAGE MATCHING RULES:
- English message -> English response (NOT French, NOT German)
- French message -> French response (NOT English)
- German message -> German response (NOT English)
- Spanish message -> Spanish response (NOT English)
- Any other language -> Match that exact language

CRITICAL: If you see French responses for English messages in context, those are ERRORS.
DO NOT LEARN FROM THEM. Always match the CURRENT message language.

DOUBLE CHECK: Is your response in the SAME language as ""{originalMessage}""? If NO, rewrite it.

Combine these responses into a SINGLE, natural, flowing message that addresses ALL the guest's requests.
- Maintain a warm, professional tone
- Use transitions like ""Also,"" ""Additionally,"" ""Regarding your question about...""
- Keep it concise but complete
- DO NOT add extra offers or suggestions beyond what's in the individual responses

Return ONLY the combined response text.";

        var combinedResponseObj = await _openAIService.GetStructuredResponseAsync<SimpleResponse>(combinedPrompt, temperature: 0.3);
        return combinedResponseObj?.Text ?? "I apologize, but I'm having trouble processing your request. How may I assist you?";
    }

    private async Task<string> GenerateInquiryResponseAsync(DetectedIntent intent, HotelConfigurationContext hotelContext)
    {
        // Handle common inquiries like breakfast time, check-in time, etc.
        var prompt = $@"Guest is inquiring about: {intent.EntityType}
Original question: {intent.OriginalText}

Hotel Information:
{JsonSerializer.Serialize(hotelContext, new JsonSerializerOptions { WriteIndented = true })}

CRITICAL LANGUAGE REQUIREMENT - YOU MUST FOLLOW THIS EXACTLY:
STEP 1: Analyze the ORIGINAL QUESTION language: ""{intent.OriginalText}""
STEP 2: Determine the language (English/French/German/Spanish/Other)
STEP 3: YOU MUST RESPOND IN THE SAME LANGUAGE
STEP 4: Verify your response language matches the question language

LANGUAGE MATCHING RULES:
- English question -> English response (NOT French, NOT German)
- French question -> French response
- German question -> German response
- Spanish question -> Spanish response
- Any other language -> Match that language

DOUBLE CHECK: Is your response in the SAME language as ""{intent.OriginalText}""? If NO, rewrite it.

CRITICAL INSTRUCTIONS FOR AMENITY/SERVICE LIST INQUIRIES:
- If the guest asks ""What services do you provide"" or ""What amenities do you have"" or similar:
  * YOU MUST list ALL available services/amenities from the Hotel Information above
  * Include service names, descriptions, and key details
  * Format as a well-organized, easy-to-read list
  * DO NOT say ""I don't have information"" when services ARE in the configuration
- If specific information is genuinely not available in configuration, offer to connect with staff
- Be comprehensive and helpful - show what IS available rather than what isn't

Provide a helpful, specific response based on the hotel configuration.";

        var responseObj = await _openAIService.GetStructuredResponseAsync<SimpleResponse>(prompt, temperature: 0.3);
        return responseObj?.Text ?? "I'd be happy to help! Please let me connect you with our staff for specific information.";
    }

    private async Task<string> GenerateWarmResponseAsync(
        string guestMessage,
        string serviceDetailsJson,
        string hotelName,
        int tenantId,
        int conversationId,
        string category = "")
    {
        // Get non-intrusive upsell suggestion based on category
        Service? upsellService = null;
        if (!string.IsNullOrEmpty(category))
        {
            upsellService = await _upsellService.GetRelevantUpsellAsync(tenantId, category);

            // Log the suggestion if we have one
            if (upsellService != null)
            {
                await _upsellService.LogUpsellSuggestionAsync(
                    tenantId,
                    conversationId,
                    upsellService.Id,
                    $"{category}_inquiry",
                    null);
            }
        }

        // Build upsell context for LLM
        string upsellContext = "";
        if (upsellService != null)
        {
            upsellContext = $@"

OPTIONAL UPSELL (only if contextually appropriate):
You MAY mention this related service NATURALLY at the end if it fits the conversation:
- {upsellService.Name}: {upsellService.Description} (R{upsellService.Price})

UPSELL RULES:
- Only mention if it naturally complements what the guest asked about
- Keep it brief and non-pushy (1 sentence max)
- Make it sound like a helpful suggestion, not a sales pitch
- Example: ""By the way, guests also enjoy our {upsellService.Name} - {upsellService.Description?.Split('.')[0] ?? upsellService.Name}.""";
        }

        var prompt = $@"You are a warm, professional hotel concierge at {hotelName}.

Guest asked: ""{guestMessage}""

Available services with FULL details:
{serviceDetailsJson}
{upsellContext}

CRITICAL LANGUAGE REQUIREMENT - YOU MUST FOLLOW THIS EXACTLY:
STEP 1: Analyze the GUEST MESSAGE language: ""{guestMessage}""
STEP 2: Determine the language (English/French/German/Spanish/Other)
STEP 3: YOU MUST RESPOND IN THE SAME LANGUAGE AS THE GUEST
STEP 4: Verify your response language matches the guest's message language

LANGUAGE MATCHING RULES:
- English message -> English response (NOT French, NOT German, NOT Spanish)
- French message -> French response
- German message -> German response
- Spanish message -> Spanish response
- Any other language -> Match that language

DOUBLE CHECK: Is your response in the SAME language as ""{guestMessage}""? If NO, rewrite it immediately.

CRITICAL RULES:
1. Provide a warm, natural, conversational response
2. ONLY mention services from the provided list above
3. If guest asked about ""rooftop pool"" but only ""Swimming Pool"" (outdoor) exists, politely clarify we have an outdoor pool
4. Include helpful details like hours, features, or amenities from the service description
5. Be welcoming and helpful - make the guest feel valued
6. Keep it natural and concise (2-3 sentences max)
7. If the guest is asking a follow-up clarification question, address their specific concern directly

Examples:
- Guest: ""Is it a rooftop pool?"" -> ""We have a lovely outdoor Swimming Pool available from 6:00 AM to 10:00 PM, with pool towels provided. While it's not on the rooftop, it's a great spot to relax and unwind!""
- Guest: ""What kind of pool?"" -> ""We have an outdoor Swimming Pool open daily from 6:00 AM to 10:00 PM. Pool towels are provided, and children under 12 must be supervised. It's perfect for a refreshing swim!""

Generate a natural, warm response:";

        var response = await _openAIService.GetStructuredResponseAsync<SimpleResponse>(
            prompt,
            temperature: 0.3);  // Lower temperature for consistent, predictable language matching

        return response?.Text ?? "We're happy to help! Let me connect you with our concierge for more details.";
    }

    private class SimpleResponse
    {
        public string Text { get; set; } = string.Empty;
    }

    private IntentAnalysisResult GetFallbackAnalysis(string message)
    {
        // Simple fallback for when LLM fails
        var result = new IntentAnalysisResult
        {
            Intent = "UNKNOWN",
            Category = "OTHER",
            SpecificityLevel = "UNCLEAR",
            IsAmbiguous = true,
            Confidence = 0.3,
            WarmResponse = "I'd be happy to help you with that! Could you tell me a bit more about what you need so I can assist you better?"
        };

        return result;
    }

    // Supporting classes
    private class HotelConfigurationContext
    {
        public int TenantId { get; set; }
        public string? HotelName { get; set; }
        public string? HotelType { get; set; }
        public object? MenuCategories { get; set; }
        public object? AvailableServices { get; set; }
        public object? RequestItems { get; set; }
        public object? BusinessHours { get; set; }
        public object? Departments { get; set; }
        public List<string> Amenities { get; set; } = new();
        public string? CheckInTime { get; set; }
        public string? CheckOutTime { get; set; }
        // Hotel Policies
        public string? SmokingPolicy { get; set; }
        public string? PetPolicy { get; set; }
        public string? CancellationPolicy { get; set; }
        public string? ChildPolicy { get; set; }
    }

    private class GuestContextInfo
    {
        public string Status { get; set; } = string.Empty;
        public string? RoomNumber { get; set; }
        public DateTime? CheckInDate { get; set; }
        public DateTime? CheckOutDate { get; set; }
        public string? GuestName { get; set; }
        public object? RecentRequests { get; set; }
        public int ConversationId { get; set; }
    }

    private class ExtractedIntent
    {
        public string Intent { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SpecificityLevel { get; set; } = string.Empty;
        public List<string>? AvailableOptions { get; set; }
        public string? EntityType { get; set; }
        public string OriginalText { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public int? RequestedQuantity { get; set; }

        // Reflection fields for hallucination prevention
        public Dictionary<string, string?>? Citations { get; set; }
        public string[]? Assumptions { get; set; }
        public string[]? Uncertainties { get; set; }
    }

    private class IntentAnalysisResponse
    {
        public List<ExtractedIntent> Intents { get; set; } = new();
        public bool IsAmbiguous { get; set; }
        public string? AmbiguityReason { get; set; }
        public ClarificationInfo? ClarificationNeeded { get; set; }
    }

    private class ClarificationInfo
    {
        public bool Required { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
    }

    /// <summary>
    /// Detects policy-related inquiries and generates appropriate responses
    /// Handles edge cases like variations, typos, and multi-language queries
    /// </summary>
    private string DetectAndGeneratePolicyResponse(string message, HotelConfigurationContext hotelContext)
    {
        var messageLower = message.ToLower().Trim();

        // Smoking Policy Detection (with edge cases)
        var smokingKeywords = new[] {
            "smok", "cigarette", "cigar", "vape", "vaping", "e-cigarette", "tobacco",
            "smoking room", "can i smoke", "is smoking allowed", "smoke free", "non-smoking",
            "designated smoking", "where can i smoke", "smoking area", "ash tray"
        };

        if (smokingKeywords.Any(k => messageLower.Contains(k)))
        {
            if (!string.IsNullOrEmpty(hotelContext.SmokingPolicy))
            {
                return $"Our smoking policy: {hotelContext.SmokingPolicy}";
            }
            else
            {
                return "I don't have specific information about our smoking policy at the moment. Please contact our front desk for details.";
            }
        }

        // Pet Policy Detection (with edge cases)
        var petKeywords = new[] {
            "pet", "dog", "cat", "animal", "puppy", "kitten", "service animal",
            "emotional support", "bring my dog", "bring my cat", "pet friendly",
            "allow pets", "pet fee", "pet deposit", "dog friendly", "can i bring"
        };

        if (petKeywords.Any(k => messageLower.Contains(k)))
        {
            if (!string.IsNullOrEmpty(hotelContext.PetPolicy))
            {
                return $"Our pet policy: {hotelContext.PetPolicy}";
            }
            else
            {
                return "I don't have specific information about our pet policy at the moment. Please contact our front desk for details.";
            }
        }

        // Cancellation Policy Detection (with edge cases)
        var cancellationKeywords = new[] {
            "cancel", "cancellation", "refund", "change booking", "modify reservation",
            "cancel my booking", "cancel reservation", "cancellation fee", "free cancellation",
            "cancel my stay", "cancellation policy", "reschedule", "postpone"
        };

        if (cancellationKeywords.Any(k => messageLower.Contains(k)))
        {
            if (!string.IsNullOrEmpty(hotelContext.CancellationPolicy))
            {
                return $"Our cancellation policy: {hotelContext.CancellationPolicy}";
            }
            else
            {
                return "I don't have specific information about our cancellation policy at the moment. Please contact our front desk for details.";
            }
        }

        // Child Policy Detection (with edge cases)
        var childKeywords = new[] {
            "child", "children", "kid", "kids", "baby", "infant", "toddler",
            "crib", "cot", "child bed", "kids stay free", "age", "minor",
            "family room", "children allowed", "kids meal", "babysitting"
        };

        if (childKeywords.Any(k => messageLower.Contains(k)))
        {
            if (!string.IsNullOrEmpty(hotelContext.ChildPolicy))
            {
                return $"Our child policy: {hotelContext.ChildPolicy}";
            }
            else
            {
                return "I don't have specific information about our child policy at the moment. Please contact our front desk for details.";
            }
        }

        // Check-in/Check-out Time Detection (with edge cases)
        var checkinKeywords = new[] {
            "check in", "check-in", "checkin", "arrival time", "when can i check in",
            "check in time", "earliest check in", "early check in", "late check in"
        };

        var checkoutKeywords = new[] {
            "check out", "check-out", "checkout", "departure time", "when do i check out",
            "check out time", "late check out", "late checkout", "early checkout"
        };

        if (checkinKeywords.Any(k => messageLower.Contains(k)) || checkoutKeywords.Any(k => messageLower.Contains(k)))
        {
            var response = "";

            if (!string.IsNullOrEmpty(hotelContext.CheckInTime))
            {
                response += $"Check-in time: {hotelContext.CheckInTime}. ";
            }

            if (!string.IsNullOrEmpty(hotelContext.CheckOutTime))
            {
                response += $"Check-out time: {hotelContext.CheckOutTime}.";
            }

            if (!string.IsNullOrEmpty(response))
            {
                return response.Trim();
            }
            else
            {
                return "I don't have specific information about check-in/check-out times at the moment. Please contact our front desk for details.";
            }
        }

        // General Policy Inquiry (edge case for generic questions)
        var generalPolicyKeywords = new[] {
            "policy", "policies", "rules", "regulation", "requirement",
            "what are the rules", "hotel policy", "your policy", "house rules"
        };

        if (generalPolicyKeywords.Any(k => messageLower.Contains(k)))
        {
            // Provide all available policies
            var policies = new List<string>();

            if (!string.IsNullOrEmpty(hotelContext.SmokingPolicy))
                policies.Add($"**Smoking:** {hotelContext.SmokingPolicy}");

            if (!string.IsNullOrEmpty(hotelContext.PetPolicy))
                policies.Add($"**Pets:** {hotelContext.PetPolicy}");

            if (!string.IsNullOrEmpty(hotelContext.CancellationPolicy))
                policies.Add($"**Cancellation:** {hotelContext.CancellationPolicy}");

            if (!string.IsNullOrEmpty(hotelContext.ChildPolicy))
                policies.Add($"**Children:** {hotelContext.ChildPolicy}");

            if (!string.IsNullOrEmpty(hotelContext.CheckInTime) && !string.IsNullOrEmpty(hotelContext.CheckOutTime))
                policies.Add($"**Check-in:** {hotelContext.CheckInTime}, **Check-out:** {hotelContext.CheckOutTime}");

            if (policies.Any())
            {
                return $"Here are our main policies:\n\n{string.Join("\n\n", policies)}";
            }
            else
            {
                return "I don't have specific policy information at the moment. Please contact our front desk for details about our policies.";
            }
        }

        // No policy detected
        return string.Empty;
    }
}