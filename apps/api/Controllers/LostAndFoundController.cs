using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Services;
using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LostAndFoundController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILostAndFoundService _lostAndFoundService;
    private readonly ILogger<LostAndFoundController> _logger;

    public LostAndFoundController(
        HostrDbContext context,
        ILostAndFoundService lostAndFoundService,
        ILogger<LostAndFoundController> logger)
    {
        _context = context;
        _lostAndFoundService = lostAndFoundService;
        _logger = logger;
    }

    private int GetTenantId()
    {
        var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
        {
            throw new UnauthorizedAccessException("Invalid tenant context");
        }
        return tenantId;
    }

    #region Stats

    /// <summary>
    /// Get Lost & Found statistics for the current tenant
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        _logger.LogInformation("[LostAndFound] GetStats endpoint HIT - Route: api/[controller]/stats");
        _logger.LogInformation("[LostAndFound] Request Path: {Path}", HttpContext.Request.Path);
        _logger.LogInformation("[LostAndFound] Request Method: {Method}", HttpContext.Request.Method);
        _logger.LogInformation("[LostAndFound] Auth header present: {HasAuth}", HttpContext.Request.Headers.ContainsKey("Authorization"));
        _logger.LogInformation("[LostAndFound] X-Tenant header: {Tenant}", HttpContext.Request.Headers["X-Tenant"].ToString());

        try
        {
            var tenantId = GetTenantId();
            _logger.LogInformation("[LostAndFound] Tenant ID resolved: {TenantId}", tenantId);

            var openReports = await _context.LostItems
                .Where(l => l.TenantId == tenantId && l.Status == "Open")
                .CountAsync();

            var itemsInStorage = await _context.FoundItems
                .Where(f => f.TenantId == tenantId &&
                       (f.Status == "AVAILABLE" || f.Status == "InStorage" || f.Status == "IN_STORAGE"))
                .CountAsync();

            var pendingMatches = await _context.LostAndFoundMatches
                .Where(m => m.TenantId == tenantId && m.Status == "Pending")
                .CountAsync();

            var totalLostItems = await _context.LostItems
                .Where(l => l.TenantId == tenantId)
                .CountAsync();

            var totalFoundItems = await _context.FoundItems
                .Where(f => f.TenantId == tenantId)
                .CountAsync();

            var totalMatched = await _context.LostAndFoundMatches
                .Where(m => m.TenantId == tenantId && m.Status == "CONFIRMED")
                .CountAsync();

            var totalClaimed = await _context.LostItems
                .Where(l => l.TenantId == tenantId && l.Status == "Claimed")
                .CountAsync();

            // Calculate urgent items (items nearing disposal or high priority)
            var today = DateTime.UtcNow.Date;
            var urgentItems = await _context.FoundItems
                .Where(f => f.TenantId == tenantId &&
                       (f.Status == "AVAILABLE" || f.Status == "InStorage" || f.Status == "IN_STORAGE") &&
                       f.DisposalDate.HasValue &&
                       f.DisposalDate.Value.Date <= today.AddDays(7))
                .CountAsync();

            var matchSuccessRate = totalLostItems > 0
                ? (double)totalClaimed / totalLostItems * 100
                : 0;

            return Ok(new
            {
                openReports,
                itemsInStorage,
                pendingMatches,
                urgentItems,
                totalLostItems,
                totalFoundItems,
                totalMatched,
                totalClaimed,
                matchSuccessRate = Math.Round(matchSuccessRate, 2)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Lost & Found stats");
            return StatusCode(500, "Error retrieving stats");
        }
    }

    #endregion

    #region Lost Items

    /// <summary>
    /// Get all lost items for the current tenant
    /// </summary>
    [HttpGet("lost-items")]
    public async Task<IActionResult> GetLostItems(
        [FromQuery] string? searchTerm,
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] string? sortBy)
    {
        try
        {
            var tenantId = GetTenantId();

            var query = _context.LostItems
                .Where(l => l.TenantId == tenantId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(l =>
                    l.ItemName.Contains(searchTerm) ||
                    (l.Description != null && l.Description.Contains(searchTerm)) ||
                    (l.ReporterName != null && l.ReporterName.Contains(searchTerm)));
            }

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(l => l.Category == category);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(l => l.Status == status);
            }

            query = sortBy switch
            {
                "oldest" => query.OrderBy(l => l.ReportedAt),
                _ => query.OrderByDescending(l => l.ReportedAt) // "newest" or default
            };

            var items = await query
                .Select(l => new
                {
                    l.Id,
                    l.ItemName,
                    l.Category,
                    l.Description,
                    l.Color,
                    l.Brand,
                    l.LocationLost,
                    l.ReportedAt,
                    l.ReporterPhone,
                    l.ReporterName,
                    l.RoomNumber,
                    l.Status,
                    l.SpecialInstructions,
                    l.RewardAmount,
                    l.ConversationId,
                    l.ClaimedAt
                })
                .ToListAsync();

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lost items");
            return StatusCode(500, "Error retrieving lost items");
        }
    }

    /// <summary>
    /// Get a specific lost item by ID
    /// </summary>
    [HttpGet("lost-items/{id:int}")]
    public async Task<IActionResult> GetLostItemById(int id)
    {
        try
        {
            var tenantId = GetTenantId();

            var item = await _context.LostItems
                .Where(l => l.Id == id && l.TenantId == tenantId)
                .Select(l => new
                {
                    l.Id,
                    l.ItemName,
                    l.Category,
                    l.Description,
                    l.Color,
                    l.Brand,
                    l.LocationLost,
                    l.ReportedAt,
                    l.ReporterPhone,
                    l.ReporterName,
                    l.Status,
                    l.SpecialInstructions,
                    l.RewardAmount,
                    l.ConversationId,
                    l.ClaimedAt,
                    l.AdditionalDetails
                })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                return NotFound("Lost item not found");
            }

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lost item {ItemId}", id);
            return StatusCode(500, "Error retrieving lost item");
        }
    }

    /// <summary>
    /// Update a lost item
    /// </summary>
    [HttpPut("lost-items/{id:int}")]
    public async Task<IActionResult> UpdateLostItem(int id, [FromBody] UpdateLostItemRequest request)
    {
        try
        {
            var tenantId = GetTenantId();

            var item = await _context.LostItems
                .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);

            if (item == null)
            {
                return NotFound("Lost item not found");
            }

            if (request.Status != null) item.Status = request.Status;
            if (request.SpecialInstructions != null) item.SpecialInstructions = request.SpecialInstructions;

            await _context.SaveChangesAsync();

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lost item {ItemId}", id);
            return StatusCode(500, "Error updating lost item");
        }
    }

    /// <summary>
    /// Close a lost item report
    /// </summary>
    [HttpPut("lost-items/{id:int}/close")]
    public async Task<IActionResult> CloseLostItem(int id, [FromBody] CloseLostItemRequest request)
    {
        try
        {
            var tenantId = GetTenantId();

            var item = await _context.LostItems
                .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);

            if (item == null)
            {
                return NotFound("Lost item not found");
            }

            item.Status = request.CloseReason switch
            {
                "Found" => "Claimed",
                "Cancelled" => "Closed",
                _ => "Closed"
            };

            if (!string.IsNullOrEmpty(request.Notes))
            {
                item.SpecialInstructions = (item.SpecialInstructions ?? "") + $"\n[CLOSED] {request.Notes}";
            }

            await _context.SaveChangesAsync();

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing lost item {ItemId}", id);
            return StatusCode(500, "Error closing lost item");
        }
    }

    /// <summary>
    /// Get matches for a specific lost item
    /// </summary>
    [HttpGet("lost-items/{id:int}/matches")]
    public async Task<IActionResult> GetMatchesForLostItem(int id)
    {
        try
        {
            var tenantId = GetTenantId();

            var lostItem = await _context.LostItems
                .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);

            if (lostItem == null)
            {
                return NotFound("Lost item not found");
            }

            var matches = await _context.LostAndFoundMatches
                .Where(m => m.LostItemId == id && m.TenantId == tenantId)
                .Include(m => m.LostItem)
                .Include(m => m.FoundItem)
                .OrderByDescending(m => m.MatchScore)
                .Select(m => new
                {
                    m.Id,
                    m.LostItemId,
                    m.FoundItemId,
                    m.MatchScore,
                    m.Status,
                    m.MatchingReason,
                    m.CreatedAt,
                    LostItem = new
                    {
                        m.LostItem.Id,
                        m.LostItem.ItemName,
                        m.LostItem.Category,
                        m.LostItem.Description,
                        m.LostItem.Color,
                        m.LostItem.Brand,
                        m.LostItem.LocationLost,
                        m.LostItem.ReporterName,
                        m.LostItem.RoomNumber
                    },
                    FoundItem = new
                    {
                        m.FoundItem.Id,
                        m.FoundItem.ItemName,
                        m.FoundItem.Category,
                        m.FoundItem.Description,
                        m.FoundItem.Color,
                        m.FoundItem.Brand,
                        m.FoundItem.LocationFound,
                        m.FoundItem.StorageLocation
                    }
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {MatchCount} matches for lost item {ItemId}", matches.Count, id);

            return Ok(matches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting matches for lost item {ItemId}", id);
            return StatusCode(500, "Error retrieving matches");
        }
    }

    #endregion

    #region Found Items

    /// <summary>
    /// Get all found items for the current tenant
    /// </summary>
    [HttpGet("found-items")]
    public async Task<IActionResult> GetFoundItems(
        [FromQuery] string? searchTerm,
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] string? sortBy)
    {
        try
        {
            var tenantId = GetTenantId();

            var query = _context.FoundItems
                .Where(f => f.TenantId == tenantId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(f =>
                    f.ItemName.Contains(searchTerm) ||
                    (f.Description != null && f.Description.Contains(searchTerm)) ||
                    (f.FinderName != null && f.FinderName.Contains(searchTerm)));
            }

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(f => f.Category == category);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(f => f.Status == status);
            }

            query = sortBy switch
            {
                "oldest" => query.OrderBy(f => f.FoundAt),
                _ => query.OrderByDescending(f => f.FoundAt) // "newest" or default
            };

            var items = await query
                .Select(f => new
                {
                    f.Id,
                    f.ItemName,
                    f.Category,
                    f.Description,
                    f.Color,
                    f.Brand,
                    f.LocationFound,
                    f.FoundAt,
                    f.FinderName,
                    f.StorageLocation,
                    f.StorageNotes,
                    f.Status,
                    f.ClaimedAt,
                    f.DisposalDate,
                    f.DisposalAfterDays
                })
                .ToListAsync();

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting found items");
            return StatusCode(500, "Error retrieving found items");
        }
    }

    /// <summary>
    /// Get a specific found item by ID
    /// </summary>
    [HttpGet("found-items/{id:int}")]
    public async Task<IActionResult> GetFoundItemById(int id)
    {
        try
        {
            var tenantId = GetTenantId();

            var item = await _context.FoundItems
                .Where(f => f.Id == id && f.TenantId == tenantId)
                .Select(f => new
                {
                    f.Id,
                    f.ItemName,
                    f.Category,
                    f.Description,
                    f.Color,
                    f.Brand,
                    f.LocationFound,
                    f.FoundAt,
                    f.FinderName,
                    f.StorageLocation,
                    f.StorageNotes,
                    f.Status,
                    f.ClaimedAt,
                    f.DisposalDate,
                    f.DisposalAfterDays,
                    f.AdditionalDetails
                })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                return NotFound("Found item not found");
            }

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting found item {ItemId}", id);
            return StatusCode(500, "Error retrieving found item");
        }
    }

    /// <summary>
    /// Register a new found item
    /// </summary>
    [HttpPost("found-items")]
    public async Task<IActionResult> RegisterFoundItem([FromBody] RegisterFoundItemRequest request)
    {
        _logger.LogInformation("[RegisterFoundItem] Request received: {@Request}", request);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("[RegisterFoundItem] Validation failed: {@Errors}", ModelState);
            return BadRequest(ModelState);
        }

        try
        {
            var tenantId = GetTenantId();

            var foundItem = new FoundItem
            {
                TenantId = tenantId,
                ItemName = request.ItemName,
                Category = request.Category,
                Description = request.Description,
                Color = request.Color,
                Brand = request.Brand,
                LocationFound = request.FoundLocation,
                FoundAt = request.FoundDate,
                FinderName = request.FoundBy,
                StorageLocation = request.StorageLocation,
                StorageNotes = request.Notes,
                Status = "InStorage"
            };

            _context.FoundItems.Add(foundItem);
            await _context.SaveChangesAsync();

            // Trigger automatic matching
            var matches = await _lostAndFoundService.FindPotentialMatchesForFoundItemAsync(
                tenantId, foundItem.Id);

            _logger.LogInformation(
                "Found item {ItemId} registered with {MatchCount} potential matches",
                foundItem.Id, matches.Count);

            return CreatedAtAction(nameof(GetFoundItemById), new { id = foundItem.Id }, new
            {
                foundItem.Id,
                foundItem.ItemName,
                foundItem.Category,
                foundItem.Description,
                foundItem.Color,
                foundItem.Brand,
                foundItem.LocationFound,
                foundItem.FoundAt,
                foundItem.FinderName,
                foundItem.StorageLocation,
                foundItem.StorageNotes,
                foundItem.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering found item");
            return StatusCode(500, "Error registering found item");
        }
    }

    /// <summary>
    /// Update a found item
    /// </summary>
    [HttpPut("found-items/{id:int}")]
    public async Task<IActionResult> UpdateFoundItem(int id, [FromBody] UpdateFoundItemRequest request)
    {
        try
        {
            var tenantId = GetTenantId();

            var item = await _context.FoundItems
                .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId);

            if (item == null)
            {
                return NotFound("Found item not found");
            }

            if (request.Status != null) item.Status = request.Status;
            if (request.StorageLocation != null) item.StorageLocation = request.StorageLocation;
            if (request.StorageNotes != null) item.StorageNotes = request.StorageNotes;

            await _context.SaveChangesAsync();

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating found item {ItemId}", id);
            return StatusCode(500, "Error updating found item");
        }
    }

    /// <summary>
    /// Find matches for a found item
    /// </summary>
    [HttpPost("found-items/{id:int}/find-matches")]
    public async Task<IActionResult> FindMatchesForFoundItem(int id)
    {
        try
        {
            var tenantId = GetTenantId();

            var foundItem = await _context.FoundItems
                .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId);

            if (foundItem == null)
            {
                return NotFound("Found item not found");
            }

            var matches = await _lostAndFoundService.FindPotentialMatchesForFoundItemAsync(tenantId, id);

            _logger.LogInformation("Found {MatchCount} matches for found item {ItemId}", matches.Count, id);

            return Ok(matches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matches for found item {ItemId}", id);
            return StatusCode(500, "Error finding matches");
        }
    }

    #endregion

    #region Matches

    /// <summary>
    /// Get all matches for the current tenant
    /// </summary>
    [HttpGet("matches")]
    public async Task<IActionResult> GetMatches([FromQuery] string? status)
    {
        try
        {
            var tenantId = GetTenantId();

            var query = _context.LostAndFoundMatches
                .Where(m => m.TenantId == tenantId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(m => m.Status == status);
            }

            var matches = await query
                .Include(m => m.LostItem)
                .Include(m => m.FoundItem)
                .OrderByDescending(m => m.MatchScore)
                .Select(m => new
                {
                    m.Id,
                    m.LostItemId,
                    m.FoundItemId,
                    m.MatchScore,
                    m.Status,
                    m.MatchingReason,
                    m.VerifiedBy,
                    m.VerifiedAt,
                    m.GuestConfirmed,
                    m.GuestConfirmedAt,
                    m.ClaimedAt,
                    m.Notes,
                    m.CreatedAt,
                    LostItem = new
                    {
                        m.LostItem.ItemName,
                        m.LostItem.Category,
                        m.LostItem.Description,
                        m.LostItem.ReporterName,
                        m.LostItem.ReporterPhone
                    },
                    FoundItem = new
                    {
                        m.FoundItem.ItemName,
                        m.FoundItem.Category,
                        m.FoundItem.Description,
                        m.FoundItem.LocationFound,
                        m.FoundItem.FoundAt
                    }
                })
                .ToListAsync();

            return Ok(matches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting matches");
            return StatusCode(500, "Error retrieving matches");
        }
    }

    /// <summary>
    /// Get a specific match by ID
    /// </summary>
    [HttpGet("matches/{id:int}")]
    public async Task<IActionResult> GetMatchById(int id)
    {
        try
        {
            var tenantId = GetTenantId();

            var match = await _context.LostAndFoundMatches
                .Where(m => m.Id == id && m.TenantId == tenantId)
                .Include(m => m.LostItem)
                .Include(m => m.FoundItem)
                .Select(m => new
                {
                    m.Id,
                    m.LostItemId,
                    m.FoundItemId,
                    m.MatchScore,
                    m.Status,
                    m.MatchingReason,
                    m.VerifiedBy,
                    m.VerifiedAt,
                    m.GuestConfirmed,
                    m.GuestConfirmedAt,
                    m.ClaimedAt,
                    m.Notes,
                    m.CreatedAt,
                    LostItem = new
                    {
                        m.LostItem.Id,
                        m.LostItem.ItemName,
                        m.LostItem.Category,
                        m.LostItem.Description,
                        m.LostItem.Color,
                        m.LostItem.Brand,
                        m.LostItem.LocationLost,
                        m.LostItem.ReportedAt,
                        m.LostItem.ReporterName,
                        m.LostItem.ReporterPhone
                    },
                    FoundItem = new
                    {
                        m.FoundItem.Id,
                        m.FoundItem.ItemName,
                        m.FoundItem.Category,
                        m.FoundItem.Description,
                        m.FoundItem.Color,
                        m.FoundItem.Brand,
                        m.FoundItem.LocationFound,
                        m.FoundItem.FoundAt,
                        m.FoundItem.StorageLocation
                    }
                })
                .FirstOrDefaultAsync();

            if (match == null)
            {
                return NotFound("Match not found");
            }

            return Ok(match);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting match {MatchId}", id);
            return StatusCode(500, "Error retrieving match");
        }
    }

    /// <summary>
    /// Verify a match (confirm or reject)
    /// </summary>
    [HttpPut("matches/{id:int}/verify")]
    public async Task<IActionResult> VerifyMatch(int id, [FromBody] VerifyMatchRequest request)
    {
        try
        {
            var tenantId = GetTenantId();

            var match = await _context.LostAndFoundMatches
                .Include(m => m.LostItem)
                .Include(m => m.FoundItem)
                .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId);

            if (match == null)
            {
                return NotFound("Match not found");
            }

            if (request.IsConfirmed)
            {
                match.Status = "CONFIRMED";
                match.LostItem.Status = "Matched";
                match.FoundItem.Status = "MATCHED";
            }
            else
            {
                match.Status = "REJECTED";
            }

            match.VerifiedAt = DateTime.UtcNow;

            // Store verification notes and rejection reason in the Notes field
            if (!string.IsNullOrEmpty(request.Notes))
            {
                var notePrefix = request.IsConfirmed ? "[CONFIRMED]" : "[REJECTED]";
                match.Notes = $"{notePrefix} {request.Notes}";
            }

            // TODO: Get current user ID from authentication context
            // For now, we'll leave VerifiedBy as null since it's int? and we don't have user ID
            // match.VerifiedBy = currentUserId;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Match {MatchId} {Action} for Lost Item {LostItemId} and Found Item {FoundItemId}",
                match.Id,
                request.IsConfirmed ? "CONFIRMED" : "REJECTED",
                match.LostItemId,
                match.FoundItemId);

            return Ok(match);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying match {MatchId}", id);
            return StatusCode(500, "Error verifying match");
        }
    }

    #endregion
}

