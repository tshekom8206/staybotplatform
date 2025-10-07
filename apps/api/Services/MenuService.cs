using Hostr.Api.Data;
using Hostr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Hostr.Api.Services;

public interface IMenuService
{
    Task<string> QueryMenuAsync(TenantContext tenantContext, string query, DateTime currentTime);
    Task<string> QueryCondensedMenuAsync(TenantContext tenantContext, DateTime currentTime);
    Task<string> QueryCategoryMenuAsync(TenantContext tenantContext, string categoryName, DateTime currentTime);
    Task SeedMockDataAsync(int tenantId);
    Task<List<MenuItem>> GetMenuItemsAsync(string mealType, bool includeSpecials = true);
    Task<List<MenuSpecial>> GetActiveSpecialsAsync(DateTime currentTime, string mealType = "all");
    Task<BusinessInfo?> GetBusinessInfoAsync(string category);
}

public class MenuService : IMenuService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<MenuService> _logger;
    
    public MenuService(HostrDbContext context, ILogger<MenuService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> QueryMenuAsync(TenantContext tenantContext, string query, DateTime currentTime)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);
            
            var queryLower = query.ToLower();
            var hour = currentTime.Hour;
            var dayOfWeek = (int)currentTime.DayOfWeek;
            
            string mealType = DetermineMealType(hour);
            
            if (IsMenuQuery(queryLower))
            {
                return await BuildMenuResponse(mealType, currentTime);
            }
            
            if (IsSpecialQuery(queryLower))
            {
                return await BuildSpecialsResponse(currentTime, mealType);
            }
            
            if (IsTimeSpecificQuery(queryLower))
            {
                return await BuildTimeSpecificResponse(queryLower, currentTime);
            }
            
            if (IsBusinessInfoQuery(queryLower))
            {
                return await BuildBusinessInfoResponse(queryLower, tenantContext.TenantId);
            }
            
            return await SearchMenuItems(queryLower, mealType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying menu for tenant {TenantId}", tenantContext.TenantId);
            return "I'm sorry, I'm having trouble accessing the menu right now. Please try again later.";
        }
    }

    public async Task SeedMockDataAsync(int tenantId)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        // Check if menu data already exists (categories, items, or specials)
        var hasCategories = await _context.MenuCategories.AnyAsync();
        var hasMenuItems = await _context.MenuItems.AnyAsync();
        var hasSpecials = await _context.MenuSpecials.AnyAsync();
        var hasBusinessInfo = await _context.BusinessInfo.AnyAsync();
        
        if (hasCategories || hasMenuItems || hasSpecials)
        {
            _logger.LogInformation("Menu data already exists for tenant {TenantId}", tenantId);
            return;
        }

        // Only seed essential business info if missing (WiFi, hours, etc.)
        if (!hasBusinessInfo)
        {
            var businessInfo = new[]
            {
                new BusinessInfo { TenantId = tenantId, Category = "hours", Title = "Opening Hours", Content = "Monday-Friday: 7:00 AM - 10:00 PM\nSaturday-Sunday: 8:00 AM - 11:00 PM\nBreakfast: 7:00 AM - 11:00 AM\nLunch: 12:00 PM - 3:00 PM\nDinner: 6:00 PM - 10:00 PM", Tags = new[] { "hours", "schedule" } },
                new BusinessInfo { TenantId = tenantId, Category = "location", Title = "Location", Content = "Please contact our front desk for location details and directions to our hotel. We're easily accessible by car or public transport.", Tags = new[] { "location", "directions" } },
                new BusinessInfo { TenantId = tenantId, Category = "amenities", Title = "Amenities", Content = "Free WiFi, outdoor seating, wheelchair accessible, child-friendly, pet-friendly patio", Tags = new[] { "wifi", "accessibility", "family" } }
            };

            _context.BusinessInfo.AddRange(businessInfo);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Business info seeded successfully for tenant {TenantId}", tenantId);
        }

        _logger.LogInformation("Menu system uses database-driven content for tenant {TenantId}", tenantId);
    }

    public async Task<List<MenuItem>> GetMenuItemsAsync(string mealType, bool includeSpecials = true)
    {
        var query = _context.MenuItems
            .Include(m => m.MenuCategory)
            .Where(m => m.IsAvailable && 
                       (m.MealType == mealType || m.MealType == "all"));
                       
        if (includeSpecials)
        {
            query = query.Where(m => m.IsSpecial || !m.IsSpecial);
        }
        
        return await query.OrderBy(m => m.MenuCategory.DisplayOrder)
                         .ThenBy(m => m.Name)
                         .ToListAsync();
    }

    public async Task<List<MenuSpecial>> GetActiveSpecialsAsync(DateTime currentTime, string mealType = "all")
    {
        var currentDayOfWeek = (int)currentTime.DayOfWeek;
        var currentDate = DateOnly.FromDateTime(currentTime);
        
        return await _context.MenuSpecials
            .Where(s => s.IsActive &&
                       (s.MealType == mealType || s.MealType == "all") &&
                       (s.DayOfWeek == null || s.DayOfWeek == currentDayOfWeek) &&
                       (s.ValidFrom == null || s.ValidFrom <= currentDate) &&
                       (s.ValidTo == null || s.ValidTo >= currentDate))
            .ToListAsync();
    }

    public async Task<BusinessInfo?> GetBusinessInfoAsync(string category)
    {
        return await _context.BusinessInfo
            .Where(b => b.IsActive && b.Category == category)
            .FirstOrDefaultAsync();
    }

    private string DetermineMealType(int hour)
    {
        return hour switch
        {
            >= 6 and < 11 => "breakfast",
            >= 11 and < 16 => "lunch", 
            >= 16 and < 23 => "dinner",
            _ => "all"
        };
    }

    private bool IsMenuQuery(string query)
    {
        var menuKeywords = new[] { "menu", "food", "eat", "meal", "dish", "what can i order", "what do you have", "dinner", "lunch", "breakfast" };
        return menuKeywords.Any(keyword => query.Contains(keyword));
    }

    private bool IsSpecialQuery(string query)
    {
        var specialKeywords = new[] { "special", "deal", "offer", "promotion", "discount" };
        return specialKeywords.Any(keyword => query.Contains(keyword));
    }

    private bool IsTimeSpecificQuery(string query)
    {
        var timeKeywords = new[] { "breakfast", "lunch", "dinner", "brunch", "morning", "evening" };
        return timeKeywords.Any(keyword => query.Contains(keyword));
    }

    private bool IsBusinessInfoQuery(string query)
    {
        var infoKeywords = new[] { "hours", "open", "close", "location", "where", "wifi", "parking" };
        return infoKeywords.Any(keyword => query.Contains(keyword));
    }

    private async Task<string> BuildMenuResponse(string mealType, DateTime currentTime)
    {
        var menuItems = await GetMenuItemsAsync(mealType);
        var specials = await GetActiveSpecialsAsync(currentTime, mealType);

        var response = $"Here's our {mealType} menu:\n\n";
        
        var categories = menuItems.GroupBy(m => m.MenuCategory.Name).OrderBy(g => menuItems.First(m => m.MenuCategory.Name == g.Key).MenuCategory.DisplayOrder);
        
        foreach (var category in categories)
        {
            response += $"**{category.Key}**\n";
            foreach (var item in category.OrderBy(i => i.Name))
            {
                response += $"• {item.Name} - R{item.PriceCents / 100:F2}\n";
                response += $"  {item.Description}\n";
                if (item.IsVegetarian) response += "  🌱 Vegetarian\n";
                if (item.IsVegan) response += "  🌿 Vegan\n";
                response += "\n";
            }
        }

        if (specials.Any())
        {
            response += "\n**Today's Specials**\n";
            foreach (var special in specials)
            {
                response += $"🌟 **{special.Title}**\n{special.Description}\n";
                if (special.SpecialPriceCents.HasValue)
                {
                    response += $"Special Price: R{special.SpecialPriceCents.Value / 100:F2}\n";
                }
                response += "\n";
            }
        }

        return response.Trim();
    }

    private async Task<string> BuildSpecialsResponse(DateTime currentTime, string mealType)
    {
        var specials = await GetActiveSpecialsAsync(currentTime, mealType);
        
        if (!specials.Any())
        {
            return "We don't have any special offers running right now, but our regular menu has lots of delicious options!";
        }

        var response = "Here are our current specials:\n\n";
        foreach (var special in specials)
        {
            response += $"🌟 **{special.Title}**\n";
            response += $"{special.Description}\n";
            if (special.SpecialPriceCents.HasValue)
            {
                response += $"Special Price: R{special.SpecialPriceCents.Value / 100:F2}\n";
            }
            response += "\n";
        }

        return response.Trim();
    }

    private async Task<string> BuildTimeSpecificResponse(string query, DateTime currentTime)
    {
        string specificMealType = "all";
        
        if (query.Contains("breakfast") || query.Contains("morning"))
            specificMealType = "breakfast";
        else if (query.Contains("lunch"))
            specificMealType = "lunch";
        else if (query.Contains("dinner") || query.Contains("evening"))
            specificMealType = "dinner";
        
        return await BuildMenuResponse(specificMealType, currentTime);
    }

    private async Task<string> BuildBusinessInfoResponse(string query, int tenantId)
    {
        if (query.Contains("hours") || query.Contains("open") || query.Contains("close"))
        {
            var hoursInfo = await GetBusinessInfoAsync("hours");
            return hoursInfo?.Content ?? "We're open daily! Please call us for specific hours.";
        }
        
        if (query.Contains("location") || query.Contains("where"))
        {
            var locationInfo = await GetBusinessInfoAsync("location");
            return locationInfo?.Content ?? "Please contact us for our location details.";
        }
        
        // Handle WiFi-specific queries with tenant-specific credentials
        if (query.Contains("wifi") && (query.Contains("password") || query.Contains("login") || query.Contains("connect") || query.Contains("access")))
        {
            var wifiInfo = await GetTenantWiFiCredentials(tenantId);
            
            if (wifiInfo.HasCredentials)
            {
                return $"Here are the WiFi details for your stay:\n\n" +
                       $"📶 **Network Name:** {wifiInfo.NetworkName}\n" +
                       $"🔐 **Password:** {wifiInfo.Password}\n\n" +
                       $"Simply connect to the network and enter the password. If you experience any connection issues, please let me know and I can send someone to assist you.";
            }
            else
            {
                return "I'll get the current WiFi details for you right away! Let me connect you with our front desk who can provide the network name and password for your stay.";
            }
        }
        
        // Handle general WiFi queries
        if (query.Contains("wifi"))
        {
            var wifiInfo = await GetTenantWiFiCredentials(tenantId);
            
            if (wifiInfo.HasCredentials)
            {
                return $"We offer complimentary high-speed WiFi throughout the property.\n\n" +
                       $"📶 **Network:** {wifiInfo.NetworkName}\n" +
                       $"🔐 **Password:** {wifiInfo.Password}\n\n" +
                       $"The network is available in all rooms, lobby, restaurant, and outdoor areas. Need help connecting?";
            }
            else
            {
                return "We offer complimentary high-speed WiFi throughout the property. The network is available in all rooms, lobby, restaurant, and outdoor areas. Let me get the current network details for you - I'll connect you with our front desk.";
            }
        }
        
        if (query.Contains("parking") || query.Contains("amenities"))
        {
            var amenitiesInfo = await GetBusinessInfoAsync("amenities");
            return amenitiesInfo?.Content ?? "We offer various amenities. Please ask our staff for details.";
        }
        
        return "I can help you with information about our hours, location, or amenities. What would you like to know?";
    }

    private async Task<string> SearchMenuItems(string query, string mealType)
    {
        var menuItems = await _context.MenuItems
            .Include(m => m.MenuCategory)
            .Where(m => m.IsAvailable && 
                       (m.MealType == mealType || m.MealType == "all") &&
                       (m.Name.ToLower().Contains(query) ||
                        m.Description.ToLower().Contains(query) ||
                        m.Tags.Any(tag => tag.ToLower().Contains(query))))
            .Take(5)
            .ToListAsync();

        if (!menuItems.Any())
        {
            return $"I couldn't find anything matching '{query}' on our menu. Would you like to see our full {mealType} menu instead?";
        }

        var response = $"Here's what I found for '{query}':\n\n";
        foreach (var item in menuItems)
        {
            response += $"**{item.Name}** - R{item.PriceCents / 100:F2}\n";
            response += $"{item.Description}\n";
            if (item.IsVegetarian) response += "🌱 Vegetarian ";
            if (item.IsVegan) response += "🌿 Vegan ";
            response += "\n\n";
        }

        return response.Trim();
    }

    private async Task<(bool HasCredentials, string NetworkName, string Password)> GetTenantWiFiCredentials(int tenantId)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantId);
            
            // Check if tenant has WiFi credentials configured in business info
            var wifiInfo = await _context.BusinessInfo
                .Where(b => b.TenantId == tenantId && b.Category == "wifi_credentials")
                .FirstOrDefaultAsync();

            if (wifiInfo != null && !string.IsNullOrEmpty(wifiInfo.Content))
            {
                // Parse WiFi credentials from structured content
                try
                {
                    var wifiData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(wifiInfo.Content);
                    if (wifiData != null && wifiData.ContainsKey("network") && wifiData.ContainsKey("password"))
                    {
                        return (true, wifiData["network"], wifiData["password"]);
                    }
                }
                catch
                {
                    // If JSON parsing fails, continue to no credentials
                }
            }

            // No WiFi credentials configured for this tenant
            return (false, "", "");
        }
        catch (Exception ex)
        {
            // Log error and return no credentials
            return (false, "", "");
        }
    }

    public async Task<string> QueryCondensedMenuAsync(TenantContext tenantContext, DateTime currentTime)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);

            var hour = currentTime.Hour;
            string mealType = DetermineMealType(hour);

            return await BuildCondensedMenuResponse(currentTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying condensed menu for tenant {TenantId}", tenantContext.TenantId);
            return "I'm sorry, I'm having trouble accessing the menu right now. Please try again later.";
        }
    }

    public async Task<string> QueryCategoryMenuAsync(TenantContext tenantContext, string categoryName, DateTime currentTime)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);

            return await BuildCategoryMenuResponse(categoryName, currentTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying category menu for tenant {TenantId}, category {CategoryName}", tenantContext.TenantId, categoryName);
            return $"I'm sorry, I'm having trouble accessing the {categoryName} menu right now. Please try again later.";
        }
    }

    private async Task<string> BuildCondensedMenuResponse(DateTime currentTime)
    {
        var response = "🍽️ **Menu Overview**\n\n";
        var totalLength = response.Length;
        const int maxLength = 1400; // Safety buffer for WhatsApp limit

        // Get all meal types that have items
        var allMealTypes = new[] { "breakfast", "lunch", "dinner" };

        foreach (var mealType in allMealTypes)
        {
            var menuItems = await GetMenuItemsAsync(mealType);
            if (!menuItems.Any()) continue;

            var mealEmoji = mealType switch
            {
                "breakfast" => "🌅",
                "lunch" => "☀️",
                "dinner" => "🌃",
                _ => "🍽️"
            };

            var timeRange = mealType switch
            {
                "breakfast" => " (6-11am)",
                "lunch" => " (11:30am-4pm)",
                "dinner" => " (5-10pm)",
                _ => ""
            };

            var sectionHeader = $"{mealEmoji} **{char.ToUpper(mealType[0]) + mealType[1..]}**{timeRange}\n";

            if (totalLength + sectionHeader.Length > maxLength - 200) break; // Leave room for footer

            response += sectionHeader;
            totalLength += sectionHeader.Length;

            // Get 2-3 featured items per category (popular/highest rated)
            var categories = menuItems.GroupBy(m => m.MenuCategory.Name)
                .OrderBy(g => menuItems.First(m => m.MenuCategory.Name == g.Key).MenuCategory.DisplayOrder);

            int itemCount = 0;
            foreach (var category in categories)
            {
                if (itemCount >= 3) break; // Limit to 3 items per meal type

                var topItems = category.OrderBy(i => i.Name).Take(2);
                foreach (var item in topItems)
                {
                    var itemLine = $"• {item.Name} - R{item.PriceCents / 100:F2}\n";

                    if (totalLength + itemLine.Length > maxLength - 150) goto FinishResponse;

                    response += itemLine;
                    totalLength += itemLine.Length;
                    itemCount++;

                    if (itemCount >= 3) break;
                }
            }

            response += "\n";
            totalLength += 1;
        }

        FinishResponse:
        // Add footer with navigation instructions
        var footer = "💡 **Want more details?**\nReply with 'Breakfast', 'Lunch', or 'Dinner' to see complete menus!\nOr ask about specific dishes for full descriptions.";

        if (totalLength + footer.Length <= maxLength)
        {
            response += footer;
        }
        else
        {
            response += "Reply 'Breakfast', 'Lunch', or 'Dinner' for complete menus!";
        }

        return response.Trim();
    }

    private async Task<string> BuildCategoryMenuResponse(string categoryName, DateTime currentTime)
    {
        var categoryLower = categoryName.ToLower();
        string mealType = categoryLower switch
        {
            var x when x.Contains("breakfast") || x.Contains("morning") => "breakfast",
            var x when x.Contains("lunch") => "lunch",
            var x when x.Contains("dinner") || x.Contains("evening") => "dinner",
            _ => "all"
        };

        if (mealType == "all")
        {
            return "I can show you our **Breakfast**, **Lunch**, or **Dinner** menus. Which one would you like to see?";
        }

        var menuItems = await GetMenuItemsAsync(mealType);
        var specials = await GetActiveSpecialsAsync(currentTime, mealType);

        if (!menuItems.Any())
        {
            return $"I don't have {mealType} menu items available right now. Please try another category or contact our staff.";
        }

        var mealEmoji = mealType switch
        {
            "breakfast" => "🌅",
            "lunch" => "☀️",
            "dinner" => "🌃",
            _ => "🍽️"
        };

        var response = $"{mealEmoji} **{char.ToUpper(mealType[0]) + mealType[1..]} Menu**\n\n";
        var totalLength = response.Length;
        const int maxLength = 1200; // More aggressive limit

        var categories = menuItems.GroupBy(m => m.MenuCategory.Name)
            .OrderBy(g => menuItems.First(m => m.MenuCategory.Name == g.Key).MenuCategory.DisplayOrder);

        int itemCount = 0;
        const int maxItems = 15; // Limit total items shown

        foreach (var category in categories)
        {
            var categoryHeader = $"**{category.Key}**\n";
            if (totalLength + categoryHeader.Length > maxLength - 150 || itemCount >= maxItems) break;

            response += categoryHeader;
            totalLength += categoryHeader.Length;

            foreach (var item in category.OrderBy(i => i.Name))
            {
                if (itemCount >= maxItems) break;

                // Simplified format - just name and price
                var itemText = $"• {item.Name} - R{item.PriceCents / 100:F2}";

                // Add dietary indicators inline (compact)
                if (item.IsVegan) itemText += " 🌿";
                else if (item.IsVegetarian) itemText += " 🌱";

                itemText += "\n";

                if (totalLength + itemText.Length > maxLength - 100) goto FinishCategoryResponse;

                response += itemText;
                totalLength += itemText.Length;
                itemCount++;
            }
        }

        // Add only special titles if they fit (no descriptions)
        if (specials.Any() && totalLength < maxLength - 200)
        {
            response += "\n**Today's Specials**\n";
            int specialCount = 0;
            foreach (var special in specials.Take(3)) // Max 3 specials
            {
                var specialText = $"🌟 {special.Title}";
                if (special.SpecialPriceCents.HasValue)
                {
                    specialText += $" - R{special.SpecialPriceCents.Value / 100:F2}";
                }
                specialText += "\n";

                if (totalLength + specialText.Length > maxLength - 50) break;

                response += specialText;
                totalLength += specialText.Length;
                specialCount++;
            }
        }

        FinishCategoryResponse:
        if (itemCount >= maxItems && menuItems.Count() > maxItems)
        {
            response += $"\n*({menuItems.Count() - maxItems} more items available - ask for specific items)*";
        }

        response += "\n\nReply with item names to order or get details!";
        return response.Trim();
    }
}