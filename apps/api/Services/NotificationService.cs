using Microsoft.AspNetCore.SignalR;
using Hostr.Api.Models;
using Hostr.Api.Hubs;

namespace Hostr.Api.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<StaffTaskHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IHubContext<StaffTaskHub> hubContext, ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyTaskCreatedAsync(int tenantId, StaffTask task, string? requestItemName = null)
    {
        var notification = new TaskNotification
        {
            TaskId = task.Id,
            TenantId = tenantId,
            TaskType = task.TaskType,
            Title = !string.IsNullOrWhiteSpace(task.Title)
                ? task.Title
                : $"New Task: {task.TaskType.Replace("_", " ").ToTitleCase()}",
            Description = task.Notes ?? "New task assigned",
            Priority = task.Priority,
            Status = task.Status,
            RoomNumber = task.RoomNumber,
            Quantity = task.Quantity,
            Notes = task.Notes,
            RequestItemName = requestItemName,
            CreatedAt = task.CreatedAt,
            CreatedBy = "System"
        };

        var payload = new NotificationPayload
        {
            Type = "task",
            Priority = task.Priority,
            Data = notification,
            Message = $"New {task.TaskType.Replace("_", " ")} task created{(string.IsNullOrEmpty(task.RoomNumber) ? "" : $" for room {task.RoomNumber}")}"
        };

        await SendNotificationToTenantAsync(tenantId, payload);
        await _hubContext.Clients.Group($"Tenant_{tenantId}").SendAsync("TaskCreated", notification);

        _logger.LogInformation("Task created notification sent for Task {TaskId} to Tenant {TenantId}", 
            task.Id, tenantId);
    }

    public async Task NotifyTaskUpdatedAsync(int tenantId, StaffTask task, string? requestItemName = null)
    {
        var notification = new TaskNotification
        {
            TaskId = task.Id,
            TenantId = tenantId,
            TaskType = task.TaskType,
            Title = !string.IsNullOrWhiteSpace(task.Title)
                ? $"Task Updated: {task.Title}"
                : $"Task Updated: {task.TaskType.Replace("_", " ").ToTitleCase()}",
            Description = task.Notes ?? "Task status updated",
            Priority = task.Priority,
            Status = task.Status,
            RoomNumber = task.RoomNumber,
            Quantity = task.Quantity,
            Notes = task.Notes,
            RequestItemName = requestItemName,
            CreatedAt = task.CreatedAt,
            CreatedBy = "System"
        };

        var payload = new NotificationPayload
        {
            Type = "task",
            Priority = task.Priority,
            Data = notification,
            Message = $"Task #{task.Id} status updated to {task.Status}"
        };

        await SendNotificationToTenantAsync(tenantId, payload);
        await _hubContext.Clients.Group($"Tenant_{tenantId}").SendAsync("TaskUpdated", notification);

        _logger.LogInformation("Task updated notification sent for Task {TaskId} to Tenant {TenantId}", 
            task.Id, tenantId);
    }

    public async Task NotifyTaskCompletedAsync(int tenantId, StaffTask task, string? requestItemName = null)
    {
        var notification = new TaskNotification
        {
            TaskId = task.Id,
            TenantId = tenantId,
            TaskType = task.TaskType,
            Title = !string.IsNullOrWhiteSpace(task.Title)
                ? $"Task Completed: {task.Title}"
                : $"Task Completed: {task.TaskType.Replace("_", " ").ToTitleCase()}",
            Description = task.Notes ?? "Task completed successfully",
            Priority = task.Priority,
            Status = task.Status,
            RoomNumber = task.RoomNumber,
            Quantity = task.Quantity,
            Notes = task.Notes,
            RequestItemName = requestItemName,
            CreatedAt = task.CreatedAt,
            CreatedBy = "System"
        };

        var payload = new NotificationPayload
        {
            Type = "task",
            Priority = "Normal", // Completed tasks are normal priority
            Data = notification,
            Message = $"Task #{task.Id} completed successfully"
        };

        await SendNotificationToTenantAsync(tenantId, payload);
        await _hubContext.Clients.Group($"Tenant_{tenantId}").SendAsync("TaskCompleted", notification);

        _logger.LogInformation("Task completed notification sent for Task {TaskId} to Tenant {TenantId}", 
            task.Id, tenantId);
    }

    public async Task NotifyEmergencyIncidentAsync(int tenantId, EmergencyIncident incident, EmergencyType emergencyType)
    {
        var notification = new EmergencyNotification
        {
            IncidentId = incident.Id,
            TenantId = tenantId,
            Title = $"EMERGENCY: {emergencyType.Name}",
            Description = incident.Description,
            EmergencyType = emergencyType.Name,
            SeverityLevel = emergencyType.SeverityLevel,
            Status = incident.Status,
            Location = incident.Location,
            ReportedBy = incident.ReportedBy,
            RequiresImmediateAction = true,
            RequiresEvacuation = emergencyType.RequiresEvacuation,
            ReportedAt = incident.ReportedAt
        };

        var payload = new NotificationPayload
        {
            Type = "emergency",
            Priority = "Urgent", // All emergencies are urgent
            Data = notification,
            Message = $"EMERGENCY: {emergencyType.Name} reported{(string.IsNullOrEmpty(incident.Location) ? "" : $" at {incident.Location}")}"
        };

        await SendNotificationToTenantAsync(tenantId, payload);
        await _hubContext.Clients.Group($"Tenant_{tenantId}").SendAsync("EmergencyAlert", notification);

        _logger.LogCritical("Emergency notification sent for Incident {IncidentId} ({EmergencyType}) to Tenant {TenantId}", 
            incident.Id, emergencyType.Name, tenantId);
    }

    public async Task NotifyEmergencyStatusUpdatedAsync(int tenantId, EmergencyIncident incident, EmergencyType emergencyType)
    {
        var notification = new EmergencyNotification
        {
            IncidentId = incident.Id,
            TenantId = tenantId,
            Title = $"Emergency Update: {emergencyType.Name}",
            Description = incident.Description,
            EmergencyType = emergencyType.Name,
            SeverityLevel = emergencyType.SeverityLevel,
            Status = incident.Status,
            Location = incident.Location,
            ReportedBy = incident.ReportedBy,
            RequiresImmediateAction = incident.Status == "Open",
            RequiresEvacuation = emergencyType.RequiresEvacuation,
            ReportedAt = incident.ReportedAt
        };

        var priority = incident.Status == "Resolved" ? "Normal" : "High";
        var payload = new NotificationPayload
        {
            Type = "emergency",
            Priority = priority,
            Data = notification,
            Message = $"Emergency #{incident.Id} status updated to {incident.Status}"
        };

        await SendNotificationToTenantAsync(tenantId, payload);
        await _hubContext.Clients.Group($"Tenant_{tenantId}").SendAsync("EmergencyAlert", notification);

        _logger.LogWarning("Emergency status update notification sent for Incident {IncidentId} to Tenant {TenantId}", 
            incident.Id, tenantId);
    }

    public async Task NotifyMaintenanceRequestAsync(int tenantId, MaintenanceRequest request, MaintenanceItem? maintenanceItem = null)
    {
        var notification = new MaintenanceNotification
        {
            RequestId = request.Id,
            TenantId = tenantId,
            MaintenanceItemId = request.MaintenanceItemId,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            Priority = request.Priority,
            Status = request.Status,
            Location = request.Location,
            ReportedBy = request.ReportedBy,
            MaintenanceItemName = maintenanceItem?.Name,
            ReportedAt = request.ReportedAt
        };

        var payload = new NotificationPayload
        {
            Type = "maintenance",
            Priority = request.Priority,
            Data = notification,
            Message = $"New {request.Category} maintenance request: {request.Title}"
        };

        await SendNotificationToTenantAsync(tenantId, payload);
        await _hubContext.Clients.Group($"Tenant_{tenantId}").SendAsync("MaintenanceRequest", notification);

        _logger.LogInformation("Maintenance request notification sent for Request {RequestId} to Tenant {TenantId}", 
            request.Id, tenantId);
    }

    public async Task NotifyMaintenanceStatusUpdatedAsync(int tenantId, MaintenanceRequest request, MaintenanceItem? maintenanceItem = null)
    {
        var notification = new MaintenanceNotification
        {
            RequestId = request.Id,
            TenantId = tenantId,
            MaintenanceItemId = request.MaintenanceItemId,
            Title = $"Maintenance Update: {request.Title}",
            Description = request.Description,
            Category = request.Category,
            Priority = request.Priority,
            Status = request.Status,
            Location = request.Location,
            ReportedBy = request.ReportedBy,
            MaintenanceItemName = maintenanceItem?.Name,
            ReportedAt = request.ReportedAt
        };

        var priority = request.Status == "Completed" ? "Normal" : request.Priority;
        var payload = new NotificationPayload
        {
            Type = "maintenance",
            Priority = priority,
            Data = notification,
            Message = $"Maintenance request #{request.Id} status updated to {request.Status}"
        };

        await SendNotificationToTenantAsync(tenantId, payload);
        await _hubContext.Clients.Group($"Tenant_{tenantId}").SendAsync("MaintenanceRequest", notification);

        _logger.LogInformation("Maintenance status update notification sent for Request {RequestId} to Tenant {TenantId}", 
            request.Id, tenantId);
    }

    public async Task SendNotificationToTenantAsync(int tenantId, NotificationPayload payload)
    {
        await _hubContext.Clients.Group($"Tenant_{tenantId}").SendAsync("Notification", payload);
        
        _logger.LogDebug("Generic notification sent to Tenant {TenantId}: {Message}", 
            tenantId, payload.Message);
    }

    public async Task SendNotificationToAllAsync(NotificationPayload payload)
    {
        await _hubContext.Clients.All.SendAsync("Notification", payload);
        
        _logger.LogDebug("Generic notification sent to all clients: {Message}", payload.Message);
    }

    public async Task JoinTenantGroupAsync(string connectionId, int tenantId)
    {
        await _hubContext.Groups.AddToGroupAsync(connectionId, $"Tenant_{tenantId}");
        
        _logger.LogDebug("Connection {ConnectionId} joined Tenant {TenantId} group", 
            connectionId, tenantId);
    }

    public async Task LeaveTenantGroupAsync(string connectionId, int tenantId)
    {
        await _hubContext.Groups.RemoveFromGroupAsync(connectionId, $"Tenant_{tenantId}");

        _logger.LogDebug("Connection {ConnectionId} left Tenant {TenantId} group",
            connectionId, tenantId);
    }

    public async Task NotifyAgentStatusChangedAsync(int tenantId, int agentId, string state, string? statusMessage)
    {
        try
        {
            var notification = new NotificationPayload
            {
                Type = "agent_status_changed",
                Message = $"Agent {agentId} changed status to {state}",
                Data = new Dictionary<string, object>
                {
                    { "agentId", agentId },
                    { "state", state },
                    { "statusMessage", statusMessage ?? "" },
                    { "title", "Agent Status Update" },
                    { "timestamp", DateTime.UtcNow }
                }
            };

            await SendNotificationToTenantAsync(tenantId, notification);

            _logger.LogInformation("Sent agent status change notification for agent {AgentId} in tenant {TenantId}",
                agentId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending agent status change notification for agent {AgentId} in tenant {TenantId}",
                agentId, tenantId);
        }
    }

    public async Task NotifyConversationAssignedAsync(int tenantId, int agentId, int conversationId)
    {
        try
        {
            var notification = new NotificationPayload
            {
                Type = "conversation_assigned",
                Message = $"Conversation {conversationId} has been assigned to you",
                Data = new Dictionary<string, object>
                {
                    { "agentId", agentId },
                    { "conversationId", conversationId },
                    { "title", "New Conversation Assigned" },
                    { "timestamp", DateTime.UtcNow }
                }
            };

            // Send specifically to the assigned agent if possible
            await SendNotificationToTenantAsync(tenantId, notification);

            _logger.LogInformation("Sent conversation assignment notification for conversation {ConversationId} to agent {AgentId} in tenant {TenantId}",
                conversationId, agentId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending conversation assignment notification for conversation {ConversationId} to agent {AgentId} in tenant {TenantId}",
                conversationId, agentId, tenantId);
        }
    }

    public async Task NotifyAgentAssignmentAsync(int agentId, int conversationId, string handoffMessage)
    {
        try
        {
            var notification = new NotificationPayload
            {
                Type = "agent_handoff",
                Priority = "High",
                Message = $"🔔 New conversation transfer - Guest needs your assistance",
                Data = new Dictionary<string, object>
                {
                    { "agentId", agentId },
                    { "conversationId", conversationId },
                    { "handoffMessage", handoffMessage },
                    { "title", "🔄 Human Handoff Request" },
                    { "timestamp", DateTime.UtcNow },
                    { "requiresAction", true }
                }
            };

            // Send to all tenant members so supervisors can see assignments
            // In a more advanced implementation, we'd send specifically to the agent
            await _hubContext.Clients.All.SendAsync("AgentHandoff", notification);

            _logger.LogInformation("Sent agent handoff notification for conversation {ConversationId} to agent {AgentId}",
                conversationId, agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending agent handoff notification for conversation {ConversationId} to agent {AgentId}",
                conversationId, agentId);
        }
    }
}

// Extension method for string title case formatting
public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var words = input.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }
        return string.Join(" ", words);
    }
}