using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hostr.Api.Services;

public interface IHumanResponsePatternService
{
    Task<string> HumanizeResponseAsync(HumanizationRequest request);
    Task<PersonalityProfile> GetPersonalityProfileAsync(int tenantId, string context = "default");
    Task<string> ApplyPersonalityTraitsAsync(string response, PersonalityProfile personality);
    Task<string> AddEmotionalIntelligenceAsync(string response, EmotionalContext context);
    Task<string> VariateResponsePatternAsync(string response, VariationLevel level = VariationLevel.Medium);
    Task<ConsistencyAnalysis> AnalyzeResponseConsistencyAsync(List<string> responses, PersonalityProfile personality);
}

public enum VariationLevel
{
    Minimal = 1,    // Keep very consistent
    Low = 2,        // Slight variations
    Medium = 3,     // Moderate variations
    High = 4,       // Significant variations
    Creative = 5    // Maximum creativity
}

public enum EmotionalTone
{
    Neutral = 1,
    Friendly = 2,
    Enthusiastic = 3,
    Empathetic = 4,
    Professional = 5,
    Apologetic = 6,
    Excited = 7,
    Concerned = 8
}

public enum ResponseStyle
{
    Formal = 1,
    Casual = 2,
    Warm = 3,
    Direct = 4,
    Detailed = 5,
    Concise = 6,
    Playful = 7,
    Serious = 8
}

public class HumanizationRequest
{
    public string OriginalResponse { get; set; } = string.Empty;
    public int TenantId { get; set; }
    public EmotionalContext EmotionalContext { get; set; } = new();
    public PersonalityProfile? PersonalityOverride { get; set; }
    public VariationLevel VariationLevel { get; set; } = VariationLevel.Medium;
    public List<string> RecentResponses { get; set; } = new(); // For consistency
    public string UserMessage { get; set; } = string.Empty;
    public string ConversationContext { get; set; } = string.Empty;
}

public class PersonalityProfile
{
    public string Name { get; set; } = string.Empty;
    public ResponseStyle PrimaryStyle { get; set; } = ResponseStyle.Warm;
    public EmotionalTone DefaultTone { get; set; } = EmotionalTone.Friendly;
    public double Formality { get; set; } = 0.6; // 0.0 = very casual, 1.0 = very formal
    public double Enthusiasm { get; set; } = 0.7; // 0.0 = neutral, 1.0 = very enthusiastic
    public double Empathy { get; set; } = 0.8; // 0.0 = robotic, 1.0 = very empathetic
    public double Verbosity { get; set; } = 0.6; // 0.0 = concise, 1.0 = detailed
    public List<string> PreferredPhrases { get; set; } = new();
    public List<string> AvoidedPhrases { get; set; } = new();
    public Dictionary<string, string> PersonalityTriggers { get; set; } = new(); // situation -> response style
}

public class EmotionalContext
{
    public EmotionalTone UserTone { get; set; } = EmotionalTone.Neutral;
    public double UserFrustrationLevel { get; set; } = 0.0; // 0.0 = calm, 1.0 = very frustrated
    public double UrgencyLevel { get; set; } = 0.0; // 0.0 = no rush, 1.0 = emergency
    public bool IsFirstInteraction { get; set; } = false;
    public bool IsComplaintResolution { get; set; } = false;
    public bool RequiresApology { get; set; } = false;
    public string ConversationMood { get; set; } = "neutral";
}

public class ConsistencyAnalysis
{
    public double ConsistencyScore { get; set; } // 0.0 = inconsistent, 1.0 = perfectly consistent
    public List<string> InconsistentElements { get; set; } = new();
    public List<string> ConsistentPatterns { get; set; } = new();
    public PersonalityProfile DetectedPersonality { get; set; } = new();
    public List<string> RecommendedAdjustments { get; set; } = new();
}

