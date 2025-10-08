using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;

namespace Hostr.Api.Services;

public interface ILostAndFoundService
{
    Task<LostItem> ReportLostItemAsync(
        int tenantId,
        string itemName,
        string category,
        string reporterPhone,
        int? conversationId,
        string? description = null,
        string? color = null,
        string? brand = null,
        string? locationLost = null,
        string? roomNumber = null,
        string? reporterName = null,
        decimal? rewardAmount = null);
        
    Task<FoundItem> RegisterFoundItemAsync(
        int tenantId,
        string itemName,
        string category,
        string locationFound,
        string? finderName = null,
        string? description = null,
        string? color = null,
        string? brand = null,
        string? storageLocation = null);
        
    Task<List<LostAndFoundMatch>> FindPotentialMatchesAsync(int tenantId, int lostItemId);
    Task<List<LostAndFoundMatch>> FindPotentialMatchesForFoundItemAsync(int tenantId, int foundItemId);
    
    Task<bool> ConfirmMatchAsync(int tenantId, int matchId, int verifiedBy, bool confirmed, string? notes = null);
    Task<bool> ProcessClaimAsync(int tenantId, int matchId, string claimerPhone);
    
    Task<List<LostItem>> GetLostItemsAsync(int tenantId, string? status = null, string? reporterPhone = null);
    Task<List<FoundItem>> GetFoundItemsAsync(int tenantId, string? status = null);
    Task<List<LostAndFoundMatch>> GetPendingMatchesAsync(int tenantId);

    Task<bool> UpdateLostItemLocationAsync(int tenantId, int lostItemId, string location);

    Task<(bool IsLostAndFoundRequest, string RequestType, object? ParsedDetails)> DetectLostAndFoundRequestAsync(
        TenantContext tenantContext,
        string message,
        string senderPhone);

    Task ProcessDisposalWarningsAsync(int tenantId);
    Task<bool> MarkItemForDisposalAsync(int tenantId, int foundItemId, string reason);
    
    Task SeedLostAndFoundDataAsync(int tenantId);
}

