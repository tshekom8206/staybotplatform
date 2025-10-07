using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Hostr.Api.Services;

namespace Hostr.Api.Hubs;

// [Authorize] // Temporarily removed for testing
public class StaffTaskHub : Hub
{
    private readonly ILogger<StaffTaskHub> _logger;
    private readonly INotificationService _notificationService;

    public StaffTaskHub(ILogger<StaffTaskHub> logger, INotificationService notificationService)
    {
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task JoinTenantGroup(int tenantId)
    {
        var groupName = $"Tenant_{tenantId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await _notificationService.JoinTenantGroupAsync(Context.ConnectionId, tenantId);
        
        _logger.LogInformation("Connection {ConnectionId} joined tenant group {TenantId}", 
            Context.ConnectionId, tenantId);
    }

    public async Task LeaveTenantGroup(int tenantId)
    {
        var groupName = $"Tenant_{tenantId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await _notificationService.LeaveTenantGroupAsync(Context.ConnectionId, tenantId);
        
        _logger.LogInformation("Connection {ConnectionId} left tenant group {TenantId}", 
            Context.ConnectionId, tenantId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}. Exception: {Exception}", 
            Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }

    // Client methods that can be called from the server:
    // - TaskCreated(taskNotification)
    // - TaskUpdated(taskNotification)  
    // - TaskCompleted(taskNotification)
    // - EmergencyAlert(emergencyNotification)
    // - MaintenanceRequest(maintenanceNotification)
    // - Notification(notificationPayload)
}