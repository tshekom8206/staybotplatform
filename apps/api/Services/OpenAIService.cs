using System.Text.Json;
using Azure.AI.OpenAI;
using Azure;

namespace Hostr.Api.Services;

public class OpenAIResponse
{
    public string Reply { get; set; } = string.Empty;
    public JsonElement? Action { get; set; }
    public JsonElement[]? Actions { get; set; }
    public string Model { get; set; } = string.Empty;
    public int TokensPrompt { get; set; }
    public int TokensCompletion { get; set; }
}

public interface IOpenAIService
{
    Task<float[]?> GetEmbeddingAsync(string text);
    Task<OpenAIResponse?> GenerateResponseAsync(string systemPrompt, string context, string itemsContext, string userMessage, string userPhone);
    Task<OpenAIResponse?> GenerateResponseWithHistoryAsync(string systemPrompt, string context, string itemsContext, List<(string Role, string Content)> conversationHistory, string userMessage, string userPhone);
    Task<T?> GetStructuredResponseAsync<T>(string prompt, double temperature = 0.7) where T : class;
}

public class OpenAIService : IOpenAIService
{
    private readonly ILogger<OpenAIService> _logger;
    private readonly OpenAIClient _openAIClient;

    public OpenAIService(IConfiguration configuration, ILogger<OpenAIService> logger)
    {
        var apiKey = configuration["OpenAI:ApiKey"]!;
        _logger = logger;
        _openAIClient = new OpenAIClient(apiKey);
    }

    public async Task<float[]?> GetEmbeddingAsync(string text)
    {
        try
        {
            _logger.LogInformation("Generating embedding for text: {Text}", text.Substring(0, Math.Min(50, text.Length)));
            
            var embeddingOptions = new EmbeddingsOptions("text-embedding-3-small", new[] { text });
            var response = await _openAIClient.GetEmbeddingsAsync(embeddingOptions);
            var embedding = response.Value.Data[0].Embedding.ToArray();
            
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            return null;
        }
    }


