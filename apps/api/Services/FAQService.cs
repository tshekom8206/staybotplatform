using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface IFAQService
{
    Task<List<FAQSearchResult>> SearchFAQsAsync(string query, int tenantId, string? language = null, int maxResults = 5);
    Task<FAQ?> GetMostRelevantFAQAsync(string question, int tenantId, string? language = null);
    Task<bool> HasRelevantFAQAsync(string question, int tenantId, double similarityThreshold = 0.7);
}

public class FAQSearchResult
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public double RelevanceScore { get; set; }
}

public class FAQService : IFAQService
{
    private readonly HostrDbContext _context;
    private readonly IOpenAIService _openAIService;
    private readonly ILogger<FAQService> _logger;

    public FAQService(
        HostrDbContext context,
        IOpenAIService openAIService,
        ILogger<FAQService> logger)
    {
        _context = context;
        _openAIService = openAIService;
        _logger = logger;
    }

    public async Task<List<FAQSearchResult>> SearchFAQsAsync(
        string query,
        int tenantId,
        string? language = null,
        int maxResults = 5)
    {
        try
        {
            // Get all FAQs for the tenant
            var faqQuery = _context.FAQs
                .Where(f => f.TenantId == tenantId);

            if (!string.IsNullOrEmpty(language))
            {
                faqQuery = faqQuery.Where(f => f.Language == language);
            }

            var faqs = await faqQuery
                .Select(f => new
                {
                    f.Id,
                    f.Question,
                    f.Answer,
                    f.Language,
                    f.Tags
                })
                .ToListAsync();

            if (!faqs.Any())
            {
                return new List<FAQSearchResult>();
            }

            // Use LLM-based semantic matching for robust FAQ matching
            var llmResults = await GetLLMBasedMatchesAsync(query, faqs, maxResults);
            if (llmResults.Any())
            {
                return llmResults;
            }

            // Fallback to keyword-based matching if LLM fails
            _logger.LogWarning("LLM FAQ matching failed, falling back to keyword-based matching for query: {Query}", query);
            return await GetKeywordBasedMatchesAsync(query, faqs, maxResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching FAQs for tenant {TenantId} with query: {Query}", tenantId, query);
            return new List<FAQSearchResult>();
        }
    }

    private async Task<List<FAQSearchResult>> GetLLMBasedMatchesAsync(
        string query,
        IEnumerable<object> faqs,
        int maxResults)
    {
        try
        {
            // Build FAQ list for LLM - using dynamic casting since the input is anonymous type
            var faqList = faqs.Select((f, index) =>
            {
                dynamic faqDynamic = f;
                return new
                {
                    Index = index + 1,
                    Question = (string)faqDynamic.Question,
                    Tags = faqDynamic.Tags != null ? string.Join(", ", (string[])faqDynamic.Tags) : ""
                };
            }).ToList();

            var faqListText = string.Join("\n", faqList.Select(f =>
                $"{f.Index}. {f.Question} (Tags: {f.Tags})"));

            var prompt = $@"You are an FAQ matching system. Analyze the user's question and determine which FAQ(s) best match their intent.

User Question: ""{query}""

Available FAQs:
{faqListText}

Task: Determine the semantic relevance of each FAQ to the user's question. Consider:
- Synonyms and different phrasings (e.g., ""check-in"" = ""check in"" = ""checking in"")
- Word order variations (e.g., ""what time is checkout"" = ""what is checkout time"")
- Intent matching (e.g., ""do you have wifi"" matches ""WiFi availability"")
- Tags that might indicate topic relevance

CRITICAL RULES:
1. Only match FAQs that are SEMANTICALLY RELATED to the user's question
2. If the user asks about a topic NOT covered by ANY FAQ, return empty matches: {{""matches"": []}}
3. DO NOT try to force a match if no FAQ is actually relevant
4. relevanceScore must be between 0.0 and 1.0
5. Only include FAQs with relevanceScore >= 0.6
6. Order matches by relevanceScore descending

Examples:
- User asks ""do you have parking?"" but no parking FAQ exists → Return {{""matches"": []}}
- User asks ""what time is check-in?"" and FAQ #1 is about check-in → Return match with high score
- User asks ""is breakfast included?"" but no breakfast FAQ exists → Return {{""matches"": []}}

Return a JSON object with this exact structure:
{{
  ""matches"": [
    {{
      ""faqIndex"": 1,
      ""relevanceScore"": 0.95,
      ""reasoning"": ""Direct semantic match - user asking about check-in time""
    }}
  ]
}}

If NO FAQs are semantically relevant, return:
{{
  ""matches"": []
}}

Always return valid JSON with the ""matches"" property.";

            var response = await _openAIService.GetStructuredResponseAsync<LLMFAQMatchResponse>(
                prompt,
                temperature: 0.1);

            if (response?.Matches == null || !response.Matches.Any())
            {
                _logger.LogInformation("LLM found no relevant FAQ matches for query: {Query}", query);
                return new List<FAQSearchResult>();
            }

            var results = new List<FAQSearchResult>();
            foreach (var match in response.Matches.Take(maxResults))
            {
                var faqArray = faqs.ToArray();
                if (match.FaqIndex < 1 || match.FaqIndex > faqArray.Length)
                {
                    _logger.LogWarning("LLM returned invalid FAQ index: {Index}", match.FaqIndex);
                    continue;
                }

                var faq = faqArray[match.FaqIndex - 1];
                dynamic faqDynamic = faq;
                var faqId = (int)faqDynamic.Id;
                var faqQuestion = (string)faqDynamic.Question;
                var faqAnswer = (string)faqDynamic.Answer;
                var faqLanguage = (string)faqDynamic.Language;
                var faqTags = (string[])faqDynamic.Tags ?? Array.Empty<string>();

                results.Add(new FAQSearchResult
                {
                    Id = faqId,
                    Question = faqQuestion,
                    Answer = faqAnswer,
                    Language = faqLanguage,
                    Tags = faqTags,
                    RelevanceScore = match.RelevanceScore
                });

                _logger.LogInformation("LLM FAQ match: Question='{Question}', Score={Score}, Reasoning={Reasoning}",
                    faqQuestion, match.RelevanceScore, match.Reasoning);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM-based FAQ matching");
            return new List<FAQSearchResult>();
        }
    }

    private Task<List<FAQSearchResult>> GetKeywordBasedMatchesAsync(
        string query,
        IEnumerable<object> faqs,
        int maxResults)
    {
        var results = new List<FAQSearchResult>();
        var queryLower = query.ToLower();
        var queryNormalized = NormalizeText(queryLower);

        foreach (var faq in faqs)
        {
            dynamic faqDynamic = faq;
            var questionLower = ((string)faqDynamic.Question).ToLower();
            var questionNormalized = NormalizeText(questionLower);
            var answerLower = ((string)faqDynamic.Answer).ToLower();
            var answerNormalized = NormalizeText(answerLower);

            // Strategy 1: Exact keyword matching (with normalization)
            double keywordScore = 0.0;
            var queryWords = queryNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var questionWords = questionNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var matchingWords = queryWords.Count(qw => questionWords.Any(fw => fw.Contains(qw) || qw.Contains(fw)));
            keywordScore = queryWords.Length > 0 ? (double)matchingWords / queryWords.Length : 0.0;

            // Strategy 2: Tag matching (with normalization)
            double tagScore = 0.0;
            if (faqDynamic.Tags != null && ((string[])faqDynamic.Tags).Length > 0)
            {
                var normalizedTags = ((string[])faqDynamic.Tags).Select(t => NormalizeText(t.ToLower())).ToArray();
                var matchingTags = normalizedTags.Count(t => queryNormalized.Contains(t));
                tagScore = ((string[])faqDynamic.Tags).Length > 0 ? (double)matchingTags / ((string[])faqDynamic.Tags).Length : 0.0;
            }

            // Strategy 3: Direct substring matching (with normalization)
            double substringScore = 0.0;
            if (questionNormalized.Contains(queryNormalized) || queryNormalized.Contains(questionNormalized))
            {
                substringScore = 0.8;
            }
            else if (answerNormalized.Contains(queryNormalized))
            {
                substringScore = 0.5;
            }

            // Combined relevance score (weighted average)
            double relevanceScore = (keywordScore * 0.4) + (tagScore * 0.3) + (substringScore * 0.3);

            if (relevanceScore > 0.2) // Minimum threshold
            {
                results.Add(new FAQSearchResult
                {
                    Id = (int)faqDynamic.Id,
                    Question = (string)faqDynamic.Question,
                    Answer = (string)faqDynamic.Answer,
                    Language = (string)faqDynamic.Language,
                    Tags = (string[])faqDynamic.Tags ?? Array.Empty<string>(),
                    RelevanceScore = relevanceScore
                });
            }
        }

        // Sort by relevance and return top results
        return Task.FromResult(results
            .OrderByDescending(r => r.RelevanceScore)
            .Take(maxResults)
            .ToList());
    }

    public async Task<FAQ?> GetMostRelevantFAQAsync(string question, int tenantId, string? language = null)
    {
        try
        {
            var results = await SearchFAQsAsync(question, tenantId, language, 1);

            if (!results.Any() || results[0].RelevanceScore < 0.5)
            {
                return null;
            }

            var faq = await _context.FAQs
                .FirstOrDefaultAsync(f => f.Id == results[0].Id && f.TenantId == tenantId);

            return faq;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting most relevant FAQ for tenant {TenantId}", tenantId);
            return null;
        }
    }

    public async Task<bool> HasRelevantFAQAsync(string question, int tenantId, double similarityThreshold = 0.7)
    {
        try
        {
            var results = await SearchFAQsAsync(question, tenantId, null, 1);
            return results.Any() && results[0].RelevanceScore >= similarityThreshold;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking FAQ relevance for tenant {TenantId}", tenantId);
            return false;
        }
    }

    /// <summary>
    /// Normalizes text by removing hyphens, extra spaces, and common punctuation to improve matching
    /// </summary>
    private static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Replace hyphens, underscores, and other separators with spaces
        text = text.Replace("-", " ")
                   .Replace("_", " ")
                   .Replace("/", " ");

        // Remove common punctuation that doesn't affect meaning
        text = text.Replace("?", "")
                   .Replace("!", "")
                   .Replace(".", "")
                   .Replace(",", "")
                   .Replace("'", "")
                   .Replace("\"", "");

        // Normalize whitespace - replace multiple spaces with single space
        while (text.Contains("  "))
        {
            text = text.Replace("  ", " ");
        }

        return text.Trim();
    }
}

// LLM Response Models for FAQ Matching
public class LLMFAQMatchResponse
{
    public List<LLMFAQMatch> Matches { get; set; } = new();
}

public class LLMFAQMatch
{
    public int FaqIndex { get; set; }
    public double RelevanceScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}
