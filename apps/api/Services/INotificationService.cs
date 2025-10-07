using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface INotificationService
{
    // Staff Task Notifications
    Task NotifyTaskCreatedAsync(int tenantId, StaffTask task, string? requestItemName = null);
    Task NotifyTaskUpdatedAsync(int tenantId, StaffTask task, string? requestItemName = null);
    Task NotifyTaskCompletedAsync(int tenantId, StaffTask task, string? requestItemName = null);

    // Emergency Notifications
    Task NotifyEmergencyIncidentAsync(int tenantId, EmergencyIncident incident, EmergencyType emergencyType);
    Task NotifyEmergencyStatusUpdatedAsync(int tenantId, EmergencyIncident incident, EmergencyType emergencyType);

    // Maintenance Notifications
    Task NotifyMaintenanceRequestAsync(int tenantId, MaintenanceRequest request, MaintenanceItem? maintenanceItem = null);
    Task NotifyMaintenanceStatusUpdatedAsync(int tenantId, MaintenanceRequest request, MaintenanceItem? maintenanceItem = null);

    // Generic Notification
    Task SendNotificationToTenantAsync(int tenantId, NotificationPayload payload);
    Task SendNotificationToAllAsync(NotificationPayload payload);

    // Agent Transfer Notifications
    Task NotifyAgentStatusChangedAsync(int tenantId, int agentId, string state, string? statusMessage);
    Task NotifyConversationAssignedAsync(int tenantId, int agentId, int conversationId);
    Task NotifyAgentAssignmentAsync(int agentId, int conversationId, string handoffMessage);

    // Connection Management
    Task JoinTenantGroupAsync(string connectionId, int tenantId);
    Task LeaveTenantGroupAsync(string connectionId, int tenantId);
}