public class LostAndFoundService : ILostAndFoundService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<LostAndFoundService> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Keywords for detecting lost and found requests
    private static readonly Dictionary<string, string[]> LostAndFoundKeywords = new()
    {
        {"lost_item", new[] {"lost", "missing", "can't find", "cannot find", "left behind", "forgot"}},
        {"found_item", new[] {"found", "discovered", "picked up", "someone left"}},
        {"claim_item", new[] {"claim", "pick up", "collect", "retrieve", "get my", "is my"}}
    };

    // Category keywords for automatic categorization
    private static readonly Dictionary<string, string[]> CategoryKeywords = new()
    {
        {"Electronics", new[] {"phone", "iphone", "android", "laptop", "tablet", "camera", "headphones", "earbuds", "charger", "cable"}},
        {"Clothing", new[] {"shirt", "jacket", "coat", "pants", "dress", "shoes", "hat", "scarf", "gloves", "socks"}},
        {"Jewelry", new[] {"ring", "necklace", "bracelet", "earrings", "watch", "chain", "pendant"}},
        {"Documents", new[] {"passport", "id", "license", "wallet", "purse", "card", "certificate", "paper", "document"}},
        {"Keys", new[] {"key", "keys", "keychain", "remote", "fob"}},
        {"Personal", new[] {"glasses", "sunglasses", "bag", "backpack", "umbrella", "bottle", "book"}},
        {"Accessories", new[] {"belt", "tie", "cufflinks", "pen", "lighter", "makeup"}}
    };

    public LostAndFoundService(
        HostrDbContext context,
        ILogger<LostAndFoundService> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<LostItem> ReportLostItemAsync(
        int tenantId,
        string itemName,
        string category,
        string reporterPhone,
        int? conversationId,
        string? description = null,
        string? color = null,
        string? brand = null,
        string? locationLost = null,
        string? roomNumber = null,
        string? reporterName = null,
        decimal? rewardAmount = null)
    {
        using var scope = new TenantScope(_context, tenantId);

        var lostItem = new LostItem
        {
            TenantId = tenantId,
            ItemName = itemName,
            Category = category,
            Description = description,
            Color = color,
            Brand = brand,
            LocationLost = locationLost,
            RoomNumber = roomNumber,
            ReporterPhone = reporterPhone,
            ReporterName = reporterName,
            ConversationId = conversationId,
            RewardAmount = rewardAmount,
            Status = "Open",
            ReportedAt = DateTime.UtcNow
        };

        _context.LostItems.Add(lostItem);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Lost item reported: {ItemId} - {ItemName} [{Category}] by {Reporter}",
            lostItem.Id, itemName, category, reporterPhone);

        // Automatically find potential matches
        _ = Task.Run(async () => await FindPotentialMatchesAsync(tenantId, lostItem.Id));

        return lostItem;
    }

    public async Task<FoundItem> RegisterFoundItemAsync(
        int tenantId,
        string itemName,
        string category,
        string locationFound,
        string? finderName = null,
        string? description = null,
        string? color = null,
        string? brand = null,
        string? storageLocation = null)
    {
        using var scope = new TenantScope(_context, tenantId);

        var foundItem = new FoundItem
        {
            TenantId = tenantId,
            ItemName = itemName,
            Category = category,
            Description = description,
            Color = color,
            Brand = brand,
            LocationFound = locationFound,
            FinderName = finderName,
            StorageLocation = storageLocation,
            Status = "AVAILABLE",
            FoundAt = DateTime.UtcNow
        };

        _context.FoundItems.Add(foundItem);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Found item registered: {ItemId} - {ItemName} [{Category}] found at {Location}",
            foundItem.Id, itemName, category, locationFound);

        // Automatically find potential matches
        _ = Task.Run(async () => await FindPotentialMatchesForFoundItemAsync(tenantId, foundItem.Id));

        return foundItem;
    }

    public async Task<List<LostAndFoundMatch>> FindPotentialMatchesAsync(int tenantId, int lostItemId)
    {
        using var scope = new TenantScope(_context, tenantId);

        var lostItem = await _context.LostItems.FindAsync(lostItemId);
        if (lostItem == null || lostItem.TenantId != tenantId)
            return new List<LostAndFoundMatch>();

        var availableFoundItems = await _context.FoundItems
            .Where(f => f.Status == "AVAILABLE")
            .ToListAsync();

        var matches = new List<LostAndFoundMatch>();

        foreach (var foundItem in availableFoundItems)
        {
            var matchScore = CalculateMatchScore(lostItem, foundItem);
            if (matchScore >= 0.3m) // Minimum threshold for considering a match
            {
                var match = new LostAndFoundMatch
                {
                    TenantId = tenantId,
                    LostItemId = lostItemId,
                    FoundItemId = foundItem.Id,
                    MatchScore = matchScore,
                    Status = "PENDING",
                    MatchingReason = GenerateMatchingReason(lostItem, foundItem, matchScore),
                    CreatedAt = DateTime.UtcNow
                };

                matches.Add(match);
            }
        }

        // Only save high-confidence matches (score >= 0.6)
        var highConfidenceMatches = matches.Where(m => m.MatchScore >= 0.6m).ToList();
        if (highConfidenceMatches.Any())
        {
            _context.LostAndFoundMatches.AddRange(highConfidenceMatches);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Found {MatchCount} potential matches for lost item {ItemId}",
                highConfidenceMatches.Count, lostItemId);

            // Notify the reporter about potential matches
            await NotifyGuestAboutMatchesAsync(tenantId, lostItem, highConfidenceMatches);
        }

        return matches;
    }

    public async Task<List<LostAndFoundMatch>> FindPotentialMatchesForFoundItemAsync(int tenantId, int foundItemId)
    {
        using var scope = new TenantScope(_context, tenantId);

        var foundItem = await _context.FoundItems.FindAsync(foundItemId);
        if (foundItem == null || foundItem.TenantId != tenantId)
            return new List<LostAndFoundMatch>();

        var openLostItems = await _context.LostItems
            .Where(l => l.Status == "Open")
            .ToListAsync();

        var matches = new List<LostAndFoundMatch>();

        foreach (var lostItem in openLostItems)
        {
            var matchScore = CalculateMatchScore(lostItem, foundItem);
            if (matchScore >= 0.6m) // Higher threshold when matching from found items
            {
                var match = new LostAndFoundMatch
                {
                    TenantId = tenantId,
                    LostItemId = lostItem.Id,
                    FoundItemId = foundItemId,
                    MatchScore = matchScore,
                    Status = "PENDING",
                    MatchingReason = GenerateMatchingReason(lostItem, foundItem, matchScore),
                    CreatedAt = DateTime.UtcNow
                };

                matches.Add(match);
            }
        }

        if (matches.Any())
        {
            _context.LostAndFoundMatches.AddRange(matches);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Found {MatchCount} potential matches for found item {ItemId}",
                matches.Count, foundItemId);

            // Notify guests about potential matches
            foreach (var match in matches)
            {
                var lostItem = openLostItems.First(l => l.Id == match.LostItemId);
                await NotifyGuestAboutMatchesAsync(tenantId, lostItem, new[] { match });
            }
        }

        return matches;
    }

    public async Task<bool> ConfirmMatchAsync(int tenantId, int matchId, int verifiedBy, bool confirmed, string? notes = null)
    {
        using var scope = new TenantScope(_context, tenantId);

        var match = await _context.LostAndFoundMatches
            .Include(m => m.LostItem)
            .Include(m => m.FoundItem)
            .FirstOrDefaultAsync(m => m.Id == matchId && m.TenantId == tenantId);

        if (match == null)
            return false;

        match.Status = confirmed ? "CONFIRMED" : "REJECTED";
        match.VerifiedBy = verifiedBy;
        match.VerifiedAt = DateTime.UtcNow;
        match.Notes = notes;

        if (confirmed)
        {
            // Update item statuses
            match.LostItem.Status = "MATCHED";
            match.FoundItem.Status = "MATCHED";

            // Notify the guest that their item is ready for pickup
            await SchedulePickupNotificationAsync(tenantId, match);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Match {MatchId} {Status} by staff {StaffId}",
            matchId, match.Status, verifiedBy);

        return true;
    }

    public async Task<bool> ProcessClaimAsync(int tenantId, int matchId, string claimerPhone)
    {
        using var scope = new TenantScope(_context, tenantId);

        var match = await _context.LostAndFoundMatches
            .Include(m => m.LostItem)
            .Include(m => m.FoundItem)
            .FirstOrDefaultAsync(m => m.Id == matchId && m.TenantId == tenantId);

        if (match == null || match.Status != "CONFIRMED")
            return false;

        // Verify the claimer is the original reporter
        if (match.LostItem.ReporterPhone != claimerPhone)
        {
            _logger.LogWarning("Claim attempt by wrong person: {ClaimerPhone} for match {MatchId} (reporter: {ReporterPhone})",
                claimerPhone, matchId, match.LostItem.ReporterPhone);
            return false;
        }

        match.Status = "CLAIMED";
        match.GuestConfirmed = true;
        match.GuestConfirmedAt = DateTime.UtcNow;
        match.ClaimedAt = DateTime.UtcNow;

        match.LostItem.Status = "CLAIMED";
        match.LostItem.ClaimedAt = DateTime.UtcNow;

        match.FoundItem.Status = "CLAIMED";
        match.FoundItem.ClaimedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Item claimed: Match {MatchId} by {ClaimerPhone}",
            matchId, claimerPhone);

        return true;
    }

    public async Task<List<LostItem>> GetLostItemsAsync(int tenantId, string? status = null, string? reporterPhone = null)
    {
        using var scope = new TenantScope(_context, tenantId);

        var query = _context.LostItems.AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(l => l.Status == status);

        if (!string.IsNullOrEmpty(reporterPhone))
            query = query.Where(l => l.ReporterPhone == reporterPhone);

        return await query
            .OrderByDescending(l => l.ReportedAt)
            .ToListAsync();
    }

    public async Task<List<FoundItem>> GetFoundItemsAsync(int tenantId, string? status = null)
    {
        using var scope = new TenantScope(_context, tenantId);

        var query = _context.FoundItems.AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(f => f.Status == status);

        return await query
            .OrderByDescending(f => f.FoundAt)
            .ToListAsync();
    }

    public async Task<List<LostAndFoundMatch>> GetPendingMatchesAsync(int tenantId)
    {
        using var scope = new TenantScope(_context, tenantId);

        return await _context.LostAndFoundMatches
            .Include(m => m.LostItem)
            .Include(m => m.FoundItem)
            .Where(m => m.Status == "PENDING")
            .OrderByDescending(m => m.MatchScore)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> UpdateLostItemLocationAsync(int tenantId, int lostItemId, string location)
    {
        using var scope = new TenantScope(_context, tenantId);

        var lostItem = await _context.LostItems
            .FirstOrDefaultAsync(l => l.Id == lostItemId && l.TenantId == tenantId);

        if (lostItem == null)
        {
            _logger.LogWarning("Lost item {LostItemId} not found for tenant {TenantId}", lostItemId, tenantId);
            return false;
        }

        lostItem.LocationLost = location;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated location for lost item {LostItemId} to '{Location}'", lostItemId, location);
        return true;
    }

    public async Task<(bool IsLostAndFoundRequest, string RequestType, object? ParsedDetails)> DetectLostAndFoundRequestAsync(
        TenantContext tenantContext,
        string message,
        string senderPhone)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);

            var messageLower = message.ToLower();

            // Detect request type based on keywords
            foreach (var requestKv in LostAndFoundKeywords)
            {
                if (requestKv.Value.Any(keyword => messageLower.Contains(keyword.ToLower())))
                {
                    var requestType = requestKv.Key;
                    var parsedDetails = ParseLostAndFoundDetails(requestType, message, messageLower);

                    _logger.LogInformation("Lost and found request detected: {Type} for {Phone} - Message: {Message}",
                        requestType, senderPhone, message);

                    return (true, requestType, parsedDetails);
                }
            }

            return (false, "", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting lost and found request");
            return (false, "", null);
        }
    }

    public async Task ProcessDisposalWarningsAsync(int tenantId)
    {
        using var scope = new TenantScope(_context, tenantId);

        var warningDate = DateTime.UtcNow.AddDays(7); // Warn 7 days before disposal
        
        var itemsNearingDisposal = await _context.FoundItems
            .Where(f => f.Status == "AVAILABLE" && 
                       f.FoundAt.AddDays(f.DisposalAfterDays) <= warningDate)
            .ToListAsync();

        foreach (var item in itemsNearingDisposal)
        {
            await ScheduleDisposalWarningAsync(tenantId, item);
        }

        // Mark items for actual disposal
        var itemsForDisposal = await _context.FoundItems
            .Where(f => f.Status == "AVAILABLE" && 
                       f.FoundAt.AddDays(f.DisposalAfterDays) <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var item in itemsForDisposal)
        {
            await MarkItemForDisposalAsync(tenantId, item.Id, "Automatic disposal - exceeded retention period");
        }
    }

    public async Task<bool> MarkItemForDisposalAsync(int tenantId, int foundItemId, string reason)
    {
        using var scope = new TenantScope(_context, tenantId);

        var item = await _context.FoundItems.FindAsync(foundItemId);
        if (item == null || item.TenantId != tenantId)
            return false;

        item.Status = "DISPOSED";
        item.DisposalDate = DateTime.UtcNow;
        
        // Store disposal reason in additional details
        var disposalInfo = new { Reason = reason, DisposedAt = DateTime.UtcNow };
        item.AdditionalDetails = JsonDocument.Parse(JsonSerializer.Serialize(disposalInfo));

        await _context.SaveChangesAsync();

        _logger.LogInformation("Item marked for disposal: {ItemId} - {Reason}", foundItemId, reason);

        return true;
    }

    public async Task SeedLostAndFoundDataAsync(int tenantId)
    {
        using var scope = new TenantScope(_context, tenantId);

        // Check if categories already exist
        var hasCategories = await _context.LostAndFoundCategories.AnyAsync();
        if (hasCategories)
        {
            _logger.LogInformation("Lost and found categories already exist for tenant {TenantId}", tenantId);
            return;
        }

        // Create default categories
        var categories = new List<LostAndFoundCategory>
        {
            new() { TenantId = tenantId, Name = "Electronics", Keywords = CategoryKeywords["Electronics"], DefaultDisposalDays = 180, RequiresSecureStorage = true },
            new() { TenantId = tenantId, Name = "Clothing", Keywords = CategoryKeywords["Clothing"], DefaultDisposalDays = 90 },
            new() { TenantId = tenantId, Name = "Jewelry", Keywords = CategoryKeywords["Jewelry"], DefaultDisposalDays = 365, RequiresSecureStorage = true },
            new() { TenantId = tenantId, Name = "Documents", Keywords = CategoryKeywords["Documents"], DefaultDisposalDays = 365, RequiresSecureStorage = true },
            new() { TenantId = tenantId, Name = "Keys", Keywords = CategoryKeywords["Keys"], DefaultDisposalDays = 180, RequiresSecureStorage = true },
            new() { TenantId = tenantId, Name = "Personal", Keywords = CategoryKeywords["Personal"], DefaultDisposalDays = 90 },
            new() { TenantId = tenantId, Name = "Accessories", Keywords = CategoryKeywords["Accessories"], DefaultDisposalDays = 90 }
        };

        _context.LostAndFoundCategories.AddRange(categories);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Lost and found system initialized for tenant {TenantId} with {CategoryCount} categories",
            tenantId, categories.Count);
    }

    private decimal CalculateMatchScore(LostItem lostItem, FoundItem foundItem)
    {
        decimal score = 0m;
        int factors = 0;

        // Category match (most important)
        if (string.Equals(lostItem.Category, foundItem.Category, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.4m;
        }
        factors++;

        // Item name similarity
        var nameSimilarity = CalculateStringSimilarity(lostItem.ItemName, foundItem.ItemName);
        score += nameSimilarity * 0.3m;
        factors++;

        // Color match
        if (!string.IsNullOrEmpty(lostItem.Color) && !string.IsNullOrEmpty(foundItem.Color))
        {
            if (string.Equals(lostItem.Color, foundItem.Color, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.15m;
            }
            factors++;
        }

        // Brand match
        if (!string.IsNullOrEmpty(lostItem.Brand) && !string.IsNullOrEmpty(foundItem.Brand))
        {
            if (string.Equals(lostItem.Brand, foundItem.Brand, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.1m;
            }
            factors++;
        }

        // Description similarity
        if (!string.IsNullOrEmpty(lostItem.Description) && !string.IsNullOrEmpty(foundItem.Description))
        {
            var descSimilarity = CalculateStringSimilarity(lostItem.Description, foundItem.Description);
            score += descSimilarity * 0.05m;
            factors++;
        }

        return Math.Min(score, 1.0m); // Cap at 1.0
    }

    private decimal CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0m;

        // Simple Levenshtein distance-based similarity
        var distance = ComputeLevenshteinDistance(str1.ToLower(), str2.ToLower());
        var maxLength = Math.Max(str1.Length, str2.Length);
        
        return maxLength == 0 ? 1m : Math.Max(0m, 1m - (decimal)distance / maxLength);
    }

    private int ComputeLevenshteinDistance(string str1, string str2)
    {
        var matrix = new int[str1.Length + 1, str2.Length + 1];

        for (int i = 0; i <= str1.Length; i++)
            matrix[i, 0] = i;
        for (int j = 0; j <= str2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= str1.Length; i++)
        {
            for (int j = 1; j <= str2.Length; j++)
            {
                int cost = str1[i - 1] == str2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[str1.Length, str2.Length];
    }

    private string GenerateMatchingReason(LostItem lostItem, FoundItem foundItem, decimal score)
    {
        var reasons = new List<string>();

        if (string.Equals(lostItem.Category, foundItem.Category, StringComparison.OrdinalIgnoreCase))
            reasons.Add("Same category");

        if (!string.IsNullOrEmpty(lostItem.Color) && !string.IsNullOrEmpty(foundItem.Color) &&
            string.Equals(lostItem.Color, foundItem.Color, StringComparison.OrdinalIgnoreCase))
            reasons.Add("Same color");

        if (!string.IsNullOrEmpty(lostItem.Brand) && !string.IsNullOrEmpty(foundItem.Brand) &&
            string.Equals(lostItem.Brand, foundItem.Brand, StringComparison.OrdinalIgnoreCase))
            reasons.Add("Same brand");

        var nameSimilarity = CalculateStringSimilarity(lostItem.ItemName, foundItem.ItemName);
        if (nameSimilarity > 0.7m)
            reasons.Add("Similar name");

        return reasons.Any() ? string.Join(", ", reasons) : "General similarity";
    }

    private object ParseLostAndFoundDetails(string requestType, string originalMessage, string messageLower)
    {
        return requestType switch
        {
            "lost_item" => ParseLostItemDetails(originalMessage, messageLower),
            "found_item" => ParseFoundItemDetails(originalMessage, messageLower),
            "claim_item" => ParseClaimDetails(originalMessage, messageLower),
            _ => new { OriginalMessage = originalMessage, RequiresManualReview = true }
        };
    }

    private object ParseLostItemDetails(string originalMessage, string messageLower)
    {
        var category = DetectCategory(messageLower);
        var color = ExtractColor(messageLower);
        var brand = ExtractBrand(messageLower);

        return new
        {
            OriginalMessage = originalMessage,
            DetectedCategory = category,
            DetectedColor = color,
            DetectedBrand = brand,
            RequiresManualReview = true
        };
    }

    private object ParseFoundItemDetails(string originalMessage, string messageLower)
    {
        return new
        {
            OriginalMessage = originalMessage,
            DetectedCategory = DetectCategory(messageLower),
            RequiresStaffAction = true
        };
    }

    private object ParseClaimDetails(string originalMessage, string messageLower)
    {
        return new
        {
            OriginalMessage = originalMessage,
            RequiresVerification = true
        };
    }

    private string DetectCategory(string messageLower)
    {
        foreach (var categoryKv in CategoryKeywords)
        {
            if (categoryKv.Value.Any(keyword => messageLower.Contains(keyword)))
            {
                return categoryKv.Key;
            }
        }
        return "Other";
    }

    private string? ExtractColor(string messageLower)
    {
        var colors = new[] { "black", "white", "red", "blue", "green", "yellow", "orange", "purple", "pink", "brown", "gray", "silver", "gold" };
        return colors.FirstOrDefault(color => messageLower.Contains(color));
    }

    private string? ExtractBrand(string messageLower)
    {
        var brands = new[] { "apple", "samsung", "google", "sony", "nike", "adidas", "louis vuitton", "gucci", "prada" };
        return brands.FirstOrDefault(brand => messageLower.Contains(brand));
    }

    private async Task NotifyGuestAboutMatchesAsync(int tenantId, LostItem lostItem, IEnumerable<LostAndFoundMatch> matches)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var scopedBroadcastService = scope.ServiceProvider.GetRequiredService<IBroadcastService>();

            var message = $"🎉 Good news! We may have found your {lostItem.ItemName}.\n\n" +
                         $"We found {matches.Count()} potential match(es). Please contact our front desk to verify and arrange pickup.\n\n" +
                         $"📞 Front desk will help you confirm if it's your item.";

            await scopedBroadcastService.SendEmergencyBroadcastAsync(
                tenantId,
                "lost_and_found",
                message,
                null,
                "Lost & Found System",
                BroadcastScope.ActiveOnly);

            _logger.LogInformation("Match notification sent to guest {Phone} for lost item {ItemId}",
                lostItem.ReporterPhone, lostItem.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying guest about matches for lost item {ItemId}", lostItem.Id);
        }
    }

    private async Task SchedulePickupNotificationAsync(int tenantId, LostAndFoundMatch match)
    {
        var notification = new LostAndFoundNotification
        {
            TenantId = tenantId,
            NotificationType = "READY_FOR_PICKUP",
            LostItemId = match.LostItemId,
            FoundItemId = match.FoundItemId,
            MatchId = match.Id,
            RecipientPhone = match.LostItem.ReporterPhone,
            Message = $"✅ Your {match.LostItem.ItemName} is ready for pickup!\n\n" +
                     $"Please visit our front desk with a valid ID to collect your item.\n\n" +
                     $"📍 Location: {match.FoundItem.LocationFound}\n" +
                     $"🕒 Available during front desk hours",
            ScheduledAt = DateTime.UtcNow,
            Status = "PENDING"
        };

        _context.LostAndFoundNotifications.Add(notification);
        await _context.SaveChangesAsync();
    }

    private async Task ScheduleDisposalWarningAsync(int tenantId, FoundItem item)
    {
        var disposalDate = item.FoundAt.AddDays(item.DisposalAfterDays);
        var notification = new LostAndFoundNotification
        {
            TenantId = tenantId,
            NotificationType = "DISPOSAL_WARNING",
            FoundItemId = item.Id,
            RecipientPhone = "STAFF", // Special marker for staff notifications
            Message = $"⚠️ DISPOSAL WARNING\n\n" +
                     $"Item: {item.ItemName}\n" +
                     $"Found: {item.FoundAt:yyyy-MM-dd}\n" +
                     $"Location: {item.LocationFound}\n" +
                     $"Storage: {item.StorageLocation}\n\n" +
                     $"Scheduled for disposal on {disposalDate:yyyy-MM-dd}",
            ScheduledAt = DateTime.UtcNow,
            Status = "PENDING"
        };

        _context.LostAndFoundNotifications.Add(notification);
        await _context.SaveChangesAsync();
    }
}