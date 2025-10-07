using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface IMaintenanceService
{
    Task<MaintenanceRequest> CreateMaintenanceRequestAsync(
        int tenantId,
        string title,
        string description,
        string category,
        string priority = "Normal",
        string? location = null,
        string? reportedBy = null,
        int? conversationId = null,
        int? maintenanceItemId = null);
        
    Task<List<MaintenanceRequest>> GetMaintenanceRequestsAsync(
        int tenantId,
        string? status = null,
        string? category = null,
        int? assignedTo = null);
        
    Task<bool> UpdateMaintenanceRequestStatusAsync(
        int requestId,
        int tenantId,
        string status,
        string? resolutionNotes = null,
        decimal? cost = null);
        
    Task<List<MaintenanceItem>> GetMaintenanceItemsAsync(int tenantId, string? category = null);
    
    Task<MaintenanceItem> CreateMaintenanceItemAsync(
        int tenantId,
        string name,
        string category,
        string? location = null,
        string? manufacturer = null,
        string? modelNumber = null,
        int serviceIntervalDays = 0);
        
    Task<List<MaintenanceItem>> GetItemsDueForServiceAsync(int tenantId, DateTime? checkDate = null);
    
    Task<MaintenanceHistory> RecordMaintenanceHistoryAsync(
        int tenantId,
        int maintenanceItemId,
        int? maintenanceRequestId,
        string serviceType,
        string description,
        DateTime serviceDate,
        string? performedBy = null,
        decimal? cost = null,
        string? partsReplaced = null,
        string? notes = null,
        DateTime? nextServiceDue = null);
        
    Task<(bool IsRecognized, string Category, string Priority)> CategorizeMaintenanceRequestAsync(
        TenantContext tenantContext,
        string description,
        string? location = null);
}