    public async Task<OpenAIResponse?> GenerateResponseAsync(string systemPrompt, string context, string itemsContext, string userMessage, string userPhone)
    {
        try
        {
            _logger.LogInformation("Generating response for user message: {Message}", userMessage);

            var chatOptions = new ChatCompletionsOptions("gpt-4.1-mini-2025-04-14", new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage($"Context: {context}\n\nAvailable Items: {itemsContext}\n\nUser Message: {userMessage}")
            })
            {
                Temperature = 0.0f  // Set to 0 for factual, deterministic responses - NO HALLUCINATION
            };

            var response = await _openAIClient.GetChatCompletionsAsync(chatOptions);
            var choice = response.Value.Choices[0];
            var responseContent = choice.Message.Content ?? "";

            _logger.LogInformation("OpenAI Response Content: {ResponseContent}", responseContent);

            // Handle function calls - parse JSON from response
            JsonElement? action = null;
            JsonElement[]? actions = null;
            
            // Try to parse JSON from response content
            try
            {
                _logger.LogInformation("Attempting to parse JSON actions from response");

                var parsedActions = ExtractActionsFromResponse(responseContent);
                if (parsedActions.SingleAction != null)
                {
                    action = parsedActions.SingleAction;
                    _logger.LogInformation("Successfully parsed single 'action' property: {Action}", parsedActions.SingleAction.Value.ToString());
                }

                if (parsedActions.MultipleActions != null && parsedActions.MultipleActions.Length > 0)
                {
                    actions = parsedActions.MultipleActions;
                    _logger.LogInformation("Successfully parsed multiple actions with {Count} items", parsedActions.MultipleActions.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse JSON from OpenAI response");
            }

            // Clean the response for user display by removing JSON actions
            var cleanReply = CleanResponseText(responseContent);

            return new OpenAIResponse
            {
                Reply = cleanReply,
                Action = action,
                Actions = actions,
                Model = response.Value.Model,
                TokensPrompt = response.Value.Usage.PromptTokens,
                TokensCompletion = response.Value.Usage.CompletionTokens
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response");
            return null;
        }
    }

    public async Task<OpenAIResponse?> GenerateResponseWithHistoryAsync(string systemPrompt, string context, string itemsContext, List<(string Role, string Content)> conversationHistory, string userMessage, string userPhone)
    {
        try
        {
            _logger.LogInformation("Generating response with conversation history for user message: {Message}", userMessage);
            
            var messages = new List<ChatRequestMessage>();
            
            // Add system prompt with enhanced context
            var enhancedSystemPrompt = $"{systemPrompt}\n\nAvailable Items:\n{itemsContext}\n\nKnowledge Base Context:\n{context}";
            messages.Add(new ChatRequestSystemMessage(enhancedSystemPrompt));
            
            // Add conversation history
            foreach (var (role, content) in conversationHistory)
            {
                if (role.ToLower() == "user" || role.ToLower() == "inbound")
                {
                    messages.Add(new ChatRequestUserMessage(content));
                }
                else if (role.ToLower() == "assistant" || role.ToLower() == "outbound")
                {
                    messages.Add(new ChatRequestAssistantMessage(content));
                }
            }
            
            // Add current user message
            messages.Add(new ChatRequestUserMessage(userMessage));

            var chatOptions = new ChatCompletionsOptions("gpt-4.1-mini-2025-04-14", messages)
            {
                Temperature = 0.0f  // Set to 0 for factual, deterministic responses - NO HALLUCINATION
            };

            var response = await _openAIClient.GetChatCompletionsAsync(chatOptions);
            var choice = response.Value.Choices[0];
            var responseContent = choice.Message.Content ?? "";

            _logger.LogInformation("OpenAI Response Content: {ResponseContent}", responseContent);

            // Handle function calls - parse JSON from response
            JsonElement? action = null;
            JsonElement[]? actions = null;
            
            // Try to parse JSON from response content
            try
            {
                _logger.LogInformation("Attempting to parse JSON actions from response");

                var parsedActions = ExtractActionsFromResponse(responseContent);
                if (parsedActions.SingleAction != null)
                {
                    action = parsedActions.SingleAction;
                    _logger.LogInformation("Successfully parsed single 'action' property: {Action}", parsedActions.SingleAction.Value.ToString());
                }

                if (parsedActions.MultipleActions != null && parsedActions.MultipleActions.Length > 0)
                {
                    actions = parsedActions.MultipleActions;
                    _logger.LogInformation("Successfully parsed multiple actions with {Count} items", parsedActions.MultipleActions.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse JSON from OpenAI response");
            }

            // Clean the response for user display by removing JSON actions
            var cleanReply = CleanResponseText(responseContent);

            return new OpenAIResponse
            {
                Reply = cleanReply,
                Action = action,
                Actions = actions,
                Model = response.Value.Model,
                TokensPrompt = response.Value.Usage.PromptTokens,
                TokensCompletion = response.Value.Usage.CompletionTokens
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response with history");
            return null;
        }
    }

    private record ActionParseResult(JsonElement? SingleAction, JsonElement[]? MultipleActions);

    private ActionParseResult ExtractActionsFromResponse(string responseContent)
    {
        if (string.IsNullOrEmpty(responseContent))
        {
            return new ActionParseResult(null, null);
        }

        // Clean markdown code blocks before parsing
        responseContent = CleanMarkdownCodeBlocks(responseContent);

        var actionList = new List<JsonElement>();
        JsonElement? singleAction = null;

        // Use regex to find all JSON objects with "action" property
        var jsonPattern = @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}";
        var matches = System.Text.RegularExpressions.Regex.Matches(responseContent, jsonPattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            try
            {
                var jsonText = match.Value.Trim();
                _logger.LogInformation("Attempting to parse JSON object: {JsonText}", jsonText);

                var jsonDoc = JsonDocument.Parse(jsonText);
                var root = jsonDoc.RootElement;

                // Check if this JSON object has an "action" property
                if (root.TryGetProperty("action", out var actionProp))
                {
                    actionList.Add(actionProp);
                    _logger.LogInformation("Found action in JSON: {Action}", actionProp.ToString());
                }
                // Also check if the root itself is an action object (for backwards compatibility)
                else if (root.TryGetProperty("type", out var _))
                {
                    actionList.Add(root);
                    _logger.LogInformation("Found direct action object: {Action}", root.ToString());
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse potential JSON: {JsonText}", match.Value);
                continue;
            }
        }

        // If we found actions, determine how to return them
        if (actionList.Count > 0)
        {
            if (actionList.Count == 1)
            {
                // Single action - set only singleAction to avoid duplication
                singleAction = actionList[0];
                return new ActionParseResult(singleAction, null);
            }
            else
            {
                // Multiple actions - return as array only
                return new ActionParseResult(null, actionList.ToArray());
            }
        }

        // Fallback: try the old method for single JSON block
        try
        {
            var jsonStartIndex = responseContent.IndexOf('{');
            var jsonEndIndex = responseContent.LastIndexOf('}');

            if (jsonStartIndex >= 0 && jsonEndIndex > jsonStartIndex)
            {
                var potentialJson = responseContent.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);

                // Only parse if it looks like a single complete JSON object
                if (potentialJson.Count(c => c == '{') == potentialJson.Count(c => c == '}'))
                {
                    var jsonDoc = JsonDocument.Parse(potentialJson);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("action", out var actionProp))
                    {
                        return new ActionParseResult(actionProp, null);
                    }
                    else if (root.TryGetProperty("type", out var _))
                    {
                        return new ActionParseResult(root, null);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallback JSON parsing also failed");
        }

        return new ActionParseResult(null, null);
    }

    private static string CleanMarkdownCodeBlocks(string responseContent)
    {
        if (string.IsNullOrEmpty(responseContent))
            return responseContent;

        // Remove markdown code blocks like ```json ... ```
        var codeBlockPattern = @"```(?:json)?\s*\n?(.*?)\n?```";
        var result = System.Text.RegularExpressions.Regex.Replace(
            responseContent,
            codeBlockPattern,
            "$1",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        return result.Trim();
    }

    private static string CleanResponseText(string responseContent)
    {
        if (string.IsNullOrEmpty(responseContent))
            return responseContent;

        // First clean markdown code blocks
        responseContent = CleanMarkdownCodeBlocks(responseContent);

        // Remove JSON blocks that start with "JSON:" and contain action objects
        var lines = responseContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var cleanLines = new List<string>();
        bool insideJsonBlock = false;
        int braceCount = 0;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip lines that start with "JSON:" or "Action:"
            if (trimmedLine.StartsWith("JSON:") || trimmedLine.StartsWith("Action:"))
            {
                continue;
            }

            // Skip markdown code block indicators
            if (trimmedLine.StartsWith("```") || trimmedLine.Equals("```json") || trimmedLine.Equals("```"))
            {
                continue;
            }

            // Detect start of JSON block
            if (trimmedLine.StartsWith("{"))
            {
                insideJsonBlock = true;
                braceCount = trimmedLine.Count(c => c == '{') - trimmedLine.Count(c => c == '}');

                // If it's a complete single-line JSON object, skip it
                if (braceCount == 0)
                {
                    continue;
                }
                // Otherwise continue tracking the multi-line JSON block
                continue;
            }

            // If inside a JSON block, track braces and skip lines
            if (insideJsonBlock)
            {
                braceCount += trimmedLine.Count(c => c == '{') - trimmedLine.Count(c => c == '}');

                // End of JSON block when braces are balanced
                if (braceCount <= 0)
                {
                    insideJsonBlock = false;
                    braceCount = 0;
                }
                continue;
            }

            // Skip lines that look like pure JSON (contain common JSON patterns)
            if (trimmedLine.Contains("\"type\":") ||
                trimmedLine.Contains("\"action\":") ||
                trimmedLine.Contains("\"item_slug\":") ||
                trimmedLine.Contains("\"quantity\":") ||
                (trimmedLine.StartsWith("\"") && trimmedLine.EndsWith("\"")) ||
                (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]")))
            {
                continue;
            }

            cleanLines.Add(line);
        }

        // Join the clean lines and trim any extra whitespace
        var cleanResponse = string.Join("\n", cleanLines).Trim();

        // Remove any trailing "Text:" labels if they exist
        if (cleanResponse.StartsWith("Text: "))
        {
            cleanResponse = cleanResponse.Substring(6).Trim();
        }

        return cleanResponse;
    }

    public async Task<T?> GetStructuredResponseAsync<T>(string prompt, double temperature = 0.0) where T : class
    {
        try
        {
            _logger.LogInformation("Generating structured response of type {Type}", typeof(T).Name);

            var chatOptions = new ChatCompletionsOptions("gpt-4.1-mini-2025-04-14", new List<ChatRequestMessage>
            {
                new ChatRequestUserMessage($"{prompt}\n\nIMPORTANT: Respond only with valid JSON that matches the requested structure. Do not include any additional text, explanations, or markdown formatting.")
            })
            {
                Temperature = (float)temperature,
                MaxTokens = 2000
            };

            var response = await _openAIClient.GetChatCompletionsAsync(chatOptions);
            var responseContent = response.Value.Choices[0].Message.Content?.Trim();

            if (string.IsNullOrEmpty(responseContent))
                return null;

            // Clean the response content
            var cleanedContent = CleanMarkdownCodeBlocks(responseContent);

            _logger.LogInformation("Cleaned JSON content for type {Type}: {Content}", typeof(T).Name, cleanedContent);

            // Parse JSON into the requested type
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var result = JsonSerializer.Deserialize<T>(cleanedContent, options);

            _logger.LogInformation("Successfully parsed structured response of type {Type}", typeof(T).Name);
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response for type {Type}: {Content}", typeof(T).Name, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating structured response for type {Type}", typeof(T).Name);
            return null;
        }
    }
}