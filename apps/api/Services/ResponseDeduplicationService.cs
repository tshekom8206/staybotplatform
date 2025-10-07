using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services
{
    public interface IResponseDeduplicationService
    {
        Task<bool> IsResponseDuplicateAsync(int conversationId, string responseText, TimeSpan lookbackWindow);
        Task<string> GetResponseHashAsync(string responseText);
        Task MarkResponseSentAsync(int conversationId, string responseText, string responseHash);
        Task<List<DuplicateResponseAlert>> DetectRecentDuplicatesAsync(int tenantId, int lookbackHours = 1);
        Task CleanupOldResponseHashesAsync(TimeSpan retentionPeriod);
    }

    public class ResponseDeduplicationService : IResponseDeduplicationService
    {
        private readonly HostrDbContext _context;
        private readonly ILogger<ResponseDeduplicationService> _logger;

        // In-memory cache for recent response hashes to improve performance
        private readonly ConcurrentDictionary<string, DateTime> _recentResponseHashes = new();
        private readonly TimeSpan _cacheRetention = TimeSpan.FromMinutes(30);

        // Response similarity thresholds
        private const double EXACT_MATCH_THRESHOLD = 1.0;
        private const double HIGH_SIMILARITY_THRESHOLD = 0.95;
        private const int MIN_RESPONSE_LENGTH_FOR_COMPARISON = 20;

        public ResponseDeduplicationService(HostrDbContext context, ILogger<ResponseDeduplicationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> IsResponseDuplicateAsync(int conversationId, string responseText, TimeSpan lookbackWindow)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(responseText) || responseText.Length < MIN_RESPONSE_LENGTH_FOR_COMPARISON)
                {
                    return false; // Don't deduplicate very short responses
                }

                // Normalize response text for comparison
                var normalizedResponse = NormalizeResponseText(responseText);
                var responseHash = await GetResponseHashAsync(normalizedResponse);

                // Check in-memory cache first for performance
                var cacheKey = $"{conversationId}:{responseHash}";
                if (_recentResponseHashes.ContainsKey(cacheKey))
                {
                    _logger.LogWarning("Duplicate response detected in cache for conversation {ConversationId}: '{ResponsePreview}'",
                        conversationId, GetResponsePreview(responseText));
                    return true;
                }

                // Check database for recent duplicates
                var cutoffTime = DateTime.UtcNow.Subtract(lookbackWindow);

                var recentMessages = await _context.Messages
                    .Where(m => m.ConversationId == conversationId &&
                               m.Direction == "Outbound" &&
                               m.CreatedAt >= cutoffTime)
                    .Select(m => m.Body)
                    .ToListAsync();

                foreach (var recentMessage in recentMessages)
                {
                    if (!string.IsNullOrEmpty(recentMessage))
                    {
                        var similarity = CalculateTextSimilarity(normalizedResponse, NormalizeResponseText(recentMessage));

                        if (similarity >= EXACT_MATCH_THRESHOLD)
                        {
                            _logger.LogWarning("Exact duplicate response detected for conversation {ConversationId}: '{ResponsePreview}'",
                                conversationId, GetResponsePreview(responseText));
                            return true;
                        }
                        else if (similarity >= HIGH_SIMILARITY_THRESHOLD)
                        {
                            _logger.LogWarning("High similarity response detected for conversation {ConversationId}: '{ResponsePreview}' (similarity: {Similarity:P1})",
                                conversationId, GetResponsePreview(responseText), similarity);
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for duplicate response in conversation {ConversationId}", conversationId);
                return false; // Fail open - allow response if error checking
            }
        }

        public async Task<string> GetResponseHashAsync(string responseText)
        {
            try
            {
                var normalizedText = NormalizeResponseText(responseText);
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedText));
                return Convert.ToBase64String(hashBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response hash");
                return string.Empty;
            }
        }

        public async Task MarkResponseSentAsync(int conversationId, string responseText, string responseHash)
        {
            try
            {
                // Add to in-memory cache
                var cacheKey = $"{conversationId}:{responseHash}";
                _recentResponseHashes.TryAdd(cacheKey, DateTime.UtcNow);

                // Clean up old cache entries periodically
                await CleanupCacheAsync();

                _logger.LogDebug("Marked response as sent for conversation {ConversationId}: hash {HashPreview}",
                    conversationId, responseHash.Substring(0, Math.Min(8, responseHash.Length)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking response as sent for conversation {ConversationId}", conversationId);
            }
        }

        public async Task<List<DuplicateResponseAlert>> DetectRecentDuplicatesAsync(int tenantId, int lookbackHours = 1)
        {
            try
            {
                using var scope = new TenantScope(_context, tenantId);
                var alerts = new List<DuplicateResponseAlert>();

                var cutoffTime = DateTime.UtcNow.AddHours(-lookbackHours);

                // Get recent outbound messages grouped by conversation
                var conversationGroups = await _context.Messages
                    .Where(m => m.Direction == "Outbound" &&
                               m.CreatedAt >= cutoffTime &&
                               !string.IsNullOrEmpty(m.Body))
                    .GroupBy(m => m.ConversationId)
                    .ToListAsync();

                foreach (var conversationGroup in conversationGroups)
                {
                    var messages = conversationGroup.OrderBy(m => m.CreatedAt).ToList();

                    // Check for duplicates within this conversation
                    for (int i = 0; i < messages.Count - 1; i++)
                    {
                        for (int j = i + 1; j < messages.Count; j++)
                        {
                            var msg1 = messages[i];
                            var msg2 = messages[j];

                            var similarity = CalculateTextSimilarity(
                                NormalizeResponseText(msg1.Body),
                                NormalizeResponseText(msg2.Body)
                            );

                            if (similarity >= HIGH_SIMILARITY_THRESHOLD)
                            {
                                alerts.Add(new DuplicateResponseAlert
                                {
                                    ConversationId = conversationGroup.Key,
                                    FirstMessageId = msg1.Id,
                                    SecondMessageId = msg2.Id,
                                    FirstMessageTime = msg1.CreatedAt,
                                    SecondMessageTime = msg2.CreatedAt,
                                    SimilarityScore = similarity,
                                    MessagePreview = GetResponsePreview(msg1.Body),
                                    TimeBetween = msg2.CreatedAt - msg1.CreatedAt
                                });
                            }
                        }
                    }
                }

                if (alerts.Any())
                {
                    _logger.LogWarning("Detected {Count} duplicate responses in the last {Hours} hours for tenant {TenantId}",
                        alerts.Count, lookbackHours, tenantId);
                }

                return alerts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting recent duplicates for tenant {TenantId}", tenantId);
                return new List<DuplicateResponseAlert>();
            }
        }

        public async Task CleanupOldResponseHashesAsync(TimeSpan retentionPeriod)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(retentionPeriod);
                var keysToRemove = _recentResponseHashes
                    .Where(kvp => kvp.Value < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _recentResponseHashes.TryRemove(key, out _);
                }

                if (keysToRemove.Any())
                {
                    _logger.LogDebug("Cleaned up {Count} old response hashes", keysToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old response hashes");
            }
        }

        #region Private Helper Methods

        private string NormalizeResponseText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Normalize text for comparison:
            // - Remove extra whitespace
            // - Convert to lowercase
            // - Remove common variations that don't affect meaning
            return text.Trim()
                      .ToLowerInvariant()
                      .Replace("  ", " ")  // Multiple spaces to single space
                      .Replace("\n", " ")  // Newlines to spaces
                      .Replace("\r", " ")  // Carriage returns to spaces
                      .Replace("!", "")    // Remove exclamation marks
                      .Replace("?", "")    // Remove question marks
                      .Replace(".", "")    // Remove periods
                      .Replace(",", "")    // Remove commas
                      .Trim();
        }

        private double CalculateTextSimilarity(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
                return 0.0;

            if (text1 == text2)
                return 1.0;

            // Use Levenshtein distance for similarity calculation
            var distance = CalculateLevenshteinDistance(text1, text2);
            var maxLength = Math.Max(text1.Length, text2.Length);

            if (maxLength == 0)
                return 1.0;

            return 1.0 - (double)distance / maxLength;
        }

        private int CalculateLevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return target?.Length ?? 0;

            if (string.IsNullOrEmpty(target))
                return source.Length;

            var sourceLength = source.Length;
            var targetLength = target.Length;
            var distance = new int[sourceLength + 1, targetLength + 1];

            // Initialize first row and column
            for (var i = 0; i <= sourceLength; i++)
                distance[i, 0] = i;

            for (var j = 0; j <= targetLength; j++)
                distance[0, j] = j;

            // Calculate distances
            for (var i = 1; i <= sourceLength; i++)
            {
                for (var j = 1; j <= targetLength; j++)
                {
                    var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[sourceLength, targetLength];
        }

        private string GetResponsePreview(string response)
        {
            if (string.IsNullOrEmpty(response))
                return "[empty]";

            return response.Length > 50
                ? response.Substring(0, 50) + "..."
                : response;
        }

        private async Task CleanupCacheAsync()
        {
            try
            {
                // Only clean up periodically to avoid performance impact
                if (_recentResponseHashes.Count > 1000)
                {
                    await CleanupOldResponseHashesAsync(_cacheRetention);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
        }

        #endregion
    }

    // Supporting classes
    public class DuplicateResponseAlert
    {
        public int ConversationId { get; set; }
        public int FirstMessageId { get; set; }
        public int SecondMessageId { get; set; }
        public DateTime FirstMessageTime { get; set; }
        public DateTime SecondMessageTime { get; set; }
        public double SimilarityScore { get; set; }
        public string MessagePreview { get; set; } = "";
        public TimeSpan TimeBetween { get; set; }
    }
}