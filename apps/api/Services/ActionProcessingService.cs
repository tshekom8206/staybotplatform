using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface IActionProcessingService
{
    Task ProcessActionsAsync(TenantContext tenantContext, Conversation conversation, MessageRoutingResponse routingResponse);
}

public class ActionProcessingService : IActionProcessingService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<ActionProcessingService> _logger;
    private readonly IMessageRoutingService _messageRoutingService;
    private readonly ITenantDepartmentService _tenantDepartmentService;
    private readonly INotificationService _notificationService;
    private readonly IDataValidationService _dataValidationService;
    private readonly IUpsellRecommendationService _upsellService;

    public ActionProcessingService(
        HostrDbContext context,
        ILogger<ActionProcessingService> logger,
        IMessageRoutingService messageRoutingService,
        ITenantDepartmentService tenantDepartmentService,
        INotificationService notificationService,
        IDataValidationService dataValidationService,
        IUpsellRecommendationService upsellService)
    {
        _context = context;
        _logger = logger;
        _messageRoutingService = messageRoutingService;
        _tenantDepartmentService = tenantDepartmentService;
        _notificationService = notificationService;
        _dataValidationService = dataValidationService;
        _upsellService = upsellService;
    }

    public async Task ProcessActionsAsync(TenantContext tenantContext, Conversation conversation, MessageRoutingResponse routingResponse)
    {
        try
        {
            var actionsToProcess = new List<JsonElement>();

            // Collect single action if present
            if (routingResponse.Action.HasValue)
            {
                actionsToProcess.Add(routingResponse.Action.Value);
            }

            // Collect multiple actions if present
            if (routingResponse.Actions != null && routingResponse.Actions.Any())
            {
                actionsToProcess.AddRange(routingResponse.Actions);
            }

            // Process all actions (avoiding duplicates by using a HashSet based on JSON string representation)
            var processedActionStrings = new HashSet<string>();

            foreach (var action in actionsToProcess)
            {
                var actionString = action.GetRawText();
                if (processedActionStrings.Add(actionString))
                {
                    await ProcessActionAsync(tenantContext, conversation, action);
                }
                else
                {
                    _logger.LogWarning("Skipping duplicate action for conversation {ConversationId}: {Action}",
                        conversation.Id, actionString);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing actions for conversation {ConversationId}", conversation.Id);
        }
    }

    private async Task ProcessActionAsync(TenantContext tenantContext, Conversation conversation, JsonElement action)
    {
        try
        {
            if (!action.TryGetProperty("type", out var typeElement))
            {
                _logger.LogWarning("Action missing 'type' property: {Action}", action.ToString());
                return;
            }

            var actionType = typeElement.GetString();
            _logger.LogInformation("Processing action type: {ActionType} for conversation {ConversationId}",
                actionType, conversation.Id);

            switch (actionType)
            {
                case "create_task":
                    await ProcessCreateTaskActionAsync(tenantContext, conversation, action);
                    break;
                case "create_food_order":
                    await ProcessCreateFoodOrderActionAsync(tenantContext, conversation, action);
                    break;
                case "create_complaint":
                    await ProcessCreateComplaintActionAsync(tenantContext, conversation, action);
                    break;
                default:
                    _logger.LogWarning("Unknown action type: {ActionType}", actionType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing individual action: {Action}", action.ToString());
        }
    }

    private async Task ProcessCreateTaskActionAsync(TenantContext tenantContext, Conversation conversation, JsonElement action)
    {
        try
        {
            // Extract action properties
            var itemSlug = action.TryGetProperty("item_slug", out var itemSlugElement)
                ? itemSlugElement.GetString() : "unknown";
            var quantity = action.TryGetProperty("quantity", out var quantityElement)
                ? quantityElement.GetInt32() : 1;
            var roomNumber = action.TryGetProperty("room_number", out var roomElement)
                ? roomElement.GetString() : null;

            // Get guest status for room number and guest name
            var guestStatus = await _messageRoutingService.DetermineGuestStatusAsync(conversation.WaUserPhone, tenantContext.TenantId);

            // Use room number from action if provided, otherwise from guest status
            if (string.IsNullOrEmpty(roomNumber))
            {
                roomNumber = guestStatus.RoomNumber;
                _logger.LogInformation("Room number not in action, using guest status room: {RoomNumber} for phone {Phone}",
                    roomNumber, conversation.WaUserPhone);
            }

            // Always get guest name from guest status
            var guestName = guestStatus.DisplayName;

            // Find the request item
            var searchTerm = itemSlug?.ToLower() ?? "";
            var requestItem = await _context.RequestItems
                .FirstOrDefaultAsync(r => r.TenantId == tenantContext.TenantId &&
                                         (r.LlmVisibleName.ToLower().Contains(searchTerm) ||
                                          r.Name.ToLower().Contains(searchTerm)));

            // Determine service category for department resolution
            string serviceCategory;
            bool requiresRoomDelivery = false;

            if (requestItem != null)
            {
                serviceCategory = requestItem.Category;
                // Check if this is typically a room delivery item
                requiresRoomDelivery = IsRoomDeliveryItem(requestItem.Name, requestItem.Category);
            }
            else
            {
                // If no specific item found, analyze the item slug to determine category
                serviceCategory = DetermineServiceCategory(itemSlug ?? "");
                requiresRoomDelivery = IsRoomDeliveryItem(itemSlug ?? "", serviceCategory);

                // Resolve department BEFORE creating the RequestItem to avoid constraint violations
                var itemName = itemSlug?.Replace("_", " ") ?? "Guest Request";
                var resolvedDepartment = await _tenantDepartmentService.ResolveDepartmentAsync(
                    tenantContext.TenantId,
                    serviceCategory,
                    itemName,
                    requiresRoomDelivery);

                // CRITICAL DEBUG: Log the department being used for RequestItem creation
                _logger.LogError("DEBUG - Creating RequestItem with Department: '{Department}', ServiceCategory: '{ServiceCategory}', ItemName: '{ItemName}', TenantId: {TenantId}",
                    resolvedDepartment, serviceCategory, itemName, tenantContext.TenantId);

                // Validate department value before creation
                if (string.IsNullOrWhiteSpace(resolvedDepartment))
                {
                    resolvedDepartment = "FrontDesk"; // Safe fallback
                    _logger.LogWarning("Empty department resolved, using fallback: FrontDesk");
                }

                // Create a generic request item with intelligent categorization and resolved department
                requestItem = new RequestItem
                {
                    TenantId = tenantContext.TenantId,
                    Name = itemName,
                    Category = serviceCategory,
                    Department = resolvedDepartment,
                    LlmVisibleName = itemName,
                    IsAvailable = true,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.RequestItems.Add(requestItem);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created new request item: {ItemName} with category {Category} and department {Department} for tenant {TenantId}",
                    requestItem.Name, serviceCategory, resolvedDepartment, tenantContext.TenantId);
            }

            // Use the already resolved department for new items, or resolve for existing items
            var targetDepartment = requestItem.Department;
            _logger.LogInformation("Initial targetDepartment from RequestItem: '{Department}'", targetDepartment);

            if (string.IsNullOrEmpty(targetDepartment))
            {
                // For existing RequestItems that may not have a department resolved yet
                _logger.LogWarning("RequestItem department is null/empty, resolving department for service category: {Category}, item: {Item}",
                    serviceCategory, requestItem.Name);
                targetDepartment = await _tenantDepartmentService.ResolveDepartmentAsync(
                    tenantContext.TenantId,
                    serviceCategory,
                    requestItem.Name,
                    requiresRoomDelivery);
                _logger.LogInformation("Resolved targetDepartment: '{Department}'", targetDepartment);
            }

            // Ensure targetDepartment is valid - fallback to safe default if needed
            if (string.IsNullOrEmpty(targetDepartment))
            {
                targetDepartment = "FrontDesk"; // Safe fallback
                _logger.LogWarning("targetDepartment was still null/empty, using fallback: FrontDesk");
            }

            // Enhanced room number validation
            if (requiresRoomDelivery)
            {
                var isValidForRoomDelivery = await _tenantDepartmentService.ValidateRoomDeliveryAsync(
                    tenantContext.TenantId, targetDepartment, guestStatus);

                if (!isValidForRoomDelivery)
                {
                    _logger.LogWarning("Room delivery validation failed for {ItemName}. Guest: {GuestType}, Room: {RoomNumber}",
                        requestItem.Name, guestStatus.Type, guestStatus.RoomNumber);

                    // For room delivery items where guest is not properly checked in,
                    // route to FrontDesk for guest to collect or check-in first
                    targetDepartment = "FrontDesk";
                    roomNumber = "Front Desk Collection"; // Clear indication this is not room delivery
                }
            }

            // Validate room number for delivery tasks
            if (string.IsNullOrEmpty(roomNumber) || roomNumber == "Unknown")
            {
                if (requiresRoomDelivery && guestStatus.Type == GuestType.Active)
                {
                    roomNumber = guestStatus.RoomNumber ?? "Unknown";
                }
                else if (requiresRoomDelivery)
                {
                    roomNumber = "Front Desk Collection";
                }
                else
                {
                    roomNumber = "N/A"; // Not applicable for non-delivery services
                }
            }

            // Create the staff task with enhanced validation
            _logger.LogInformation("Creating StaffTask with targetDepartment: '{Department}' for tenant {TenantId}",
                targetDepartment, tenantContext.TenantId);

            // CRITICAL DEBUG: Log all critical values
            _logger.LogError("DEBUG - About to create StaffTask with Department: '{Department}', ServiceCategory: '{ServiceCategory}', ItemName: '{ItemName}'",
                targetDepartment, serviceCategory, requestItem.Name);

            var task = new StaffTask
            {
                TenantId = tenantContext.TenantId,
                ConversationId = conversation.Id,
                RequestItemId = requestItem.Id,
                Title = $"{quantity}x {requestItem.Name}",
                TaskType = "deliver_item",
                Department = targetDepartment,
                Quantity = quantity,
                Priority = _dataValidationService.StandardizeTaskPriority(DeterminePriority(serviceCategory, guestStatus)),
                Status = _dataValidationService.StandardizeTaskStatus("Open"),
                RoomNumber = roomNumber,
                GuestName = guestName,
                GuestPhone = conversation.WaUserPhone,
                Notes = $"Guest requested: {quantity}x {requestItem.Name}" +
                       (requiresRoomDelivery && roomNumber == "Front Desk Collection"
                           ? " - Front desk collection (guest not checked in)"
                           : ""),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            // Send SignalR notification for task creation
            try
            {
                await _notificationService.NotifyTaskCreatedAsync(tenantContext.TenantId, task, requestItem.Name);
                _logger.LogInformation("SignalR notification sent for task {TaskId}", task.Id);
            }
            catch (Exception notificationEx)
            {
                _logger.LogError(notificationEx, "Failed to send SignalR notification for task {TaskId}", task.Id);
            }

            // Track upsell metrics for chargeable services
            try
            {
                var service = await _context.Services
                    .FirstOrDefaultAsync(s => s.TenantId == tenantContext.TenantId
                        && s.Name == requestItem.Name
                        && s.IsChargeable == true
                        && s.IsAvailable == true);

                if (service != null)
                {
                    // Log the upsell suggestion (guest requested it)
                    await _upsellService.LogUpsellSuggestionAsync(
                        tenantContext.TenantId,
                        conversation.Id,
                        service.Id,
                        "direct_request",
                        null);

                    // Immediately mark as accepted (task was created)
                    await _upsellService.MarkUpsellAcceptedAsync(
                        conversation.Id,
                        service.Id,
                        service.Price ?? 0);

                    _logger.LogInformation("Tracked upsell metric for {ServiceName} (R{Price}) in conversation {ConversationId}",
                        service.Name, service.Price, conversation.Id);
                }
            }
            catch (Exception upsellEx)
            {
                _logger.LogError(upsellEx, "Failed to track upsell metric for task {TaskId}", task.Id);
            }

            _logger.LogInformation("Created task {TaskId} for {ItemName} (quantity: {Quantity}) in room {RoomNumber}",
                task.Id, requestItem.Name, quantity, roomNumber ?? "Unknown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task from action: {Action}", action.ToString());
        }
    }

    private async Task ProcessCreateFoodOrderActionAsync(TenantContext tenantContext, Conversation conversation, JsonElement action)
    {
        try
        {
            var menuItem = action.TryGetProperty("menu_item", out var menuItemElement)
                ? menuItemElement.GetString() : "Food Order";
            var quantity = action.TryGetProperty("quantity", out var quantityElement)
                ? quantityElement.GetInt32() : 1;
            var roomNumber = action.TryGetProperty("room_number", out var roomElement)
                ? roomElement.GetString() : null;

            // Find or create food & beverage request item
            var requestItem = await _context.RequestItems
                .FirstOrDefaultAsync(r => r.TenantId == tenantContext.TenantId &&
                                         r.Category == "Food & Beverage" &&
                                         r.Name.ToLower().Contains("room service"));

            if (requestItem == null)
            {
                requestItem = new RequestItem
                {
                    TenantId = tenantContext.TenantId,
                    Name = "Room Service",
                    Category = "Food & Beverage",
                    Department = "FoodService",
                    LlmVisibleName = "Room Service",
                    IsAvailable = true,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.RequestItems.Add(requestItem);
                await _context.SaveChangesAsync();
            }

            var task = new StaffTask
            {
                TenantId = tenantContext.TenantId,
                ConversationId = conversation.Id,
                RequestItemId = requestItem.Id,
                Title = $"Food Order: {quantity}x {menuItem}",
                TaskType = "deliver_item",
                Department = "FoodService",
                Quantity = quantity,
                Priority = _dataValidationService.StandardizeTaskPriority("Normal"),
                Status = _dataValidationService.StandardizeTaskStatus("Open"),
                RoomNumber = roomNumber ?? "Unknown",
                Notes = $"Food Order: {quantity}x {menuItem}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            // Send SignalR notification for task creation
            try
            {
                await _notificationService.NotifyTaskCreatedAsync(tenantContext.TenantId, task);
                _logger.LogInformation("SignalR notification sent for food order task {TaskId}", task.Id);
            }
            catch (Exception notificationEx)
            {
                _logger.LogError(notificationEx, "Failed to send SignalR notification for food order task {TaskId}", task.Id);
            }

            _logger.LogInformation("Created food order task {TaskId} for {MenuItem} (quantity: {Quantity})",
                task.Id, menuItem, quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating food order from action: {Action}", action.ToString());
        }
    }

    private async Task ProcessCreateComplaintActionAsync(TenantContext tenantContext, Conversation conversation, JsonElement action)
    {
        try
        {
            var issue = action.TryGetProperty("issue", out var issueElement)
                ? issueElement.GetString() : "Guest Complaint";
            var priority = action.TryGetProperty("priority", out var priorityElement)
                ? priorityElement.GetString() : "Medium";
            var roomNumber = action.TryGetProperty("room_number", out var roomElement)
                ? roomElement.GetString() : null;

            // Find or create complaint request item
            var requestItem = await _context.RequestItems
                .FirstOrDefaultAsync(r => r.TenantId == tenantContext.TenantId &&
                                         r.Name.ToLower().Contains("complaint"));

            if (requestItem == null)
            {
                requestItem = new RequestItem
                {
                    TenantId = tenantContext.TenantId,
                    Name = "Guest Complaint",
                    Category = "Management",
                    Department = "FrontDesk",
                    LlmVisibleName = "Guest Complaint",
                    IsAvailable = true,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.RequestItems.Add(requestItem);
                await _context.SaveChangesAsync();
            }

            var task = new StaffTask
            {
                TenantId = tenantContext.TenantId,
                ConversationId = conversation.Id,
                RequestItemId = requestItem.Id,
                Title = $"Guest Complaint: {issue}",
                TaskType = "general",
                Department = "FrontDesk",
                Priority = _dataValidationService.StandardizeTaskPriority(priority ?? "Normal"),
                Status = _dataValidationService.StandardizeTaskStatus("Open"),
                RoomNumber = roomNumber ?? "Unknown",
                Notes = $"Complaint: {issue}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            // Send SignalR notification for task creation
            try
            {
                await _notificationService.NotifyTaskCreatedAsync(tenantContext.TenantId, task);
                _logger.LogInformation("SignalR notification sent for complaint task {TaskId}", task.Id);
            }
            catch (Exception notificationEx)
            {
                _logger.LogError(notificationEx, "Failed to send SignalR notification for complaint task {TaskId}", task.Id);
            }

            _logger.LogInformation("Created complaint task {TaskId} for issue: {Issue}", task.Id, issue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating complaint from action: {Action}", action.ToString());
        }
    }

    private static string DetermineServiceCategory(string itemName)
    {
        var itemLower = itemName.ToLower();

        // CRITICAL DEBUG: Log service category determination
        var result = DetermineServiceCategoryInternal(itemLower);
        Console.WriteLine($"DEBUG - DetermineServiceCategory('{itemName}') → '{result}'");
        return result;
    }

    private static string DetermineServiceCategoryInternal(string itemLower)
    {
        if (itemLower.Contains("tour") || itemLower.Contains("excursion") || itemLower.Contains("trip"))
            return "Local Tours";

        if (itemLower.Contains("transfer") || itemLower.Contains("transport") || itemLower.Contains("taxi"))
            return "Transportation";

        if (itemLower.Contains("charger") || itemLower.Contains("cable") || itemLower.Contains("adapter"))
            return "Electronics";

        if (itemLower.Contains("towel") || itemLower.Contains("amenity") || itemLower.Contains("toiletry"))
            return "Amenities";

        if (itemLower.Contains("food") || itemLower.Contains("meal") || itemLower.Contains("breakfast") ||
            itemLower.Contains("drink") || itemLower.Contains("water") || itemLower.Contains("beverage") ||
            itemLower.Contains("juice") || itemLower.Contains("coffee") || itemLower.Contains("tea") ||
            itemLower.Contains("soda") || itemLower.Contains("wine") || itemLower.Contains("beer") ||
            itemLower.Contains("cocktail") || itemLower.Contains("smoothie"))
            return "Food & Beverage";

        if (itemLower.Contains("laundry") || itemLower.Contains("cleaning"))
            return "Laundry";

        if (IsWordMatch(itemLower, "spa") || itemLower.Contains("massage") || itemLower.Contains("wellness"))
            return "Wellness";

        if (itemLower.Contains("maintenance") || itemLower.Contains("repair") || itemLower.Contains("fix"))
            return "Maintenance";

        // Default fallback
        return "Concierge";
    }

    private static bool IsWordMatch(string text, string word)
    {
        // Use word boundary matching to prevent substring matches
        // "spa" should match "spa services" but not "sparkling"
        var pattern = $@"\b{System.Text.RegularExpressions.Regex.Escape(word)}\b";
        return System.Text.RegularExpressions.Regex.IsMatch(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static bool IsRoomDeliveryItem(string itemName, string category)
    {
        var itemLower = itemName.ToLower();
        var categoryLower = category.ToLower();

        // Physical items that are typically delivered to rooms
        if (categoryLower.Contains("electronics") || categoryLower.Contains("amenities") ||
            categoryLower.Contains("laundry") || categoryLower.Contains("food") ||
            categoryLower.Contains("dining"))
        {
            return true;
        }

        // Specific items that require room delivery
        if (itemLower.Contains("towel") || itemLower.Contains("charger") || itemLower.Contains("amenity") ||
            itemLower.Contains("toiletry") || itemLower.Contains("pillow") || itemLower.Contains("blanket") ||
            itemLower.Contains("iron") || itemLower.Contains("hair dryer") || itemLower.Contains("food") ||
            itemLower.Contains("meal") || itemLower.Contains("drink") || itemLower.Contains("bottle"))
        {
            return true;
        }

        // Services that don't require room delivery
        if (itemLower.Contains("tour") || itemLower.Contains("transfer") || itemLower.Contains("information") ||
            itemLower.Contains("reservation") || itemLower.Contains("booking"))
        {
            return false;
        }

        // Default: assume physical requests require room delivery
        return !categoryLower.Contains("information") && !categoryLower.Contains("concierge");
    }

    private static string DeterminePriority(string serviceCategory, GuestStatus guestStatus)
    {
        var categoryLower = serviceCategory.ToLower();

        // High priority for revenue-generating services
        if (categoryLower.Contains("tour") || categoryLower.Contains("transportation"))
            return "High";

        // High priority for VIP guests
        if (guestStatus.Type == GuestType.VipMember)
            return "High";

        // Medium priority for food services (time-sensitive)
        if (categoryLower.Contains("food") || categoryLower.Contains("dining"))
            return "Medium";

        // Urgent for maintenance and safety issues
        if (categoryLower.Contains("maintenance") || categoryLower.Contains("emergency"))
            return "Urgent";

        // Default priority
        return "Normal";
    }
}