#region Request DTOs

public class RegisterFoundItemRequest
{
    [Required, MaxLength(100)]
    public string ItemName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? Brand { get; set; }

    [Required, MaxLength(100)]
    public string FoundLocation { get; set; } = string.Empty;

    [Required]
    public DateTime FoundDate { get; set; }

    [MaxLength(100)]
    public string? FoundBy { get; set; }

    [Required, MaxLength(50)]
    public string StorageLocation { get; set; } = string.Empty;

    public bool IsHighValue { get; set; }

    [MaxLength(100)]
    public string? Notes { get; set; }
}

public class UpdateFoundItemRequest
{
    [MaxLength(20)]
    public string? Status { get; set; }

    [MaxLength(50)]
    public string? StorageLocation { get; set; }

    [MaxLength(100)]
    public string? StorageNotes { get; set; }
}

public class UpdateLostItemRequest
{
    [MaxLength(20)]
    public string? Status { get; set; }

    public string? SpecialInstructions { get; set; }
}

public class CloseLostItemRequest
{
    [Required]
    public string CloseReason { get; set; } = string.Empty; // "Found", "Cancelled", "Other"

    public string? Notes { get; set; }
}

public class VerifyMatchRequest
{
    public bool IsConfirmed { get; set; }

    public string? Notes { get; set; }
}

#endregion
