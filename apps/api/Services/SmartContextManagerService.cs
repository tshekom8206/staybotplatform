using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services
{
    public interface ISmartContextManagerService
    {
        Task<ConversationContext> BuildContextAsync(int conversationId, int tenantId);
        Task UpdateContextAsync(int conversationId, string newTopic, string lastAction);
        Task<bool> IsContextRelevantAsync(int conversationId, string currentMessage);
        Task<List<string>> GetRelevantHistoryAsync(int conversationId, int messageCount = 5);
        Task<string> GetCurrentTopicAsync(int conversationId);
        Task<Dictionary<string, object>> GetContextVariablesAsync(int conversationId);
        Task StoreContextVariableAsync(int conversationId, string key, object value);
    }

    public class SmartContextManagerService : ISmartContextManagerService
    {
        private readonly HostrDbContext _context;
        private readonly ILogger<SmartContextManagerService> _logger;

        // Context tracking for ongoing conversations
        private readonly Dictionary<int, ConversationContextState> _activeContexts = new();

        public SmartContextManagerService(HostrDbContext context, ILogger<SmartContextManagerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ConversationContext> BuildContextAsync(int conversationId, int tenantId)
        {
            try
            {
                // Get recent messages for context
                var recentMessages = await GetRecentMessagesAsync(conversationId, 10);

                // Get or create context state
                var contextState = await GetOrCreateContextStateAsync(conversationId);

                // Analyze conversation patterns
                var topics = ExtractTopicsFromMessages(recentMessages);
                var currentTopic = DetermineCurrentTopic(topics, contextState.LastTopic);

                // Build context object
                var context = new ConversationContext
                {
                    ConversationId = conversationId,
                    RecentMessages = recentMessages.Select(m => $"{m.Role}: {m.Content}").ToList(),
                    LastInteraction = recentMessages.FirstOrDefault()?.CreatedAt ?? DateTime.UtcNow,
                    CurrentTopic = currentTopic,
                    ConversationState = contextState.Variables ?? new Dictionary<string, object>()
                };

                // Update the active context
                contextState.LastTopic = currentTopic;
                contextState.LastUpdated = DateTime.UtcNow;
                _activeContexts[conversationId] = contextState;

                _logger.LogInformation("Built context for conversation {ConversationId}: Topic={Topic}, Messages={MessageCount}",
                    conversationId, currentTopic, recentMessages.Count);

                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building context for conversation {ConversationId}", conversationId);

                // Return minimal context on error
                return new ConversationContext
                {
                    ConversationId = conversationId,
                    RecentMessages = new List<string>(),
                    LastInteraction = DateTime.UtcNow,
                    CurrentTopic = "general_inquiry"
                };
            }
        }

        public async Task UpdateContextAsync(int conversationId, string newTopic, string lastAction)
        {
            try
            {
                var contextState = await GetOrCreateContextStateAsync(conversationId);

                contextState.LastTopic = newTopic;
                contextState.LastAction = lastAction;
                contextState.LastUpdated = DateTime.UtcNow;

                // Track topic transitions for pattern recognition
                if (contextState.TopicHistory.Count > 10)
                {
                    contextState.TopicHistory.RemoveAt(0); // Keep only recent history
                }
                contextState.TopicHistory.Add(new TopicTransition
                {
                    Topic = newTopic,
                    Timestamp = DateTime.UtcNow,
                    Action = lastAction
                });

                _activeContexts[conversationId] = contextState;

                _logger.LogInformation("Updated context for conversation {ConversationId}: Topic={Topic}, Action={Action}",
                    conversationId, newTopic, lastAction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating context for conversation {ConversationId}", conversationId);
            }
        }

        public async Task<bool> IsContextRelevantAsync(int conversationId, string currentMessage)
        {
            try
            {
                var contextState = await GetOrCreateContextStateAsync(conversationId);
                var currentTopic = await GetCurrentTopicAsync(conversationId);

                // Check if message relates to current topic
                var messageTopics = ExtractTopicsFromMessage(currentMessage);

                // Context is relevant if:
                // 1. Message relates to current topic
                // 2. Or it's a follow-up to recent action
                // 3. Or it contains contextual references

                var isTopicRelated = messageTopics.Contains(currentTopic) ||
                                   IsTopicRelated(messageTopics, currentTopic);

                var isFollowUp = IsFollowUpMessage(currentMessage, contextState.LastAction);

                var hasContextualReferences = HasContextualReferences(currentMessage);

                var isRelevant = isTopicRelated || isFollowUp || hasContextualReferences;

                _logger.LogDebug("Context relevance for conversation {ConversationId}: {IsRelevant} (Topic: {IsTopicRelated}, FollowUp: {IsFollowUp}, References: {HasReferences})",
                    conversationId, isRelevant, isTopicRelated, isFollowUp, hasContextualReferences);

                return isRelevant;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking context relevance for conversation {ConversationId}", conversationId);
                return false; // Default to not relevant on error
            }
        }

        public async Task<List<string>> GetRelevantHistoryAsync(int conversationId, int messageCount = 5)
        {
            try
            {
                var messages = await GetRecentMessagesAsync(conversationId, messageCount * 2); // Get more to filter
                var currentTopic = await GetCurrentTopicAsync(conversationId);

                // Filter messages by relevance to current topic
                var relevantMessages = messages
                    .Where(m => IsMessageRelevantToTopic(m.Content, currentTopic))
                    .Take(messageCount)
                    .Select(m => $"{m.Role}: {m.Content}")
                    .ToList();

                return relevantMessages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting relevant history for conversation {ConversationId}", conversationId);
                return new List<string>();
            }
        }

        public async Task<string> GetCurrentTopicAsync(int conversationId)
        {
            try
            {
                var contextState = await GetOrCreateContextStateAsync(conversationId);
                return contextState.LastTopic ?? "general_inquiry";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current topic for conversation {ConversationId}", conversationId);
                return "general_inquiry";
            }
        }

        public async Task<Dictionary<string, object>> GetContextVariablesAsync(int conversationId)
        {
            try
            {
                var contextState = await GetOrCreateContextStateAsync(conversationId);
                return contextState.Variables ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting context variables for conversation {ConversationId}", conversationId);
                return new Dictionary<string, object>();
            }
        }

        public async Task StoreContextVariableAsync(int conversationId, string key, object value)
        {
            try
            {
                var contextState = await GetOrCreateContextStateAsync(conversationId);
                contextState.Variables ??= new Dictionary<string, object>();
                contextState.Variables[key] = value;

                _activeContexts[conversationId] = contextState;

                _logger.LogDebug("Stored context variable for conversation {ConversationId}: {Key}={Value}",
                    conversationId, key, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing context variable for conversation {ConversationId}", conversationId);
            }
        }

        #region Private Helper Methods

        private async Task<List<ContextMessage>> GetRecentMessagesAsync(int conversationId, int count)
        {
            return await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(count)
                .Select(m => new ContextMessage
                {
                    Role = m.Direction == "Inbound" ? "user" : "assistant",
                    Content = m.Body ?? "",
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();
        }

        private async Task<ConversationContextState> GetOrCreateContextStateAsync(int conversationId)
        {
            if (_activeContexts.TryGetValue(conversationId, out var existingState) &&
                existingState.LastUpdated > DateTime.UtcNow.AddMinutes(-30)) // 30-minute cache
            {
                return existingState;
            }

            // Create new context state
            var newState = new ConversationContextState
            {
                ConversationId = conversationId,
                LastTopic = "general_inquiry",
                LastAction = "start_conversation",
                LastUpdated = DateTime.UtcNow,
                Variables = new Dictionary<string, object>(),
                TopicHistory = new List<TopicTransition>()
            };

            return newState;
        }

        private List<string> ExtractTopicsFromMessages(List<ContextMessage> messages)
        {
            var topics = new HashSet<string>();

            foreach (var message in messages)
            {
                var messageTopics = ExtractTopicsFromMessage(message.Content);
                foreach (var topic in messageTopics)
                {
                    topics.Add(topic);
                }
            }

            return topics.ToList();
        }

        private List<string> ExtractTopicsFromMessage(string message)
        {
            var topics = new List<string>();
            var messageLower = message.ToLower();

            // Define topic keywords - this is much more scalable than complex pattern matching
            var topicKeywords = new Dictionary<string, List<string>>
            {
                { "housekeeping", new List<string> { "towel", "clean", "housekeeping", "toilet paper", "bathroom", "sheets", "pillow" } },
                { "food_service", new List<string> { "menu", "food", "restaurant", "order", "breakfast", "lunch", "dinner", "meal" } },
                { "technical_support", new List<string> { "wifi", "internet", "tv", "remote", "charger", "technical", "connection" } },
                { "reception", new List<string> { "reception", "front desk", "check-in", "check-out", "booking", "reservation" } },
                { "maintenance", new List<string> { "broken", "fix", "repair", "maintenance", "not working", "problem", "issue" } },
                { "concierge", new List<string> { "directions", "recommendation", "tourist", "attraction", "taxi", "transport" } },
                { "room_service", new List<string> { "room service", "deliver", "bring", "send", "room" } },
                { "billing", new List<string> { "bill", "charge", "payment", "cost", "price", "invoice" } },
                { "complaint", new List<string> { "complain", "unhappy", "dissatisfied", "problem", "issue", "bad" } },
                { "feedback", new List<string> { "feedback", "review", "rating", "experience", "good", "excellent", "poor" } }
            };

            foreach (var topicKeyword in topicKeywords)
            {
                if (topicKeyword.Value.Any(keyword => messageLower.Contains(keyword)))
                {
                    topics.Add(topicKeyword.Key);
                }
            }

            return topics.Any() ? topics : new List<string> { "general_inquiry" };
        }

        private string DetermineCurrentTopic(List<string> topics, string lastTopic)
        {
            if (!topics.Any())
                return lastTopic ?? "general_inquiry";

            // Prioritize certain topics
            var priorityTopics = new[] { "maintenance", "complaint", "technical_support", "housekeeping", "food_service" };

            foreach (var priority in priorityTopics)
            {
                if (topics.Contains(priority))
                    return priority;
            }

            return topics.First();
        }

        private bool IsTopicRelated(List<string> messageTopics, string currentTopic)
        {
            if (string.IsNullOrEmpty(currentTopic))
                return false;

            // Define related topic groups
            var relatedTopics = new Dictionary<string, List<string>>
            {
                { "housekeeping", new List<string> { "room_service", "maintenance" } },
                { "food_service", new List<string> { "room_service", "billing" } },
                { "technical_support", new List<string> { "maintenance", "room_service" } },
                { "maintenance", new List<string> { "housekeeping", "technical_support" } },
                { "complaint", new List<string> { "feedback", "maintenance", "housekeeping", "food_service" } }
            };

            if (relatedTopics.TryGetValue(currentTopic, out var related))
            {
                return messageTopics.Any(topic => related.Contains(topic));
            }

            return false;
        }

        private bool IsFollowUpMessage(string message, string lastAction)
        {
            if (string.IsNullOrEmpty(lastAction))
                return false;

            var messageLower = message.ToLower();

            // Simple follow-up patterns
            var followUpIndicators = new[] { "yes", "no", "ok", "thanks", "thank you", "good", "great", "that's all", "nothing else" };

            return followUpIndicators.Any(indicator => messageLower.Contains(indicator)) && message.Length < 50;
        }

        private bool HasContextualReferences(string message)
        {
            var messageLower = message.ToLower();

            // Look for contextual references
            var contextualWords = new[] { "that", "it", "this", "same", "again", "more", "another", "still", "also" };

            return contextualWords.Any(word => messageLower.Contains(word));
        }

        private bool IsMessageRelevantToTopic(string message, string topic)
        {
            var messageTopics = ExtractTopicsFromMessage(message);
            return messageTopics.Contains(topic) || IsTopicRelated(messageTopics, topic);
        }

        #endregion
    }

    // Helper classes for context management
    public class ConversationContextState
    {
        public int ConversationId { get; set; }
        public string? LastTopic { get; set; }
        public string? LastAction { get; set; }
        public DateTime LastUpdated { get; set; }
        public Dictionary<string, object>? Variables { get; set; }
        public List<TopicTransition> TopicHistory { get; set; } = new();
    }

    public class TopicTransition
    {
        public string Topic { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = "";
    }

    public class ContextMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}