using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Hostr.Tests.ChatbotQA.Models;
using Microsoft.Extensions.Logging;

namespace Hostr.Tests.ChatbotQA.Services;

public class ChatbotEvaluatorService
{
    private readonly OpenAIClient _openAIClient;
    private readonly ILogger<ChatbotEvaluatorService> _logger;
    private readonly string _model;

    private const string SystemPrompt = @"You are a quality-assurance evaluator for a hospitality chatbot (StayBot).
Given:
1) the guest message,
2) the chatbot's reply,
3) the hotel's ground-truth data (policy/KB snippets and/or retrieved RAG context),

evaluate the reply on five criteria:

- Understanding (0–1): Did it interpret the request correctly?
- Accuracy (0–1): Is it factually correct vs the ground truth?
- Completeness (0–1): Did it include needed details or ask the right clarifying question(s)?
- Policy Compliance (0–1): Did it respect hours, fees, limits, and plan gating?
- Tone (0–1): Polite, helpful, on-brand (no arguments, no sensitive info leaks).

Output STRICT JSON matching the schema below. Do not add extra text.

Scoring rules:
- If a required fact conflicts with the hotel data → Accuracy=0.
- If info is missing but the bot asked a precise clarifier → Completeness can be ≥0.7.
- If policy is violated (e.g., confirms dinner after kitchen close) → Policy=0.
- If the reply invents facilities/discounts → Accuracy=0, include a hallucination flag.
- Tone penalized for rudeness or unsafe content.

Return an overall verdict:
- ""accurate"" if average score ≥0.85 and no hard violations,
- ""partial"" if 0.6–0.84 or minor issues,
- ""inaccurate"" if <0.6 or any hard violation (policy breach, hallucination).

Output JSON schema:
{
  ""case_id"": ""string"",
  ""scores"": {
    ""understanding"": 0.0,
    ""accuracy"": 0.0,
    ""completeness"": 0.0,
    ""policy_compliance"": 0.0,
    ""tone"": 0.0
  },
  ""verdict"": ""accurate | partial | inaccurate"",
  ""issues"": [""string""],
  ""hallucination"": false,
  ""recommended_fix"": ""string""
}";

    public ChatbotEvaluatorService(string apiKey, string model = "gpt-4", ILogger<ChatbotEvaluatorService>? logger = null)
    {
        _openAIClient = new OpenAIClient(apiKey);
        _model = model;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ChatbotEvaluatorService>.Instance;
    }

    public async Task<EvaluationResult> EvaluateAsync(TestCase testCase, CancellationToken cancellationToken = default)
    {
        try
        {
            var userPayload = JsonSerializer.Serialize(testCase, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = _model,
                Messages =
                {
                    new ChatRequestSystemMessage(SystemPrompt),
                    new ChatRequestUserMessage(userPayload)
                },
                Temperature = 0,
                MaxTokens = 1000
            };

            _logger.LogInformation("Evaluating test case {CaseId}", testCase.CaseId);

            var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions, cancellationToken);
            var content = response.Value.Choices[0].Message.Content;

            _logger.LogDebug("LLM Response for {CaseId}: {Content}", testCase.CaseId, content);

            var result = JsonSerializer.Deserialize<EvaluationResult>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                throw new InvalidOperationException($"Failed to deserialize evaluation result for case {testCase.CaseId}");
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for case {CaseId}. Retrying with reprompt.", testCase.CaseId);

            // Retry once with stricter instruction
            return await RetryWithStricterPromptAsync(testCase, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating case {CaseId}", testCase.CaseId);
            throw;
        }
    }

    private async Task<EvaluationResult> RetryWithStricterPromptAsync(TestCase testCase, CancellationToken cancellationToken)
    {
        var stricterPrompt = SystemPrompt + "\n\nIMPORTANT: Return ONLY valid JSON. No additional text before or after the JSON object.";

        var userPayload = JsonSerializer.Serialize(testCase, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = _model,
            Messages =
            {
                new ChatRequestSystemMessage(stricterPrompt),
                new ChatRequestUserMessage(userPayload)
            },
            Temperature = 0,
            MaxTokens = 1000
        };

        var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions, cancellationToken);
        var content = response.Value.Choices[0].Message.Content.Trim();

        // Try to extract JSON if wrapped in markdown code blocks
        if (content.StartsWith("```json"))
        {
            content = content.Replace("```json", "").Replace("```", "").Trim();
        }
        else if (content.StartsWith("```"))
        {
            content = content.Replace("```", "").Trim();
        }

        var result = JsonSerializer.Deserialize<EvaluationResult>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result == null)
        {
            throw new InvalidOperationException($"Failed to deserialize evaluation result after retry for case {testCase.CaseId}");
        }

        return result;
    }

    public async Task<List<EvaluationResult>> EvaluateBatchAsync(List<TestCase> testCases, CancellationToken cancellationToken = default)
    {
        var results = new List<EvaluationResult>();

        foreach (var testCase in testCases)
        {
            try
            {
                var result = await EvaluateAsync(testCase, cancellationToken);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate case {CaseId}, skipping.", testCase.CaseId);

                // Add a failed result
                results.Add(new EvaluationResult
                {
                    CaseId = testCase.CaseId,
                    Verdict = "inaccurate",
                    Issues = new List<string> { $"Evaluation failed: {ex.Message}" },
                    Scores = new ScoringCriteria()
                });
            }
        }

        return results;
    }
}
