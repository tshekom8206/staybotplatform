using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;

namespace Hostr.Api.Services;

public interface IConversationStateService
{
    Task<ConversationState> GetStateAsync(int conversationId);
    Task UpdateStateAsync(int conversationId, ConversationState state);
    Task SetVariableAsync(int conversationId, string key, object value);
    Task<T?> GetVariableAsync<T>(int conversationId, string key);
    Task AddPendingClarificationAsync(int conversationId, string clarification);
    Task<List<string>> GetPendingClarificationsAsync(int conversationId);
    Task ClearPendingClarificationsAsync(int conversationId);
    Task SetCurrentIntentAsync(int conversationId, string intent, ConfidenceLevel confidence);
    Task<(string intent, ConfidenceLevel confidence)> GetCurrentIntentAsync(int conversationId);
    Task MarkInteractionAsync(int conversationId);
    Task<bool> IsRecentInteractionAsync(int conversationId, TimeSpan threshold);
    Task StoreLastUserMessageAsync(int conversationId, string message);
    Task StoreLastBotResponseAsync(int conversationId, string response);

    // New structured state management methods
    Task CreatePendingStateAsync(int conversationId, int tenantId, string stateType, string entityType, int entityId, string? pendingField = null, string? contextData = null);
    Task<ConversationStateRecord?> GetPendingStateAsync(int conversationId, string stateType, string entityType);
    Task<ConversationStateRecord?> GetAnyPendingStateForConversationAsync(int conversationId);
    Task ResolvePendingStateAsync(int stateId);
    Task<bool> HasPendingStateAsync(int conversationId, string stateType, string entityType);
}

public class ConversationState
{
    public int ConversationId { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
    public List<string> PendingClarifications { get; set; } = new();
    public DateTime LastInteraction { get; set; }
    public string CurrentIntent { get; set; } = string.Empty;
    public ConfidenceLevel IntentConfidence { get; set; } = ConfidenceLevel.Unknown;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public string Context { get; set; } = string.Empty;
    public bool RequiresClarification { get; set; }
    public string LastUserMessage { get; set; } = string.Empty;
    public string LastBotResponse { get; set; } = string.Empty;
    public int MessageCount { get; set; }
}

public enum ConfidenceLevel
{
    Unknown = 0,
    VeryLow = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    VeryHigh = 5
}

public class ConversationStateService : IConversationStateService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<ConversationStateService> _logger;
    private readonly Dictionary<int, ConversationState> _stateCache = new();
    private readonly object _cacheLock = new();

