using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public class RecipientGroup
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Count { get; set; }
}

public interface IBroadcastService
{
    Task<(bool Success, string Message, int BroadcastId)> SendEmergencyBroadcastAsync(
        int tenantId,
        string messageType,
        string? customMessage = null,
        string? estimatedRestorationTime = null,
        string createdBy = "System",
        BroadcastScope broadcastScope = BroadcastScope.ActiveOnly);

    Task<(bool Success, string Message, int BroadcastId)> SendGeneralBroadcastAsync(
        int tenantId,
        string title,
        string content,
        List<string> recipients,
        string priority,
        DateTime? scheduledAt,
        string createdBy = "System");

    Task<List<RecipientGroup>> GetRecipientGroupsAsync(int tenantId);
    Task<BroadcastMessage?> GetBroadcastStatusAsync(int broadcastId, int tenantId);
    Task<IEnumerable<BroadcastMessage>> GetRecentBroadcastsAsync(int tenantId, int limit = 10);

    // Template management methods
    Task<IEnumerable<BroadcastTemplate>> GetTemplatesAsync(int tenantId);
    Task<BroadcastTemplate?> GetTemplateByIdAsync(int templateId, int tenantId);
    Task<BroadcastTemplate> CreateTemplateAsync(BroadcastTemplate template);
    Task<BroadcastTemplate?> UpdateTemplateAsync(int templateId, BroadcastTemplate template);
    Task<bool> DeleteTemplateAsync(int templateId, int tenantId);
    Task<bool> SetDefaultTemplateAsync(int templateId, int tenantId, string category);
}

