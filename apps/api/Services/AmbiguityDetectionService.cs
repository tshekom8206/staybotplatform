using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.RegularExpressions;

namespace Hostr.Api.Services;

public interface IAmbiguityDetectionService
{
    Task<AmbiguityResult> AnalyzeMessageAsync(string message, int tenantId, int conversationId);
    Task<bool> HasAmbiguousTimeReferenceAsync(string message);
    Task<bool> HasMultipleOptionsAsync(string message, int tenantId);
    Task<bool> HasPrivacyViolationAsync(string message);
    Task<bool> HasConflictingContextAsync(string message, int conversationId);
    Task<List<string>> ExtractAmbiguousTermsAsync(string message);
}

public enum AmbiguityType
{
    None = 0,
    MultipleOptions = 1,      // "change my booking" (which one?)
    MissingContext = 2,       // "is it available?" (what?)
    TemporalVague = 3,        // "later" (when exactly?)
    PrivacyViolation = 4,     // "where is John?"
    ConflictingContext = 5,   // "check out" when already checked out
    IncompleteRequest = 6,    // "I need help" (with what?)
    MultipleIntents = 7,      // "book a table and order room service"
    ImpossibleRequest = 8     // "cancel my booking" when no booking exists
}

public class AmbiguityResult
{
    public bool IsAmbiguous { get; set; }
    public bool HasAmbiguity => IsAmbiguous;
    public List<AmbiguityType> AmbiguityTypes { get; set; } = new();
    public List<string> AmbiguousTerms { get; set; } = new();
    public List<string> ClarificationQuestions { get; set; } = new();
    public Dictionary<string, List<object>> SuggestedOptions { get; set; } = new();
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Medium;
    public string Explanation { get; set; } = string.Empty;
}

