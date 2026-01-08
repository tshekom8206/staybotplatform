using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Middleware;
using Hostr.Api.Services;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public class TaskController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<TaskController> _logger;
    private readonly IRatingService _ratingService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly INotificationService _notificationService;
    private readonly IPushNotificationService _pushNotificationService;

    public TaskController(
        HostrDbContext context,
        ILogger<TaskController> logger,
        IRatingService ratingService,
        IWhatsAppService whatsAppService,
        INotificationService notificationService,
        IPushNotificationService pushNotificationService)
    {
        _context = context;
        _logger = logger;
        _ratingService = ratingService;
        _whatsAppService = whatsAppService;
        _notificationService = notificationService;
        _pushNotificationService = pushNotificationService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetTasks(
        [FromQuery] string? department = null,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] int? assignedToId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        var query = _context.StaffTasks
            .Include(t => t.RequestItem)
            .Include(t => t.Conversation)
            .Include(t => t.CreatedByUser)
            .Where(t => t.TenantId == tenantId);

        if (!string.IsNullOrEmpty(department))
        {
            query = query.Where(t => t.Department == department);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.Status == status.ToUpper());
        }

        if (!string.IsNullOrEmpty(priority))
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (assignedToId.HasValue)
        {
            query = query.Where(t => t.CreatedBy == assignedToId.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(t =>
                (t.RequestItem != null && t.RequestItem.Name.Contains(searchTerm)) ||
                (t.Notes != null && t.Notes.Contains(searchTerm)) ||
                (t.RoomNumber != null && t.RoomNumber.Contains(searchTerm)));
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= dateTo.Value);
        }

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.TenantId,
                t.ConversationId,
                RequestItemId = t.RequestItemId,
                BookingId = (int?)null, // TODO: Add when booking relationship exists
                Title = t.Title ?? (t.RequestItem != null ? t.RequestItem.Name : "General Task"),
                Description = t.Description ?? (t.RequestItem != null ? t.RequestItem.Name : "General Task"),
                Department = t.Department,
                Priority = MapPriorityToFrontend(t.Priority),
                Status = MapStatusToFrontend(t.Status),
                AssignedToId = t.AssignedToId ?? t.CreatedBy,
                AssignedTo = t.CreatedByUser != null ? new
                {
                    t.CreatedByUser.Id,
                    UserName = t.CreatedByUser.UserName,
                    Email = t.CreatedByUser.Email
                } : null,
                t.RoomNumber,
                GuestName = t.GuestName,
                GuestPhone = t.GuestPhone ?? (t.Conversation != null ? t.Conversation.WaUserPhone : null),
                t.Quantity,
                t.EstimatedCompletionTime,
                t.CompletedAt,
                CompletedBy = t.CompletedBy,
                t.Notes,
                t.CreatedAt,
                t.UpdatedAt,
                RequestItem = t.RequestItem != null ? new
                {
                    t.RequestItem.Id,
                    t.RequestItem.TenantId,
                    t.RequestItem.Name,
                    t.RequestItem.Category,
                    Department = t.Department,
                    t.RequestItem.LlmVisibleName,
                    t.RequestItem.EstimatedTime,
                    RequiresQuantity = t.RequestItem.RequiresQuantity,
                    DefaultQuantityLimit = t.RequestItem.DefaultQuantityLimit,
                    NotesForStaff = t.RequestItem.NotesForStaff,
                    IsActive = t.RequestItem.IsAvailable,
                    t.RequestItem.IsUrgent,
                    t.RequestItem.DisplayOrder
                } : null,
                Conversation = t.Conversation != null ? new
                {
                    t.Conversation.Id,
                    t.Conversation.WaUserPhone
                } : null
            })
            .ToListAsync();

        return Ok(tasks);
    }

    [HttpGet("my")]
    public async Task<ActionResult<IEnumerable<object>>> GetMyTasks(
        [FromQuery] string? department = null,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        // For now, return all tasks since we don't have user authentication
        // TODO: Filter by actual authenticated user ID
        return await GetTasks(department, status, priority, null, searchTerm, dateFrom, dateTo);
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<object>> GetTaskStatistics(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        var query = _context.StaffTasks.Where(t => t.TenantId == tenantId);

        if (dateFrom.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= dateTo.Value);
        }

        var tasks = await query.ToListAsync();

        var statistics = new
        {
            TotalTasks = tasks.Count,
            PendingTasks = tasks.Count(t => t.Status == "Open"),
            InProgressTasks = tasks.Count(t => t.Status == "InProgress"),
            CompletedTasks = tasks.Count(t => t.Status == "Completed"),
            AverageCompletionTime = 45, // TODO: Calculate actual average
            TasksByDepartment = tasks.GroupBy(t => t.Department)
                .ToDictionary(g => g.Key, g => g.Count()),
            TasksByPriority = tasks.GroupBy(t => MapPriorityToFrontend(t.Priority))
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return Ok(statistics);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetTask(int id)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        var task = await _context.StaffTasks
            .Include(t => t.RequestItem)
            .Include(t => t.Conversation)
            .Include(t => t.CreatedByUser)
            .Where(t => t.Id == id && t.TenantId == tenantId)
            .Select(t => new
            {
                t.Id,
                t.TenantId,
                t.ConversationId,
                RequestItemId = t.RequestItemId,
                BookingId = (int?)null,
                Title = t.Title ?? (t.RequestItem != null ? t.RequestItem.Name : "General Task"),
                Description = t.Description ?? (t.RequestItem != null ? t.RequestItem.Name : "General Task"),
                Department = t.Department,
                Priority = MapPriorityToFrontend(t.Priority),
                Status = MapStatusToFrontend(t.Status),
                AssignedToId = t.AssignedToId ?? t.CreatedBy,
                AssignedTo = t.CreatedByUser != null ? new
                {
                    t.CreatedByUser.Id,
                    UserName = t.CreatedByUser.UserName,
                    Email = t.CreatedByUser.Email
                } : null,
                t.RoomNumber,
                GuestName = (string?)null,
                GuestPhone = t.Conversation != null ? t.Conversation.WaUserPhone : null,
                t.Quantity,
                EstimatedCompletionTime = (DateTime?)null,
                t.CompletedAt,
                CompletedBy = (int?)null,
                t.Notes,
                t.CreatedAt,
                UpdatedAt = (DateTime?)null,
                RequestItem = t.RequestItem != null ? new
                {
                    t.RequestItem.Id,
                    t.RequestItem.TenantId,
                    t.RequestItem.Name,
                    t.RequestItem.Category,
                    Department = t.Department,
                    t.RequestItem.LlmVisibleName,
                    EstimatedTime = (int?)null,
                    RequiresQuantity = t.RequestItem.RequiresQuantity,
                    DefaultQuantityLimit = t.RequestItem.DefaultQuantityLimit,
                    NotesForStaff = t.RequestItem.NotesForStaff,
                    IsActive = t.RequestItem.IsAvailable,
                    IsUrgent = false,
                    DisplayOrder = 0
                } : null,
                Conversation = t.Conversation != null ? new
                {
                    t.Conversation.Id,
                    t.Conversation.WaUserPhone
                } : null
            })
            .FirstOrDefaultAsync();

        if (task == null)
        {
            return NotFound();
        }

        return Ok(task);
    }

    [HttpPost]
    public async Task<ActionResult<object>> CreateTask([FromBody] CreateTaskRequest request)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        // Look up guest phone if not provided but room number is available
        string? guestPhone = request.GuestPhone;
        if (string.IsNullOrEmpty(guestPhone) && !string.IsNullOrEmpty(request.RoomNumber))
        {
            var booking = await _context.Bookings
                .Where(b => b.TenantId == tenantId && b.RoomNumber == request.RoomNumber)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();
            guestPhone = booking?.Phone;
        }

        // Find related conversation if we have phone number
        int? conversationId = null;
        if (!string.IsNullOrEmpty(guestPhone))
        {
            var conversation = await _context.Conversations
                .Where(c => c.TenantId == tenantId && c.WaUserPhone == guestPhone)
                .FirstOrDefaultAsync();
            conversationId = conversation?.Id;
        }

        var task = new StaffTask
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            Title = request.Title,
            Description = request.Description,
            TaskType = MapDepartmentToTaskType(request.Department),
            Department = request.Department,
            Status = "Open",
            Priority = MapPriorityToBackend(request.Priority),
            RoomNumber = request.RoomNumber,
            GuestName = request.GuestName,
            GuestPhone = guestPhone,
            EstimatedCompletionTime = request.EstimatedCompletionTime,
            AssignedToId = request.AssignedToId,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Quantity = 1
        };

        _context.StaffTasks.Add(task);
        await _context.SaveChangesAsync();

        // Send SignalR notification for task creation
        try
        {
            await _notificationService.NotifyTaskCreatedAsync(tenantId, task);
            _logger.LogInformation("SignalR task creation notification sent for task {TaskId}", task.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR task creation notification for task {TaskId}", task.Id);
        }

        // Send push notification to assigned agent
        if (task.AssignedToId.HasValue)
        {
            try
            {
                await _pushNotificationService.NotifyTaskAssigned(
                    agentId: task.AssignedToId.Value,
                    taskId: task.Id,
                    taskTitle: task.Title ?? "New Task",
                    taskDescription: task.Description ?? ""
                );
                _logger.LogInformation("Push notification sent to agent {AgentId} for task {TaskId}", task.AssignedToId.Value, task.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification for task {TaskId}", task.Id);
            }
        }

        // Send WhatsApp notification to guest if we have their phone number
        if (!string.IsNullOrEmpty(guestPhone))
        {
            await SendTaskCreationNotificationAsync(task);

            // Update conversation timestamp to make it appear in Active Conversations
            if (conversationId.HasValue)
            {
                await UpdateConversationTimestampAsync(conversationId.Value);
            }
        }

        // Return the created task in the same format as GetTask
        return await GetTask(task.Id);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<object>> UpdateTask(int id, [FromBody] UpdateTaskRequest request)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        var task = await _context.StaffTasks
            .Include(t => t.RequestItem)
            .Include(t => t.Conversation)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        if (task == null)
        {
            return NotFound();
        }

        if (request.Status != null)
        {
            var previousStatus = task.Status;
            task.Status = MapStatusToBackend(request.Status);
            if (request.Status == "Completed")
            {
                task.CompletedAt = DateTime.UtcNow;

                // Send rating request after task completion
                if (previousStatus != "Completed" && task.ConversationId.HasValue)
                {
                    try
                    {
                        await _ratingService.SendRatingRequestAsync(tenantId, task.ConversationId.Value, task.Id);
                        _logger.LogInformation("Rating request sent for task {TaskId}", task.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send rating request for task {TaskId}", task.Id);
                        // Don't fail the task update if rating request fails
                    }
                }

                // Send push notification to guest when task is completed
                if (previousStatus != "Completed")
                {
                    // Get guest identifier: phone from task/conversation, or fall back to room number
                    // PushNotificationService.SendToGuest supports lookup by both phone AND room number
                    var guestIdentifier = task.GuestPhone ?? task.Conversation?.WaUserPhone ?? task.RoomNumber;
                    if (!string.IsNullOrEmpty(guestIdentifier))
                    {
                        try
                        {
                            var serviceName = task.RequestItem?.Name ?? task.Title ?? "Your request";
                            await _pushNotificationService.NotifyGuestServiceUpdate(
                                tenantId,
                                guestIdentifier,
                                serviceName,
                                "completed",
                                $"Your {serviceName} request has been completed!"
                            );
                            _logger.LogInformation("Push notification sent to guest {Identifier} for completed task {TaskId}", guestIdentifier, task.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send push notification for task {TaskId}", task.Id);
                            // Don't fail the task update if push notification fails
                        }
                    }
                }
            }
            // Send push notification when task is picked up (In Progress)
            else if (request.Status == "InProgress" && previousStatus != "InProgress" && previousStatus != "INPROGRESS")
            {
                // Get guest identifier: phone from task/conversation, or fall back to room number
                var guestIdentifier = task.GuestPhone ?? task.Conversation?.WaUserPhone ?? task.RoomNumber;
                if (!string.IsNullOrEmpty(guestIdentifier))
                {
                    try
                    {
                        var serviceName = task.RequestItem?.Name ?? task.Title ?? "Your request";
                        await _pushNotificationService.NotifyGuestServiceUpdate(
                            tenantId,
                            guestIdentifier,
                            serviceName,
                            "in progress",
                            $"Good news! Your {serviceName} request is now being handled."
                        );
                        _logger.LogInformation("Push notification sent to guest {Identifier} for in-progress task {TaskId}", guestIdentifier, task.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send push notification for task {TaskId}", task.Id);
                        // Don't fail the task update if push notification fails
                    }
                }
            }
        }

        if (request.Priority != null)
        {
            task.Priority = MapPriorityToBackend(request.Priority);
        }

        if (request.AssignedToId.HasValue)
        {
            // Validate that the user exists before assigning
            var userExists = await _context.Users.AnyAsync(u => u.Id == request.AssignedToId.Value);
            if (userExists)
            {
                task.AssignedToId = request.AssignedToId.Value;
            }
            else
            {
                _logger.LogWarning("Attempted to assign task to non-existent user ID: {UserId}", request.AssignedToId.Value);
                // Don't assign to invalid user ID, keep existing assignment
            }
        }

        if (request.Notes != null)
        {
            task.Notes = request.Notes;
        }

        if (request.EstimatedCompletionTime.HasValue)
        {
            task.EstimatedCompletionTime = request.EstimatedCompletionTime.Value;
        }

        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetTask(id);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteTask(int id)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        var task = await _context.StaffTasks
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        if (task == null)
        {
            return NotFound();
        }

        _context.StaffTasks.Remove(task);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("bulk")]
    public async Task<ActionResult> BulkAction([FromBody] TaskBulkActionRequest request)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        var tasks = await _context.StaffTasks
            .Where(t => request.TaskIds.Contains(t.Id) && t.TenantId == tenantId)
            .ToListAsync();

        switch (request.Action)
        {
            case "assign":
                if (request.AssignedToId.HasValue)
                {
                    // Validate that the user exists before assigning
                    var userExists = await _context.Users.AnyAsync(u => u.Id == request.AssignedToId.Value);
                    if (userExists)
                    {
                        foreach (var task in tasks)
                        {
                            task.AssignedToId = request.AssignedToId.Value;
                            task.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Attempted to bulk assign tasks to non-existent user ID: {UserId}", request.AssignedToId.Value);
                        // Don't assign to invalid user ID, keep existing assignments
                    }
                }
                break;

            case "updateStatus":
                if (request.Status != null)
                {
                    var backendStatus = MapStatusToBackend(request.Status);
                    foreach (var task in tasks)
                    {
                        task.Status = backendStatus;
                        task.UpdatedAt = DateTime.UtcNow;
                        if (request.Status == "Completed")
                        {
                            task.CompletedAt = DateTime.UtcNow;
                        }
                    }
                }
                break;

            case "delete":
                _context.StaffTasks.RemoveRange(tasks);
                break;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    private static string GetDepartmentFromTaskType(string taskType)
    {
        return taskType.ToLower() switch
        {
            "deliver_item" => "Housekeeping",
            "collect_item" => "Housekeeping",
            "maintenance" => "Maintenance",
            "electrical" => "Maintenance",
            "plumbing" => "Maintenance",
            "hvac" => "Maintenance",
            "mechanical" => "Maintenance",
            "structural" => "Maintenance",
            "safety" => "Maintenance",
            _ => "General"
        };
    }

    private static string GetDepartmentFromCategory(string category)
    {
        return category.ToLower() switch
        {
            "housekeeping" => "Housekeeping",
            "maintenance" => "Maintenance",
            "frontdesk" => "FrontDesk",
            "concierge" => "Concierge",
            "food" => "FoodService",
            _ => "General"
        };
    }

    private static string MapDepartmentToTaskType(string department)
    {
        return department.ToLower() switch
        {
            "housekeeping" => "deliver_item",
            "maintenance" => "maintenance",
            "frontdesk" => "frontdesk",
            "concierge" => "concierge",
            "foodservice" => "food",
            _ => "general"
        };
    }

    private static string MapPriorityToFrontend(string priority)
    {
        return priority switch
        {
            "Low" => "Low",
            "Normal" => "Medium",
            "High" => "High",
            "Urgent" => "Urgent",
            _ => "Medium"
        };
    }

    private static string MapPriorityToBackend(string priority)
    {
        return priority switch
        {
            "Low" => "Low",
            "Medium" => "Normal",
            "High" => "High",
            "Urgent" => "Urgent",
            _ => "Normal"
        };
    }

    private static string MapStatusToFrontend(string status)
    {
        return status switch
        {
            "Open" => "Pending",
            "InProgress" => "InProgress",
            "Completed" => "Completed",
            "Cancelled" => "Cancelled",
            _ => "Pending"
        };
    }

    private static string MapStatusToBackend(string status)
    {
        return status switch
        {
            "Pending" => "Open",
            "InProgress" => "InProgress",
            "Completed" => "Completed",
            "Cancelled" => "Cancelled",
            "OnHold" => "Open", // Map OnHold to Open for now
            _ => "Open"
        };
    }

    private async Task SendTaskCreationNotificationAsync(StaffTask task)
    {
        try
        {
            if (string.IsNullOrEmpty(task.GuestPhone))
                return;

            string message;

            if (!string.IsNullOrEmpty(task.RoomNumber) && !string.IsNullOrEmpty(task.GuestName))
            {
                message = $"Hi {task.GuestName}, we've received your request for '{task.Title}'. Our {task.Department} team will deliver this to Room {task.RoomNumber} shortly. Task ID: #{task.Id}";
            }
            else if (!string.IsNullOrEmpty(task.GuestName))
            {
                message = $"Hi {task.GuestName}, we've received your request for '{task.Title}'. Our {task.Department} team will handle this shortly. Task ID: #{task.Id}";
            }
            else
            {
                message = $"We've received your request for '{task.Title}'. Our {task.Department} team will handle this shortly. Task ID: #{task.Id}";
            }

            await _whatsAppService.SendTextMessageAsync(task.TenantId, task.GuestPhone, message);

            _logger.LogInformation("Task creation notification sent to {Phone} for task {TaskId}", task.GuestPhone, task.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send task creation notification for task {TaskId}", task.Id);
        }
    }

    private async Task UpdateConversationTimestampAsync(int conversationId)
    {
        try
        {
            var conversation = await _context.Conversations.FindAsync(conversationId);
            if (conversation != null)
            {
                conversation.LastBotReplyAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated conversation {ConversationId} timestamp for task activity", conversationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update conversation timestamp for conversation {ConversationId}", conversationId);
        }
    }
}

public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public string? RoomNumber { get; set; }
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }
    public int? AssignedToId { get; set; }
    public DateTime? EstimatedCompletionTime { get; set; }
    public string? Notes { get; set; }
}

public class UpdateTaskRequest
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public int? AssignedToId { get; set; }
    public string? Notes { get; set; }
    public DateTime? EstimatedCompletionTime { get; set; }
}

public class TaskBulkActionRequest
{
    public int[] TaskIds { get; set; } = Array.Empty<int>();
    public string Action { get; set; } = string.Empty; // assign, updateStatus, delete
    public int? AssignedToId { get; set; }
    public string? Status { get; set; }
}