public class BroadcastService : IBroadcastService
{
    private readonly HostrDbContext _context;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<BroadcastService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public BroadcastService(
        HostrDbContext context, 
        IWhatsAppService whatsAppService, 
        ILogger<BroadcastService> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _whatsAppService = whatsAppService;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<(bool Success, string Message, int BroadcastId)> SendEmergencyBroadcastAsync(
        int tenantId, 
        string messageType, 
        string? customMessage = null, 
        string? estimatedRestorationTime = null,
        string createdBy = "System",
        BroadcastScope broadcastScope = BroadcastScope.ActiveOnly)
    {
        try
        {
            // Get conversations based on broadcast scope
            var conversations = await GetConversationsByScope(tenantId, broadcastScope);

            if (!conversations.Any())
            {
                var scopeMessage = broadcastScope switch
                {
                    BroadcastScope.ActiveOnly => "No active guest conversations found for this tenant.",
                    BroadcastScope.RecentGuests => "No active or recent guest conversations found for this tenant.",
                    BroadcastScope.AllGuests => "No guest conversations found for this tenant.",
                    _ => "No guest conversations found for this tenant."
                };
                return (false, scopeMessage, 0);
            }

            // Generate message content
            var messageContent = GenerateEmergencyMessage(messageType, customMessage, estimatedRestorationTime);
            
            // Create broadcast record
            var broadcast = new BroadcastMessage
            {
                TenantId = tenantId,
                MessageType = messageType,
                Content = messageContent,
                EstimatedRestorationTime = estimatedRestorationTime,
                TotalRecipients = conversations.Count,
                CreatedBy = createdBy,
                Status = "InProgress"
            };

            _context.BroadcastMessages.Add(broadcast);
            await _context.SaveChangesAsync();

            // Create recipient records
            var recipients = conversations.Select(conv => new BroadcastRecipient
            {
                BroadcastMessageId = broadcast.Id,
                ConversationId = conv.Id,
                PhoneNumber = conv.WaUserPhone,
                DeliveryStatus = "Pending"
            }).ToList();

            _context.BroadcastRecipients.AddRange(recipients);
            await _context.SaveChangesAsync();

            // Send messages in background task
            _ = Task.Run(async () => await ProcessBroadcastDelivery(broadcast.Id));

            _logger.LogInformation("Emergency broadcast initiated: {BroadcastId} for tenant {TenantId}, type: {MessageType}, recipients: {Count}", 
                broadcast.Id, tenantId, messageType, conversations.Count);

            return (true, $"Emergency broadcast initiated to {conversations.Count} guests.", broadcast.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate emergency broadcast for tenant {TenantId}", tenantId);
            return (false, "Failed to initiate emergency broadcast. Please try again.", 0);
        }
    }

    private async Task ProcessBroadcastDelivery(int broadcastId)
    {
        try
        {
            // Create a new scope for background processing to avoid disposed context issues
            using var scope = _serviceProvider.CreateScope();
            var scopedContext = scope.ServiceProvider.GetRequiredService<HostrDbContext>();
            var scopedWhatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
            
            var broadcast = await scopedContext.BroadcastMessages
                .Include(b => b.Recipients)
                .FirstOrDefaultAsync(b => b.Id == broadcastId);

            if (broadcast == null) return;

            int successCount = 0;
            int failureCount = 0;

            foreach (var recipient in broadcast.Recipients.Where(r => r.DeliveryStatus == "Pending"))
            {
                try
                {
                    var success = await scopedWhatsAppService.SendTextMessageAsync(
                        broadcast.TenantId, 
                        recipient.PhoneNumber, 
                        broadcast.Content);

                    recipient.DeliveryStatus = success ? "Sent" : "Failed";
                    recipient.SentAt = DateTime.UtcNow;
                    
                    if (!success)
                    {
                        recipient.ErrorMessage = "WhatsApp API call failed";
                        failureCount++;
                    }
                    else
                    {
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    recipient.DeliveryStatus = "Failed";
                    recipient.ErrorMessage = ex.Message;
                    failureCount++;
                    
                    _logger.LogError(ex, "Failed to send broadcast message to {PhoneNumber} for broadcast {BroadcastId}", 
                        recipient.PhoneNumber, broadcastId);
                }

                // Add small delay between messages to avoid rate limiting
                await Task.Delay(500);
            }

            // Update broadcast status
            broadcast.SuccessfulDeliveries = successCount;
            broadcast.FailedDeliveries = failureCount;
            broadcast.Status = "Completed";
            broadcast.CompletedAt = DateTime.UtcNow;

            await scopedContext.SaveChangesAsync();

            _logger.LogInformation("Broadcast {BroadcastId} completed: {SuccessCount} successful, {FailureCount} failed", 
                broadcastId, successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing broadcast delivery for broadcast {BroadcastId}", broadcastId);
            
            // Mark as failed using a new scope
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var scopedContext = scope.ServiceProvider.GetRequiredService<HostrDbContext>();
                var broadcast = await scopedContext.BroadcastMessages.FindAsync(broadcastId);
                if (broadcast != null)
                {
                    broadcast.Status = "Failed";
                    broadcast.CompletedAt = DateTime.UtcNow;
                    await scopedContext.SaveChangesAsync();
                }
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to update broadcast status to failed for broadcast {BroadcastId}", broadcastId);
            }
        }
    }

    private string GenerateEmergencyMessage(string messageType, string? customMessage, string? estimatedRestorationTime)
    {
        if (!string.IsNullOrEmpty(customMessage))
        {
            return customMessage;
        }

        var eta = !string.IsNullOrEmpty(estimatedRestorationTime) ? $" Estimated restoration time: {estimatedRestorationTime}." : "";
        
        return messageType.ToLower() switch
        {
            "power_outage" => $"⚠️ **Power Outage Update**\n\nDue to an area power outage affecting our neighborhood, electricity service is temporarily unavailable.{eta} We have backup generators for emergency lighting and elevators. We'll update you as soon as power is restored. We apologize for any inconvenience.",
            
            "water_outage" => $"💧 **Water Service Update**\n\nMunicipal water supply is temporarily interrupted in our area.{eta} Complimentary bottled water is available at our reception desk. We're working closely with authorities to restore service. We apologize for the inconvenience.",
            
            "internet_down" => $"🌐 **Internet Service Update**\n\nArea internet service is currently disrupted.{eta} Mobile data and lobby WiFi may still be available. Our technical team is working on restoration. We'll notify you once service is fully restored. Thank you for your patience.",
            
            "emergency" => $"🚨 **Emergency Update**\n\n{customMessage ?? "We are currently experiencing an emergency situation."}{eta} Please follow staff instructions and remain calm. We will provide updates as they become available. Your safety is our priority.",
            
            _ => $"ℹ️ **Service Update**\n\n{customMessage ?? "We are currently experiencing a temporary service disruption."}{eta} We apologize for any inconvenience and will update you as soon as the situation is resolved."
        };
    }

    private async Task<List<Conversation>> GetConversationsByScope(int tenantId, BroadcastScope broadcastScope)
    {
        return broadcastScope switch
        {
            BroadcastScope.ActiveOnly => await _context.Conversations
                .Where(c => c.TenantId == tenantId && c.Status == "Active")
                .ToListAsync(),
                
            BroadcastScope.RecentGuests => await _context.Conversations
                .Where(c => c.TenantId == tenantId && 
                           (c.Status == "Active" || c.CreatedAt >= DateTime.UtcNow.AddDays(-7)))
                .ToListAsync(),
                
            BroadcastScope.AllGuests => await _context.Conversations
                .Where(c => c.TenantId == tenantId)
                .ToListAsync(),
                
            _ => await _context.Conversations
                .Where(c => c.TenantId == tenantId && c.Status == "Active")
                .ToListAsync()
        };
    }

    public async Task<BroadcastMessage?> GetBroadcastStatusAsync(int broadcastId, int tenantId)
    {
        return await _context.BroadcastMessages
            .Include(b => b.Recipients)
            .Where(b => b.Id == broadcastId && b.TenantId == tenantId)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<BroadcastMessage>> GetRecentBroadcastsAsync(int tenantId, int limit = 10)
    {
        return await _context.BroadcastMessages
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message, int BroadcastId)> SendGeneralBroadcastAsync(
        int tenantId,
        string title,
        string content,
        List<string> recipients,
        string priority,
        DateTime? scheduledAt,
        string createdBy = "System")
    {
        try
        {
            // Get conversations based on recipient selections
            var conversations = await GetConversationsByRecipients(tenantId, recipients);

            if (!conversations.Any())
            {
                return (false, "No guest conversations found for the selected recipient groups.", 0);
            }

            // Create broadcast record
            var broadcast = new BroadcastMessage
            {
                TenantId = tenantId,
                MessageType = "general",
                Content = content,
                TotalRecipients = conversations.Count,
                CreatedBy = createdBy,
                Status = scheduledAt.HasValue ? "Scheduled" : "InProgress"
            };

            _context.BroadcastMessages.Add(broadcast);
            await _context.SaveChangesAsync();

            // Create recipient records
            var broadcastRecipients = conversations.Select(conv => new BroadcastRecipient
            {
                BroadcastMessageId = broadcast.Id,
                ConversationId = conv.Id,
                PhoneNumber = conv.WaUserPhone,
                DeliveryStatus = "Pending"
            }).ToList();

            _context.BroadcastRecipients.AddRange(broadcastRecipients);
            await _context.SaveChangesAsync();

            // Send messages immediately or schedule for later
            if (!scheduledAt.HasValue)
            {
                _ = Task.Run(async () => await ProcessBroadcastDelivery(broadcast.Id));
            }

            _logger.LogInformation("General broadcast initiated: {BroadcastId} for tenant {TenantId}, recipients: {Count}",
                broadcast.Id, tenantId, conversations.Count);

            var actionText = scheduledAt.HasValue ? "scheduled" : "initiated";
            return (true, $"General broadcast {actionText} to {conversations.Count} guests.", broadcast.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send general broadcast for tenant {TenantId}", tenantId);
            return (false, "Failed to send general broadcast. Please try again.", 0);
        }
    }

    public async Task<List<RecipientGroup>> GetRecipientGroupsAsync(int tenantId)
    {
        try
        {
            var groups = new List<RecipientGroup>();

            // All active guests
            var activeCount = await _context.Conversations
                .Where(c => c.TenantId == tenantId && c.Status == "Active")
                .CountAsync();

            if (activeCount > 0)
            {
                groups.Add(new RecipientGroup
                {
                    Id = "all_active",
                    Type = "all",
                    Name = "All Active Guests",
                    Description = "Send to all current active guests",
                    Count = activeCount
                });
            }

            // Recent guests (last 7 days)
            var recentCount = await _context.Conversations
                .Where(c => c.TenantId == tenantId &&
                           (c.Status == "Active" || c.CreatedAt >= DateTime.UtcNow.AddDays(-7)))
                .CountAsync();

            if (recentCount > activeCount)
            {
                groups.Add(new RecipientGroup
                {
                    Id = "recent_guests",
                    Type = "recent",
                    Name = "Recent Guests",
                    Description = "Active guests + guests from last 7 days",
                    Count = recentCount
                });
            }

            // Today's check-ins (if we have booking data)
            var todayCheckins = await _context.Bookings
                .Where(b => b.TenantId == tenantId &&
                           b.CheckinDate == DateOnly.FromDateTime(DateTime.UtcNow))
                .Join(_context.Conversations,
                     booking => new { booking.TenantId, booking.Phone },
                     conv => new { conv.TenantId, Phone = conv.WaUserPhone },
                     (booking, conv) => conv)
                .CountAsync();

            if (todayCheckins > 0)
            {
                groups.Add(new RecipientGroup
                {
                    Id = "todays_checkins",
                    Type = "checkedin",
                    Name = "Today's Check-ins",
                    Description = "Guests who checked in today",
                    Count = todayCheckins
                });
            }

            // Floor-based groups (if room numbers are available)
            var floorsWithGuests = await _context.Bookings
                .Where(b => b.TenantId == tenantId && !string.IsNullOrEmpty(b.RoomNumber))
                .Join(_context.Conversations,
                     booking => new { booking.TenantId, booking.Phone },
                     conv => new { conv.TenantId, Phone = conv.WaUserPhone },
                     (booking, conv) => new { booking.RoomNumber, Conversation = conv })
                .ToListAsync();

            var floorGroups = floorsWithGuests
                .Where(x => x.RoomNumber.Length > 0)
                .GroupBy(x => x.RoomNumber[0].ToString()) // First digit as floor
                .Where(g => g.Count() > 0)
                .Select(g => new RecipientGroup
                {
                    Id = $"floor_{g.Key}",
                    Type = "floor",
                    Name = $"Floor {g.Key}",
                    Description = $"All guests on floor {g.Key}",
                    Count = g.Count()
                })
                .OrderBy(g => g.Name)
                .ToList();

            groups.AddRange(floorGroups);

            return groups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recipient groups for tenant {TenantId}", tenantId);
            return new List<RecipientGroup>();
        }
    }

    private async Task<List<Conversation>> GetConversationsByRecipients(int tenantId, List<string> recipients)
    {
        var conversations = new List<Conversation>();

        foreach (var recipient in recipients)
        {
            var recipientConversations = recipient switch
            {
                "all_active" => await _context.Conversations
                    .Where(c => c.TenantId == tenantId && c.Status == "Active")
                    .ToListAsync(),

                "recent_guests" => await _context.Conversations
                    .Where(c => c.TenantId == tenantId &&
                               (c.Status == "Active" || c.CreatedAt >= DateTime.UtcNow.AddDays(-7)))
                    .ToListAsync(),

                "todays_checkins" => await _context.Bookings
                    .Where(b => b.TenantId == tenantId &&
                               b.CheckinDate == DateOnly.FromDateTime(DateTime.UtcNow))
                    .Join(_context.Conversations,
                         booking => new { booking.TenantId, booking.Phone },
                         conv => new { conv.TenantId, Phone = conv.WaUserPhone },
                         (booking, conv) => conv)
                    .ToListAsync(),

                var floorId when floorId.StartsWith("floor_") => await GetFloorConversations(tenantId, floorId.Replace("floor_", "")),

                _ => new List<Conversation>()
            };

            // Add conversations that aren't already included
            foreach (var conv in recipientConversations)
            {
                if (!conversations.Any(c => c.Id == conv.Id))
                {
                    conversations.Add(conv);
                }
            }
        }

        return conversations;
    }

    private async Task<List<Conversation>> GetFloorConversations(int tenantId, string floor)
    {
        return await _context.Bookings
            .Where(b => b.TenantId == tenantId &&
                       !string.IsNullOrEmpty(b.RoomNumber) &&
                       b.RoomNumber.StartsWith(floor))
            .Join(_context.Conversations,
                 booking => new { booking.TenantId, booking.Phone },
                 conv => new { conv.TenantId, Phone = conv.WaUserPhone },
                 (booking, conv) => conv)
            .ToListAsync();
    }

    // Template management methods implementation
    public async Task<IEnumerable<BroadcastTemplate>> GetTemplatesAsync(int tenantId)
    {
        return await _context.BroadcastTemplates
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<BroadcastTemplate?> GetTemplateByIdAsync(int templateId, int tenantId)
    {
        return await _context.BroadcastTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId);
    }

    public async Task<BroadcastTemplate> CreateTemplateAsync(BroadcastTemplate template)
    {
        try
        {
            // If this is set as default, unset other defaults in the same category
            if (template.IsDefault)
            {
                var existingDefaults = await _context.BroadcastTemplates
                    .Where(t => t.TenantId == template.TenantId &&
                               t.Category == template.Category &&
                               t.IsDefault)
                    .ToListAsync();

                foreach (var existing in existingDefaults)
                {
                    existing.IsDefault = false;
                }
            }

            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;

            _context.BroadcastTemplates.Add(template);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created broadcast template {TemplateId} for tenant {TenantId}",
                template.Id, template.TenantId);

            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create broadcast template for tenant {TenantId}", template.TenantId);
            throw;
        }
    }

    public async Task<BroadcastTemplate?> UpdateTemplateAsync(int templateId, BroadcastTemplate template)
    {
        try
        {
            var existingTemplate = await _context.BroadcastTemplates
                .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == template.TenantId);

            if (existingTemplate == null)
                return null;

            // If this is being set as default, unset other defaults in the same category
            if (template.IsDefault && !existingTemplate.IsDefault)
            {
                var otherDefaults = await _context.BroadcastTemplates
                    .Where(t => t.TenantId == template.TenantId &&
                               t.Category == template.Category &&
                               t.IsDefault &&
                               t.Id != templateId)
                    .ToListAsync();

                foreach (var other in otherDefaults)
                {
                    other.IsDefault = false;
                }
            }

            // Update fields
            existingTemplate.Name = template.Name;
            existingTemplate.Category = template.Category;
            existingTemplate.Subject = template.Subject;
            existingTemplate.Content = template.Content;
            existingTemplate.IsActive = template.IsActive;
            existingTemplate.IsDefault = template.IsDefault;
            existingTemplate.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated broadcast template {TemplateId} for tenant {TenantId}",
                templateId, template.TenantId);

            return existingTemplate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update broadcast template {TemplateId} for tenant {TenantId}",
                templateId, template.TenantId);
            throw;
        }
    }

    public async Task<bool> DeleteTemplateAsync(int templateId, int tenantId)
    {
        try
        {
            var template = await _context.BroadcastTemplates
                .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId);

            if (template == null)
                return false;

            _context.BroadcastTemplates.Remove(template);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted broadcast template {TemplateId} for tenant {TenantId}",
                templateId, tenantId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete broadcast template {TemplateId} for tenant {TenantId}",
                templateId, tenantId);
            return false;
        }
    }

    public async Task<bool> SetDefaultTemplateAsync(int templateId, int tenantId, string category)
    {
        try
        {
            // Unset all current defaults in this category
            var currentDefaults = await _context.BroadcastTemplates
                .Where(t => t.TenantId == tenantId &&
                           t.Category == category &&
                           t.IsDefault)
                .ToListAsync();

            foreach (var current in currentDefaults)
            {
                current.IsDefault = false;
            }

            // Set the new default
            var template = await _context.BroadcastTemplates
                .FirstOrDefaultAsync(t => t.Id == templateId &&
                                         t.TenantId == tenantId &&
                                         t.Category == category);

            if (template == null)
                return false;

            template.IsDefault = true;
            template.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Set template {TemplateId} as default for category {Category} in tenant {TenantId}",
                templateId, category, tenantId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set default template {TemplateId} for tenant {TenantId}",
                templateId, tenantId);
            return false;
        }
    }
}