public class AmbiguityDetectionService : IAmbiguityDetectionService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<AmbiguityDetectionService> _logger;
    private readonly IConversationStateService _conversationStateService;

    // Enhanced patterns for detecting different types of ambiguity
    private static readonly Dictionary<AmbiguityType, List<Regex>> AmbiguityPatterns = new()
    {
        {
            AmbiguityType.TemporalVague,
            new List<Regex>
            {
                new(@"\b(later|soon|sometime|eventually|when possible|asap|whenever)\b", RegexOptions.IgnoreCase),
                new(@"\b(this week|next week|weekend)\b(?!\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday))", RegexOptions.IgnoreCase),
                new(@"\b(morning|afternoon|evening)\b(?!\s+(tomorrow|today|yesterday|at|around))", RegexOptions.IgnoreCase),
                new(@"\b(in a (bit|while|moment)|shortly|promptly)\b", RegexOptions.IgnoreCase),
                new(@"\b(around\s+)?\d{1,2}(ish|ish\s+(am|pm))\b", RegexOptions.IgnoreCase)
            }
        },
        {
            AmbiguityType.MissingContext,
            new List<Regex>
            {
                new(@"\b(is (?:it|that|this) (?:available|open|ready|possible|working|included))\b", RegexOptions.IgnoreCase),
                new(@"\b(can (?:i|you) (?:get|have|do|fix|change) (?:it|that|this))\b", RegexOptions.IgnoreCase),
                new(@"\b(what about (?:it|that|this)|how about (?:it|that|this))\b", RegexOptions.IgnoreCase),
                new(@"\b(how much (?:is it|does it cost|will it be))\b(?!\s+(for|to))", RegexOptions.IgnoreCase),
                new(@"\b(where is (?:it|that|this)|when is (?:it|that|this))\b", RegexOptions.IgnoreCase)
            }
        },
        {
            AmbiguityType.IncompleteRequest,
            new List<Regex>
            {
                new(@"\b(i need help|help me|can you help|assist me)\b(?!\s+(with|finding|getting))", RegexOptions.IgnoreCase),
                new(@"\b(i want|i need|i would like|could i get)\s+(water|towels|help|assistance|coffee|tea|juice)\b", RegexOptions.IgnoreCase),
                new(@"\b(book|reserve|cancel|change|order|request)\b(?!\s+\w+)", RegexOptions.IgnoreCase),
                new(@"\b(tell me about|what is|explain|show me)\b(?!\s+\w+)", RegexOptions.IgnoreCase),
                new(@"^(hi|hello|hey|good morning|good afternoon|good evening)\s*$", RegexOptions.IgnoreCase)
            }
        },
        {
            AmbiguityType.MultipleOptions,
            new List<Regex>
            {
                new(@"\b(change|modify|update|cancel)\s+(my|the)\s+(booking|reservation|appointment|order)\b", RegexOptions.IgnoreCase),
                new(@"\b(my\s+(?:booking|reservation|appointment|order|room|table))\b", RegexOptions.IgnoreCase),
                new(@"\b(the\s+(?:room|table|service|appointment))\b(?!\s+(service|menu|I|we))", RegexOptions.IgnoreCase),
                new(@"\b(which\s+(?:room|table|booking|reservation))\b", RegexOptions.IgnoreCase),
                new(@"\b(water|coffee|tea|juice)\b(?!\s+(bottle|cup|glass|still|sparkling|bottled|tap|green|black|herbal|espresso|latte|cappuccino|americano|orange|apple))", RegexOptions.IgnoreCase)
            }
        },
        {
            AmbiguityType.PrivacyViolation,
            new List<Regex>
            {
                new(@"\b(where is|what room|room number of)\s+(?:[A-Z][a-z]+(?:\s+[A-Z][a-z]+)?|Mr\.?\s+\w+|Mrs\.?\s+\w+|Ms\.?\s+\w+)\b", RegexOptions.IgnoreCase),
                new(@"\b(who is in|guest in|staying in|occupying)\s+room\s+\d+", RegexOptions.IgnoreCase),
                new(@"\b(contact|call|reach|find)\s+(?:[A-Z][a-z]+(?:\s+[A-Z][a-z]+)?|Mr\.?\s+\w+|Mrs\.?\s+\w+)\b", RegexOptions.IgnoreCase),
                new(@"\b(guest list|who's staying|other guests)\b", RegexOptions.IgnoreCase)
            }
        },
        {
            AmbiguityType.MultipleIntents,
            new List<Regex>
            {
                new(@"\b(and also|and then|plus|as well as|additionally|furthermore)\b", RegexOptions.IgnoreCase),
                new(@"\b(book.*and.*(order|request)|reserve.*and.*(cancel|change)|order.*and.*(book|reserve))\b", RegexOptions.IgnoreCase),
                new(@"\b(first.*then|after that|once.*also)\b", RegexOptions.IgnoreCase)
            }
        },
        {
            AmbiguityType.ImpossibleRequest,
            new List<Regex>
            {
                new(@"\b(extend|upgrade)\s+checkout\b", RegexOptions.IgnoreCase),
                new(@"\b(book|reserve)\s+.*\s+(yesterday|last\s+(week|month))", RegexOptions.IgnoreCase),
                new(@"\b(cancel|change)\s+.*\s+(completed|finished|past)\b", RegexOptions.IgnoreCase)
            }
        }
    };

    // Enhanced vague terms and context indicators
    private static readonly string[] VagueTerms =
    {
        "it", "that", "this", "there", "here", "something", "anything", "everything",
        "later", "soon", "sometime", "somewhere", "someone", "maybe", "probably",
        "stuff", "things", "item", "place", "person", "whatever", "whenever", "however"
    };

    // Context dependency indicators
    private static readonly string[] ContextDependentTerms =
    {
        "my", "the", "our", "their", "his", "her", "its", "these", "those", "such"
    };

    // Enhanced ambiguity severity weights
    private static readonly Dictionary<AmbiguityType, double> AmbiguitySeverityWeights = new()
    {
        { AmbiguityType.PrivacyViolation, 1.0 },
        { AmbiguityType.ImpossibleRequest, 0.9 },
        { AmbiguityType.ConflictingContext, 0.8 },
        { AmbiguityType.MultipleOptions, 0.7 },
        { AmbiguityType.MultipleIntents, 0.6 },
        { AmbiguityType.IncompleteRequest, 0.5 },
        { AmbiguityType.TemporalVague, 0.4 },
        { AmbiguityType.MissingContext, 0.3 }
    };

    public AmbiguityDetectionService(
        HostrDbContext context,
        ILogger<AmbiguityDetectionService> logger,
        IConversationStateService conversationStateService)
    {
        _context = context;
        _logger = logger;
        _conversationStateService = conversationStateService;
    }

    public async Task<AmbiguityResult> AnalyzeMessageAsync(string message, int tenantId, int conversationId)
    {
        try
        {
            var result = new AmbiguityResult();
            var detectedTypes = new List<AmbiguityType>();
            var matchedPatterns = new List<string>();

            // Enhanced pattern matching with context awareness
            foreach (var patternGroup in AmbiguityPatterns)
            {
                foreach (var pattern in patternGroup.Value)
                {
                    var match = pattern.Match(message);
                    if (match.Success)
                    {
                        detectedTypes.Add(patternGroup.Key);
                        matchedPatterns.Add($"{patternGroup.Key}: {match.Value}");
                        break;
                    }
                }
            }

            // Additional context-based ambiguity checks
            await CheckContextualAmbiguityAsync(message, conversationId, detectedTypes);

            // Remove duplicates and sort by severity
            result.AmbiguityTypes = detectedTypes.Distinct()
                .OrderByDescending(t => AmbiguitySeverityWeights.GetValueOrDefault(t, 0.1))
                .ToList();
            result.IsAmbiguous = result.AmbiguityTypes.Any();

            if (result.IsAmbiguous)
            {
                // Extract ambiguous terms with enhanced detection
                result.AmbiguousTerms = await ExtractAmbiguousTermsAsync(message);

                // Generate contextual clarification questions
                await GenerateClarificationQuestionsAsync(result, message, tenantId, conversationId);

                // Enhanced confidence calculation
                result.Confidence = DetermineEnhancedConfidenceLevel(result.AmbiguityTypes, message, matchedPatterns);

                // Generate detailed explanation
                result.Explanation = GenerateDetailedExplanation(result.AmbiguityTypes, matchedPatterns);
            }

            _logger.LogInformation("Ambiguity analysis for conversation {ConversationId}: IsAmbiguous={IsAmbiguous}, Types=[{Types}], Confidence={Confidence}",
                conversationId, result.IsAmbiguous, string.Join(", ", result.AmbiguityTypes), result.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing message ambiguity for conversation {ConversationId}", conversationId);
            return new AmbiguityResult { IsAmbiguous = false, Confidence = ConfidenceLevel.Low };
        }
    }

    public async Task<bool> HasAmbiguousTimeReferenceAsync(string message)
    {
        var patterns = AmbiguityPatterns[AmbiguityType.TemporalVague];
        return patterns.Any(pattern => pattern.IsMatch(message));
    }

    public async Task<bool> HasMultipleOptionsAsync(string message, int tenantId)
    {
        try
        {
            // Check for booking/reservation references when multiple exist
            if (Regex.IsMatch(message, @"\b(my|the)\s+(booking|reservation|appointment)\b", RegexOptions.IgnoreCase))
            {
                // This would need guest context to determine if multiple bookings exist
                // For now, we'll assume potential ambiguity
                return true;
            }

            // Check for service references that might have multiple options
            var serviceKeywords = new[] { "restaurant", "spa", "room service", "cleaning", "maintenance" };
            var messageWords = message.ToLower().Split(' ');

            var matchedServices = serviceKeywords.Where(s => messageWords.Contains(s)).ToList();
            return matchedServices.Count > 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking multiple options for tenant {TenantId}", tenantId);
            return false;
        }
    }

    public async Task<bool> HasPrivacyViolationAsync(string message)
    {
        var patterns = AmbiguityPatterns[AmbiguityType.PrivacyViolation];
        return patterns.Any(pattern => pattern.IsMatch(message));
    }

    public async Task<bool> HasConflictingContextAsync(string message, int conversationId)
    {
        try
        {
            var conversationState = await _conversationStateService.GetStateAsync(conversationId);

            // Check for checkout requests when already checked out
            if (Regex.IsMatch(message, @"\b(check out|checking out)\b", RegexOptions.IgnoreCase))
            {
                var guestStatus = await _conversationStateService.GetVariableAsync<string>(conversationId, "GuestStatus");
                if (guestStatus == "CheckedOut")
                {
                    return true;
                }
            }

            // Check for booking changes when no booking exists
            if (Regex.IsMatch(message, @"\b(change|modify|cancel).*booking\b", RegexOptions.IgnoreCase))
            {
                var hasBooking = await _conversationStateService.GetVariableAsync<bool>(conversationId, "HasActiveBooking");
                if (!hasBooking)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking conflicting context for conversation {ConversationId}", conversationId);
            return false;
        }
    }

    public async Task<List<string>> ExtractAmbiguousTermsAsync(string message)
    {
        var ambiguousTerms = new List<string>();
        var words = message.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length; i++)
        {
            var cleanWord = Regex.Replace(words[i], @"[^\w]", "");

            // Check for vague terms
            if (VagueTerms.Contains(cleanWord))
            {
                ambiguousTerms.Add(cleanWord);
            }

            // Check for context-dependent terms that lack specificity
            if (ContextDependentTerms.Contains(cleanWord))
            {
                // Check if the next word provides context
                if (i + 1 < words.Length)
                {
                    var nextWord = Regex.Replace(words[i + 1], @"[^\w]", "");
                    // If next word is also vague, mark as ambiguous
                    if (VagueTerms.Contains(nextWord) || ContextDependentTerms.Contains(nextWord))
                    {
                        ambiguousTerms.Add($"{cleanWord} {nextWord}");
                    }
                }
                else
                {
                    // Context-dependent term at end of message
                    ambiguousTerms.Add(cleanWord);
                }
            }
        }

        // Check for incomplete phrases
        var incompletePatterns = new[]
        {
            @"\b(book|reserve|cancel|change)\s*$",
            @"\b(i (want|need))\s*$",
            @"\b(how much)\s*$",
            @"\b(where is)\s*$"
        };

        foreach (var pattern in incompletePatterns)
        {
            var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                ambiguousTerms.Add(match.Value.Trim());
            }
        }

        return ambiguousTerms.Distinct().ToList();
    }

    private async Task GenerateClarificationQuestionsAsync(AmbiguityResult result, string message, int tenantId, int conversationId)
    {
        var questions = new List<string>();
        var options = new Dictionary<string, List<object>>();

        foreach (var ambiguityType in result.AmbiguityTypes)
        {
            switch (ambiguityType)
            {
                case AmbiguityType.TemporalVague:
                    questions.Add("When would you like this to happen? Could you specify the date and time?");
                    options["time_options"] = new List<object> { "This morning", "This afternoon", "This evening", "Tomorrow", "Specific time" };
                    break;

                case AmbiguityType.MissingContext:
                    questions.Add("Could you please clarify what specifically you're referring to?");
                    break;

                case AmbiguityType.IncompleteRequest:
                    questions.Add("I'd be happy to help! Could you tell me more about what you need assistance with?");
                    break;

                case AmbiguityType.MultipleOptions:
                    if (Regex.IsMatch(message, @"\b(booking|reservation)\b", RegexOptions.IgnoreCase))
                    {
                        questions.Add("I see you have multiple bookings. Which one would you like to modify?");
                        // We would load actual booking options here
                        options["booking_options"] = await GetGuestBookingOptionsAsync(conversationId);
                    }
                    else if (Regex.IsMatch(message, @"\bwater\b", RegexOptions.IgnoreCase))
                    {
                        questions.Add("Would you prefer sparkling water or still water?");
                        options["water_options"] = new List<object> { "Sparkling Water", "Still Water" };
                    }
                    else if (Regex.IsMatch(message, @"\bwine\b", RegexOptions.IgnoreCase))
                    {
                        questions.Add("What type of wine would you prefer?");
                        options["wine_options"] = await GetMenuItemOptionsAsync(conversationId, "wine");
                    }
                    else if (Regex.IsMatch(message, @"\bcoffee\b", RegexOptions.IgnoreCase))
                    {
                        questions.Add("What type of coffee would you like?");
                        options["coffee_options"] = await GetMenuItemOptionsAsync(conversationId, "coffee");
                    }
                    else if (Regex.IsMatch(message, @"\btea\b", RegexOptions.IgnoreCase))
                    {
                        questions.Add("What type of tea would you prefer?");
                        options["tea_options"] = await GetMenuItemOptionsAsync(conversationId, "tea");
                    }
                    else if (Regex.IsMatch(message, @"\bjuice\b", RegexOptions.IgnoreCase))
                    {
                        questions.Add("What type of juice would you like?");
                        options["juice_options"] = await GetMenuItemOptionsAsync(conversationId, "juice");
                    }
                    else if (Regex.IsMatch(message, @"\bbeer\b", RegexOptions.IgnoreCase))
                    {
                        questions.Add("What type of beer would you prefer?");
                        options["beer_options"] = await GetMenuItemOptionsAsync(conversationId, "beer");
                    }
                    else
                    {
                        questions.Add("I found multiple options that match your request. Could you be more specific?");
                    }
                    break;

                case AmbiguityType.PrivacyViolation:
                    questions.Add("I cannot provide information about other guests due to privacy policies. Are you looking for someone you're traveling with?");
                    break;

                case AmbiguityType.ConflictingContext:
                    questions.Add("I notice there might be a conflict with your current status. Could you provide more details?");
                    break;

                case AmbiguityType.MultipleIntents:
                    questions.Add("I see you're asking about multiple things. Would you like me to help with them one at a time? Which should we start with?");
                    break;
            }
        }

        result.ClarificationQuestions = questions;
        result.SuggestedOptions = options;
    }

    private async Task CheckContextualAmbiguityAsync(string message, int conversationId, List<AmbiguityType> detectedTypes)
    {
        try
        {
            // Check for service availability conflicts
            if (await HasServiceAvailabilityConflictAsync(message, conversationId))
            {
                detectedTypes.Add(AmbiguityType.ImpossibleRequest);
            }

            // Check for booking status conflicts
            if (await HasBookingStatusConflictAsync(message, conversationId))
            {
                detectedTypes.Add(AmbiguityType.ConflictingContext);
            }

            // Check for multiple booking references
            if (await HasMultipleBookingReferencesAsync(message, conversationId))
            {
                detectedTypes.Add(AmbiguityType.MultipleOptions);
            }

            // Check for ambiguous menu items (like "water" when there's sparkling and still)
            if (await HasAmbiguousMenuItemReferenceAsync(message, conversationId))
            {
                detectedTypes.Add(AmbiguityType.MultipleOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking contextual ambiguity for conversation {ConversationId}", conversationId);
        }
    }

    private async Task<bool> HasServiceAvailabilityConflictAsync(string message, int conversationId)
    {
        // Check if requesting service outside business hours
        var timePatterns = new[] { @"\b(\d{1,2}):?(\d{2})?\s*(am|pm)\b", @"\b(midnight|2am|3am|4am|5am)\b" };
        foreach (var pattern in timePatterns)
        {
            if (Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase))
            {
                var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
                var timeStr = match.Value.ToLower();

                // Check if it's clearly outside business hours
                if (timeStr.Contains("midnight") || timeStr.Contains("2am") || timeStr.Contains("3am") ||
                    timeStr.Contains("4am") || timeStr.Contains("5am"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private async Task<bool> HasBookingStatusConflictAsync(string message, int conversationId)
    {
        try
        {
            var conversationState = await _conversationStateService.GetStateAsync(conversationId);
            var guestStatus = await _conversationStateService.GetVariableAsync<string>(conversationId, "GuestStatus");

            // Check for checkout conflicts
            if (Regex.IsMatch(message, @"\b(extend|late checkout|stay longer)\b", RegexOptions.IgnoreCase) &&
                guestStatus == "CheckedOut")
            {
                return true;
            }

            // Check for check-in conflicts
            if (Regex.IsMatch(message, @"\b(early checkin|check in now)\b", RegexOptions.IgnoreCase) &&
                guestStatus == "CheckedIn")
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> HasMultipleBookingReferencesAsync(string message, int conversationId)
    {
        // This would check if guest has multiple active bookings
        // For now, assume potential ambiguity if message contains booking reference
        return Regex.IsMatch(message, @"\b(my|the) (booking|reservation|appointment)\b", RegexOptions.IgnoreCase);
    }

    private async Task<bool> HasAmbiguousMenuItemReferenceAsync(string message, int conversationId)
    {
        try
        {
            _logger.LogInformation("HasAmbiguousMenuItemReferenceAsync called for message: '{Message}', conversationId: {ConversationId}", message, conversationId);
            var messageLower = message.ToLower();

            // Get tenant ID from conversation
            var conversation = await _context.Conversations
                .Where(c => c.Id == conversationId)
                .Select(c => c.TenantId)
                .FirstOrDefaultAsync();

            if (conversation == 0)
            {
                _logger.LogWarning("Could not find conversation {ConversationId} for ambiguity detection", conversationId);
                return false;
            }

            // Define ambiguous terms and their database search patterns
            var ambiguousTermChecks = new Dictionary<string, string>
            {
                [@"\bwater\b"] = "water",
                [@"\bcoffee\b"] = "coffee",
                [@"\btea\b"] = "tea",
                [@"\bjuice\b"] = "juice",
                [@"\bsoda\b"] = "soda",
                [@"\bbeer\b"] = "beer",
                [@"\bwine\b"] = "wine"
            };

            foreach (var termCheck in ambiguousTermChecks)
            {
                var regex = termCheck.Key;
                var searchTerm = termCheck.Value;

                if (Regex.IsMatch(messageLower, regex))
                {
                    _logger.LogInformation("Found potential ambiguous term: '{SearchTerm}' in message", searchTerm);

                    // For water, check if user already specified type
                    if (searchTerm == "water" &&
                        Regex.IsMatch(messageLower, @"\b(still|sparkling|bottled|tap)\s*water\b"))
                    {
                        _logger.LogInformation("Water request is specific, not ambiguous");
                        continue;
                    }

                    // Check database for multiple matching items
                    var matchingItems = await _context.MenuItems
                        .Where(m => m.TenantId == conversation &&
                                   m.IsAvailable &&
                                   m.Name.ToLower().Contains(searchTerm))
                        .Select(m => new { m.Name, m.Description })
                        .ToListAsync();

                    var matchingRequestItems = await _context.RequestItems
                        .Where(r => r.TenantId == conversation &&
                                   r.IsAvailable &&
                                   r.Name.ToLower().Contains(searchTerm))
                        .Select(r => new { r.Name, r.Description })
                        .ToListAsync();

                    var totalMatches = matchingItems.Count + matchingRequestItems.Count;

                    _logger.LogInformation("Found {MenuItems} menu items and {RequestItems} request items matching '{SearchTerm}' for tenant {TenantId}",
                        matchingItems.Count, matchingRequestItems.Count, searchTerm, conversation);

                    if (totalMatches > 1)
                    {
                        _logger.LogInformation("Detected ambiguous {SearchTerm} reference - {TotalMatches} options available: {MenuItems}, {RequestItems}",
                            searchTerm, totalMatches,
                            string.Join(", ", matchingItems.Select(i => i.Name)),
                            string.Join(", ", matchingRequestItems.Select(i => i.Name)));
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for ambiguous menu item references");
            return false;
        }
    }

    private ConfidenceLevel DetermineEnhancedConfidenceLevel(List<AmbiguityType> ambiguityTypes, string message, List<string> matchedPatterns)
    {
        if (!ambiguityTypes.Any()) return ConfidenceLevel.VeryHigh;

        // Calculate weighted confidence based on severity and number of matches
        double totalWeight = 0;
        double maxWeight = 0;

        foreach (var type in ambiguityTypes)
        {
            var weight = AmbiguitySeverityWeights.GetValueOrDefault(type, 0.1);
            totalWeight += weight;
            maxWeight = Math.Max(maxWeight, weight);
        }

        // Factor in message length and specificity
        var messageSpecificity = CalculateMessageSpecificity(message);
        var adjustedWeight = totalWeight * (1.0 - messageSpecificity * 0.3);

        return adjustedWeight switch
        {
            >= 0.8 => ConfidenceLevel.VeryHigh,
            >= 0.6 => ConfidenceLevel.High,
            >= 0.4 => ConfidenceLevel.Medium,
            >= 0.2 => ConfidenceLevel.Low,
            _ => ConfidenceLevel.VeryLow
        };
    }

    private double CalculateMessageSpecificity(string message)
    {
        var specificityScore = 0.0;
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Check for specific entities
        if (Regex.IsMatch(message, @"\b\d{3,4}\b")) specificityScore += 0.2; // Room number
        if (Regex.IsMatch(message, @"\b\d{1,2}:\d{2}\s*(am|pm)?\b", RegexOptions.IgnoreCase)) specificityScore += 0.2; // Time
        if (Regex.IsMatch(message, @"\b(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b", RegexOptions.IgnoreCase)) specificityScore += 0.1; // Day
        if (Regex.IsMatch(message, @"\b(spa|restaurant|room service|housekeeping|maintenance|concierge)\b", RegexOptions.IgnoreCase)) specificityScore += 0.2; // Service
        if (words.Length > 10) specificityScore += 0.1; // Longer messages tend to be more specific
        if (words.Length > 20) specificityScore += 0.2;

        return Math.Min(1.0, specificityScore);
    }

    private string GenerateDetailedExplanation(List<AmbiguityType> ambiguityTypes, List<string> matchedPatterns)
    {
        if (!ambiguityTypes.Any()) return "Message is clear and unambiguous.";

        var explanations = new List<string>();

        foreach (var type in ambiguityTypes)
        {
            var explanation = type switch
            {
                AmbiguityType.TemporalVague => "Contains vague time references that need clarification",
                AmbiguityType.MissingContext => "Missing specific context about what is being referenced",
                AmbiguityType.IncompleteRequest => "Request is incomplete and needs more details to proceed",
                AmbiguityType.MultipleOptions => "Could refer to multiple items, bookings, or services",
                AmbiguityType.PrivacyViolation => "May involve sharing private information about other guests (not allowed)",
                AmbiguityType.ConflictingContext => "Conflicts with current guest status or booking context",
                AmbiguityType.MultipleIntents => "Contains multiple different requests that should be handled separately",
                AmbiguityType.ImpossibleRequest => "Request may not be possible given current constraints",
                _ => "Contains unclear elements"
            };
            explanations.Add(explanation);
        }

        var result = $"Ambiguous because: {string.Join("; ", explanations)}.";

        if (matchedPatterns.Any())
        {
            result += $" Detected patterns: {string.Join(", ", matchedPatterns.Take(3))}.";
        }

        return result;
    }

    private ConfidenceLevel DetermineConfidenceLevel(List<AmbiguityType> ambiguityTypes, string message)
    {
        if (!ambiguityTypes.Any()) return ConfidenceLevel.VeryHigh;

        // High confidence in ambiguity if multiple types detected
        if (ambiguityTypes.Count >= 3) return ConfidenceLevel.VeryHigh;
        if (ambiguityTypes.Count == 2) return ConfidenceLevel.High;

        // Check severity of single ambiguity type
        var singleType = ambiguityTypes.First();
        return singleType switch
        {
            AmbiguityType.PrivacyViolation => ConfidenceLevel.VeryHigh,
            AmbiguityType.ConflictingContext => ConfidenceLevel.VeryHigh,
            AmbiguityType.MultipleOptions => ConfidenceLevel.High,
            AmbiguityType.IncompleteRequest => ConfidenceLevel.Medium,
            AmbiguityType.TemporalVague => ConfidenceLevel.Medium,
            AmbiguityType.MissingContext => ConfidenceLevel.Medium,
            _ => ConfidenceLevel.Low
        };
    }

    private string GenerateExplanation(List<AmbiguityType> ambiguityTypes)
    {
        if (!ambiguityTypes.Any()) return "Message is clear and unambiguous.";

        var explanations = ambiguityTypes.Select(type => type switch
        {
            AmbiguityType.TemporalVague => "Contains vague time references",
            AmbiguityType.MissingContext => "Missing specific context about what is being referenced",
            AmbiguityType.IncompleteRequest => "Request is incomplete and needs more details",
            AmbiguityType.MultipleOptions => "Could refer to multiple items/bookings",
            AmbiguityType.PrivacyViolation => "May involve sharing private information about other guests",
            AmbiguityType.ConflictingContext => "Conflicts with current guest status or context",
            AmbiguityType.MultipleIntents => "Contains multiple different requests",
            _ => "Contains unclear elements"
        });

        return $"Ambiguous because: {string.Join(", ", explanations)}.";
    }

    private async Task<List<object>> GetGuestBookingOptionsAsync(int conversationId)
    {
        try
        {
            // This would retrieve actual booking data for the guest
            // For now, return placeholder options
            return new List<object>
            {
                new { id = 1, type = "Restaurant", date = "Tonight 7 PM", location = "Main Restaurant" },
                new { id = 2, type = "Spa", date = "Tomorrow 2 PM", location = "Wellness Center" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking options for conversation {ConversationId}", conversationId);
            return new List<object>();
        }
    }

    private async Task<List<object>> GetMenuItemOptionsAsync(int conversationId, string itemType)
    {
        try
        {
            // Get tenant ID from conversation
            var tenantId = await _context.Conversations
                .Where(c => c.Id == conversationId)
                .Select(c => c.TenantId)
                .FirstOrDefaultAsync();

            if (tenantId == 0)
            {
                _logger.LogWarning("Could not find conversation {ConversationId} for menu options", conversationId);
                return new List<object>();
            }

            // Get matching menu items
            var menuItems = await _context.MenuItems
                .Where(m => m.TenantId == tenantId &&
                           m.IsAvailable &&
                           m.Name.ToLower().Contains(itemType.ToLower()))
                .Select(m => new { m.Name, m.Description, m.PriceCents, m.Currency })
                .ToListAsync();

            // Get matching request items
            var requestItems = await _context.RequestItems
                .Where(r => r.TenantId == tenantId &&
                           r.IsAvailable &&
                           r.Name.ToLower().Contains(itemType.ToLower()))
                .Select(r => new { r.Name, r.Description })
                .ToListAsync();

            var options = new List<object>();

            // Add menu items
            foreach (var item in menuItems)
            {
                options.Add(new {
                    name = item.Name,
                    description = item.Description,
                    price = $"{item.PriceCents / 100.0:F2} {item.Currency}",
                    type = "menu"
                });
            }

            // Add request items
            foreach (var item in requestItems)
            {
                options.Add(new {
                    name = item.Name,
                    description = item.Description,
                    type = "request"
                });
            }

            _logger.LogInformation("Found {Count} {ItemType} options for conversation {ConversationId}",
                options.Count, itemType, conversationId);

            return options;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting menu item options for {ItemType} in conversation {ConversationId}", itemType, conversationId);
            return new List<object>();
        }
    }
}