    public ConversationStateService(HostrDbContext context, ILogger<ConversationStateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ConversationState> GetStateAsync(int conversationId)
    {
        try
        {
            lock (_cacheLock)
            {
                if (_stateCache.TryGetValue(conversationId, out var cachedState))
                {
                    return cachedState;
                }
            }

            // Try to load from database - OPTIMIZED to avoid loading all messages
            var conversation = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                throw new ArgumentException($"Conversation {conversationId} not found");
            }

            // Get message count efficiently
            var messageCount = await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .CountAsync();

            var state = new ConversationState
            {
                ConversationId = conversationId,
                LastInteraction = conversation.LastBotReplyAt ?? conversation.CreatedAt,
                CreatedAt = conversation.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                MessageCount = messageCount
            };

            // Load state variables from database
            if (!string.IsNullOrEmpty(conversation.StateVariables))
            {
                try
                {
                    var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(conversation.StateVariables);
                    if (variables != null)
                    {
                        state.Variables = variables;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize StateVariables for conversation {ConversationId}", conversationId);
                }
            }

            // Get only the most recent messages efficiently without loading all
            var lastUserMessage = await _context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId && m.Direction == "Inbound")
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.Body)
                .FirstOrDefaultAsync();

            var lastBotMessage = await _context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId && m.Direction == "Outbound")
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.Body)
                .FirstOrDefaultAsync();

            state.LastUserMessage = lastUserMessage ?? string.Empty;
            state.LastBotResponse = lastBotMessage ?? string.Empty;

            lock (_cacheLock)
            {
                _stateCache[conversationId] = state;
            }

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation state for {ConversationId}", conversationId);
            return CreateDefaultState(conversationId);
        }
    }

    public async Task UpdateStateAsync(int conversationId, ConversationState state)
    {
        try
        {
            state.UpdatedAt = DateTime.UtcNow;
            state.ConversationId = conversationId;

            lock (_cacheLock)
            {
                _stateCache[conversationId] = state;
            }

            // Persist state variables to database
            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId);
            if (conversation != null)
            {
                conversation.StateVariables = JsonSerializer.Serialize(state.Variables);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Persisted conversation state for {ConversationId} to database", conversationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating conversation state for {ConversationId}", conversationId);
        }
    }

    public async Task SetVariableAsync(int conversationId, string key, object value)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            var serializedValue = JsonSerializer.Serialize(value);
            state.Variables[key] = serializedValue;
            await UpdateStateAsync(conversationId, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting variable {Key} for conversation {ConversationId}", key, conversationId);
        }
    }

    public async Task<T?> GetVariableAsync<T>(int conversationId, string key)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            if (state.Variables.TryGetValue(key, out var serializedValue))
            {
                return JsonSerializer.Deserialize<T>(serializedValue);
            }
            return default(T);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting variable {Key} for conversation {ConversationId}", key, conversationId);
            return default(T);
        }
    }

    public async Task AddPendingClarificationAsync(int conversationId, string clarification)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            if (!state.PendingClarifications.Contains(clarification))
            {
                state.PendingClarifications.Add(clarification);
                state.RequiresClarification = true;
                await UpdateStateAsync(conversationId, state);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding pending clarification for conversation {ConversationId}", conversationId);
        }
    }

    public async Task<List<string>> GetPendingClarificationsAsync(int conversationId)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            return new List<string>(state.PendingClarifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending clarifications for conversation {ConversationId}", conversationId);
            return new List<string>();
        }
    }

    public async Task ClearPendingClarificationsAsync(int conversationId)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            state.PendingClarifications.Clear();
            state.RequiresClarification = false;
            await UpdateStateAsync(conversationId, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing pending clarifications for conversation {ConversationId}", conversationId);
        }
    }

    public async Task SetCurrentIntentAsync(int conversationId, string intent, ConfidenceLevel confidence)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            state.CurrentIntent = intent;
            state.IntentConfidence = confidence;
            await UpdateStateAsync(conversationId, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting current intent for conversation {ConversationId}", conversationId);
        }
    }

    public async Task<(string intent, ConfidenceLevel confidence)> GetCurrentIntentAsync(int conversationId)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            return (state.CurrentIntent, state.IntentConfidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current intent for conversation {ConversationId}", conversationId);
            return (string.Empty, ConfidenceLevel.Unknown);
        }
    }

    public async Task MarkInteractionAsync(int conversationId)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            state.LastInteraction = DateTime.UtcNow;
            state.MessageCount++;
            await UpdateStateAsync(conversationId, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking interaction for conversation {ConversationId}", conversationId);
        }
    }

    public async Task<bool> IsRecentInteractionAsync(int conversationId, TimeSpan threshold)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            return DateTime.UtcNow - state.LastInteraction < threshold;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking recent interaction for conversation {ConversationId}", conversationId);
            return false;
        }
    }

    private ConversationState CreateDefaultState(int conversationId)
    {
        return new ConversationState
        {
            ConversationId = conversationId,
            LastInteraction = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Variables = new Dictionary<string, string>(),
            PendingClarifications = new List<string>(),
            CurrentIntent = string.Empty,
            IntentConfidence = ConfidenceLevel.Unknown,
            Context = string.Empty,
            RequiresClarification = false,
            LastUserMessage = string.Empty,
            LastBotResponse = string.Empty,
            MessageCount = 0
        };
    }

    // Helper methods for common conversation state operations
    public async Task StoreLastUserMessageAsync(int conversationId, string message)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            state.LastUserMessage = message;
            await UpdateStateAsync(conversationId, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing last user message for conversation {ConversationId}", conversationId);
        }
    }

    public async Task StoreLastBotResponseAsync(int conversationId, string response)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            state.LastBotResponse = response;
            await UpdateStateAsync(conversationId, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing last bot response for conversation {ConversationId}", conversationId);
        }
    }

    public async Task<bool> HasPendingClarificationsAsync(int conversationId)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            return state.RequiresClarification && state.PendingClarifications.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking pending clarifications for conversation {ConversationId}", conversationId);
            return false;
        }
    }

    public async Task SetContextAsync(int conversationId, string contextDescription)
    {
        try
        {
            var state = await GetStateAsync(conversationId);
            state.Context = contextDescription;
            await UpdateStateAsync(conversationId, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting context for conversation {ConversationId}", conversationId);
        }
    }

    // ============= NEW STRUCTURED STATE MANAGEMENT =============

    /// <summary>
    /// Creates a new pending state record for a conversation
    /// </summary>
    public async Task CreatePendingStateAsync(int conversationId, int tenantId, string stateType, string entityType, int entityId, string? pendingField = null, string? contextData = null)
    {
        try
        {
            var stateRecord = new ConversationStateRecord
            {
                TenantId = tenantId,
                ConversationId = conversationId,
                StateType = stateType,
                EntityType = entityType,
                EntityId = entityId,
                PendingField = pendingField,
                ContextData = contextData,
                CreatedAt = DateTime.UtcNow
            };

            _context.ConversationStateRecords.Add(stateRecord);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created pending state: {StateType} for {EntityType}:{EntityId} in conversation {ConversationId}",
                stateType, entityType, entityId, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pending state for conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Gets a specific pending state record
    /// </summary>
    public async Task<ConversationStateRecord?> GetPendingStateAsync(int conversationId, string stateType, string entityType)
    {
        try
        {
            return await _context.ConversationStateRecords
                .Where(s => s.ConversationId == conversationId
                    && s.StateType == stateType
                    && s.EntityType == entityType
                    && s.ResolvedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending state for conversation {ConversationId}", conversationId);
            return null;
        }
    }

    /// <summary>
    /// Gets any pending state for a conversation (most recent first)
    /// </summary>
    public async Task<ConversationStateRecord?> GetAnyPendingStateForConversationAsync(int conversationId)
    {
        try
        {
            return await _context.ConversationStateRecords
                .Where(s => s.ConversationId == conversationId && s.ResolvedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting any pending state for conversation {ConversationId}", conversationId);
            return null;
        }
    }

    /// <summary>
    /// Marks a pending state as resolved
    /// </summary>
    public async Task ResolvePendingStateAsync(int stateId)
    {
        try
        {
            var stateRecord = await _context.ConversationStateRecords.FindAsync(stateId);
            if (stateRecord != null)
            {
                stateRecord.ResolvedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Resolved pending state {StateId} ({StateType} for {EntityType}:{EntityId})",
                    stateId, stateRecord.StateType, stateRecord.EntityType, stateRecord.EntityId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving pending state {StateId}", stateId);
        }
    }

    /// <summary>
    /// Checks if a conversation has a specific pending state
    /// </summary>
    public async Task<bool> HasPendingStateAsync(int conversationId, string stateType, string entityType)
    {
        try
        {
            return await _context.ConversationStateRecords
                .AnyAsync(s => s.ConversationId == conversationId
                    && s.StateType == stateType
                    && s.EntityType == entityType
                    && s.ResolvedAt == null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking pending state for conversation {ConversationId}", conversationId);
            return false;
        }
    }
}