public class MaintenanceService : IMaintenanceService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<MaintenanceService> _logger;
    private readonly INotificationService _notificationService;
    
    // Maintenance categories and their keywords
    private static readonly Dictionary<string, string[]> MaintenanceCategories = new()
    {
        {"HVAC", new[] {"air conditioning", "ac", "heating", "ventilation", "thermostat", "temperature", "hot", "cold", "hvac"}},
        {"Plumbing", new[] {"water", "leak", "pipe", "drain", "toilet", "shower", "faucet", "sink", "plumbing", "bathroom"}},
        {"Electrical", new[] {"light", "power", "electricity", "outlet", "switch", "electrical", "lamp", "bulb", "wire"}},
        {"Elevator", new[] {"elevator", "lift", "stuck", "floor"}},
        {"Pool", new[] {"pool", "swimming", "chlorine", "filter", "pump"}},
        {"Security", new[] {"lock", "key", "door", "window", "alarm", "security", "access"}},
        {"Appliance", new[] {"refrigerator", "fridge", "microwave", "dishwasher", "oven", "stove", "washing machine", "dryer"}},
        {"General", new[] {"repair", "fix", "broken", "maintenance", "clean", "replace"}}
    };
    
    // Priority keywords
    private static readonly Dictionary<string, string[]> PriorityKeywords = new()
    {
        {"Urgent", new[] {"emergency", "urgent", "immediate", "critical", "dangerous", "flooding", "fire", "smoke", "gas leak"}},
        {"High", new[] {"important", "asap", "soon", "priority", "serious"}},
        {"Normal", new[] {"when possible", "convenient", "routine", "regular"}},
        {"Low", new[] {"minor", "small", "cosmetic", "whenever"}}
    };

    public MaintenanceService(HostrDbContext context, ILogger<MaintenanceService> logger, INotificationService notificationService)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<MaintenanceRequest> CreateMaintenanceRequestAsync(
        int tenantId,
        string title,
        string description,
        string category,
        string priority = "Normal",
        string? location = null,
        string? reportedBy = null,
        int? conversationId = null,
        int? maintenanceItemId = null)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        var request = new MaintenanceRequest
        {
            TenantId = tenantId,
            MaintenanceItemId = maintenanceItemId,
            ConversationId = conversationId,
            Title = title,
            Description = description,
            Category = category,
            Priority = priority,
            Location = location,
            ReportedBy = reportedBy,
            ReportedAt = DateTime.UtcNow,
            Status = "Open"
        };
        
        _context.MaintenanceRequests.Add(request);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Maintenance request created: {RequestId} - {Title} [{Category}]", 
            request.Id, request.Title, request.Category);
            
        // Send real-time notification
        var maintenanceItem = maintenanceItemId.HasValue ? 
            await _context.MaintenanceItems.FindAsync(maintenanceItemId.Value) : null;
        await _notificationService.NotifyMaintenanceRequestAsync(tenantId, request, maintenanceItem);
            
        return request;
    }
    
    public async Task<List<MaintenanceRequest>> GetMaintenanceRequestsAsync(
        int tenantId,
        string? status = null,
        string? category = null,
        int? assignedTo = null)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        var query = _context.MaintenanceRequests
            .Include(r => r.MaintenanceItem)
            .Include(r => r.Conversation)
            .Include(r => r.AssignedToUser)
            .AsQueryable();
            
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);
            
        if (!string.IsNullOrEmpty(category))
            query = query.Where(r => r.Category == category);
            
        if (assignedTo.HasValue)
            query = query.Where(r => r.AssignedTo == assignedTo.Value);
            
        return await query
            .OrderByDescending(r => r.ReportedAt)
            .ToListAsync();
    }
    
    public async Task<bool> UpdateMaintenanceRequestStatusAsync(
        int requestId,
        int tenantId,
        string status,
        string? resolutionNotes = null,
        decimal? cost = null)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        var request = await _context.MaintenanceRequests.FindAsync(requestId);
        if (request == null || request.TenantId != tenantId)
            return false;
            
        request.Status = status;
        request.ResolutionNotes = resolutionNotes;
        request.Cost = cost;
        
        if (status == "Completed")
            request.CompletedAt = DateTime.UtcNow;
            
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Maintenance request {RequestId} status updated to {Status}", 
            requestId, status);
            
        // Send real-time notification for status update
        var maintenanceItem = request.MaintenanceItemId.HasValue ? 
            await _context.MaintenanceItems.FindAsync(request.MaintenanceItemId.Value) : null;
        await _notificationService.NotifyMaintenanceStatusUpdatedAsync(tenantId, request, maintenanceItem);
            
        return true;
    }
    
    public async Task<List<MaintenanceItem>> GetMaintenanceItemsAsync(int tenantId, string? category = null)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        var query = _context.MaintenanceItems
            .Include(i => i.MaintenanceRequests)
            .Where(i => i.IsActive);
            
        if (!string.IsNullOrEmpty(category))
            query = query.Where(i => i.Category == category);
            
        return await query
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .ToListAsync();
    }
    
    public async Task<MaintenanceItem> CreateMaintenanceItemAsync(
        int tenantId,
        string name,
        string category,
        string? location = null,
        string? manufacturer = null,
        string? modelNumber = null,
        int serviceIntervalDays = 0)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        var item = new MaintenanceItem
        {
            TenantId = tenantId,
            Name = name,
            Category = category,
            Location = location,
            Manufacturer = manufacturer,
            ModelNumber = modelNumber,
            ServiceIntervalDays = serviceIntervalDays,
            Status = "Operational",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        if (serviceIntervalDays > 0)
        {
            item.NextScheduledService = DateTime.UtcNow.AddDays(serviceIntervalDays);
        }
        
        _context.MaintenanceItems.Add(item);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Maintenance item created: {ItemId} - {Name} [{Category}]", 
            item.Id, item.Name, item.Category);
            
        return item;
    }
    
    public async Task<List<MaintenanceItem>> GetItemsDueForServiceAsync(int tenantId, DateTime? checkDate = null)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        var targetDate = checkDate ?? DateTime.UtcNow.AddDays(7); // Check 7 days ahead by default
        
        return await _context.MaintenanceItems
            .Where(i => i.IsActive && 
                       i.NextScheduledService.HasValue && 
                       i.NextScheduledService <= targetDate)
            .OrderBy(i => i.NextScheduledService)
            .ToListAsync();
    }
    
    public async Task<MaintenanceHistory> RecordMaintenanceHistoryAsync(
        int tenantId,
        int maintenanceItemId,
        int? maintenanceRequestId,
        string serviceType,
        string description,
        DateTime serviceDate,
        string? performedBy = null,
        decimal? cost = null,
        string? partsReplaced = null,
        string? notes = null,
        DateTime? nextServiceDue = null)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        var history = new MaintenanceHistory
        {
            TenantId = tenantId,
            MaintenanceItemId = maintenanceItemId,
            MaintenanceRequestId = maintenanceRequestId,
            ServiceType = serviceType,
            Description = description,
            ServiceDate = serviceDate,
            PerformedBy = performedBy,
            Cost = cost,
            PartsReplaced = partsReplaced,
            Notes = notes,
            NextServiceDue = nextServiceDue,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.MaintenanceHistory.Add(history);
        
        // Update the maintenance item's last service date and next scheduled service
        var item = await _context.MaintenanceItems.FindAsync(maintenanceItemId);
        if (item != null && item.TenantId == tenantId)
        {
            item.LastServiceDate = serviceDate;
            if (nextServiceDue.HasValue)
            {
                item.NextScheduledService = nextServiceDue;
            }
            else if (item.ServiceIntervalDays > 0)
            {
                item.NextScheduledService = serviceDate.AddDays(item.ServiceIntervalDays);
            }
            item.UpdatedAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Maintenance history recorded: {HistoryId} for item {ItemId}", 
            history.Id, maintenanceItemId);
            
        return history;
    }
    
    public async Task<(bool IsRecognized, string Category, string Priority)> CategorizeMaintenanceRequestAsync(
        TenantContext tenantContext,
        string description,
        string? location = null)
    {
        try
        {
            var descriptionLower = description.ToLower();
            var locationLower = location?.ToLower() ?? "";
            var combinedText = $"{descriptionLower} {locationLower}";
            
            // Determine category based on keywords
            string category = "General";
            int maxMatches = 0;
            
            foreach (var categoryKv in MaintenanceCategories)
            {
                int matches = categoryKv.Value.Count(keyword => combinedText.Contains(keyword.ToLower()));
                if (matches > maxMatches)
                {
                    maxMatches = matches;
                    category = categoryKv.Key;
                }
            }
            
            // Determine priority based on keywords
            string priority = "Normal";
            
            foreach (var priorityKv in PriorityKeywords)
            {
                if (priorityKv.Value.Any(keyword => combinedText.Contains(keyword.ToLower())))
                {
                    priority = priorityKv.Key;
                    break; // Take the first match (ordered by priority)
                }
            }
            
            bool isRecognized = maxMatches > 0;
            
            _logger.LogInformation("Maintenance categorization: {Description} -> Category: {Category}, Priority: {Priority} (Recognized: {IsRecognized})", 
                description, category, priority, isRecognized);
                
            return (isRecognized, category, priority);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error categorizing maintenance request");
            return (false, "General", "Normal");
        }
    }
}