public class HumanResponsePatternService : IHumanResponsePatternService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<HumanResponsePatternService> _logger;

    // Personality-based phrase libraries
    private static readonly Dictionary<ResponseStyle, List<string>> StylePhrases = new()
    {
        { ResponseStyle.Warm, new List<string> { "I'd be happy to help", "I understand", "That sounds lovely", "Absolutely", "Of course" } },
        { ResponseStyle.Formal, new List<string> { "I can assist you with", "Please allow me to", "I recommend", "Our policy", "According to" } },
        { ResponseStyle.Casual, new List<string> { "Sure thing", "No problem", "Got it", "Cool", "Awesome" } },
        { ResponseStyle.Playful, new List<string> { "Oh, that's exciting", "How fun", "I love that", "That's fantastic", "Wonderful" } },
        { ResponseStyle.Direct, new List<string> { "Here's what you need", "The answer is", "Simply put", "Directly", "To be clear" } }
    };

    private static readonly Dictionary<EmotionalTone, List<string>> TonePhrases = new()
    {
        { EmotionalTone.Empathetic, new List<string> { "I understand how that feels", "That must be frustrating", "I can imagine", "I hear you" } },
        { EmotionalTone.Apologetic, new List<string> { "I apologize for", "I'm sorry about", "My sincere apologies", "Please forgive" } },
        { EmotionalTone.Enthusiastic, new List<string> { "That's wonderful", "How exciting", "I'm thrilled to", "Fantastic" } },
        { EmotionalTone.Professional, new List<string> { "I would be pleased to", "Allow me to assist", "I can help you with", "Our team" } }
    };

    private static readonly Dictionary<string, List<string>> VariationTemplates = new()
    {
        { "greeting", new List<string> { "Hello", "Hi there", "Good [timeofday]", "Welcome", "Greetings" } },
        { "affirmation", new List<string> { "Absolutely", "Certainly", "Of course", "Definitely", "Without a doubt" } },
        { "transition", new List<string> { "Additionally", "Also", "Furthermore", "In addition", "Moreover" } },
        { "closing", new List<string> { "Is there anything else", "How else can I help", "What else would you like", "Anything additional" } }
    };

    public HumanResponsePatternService(HostrDbContext context, ILogger<HumanResponsePatternService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> HumanizeResponseAsync(HumanizationRequest request)
    {
        try
        {
            var personality = request.PersonalityOverride ??
                             await GetPersonalityProfileAsync(request.TenantId, request.ConversationContext);

            var humanizedResponse = request.OriginalResponse;

            // Apply personality traits
            humanizedResponse = await ApplyPersonalityTraitsAsync(humanizedResponse, personality);

            // Add emotional intelligence
            humanizedResponse = await AddEmotionalIntelligenceAsync(humanizedResponse, request.EmotionalContext);

            // Apply response variation to avoid roboticism
            humanizedResponse = await VariateResponsePatternAsync(humanizedResponse, request.VariationLevel);

            // Ensure consistency with recent responses
            humanizedResponse = await EnsurePersonalityConsistencyAsync(humanizedResponse, request.RecentResponses, personality);

            // Add contextual human touches
            humanizedResponse = await AddContextualHumanTouchesAsync(humanizedResponse, request);

            return humanizedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error humanizing response");
            return request.OriginalResponse; // Fallback to original response
        }
    }

    public async Task<PersonalityProfile> GetPersonalityProfileAsync(int tenantId, string context = "default")
    {
        try
        {
            // In production, this would be stored in database per tenant
            // For now, return a configurable default personality

            var hotelInfo = await _context.HotelInfos.FirstOrDefaultAsync(h => h.TenantId == tenantId);

            if (hotelInfo?.Category?.ToLower().Contains("luxury") == true)
            {
                return new PersonalityProfile
                {
                    Name = "Luxury Concierge",
                    PrimaryStyle = ResponseStyle.Formal,
                    DefaultTone = EmotionalTone.Professional,
                    Formality = 0.8,
                    Enthusiasm = 0.6,
                    Empathy = 0.9,
                    Verbosity = 0.7,
                    PreferredPhrases = new List<string> { "It would be my pleasure", "Allow me to", "I would be delighted", "Certainly" },
                    AvoidedPhrases = new List<string> { "sure thing", "cool", "awesome", "no problem" }
                };
            }
            else if (hotelInfo?.Category?.ToLower().Contains("budget") == true || hotelInfo?.Category?.ToLower().Contains("business") == true)
            {
                return new PersonalityProfile
                {
                    Name = "Friendly Helper",
                    PrimaryStyle = ResponseStyle.Casual,
                    DefaultTone = EmotionalTone.Friendly,
                    Formality = 0.4,
                    Enthusiasm = 0.8,
                    Empathy = 0.7,
                    Verbosity = 0.5,
                    PreferredPhrases = new List<string> { "Sure thing", "Happy to help", "No problem", "Sounds good" },
                    AvoidedPhrases = new List<string> { "I would be pleased to", "Allow me to", "It would be my pleasure" }
                };
            }
            else
            {
                // Default warm, professional personality
                return new PersonalityProfile
                {
                    Name = "Warm Professional",
                    PrimaryStyle = ResponseStyle.Warm,
                    DefaultTone = EmotionalTone.Friendly,
                    Formality = 0.6,
                    Enthusiasm = 0.7,
                    Empathy = 0.8,
                    Verbosity = 0.6,
                    PreferredPhrases = new List<string> { "I'd be happy to help", "Absolutely", "I understand", "Of course" },
                    AvoidedPhrases = new List<string> { "negative", "impossible", "can't do that", "no way" }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting personality profile for tenant {TenantId}", tenantId);
            return new PersonalityProfile(); // Return default
        }
    }

    public async Task<string> ApplyPersonalityTraitsAsync(string response, PersonalityProfile personality)
    {
        try
        {
            var modifiedResponse = response;

            // Apply formality level
            modifiedResponse = AdjustFormality(modifiedResponse, personality.Formality);

            // Apply enthusiasm level
            modifiedResponse = AdjustEnthusiasm(modifiedResponse, personality.Enthusiasm);

            // Apply verbosity
            modifiedResponse = AdjustVerbosity(modifiedResponse, personality.Verbosity);

            // Replace with preferred phrases
            modifiedResponse = ApplyPreferredPhrases(modifiedResponse, personality);

            // Apply style-specific modifications
            modifiedResponse = ApplyStyleModifications(modifiedResponse, personality.PrimaryStyle);

            return modifiedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying personality traits");
            return response;
        }
    }

    public async Task<string> AddEmotionalIntelligenceAsync(string response, EmotionalContext context)
    {
        try
        {
            var emotionalResponse = response;

            // Handle user frustration
            if (context.UserFrustrationLevel > 0.7)
            {
                emotionalResponse = AddEmpathyAndCalming(emotionalResponse);
            }

            // Handle urgency
            if (context.UrgencyLevel > 0.8)
            {
                emotionalResponse = AddUrgencyAcknowledgment(emotionalResponse);
            }

            // Handle complaints
            if (context.IsComplaintResolution || context.RequiresApology)
            {
                emotionalResponse = AddApologyAndResolution(emotionalResponse);
            }

            // First interaction warmth
            if (context.IsFirstInteraction)
            {
                emotionalResponse = AddWelcomingTone(emotionalResponse);
            }

            // Match user's emotional tone appropriately
            emotionalResponse = await MatchEmotionalToneAsync(emotionalResponse, context.UserTone);

            return emotionalResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding emotional intelligence");
            return response;
        }
    }

    public async Task<string> VariateResponsePatternAsync(string response, VariationLevel level = VariationLevel.Medium)
    {
        try
        {
            if (level == VariationLevel.Minimal) return response;

            var variatedResponse = response;

            // Apply variation based on level
            var variationIntensity = (int)level / 5.0;

            // Vary sentence starters
            variatedResponse = VaryAfirmations(variatedResponse, variationIntensity);

            // Vary transitions
            variatedResponse = VaryTransitions(variatedResponse, variationIntensity);

            // Add conversational fillers based on level
            if (level >= VariationLevel.Medium)
            {
                variatedResponse = AddConversationalFillers(variatedResponse, variationIntensity);
            }

            // Add personality-based interjections
            if (level >= VariationLevel.High)
            {
                variatedResponse = AddPersonalityInterjections(variatedResponse);
            }

            return variatedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error varying response pattern");
            return response;
        }
    }

    public async Task<ConsistencyAnalysis> AnalyzeResponseConsistencyAsync(List<string> responses, PersonalityProfile personality)
    {
        try
        {
            var analysis = new ConsistencyAnalysis();

            // Analyze formality consistency
            var formalityScores = responses.Select(CalculateFormalityScore).ToList();
            var formalityConsistency = CalculateConsistency(formalityScores);

            // Analyze tone consistency
            var toneConsistency = AnalyzeToneConsistency(responses);

            // Analyze phrase usage consistency
            var phraseConsistency = AnalyzePhraseConsistency(responses, personality);

            // Calculate overall consistency
            analysis.ConsistencyScore = (formalityConsistency + toneConsistency + phraseConsistency) / 3.0;

            // Generate recommendations
            analysis.RecommendedAdjustments = GenerateConsistencyRecommendations(analysis.ConsistencyScore, responses);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing response consistency");
            return new ConsistencyAnalysis { ConsistencyScore = 0.5 };
        }
    }

    // Helper methods

    private string AdjustFormality(string response, double formalityLevel)
    {
        if (formalityLevel > 0.7) // Formal
        {
            response = response.Replace("can't", "cannot");
            response = response.Replace("won't", "will not");
            response = response.Replace("I'll", "I will");
            response = response.Replace("you're", "you are");
        }
        else if (formalityLevel < 0.4) // Casual
        {
            response = response.Replace("cannot", "can't");
            response = response.Replace("will not", "won't");
            response = response.Replace("I would", "I'd");
        }

        return response;
    }

    private string AdjustEnthusiasm(string response, double enthusiasmLevel)
    {
        if (enthusiasmLevel > 0.8)
        {
            // Add enthusiastic punctuation and words
            if (!response.Contains("!") && enthusiasmLevel > 0.9)
            {
                response = response.Replace(".", "!");
            }

            // Add enthusiastic modifiers
            response = AddEnthusiasticModifiers(response);
        }

        return response;
    }

    private string AdjustVerbosity(string response, double verbosityLevel)
    {
        if (verbosityLevel > 0.7)
        {
            // Make more detailed
            response = AddDetailingPhrases(response);
        }
        else if (verbosityLevel < 0.4)
        {
            // Make more concise
            response = RemoveUnnecessaryWords(response);
        }

        return response;
    }

    private string ApplyPreferredPhrases(string response, PersonalityProfile personality)
    {
        // Replace generic phrases with preferred ones
        var commonReplacements = new Dictionary<string, string>
        {
            { "yes", personality.PreferredPhrases.FirstOrDefault() ?? "yes" },
            { "okay", personality.PreferredPhrases.Skip(1).FirstOrDefault() ?? "okay" }
        };

        foreach (var (original, preferred) in commonReplacements)
        {
            if (response.ToLower().Contains(original.ToLower()))
            {
                response = Regex.Replace(response, $"\\b{original}\\b", preferred, RegexOptions.IgnoreCase);
            }
        }

        return response;
    }

    private string ApplyStyleModifications(string response, ResponseStyle style)
    {
        if (StylePhrases.TryGetValue(style, out var phrases))
        {
            // Randomly insert style-appropriate phrases
            if (phrases.Any() && Random.Shared.NextDouble() > 0.7)
            {
                var phrase = phrases[Random.Shared.Next(phrases.Count)];
                response = $"{phrase}! {response}";
            }
        }

        return response;
    }

    private string AddEmpathyAndCalming(string response)
    {
        var empathyPhrases = new[] { "I understand this can be frustrating", "I can see why this would be concerning", "Let me help resolve this for you" };
        var phrase = empathyPhrases[Random.Shared.Next(empathyPhrases.Length)];
        return $"{phrase}. {response}";
    }

    private string AddUrgencyAcknowledgment(string response)
    {
        var urgencyPhrases = new[] { "I'll prioritize this for you", "Let me address this right away", "I understand this is urgent" };
        var phrase = urgencyPhrases[Random.Shared.Next(urgencyPhrases.Length)];
        return $"{phrase}. {response}";
    }

    private string AddApologyAndResolution(string response)
    {
        if (!response.ToLower().Contains("sorry") && !response.ToLower().Contains("apologize"))
        {
            return $"I apologize for any inconvenience. {response}";
        }
        return response;
    }

    private string AddWelcomingTone(string response)
    {
        var welcomePhrases = new[] { "Welcome", "Thank you for choosing us", "I'm here to help" };
        var phrase = welcomePhrases[Random.Shared.Next(welcomePhrases.Length)];
        return $"{phrase}! {response}";
    }

    private async Task<string> MatchEmotionalToneAsync(string response, EmotionalTone userTone)
    {
        if (TonePhrases.TryGetValue(userTone, out var phrases) && phrases.Any())
        {
            if (Random.Shared.NextDouble() > 0.8) // 20% chance to add matching tone
            {
                var phrase = phrases[Random.Shared.Next(phrases.Count)];
                response = $"{phrase}. {response}";
            }
        }

        return response;
    }

    private string VaryAfirmations(string response, double intensity)
    {
        if (VariationTemplates.TryGetValue("affirmation", out var affirmations))
        {
            foreach (var basic in new[] { "yes", "ok", "okay" })
            {
                if (response.ToLower().Contains(basic) && Random.Shared.NextDouble() < intensity)
                {
                    var replacement = affirmations[Random.Shared.Next(affirmations.Count)];
                    response = Regex.Replace(response, $"\\b{basic}\\b", replacement, RegexOptions.IgnoreCase);
                }
            }
        }

        return response;
    }

    private string VaryTransitions(string response, double intensity)
    {
        if (VariationTemplates.TryGetValue("transition", out var transitions) && Random.Shared.NextDouble() < intensity)
        {
            // Add varied transitions between sentences
            var sentences = response.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (sentences.Length > 1)
            {
                var transition = transitions[Random.Shared.Next(transitions.Count)];
                sentences[1] = $" {transition}, {sentences[1].Trim()}";
                response = string.Join(".", sentences) + ".";
            }
        }

        return response;
    }

    private string AddConversationalFillers(string response, double intensity)
    {
        var fillers = new[] { "Well", "You know", "Actually", "Indeed", "Certainly" };

        if (Random.Shared.NextDouble() < intensity * 0.3) // Reduced probability
        {
            var filler = fillers[Random.Shared.Next(fillers.Length)];
            response = $"{filler}, {response.ToLower()}";
        }

        return response;
    }

    private string AddPersonalityInterjections(string response)
    {
        var interjections = new[] { "Oh", "Ah", "Hmm", "Right" };

        if (Random.Shared.NextDouble() > 0.85) // Low probability
        {
            var interjection = interjections[Random.Shared.Next(interjections.Length)];
            response = $"{interjection}! {response}";
        }

        return response;
    }

    private async Task<string> EnsurePersonalityConsistencyAsync(string response, List<string> recentResponses, PersonalityProfile personality)
    {
        // Ensure the response maintains personality consistency with recent interactions
        if (recentResponses.Any())
        {
            var avgFormality = recentResponses.Select(CalculateFormalityScore).Average();
            var currentFormality = CalculateFormalityScore(response);

            // Adjust if too different
            if (Math.Abs(avgFormality - currentFormality) > 0.3)
            {
                response = AdjustFormality(response, avgFormality);
            }
        }

        return response;
    }

    private async Task<string> AddContextualHumanTouchesAsync(string response, HumanizationRequest request)
    {
        // Add small human touches based on context
        if (request.UserMessage.ToLower().Contains("thank"))
        {
            if (!response.ToLower().Contains("welcome") && Random.Shared.NextDouble() > 0.7)
            {
                response += " You're very welcome!";
            }
        }

        return response;
    }

    private string AddEnthusiasticModifiers(string response)
    {
        var modifiers = new[] { "really", "absolutely", "definitely", "certainly" };
        var words = response.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].ToLower() == "good" || words[i].ToLower() == "great")
            {
                if (Random.Shared.NextDouble() > 0.7)
                {
                    var modifier = modifiers[Random.Shared.Next(modifiers.Length)];
                    words[i] = $"{modifier} {words[i]}";
                }
            }
        }

        return string.Join(" ", words);
    }

    private string AddDetailingPhrases(string response)
    {
        var detailPhrases = new[] { "specifically", "in particular", "to be precise", "more precisely" };

        if (Random.Shared.NextDouble() > 0.8)
        {
            var phrase = detailPhrases[Random.Shared.Next(detailPhrases.Length)];
            response = response.Replace(".", $", {phrase}.");
        }

        return response;
    }

    private string RemoveUnnecessaryWords(string response)
    {
        var unnecessaryWords = new[] { "really", "quite", "very", "absolutely", "definitely" };

        foreach (var word in unnecessaryWords)
        {
            response = Regex.Replace(response, $"\\b{word}\\b\\s*", "", RegexOptions.IgnoreCase);
        }

        return response.Trim();
    }

    private double CalculateFormalityScore(string text)
    {
        var formalIndicators = new[] { "would", "shall", "could", "please", "kindly", "certainly" };
        var informalIndicators = new[] { "can't", "won't", "I'll", "you're", "it's", "that's" };

        var formalCount = formalIndicators.Sum(f => Regex.Matches(text, $"\\b{f}\\b", RegexOptions.IgnoreCase).Count);
        var informalCount = informalIndicators.Sum(f => Regex.Matches(text, $"\\b{f}\\b", RegexOptions.IgnoreCase).Count);

        var totalIndicators = formalCount + informalCount;
        if (totalIndicators == 0) return 0.5; // Neutral

        return (double)formalCount / totalIndicators;
    }

    private double CalculateConsistency(List<double> scores)
    {
        if (!scores.Any()) return 1.0;

        var mean = scores.Average();
        var variance = scores.Select(s => Math.Pow(s - mean, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);

        // Convert to consistency score (lower deviation = higher consistency)
        return Math.Max(0.0, 1.0 - (standardDeviation * 2));
    }

    private double AnalyzeToneConsistency(List<string> responses)
    {
        // Simplified tone consistency analysis
        return 0.8; // Placeholder - in production this would be more sophisticated
    }

    private double AnalyzePhraseConsistency(List<string> responses, PersonalityProfile personality)
    {
        // Check if preferred phrases are being used consistently
        var usageCount = 0;
        var totalPossibleUsage = 0;

        foreach (var response in responses)
        {
            foreach (var phrase in personality.PreferredPhrases)
            {
                totalPossibleUsage++;
                if (response.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    usageCount++;
                }
            }
        }

        return totalPossibleUsage > 0 ? (double)usageCount / totalPossibleUsage : 0.5;
    }

    private List<string> GenerateConsistencyRecommendations(double consistencyScore, List<string> responses)
    {
        var recommendations = new List<string>();

        if (consistencyScore < 0.6)
        {
            recommendations.Add("Consider maintaining more consistent formality levels across responses");
            recommendations.Add("Ensure personality traits are applied uniformly");
        }

        if (consistencyScore < 0.4)
        {
            recommendations.Add("Review and standardize preferred phrase usage");
            recommendations.Add("Implement stronger personality consistency controls");
        }

        return recommendations;
    }
}