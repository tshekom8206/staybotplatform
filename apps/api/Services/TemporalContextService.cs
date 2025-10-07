using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Hostr.Api.Services;

public enum MealPeriod
{
    None = 0,
    Breakfast = 1,
    Lunch = 2,
    Dinner = 3,
    LateNight = 4
}

public interface ITemporalContextService
{
    Task<TimeContext> GetCurrentTimeContextAsync(int tenantId);
    Task<bool> IsWithinBusinessHoursAsync(int tenantId, string serviceType);
    Task<DateTime> ResolveTimeReferenceAsync(string timeExpression, int tenantId);
    Task<MealPeriod> GetMealPeriodAsync(int tenantId);
    Task<bool> IsServiceAvailableAsync(int tenantId, string serviceName, DateTime? targetTime = null);
    DateTime ConvertToTenantTime(DateTime utcTime, string tenantTimezone);
    DateTime ConvertToUtc(DateTime localTime, string tenantTimezone);
}

public class TimeContext
{
    public DateTime CurrentTime { get; set; }
    public DateTime LocalTime { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public MealPeriod MealPeriod { get; set; } = MealPeriod.None;
    public bool IsBusinessHours { get; set; }
    public Dictionary<string, bool> ServiceAvailability { get; set; } = new();
    public string TimeOfDayDescription { get; set; } = string.Empty;
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWeekend { get; set; }
}

public class TemporalContextService : ITemporalContextService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<TemporalContextService> _logger;
    private readonly ITenantCacheService _tenantCache;

    private static readonly Dictionary<string, TimeSpan> MealPeriods = new()
    {
        { "breakfast", new TimeSpan(6, 0, 0) },
        { "lunch", new TimeSpan(11, 30, 0) },
        { "dinner", new TimeSpan(17, 0, 0) },
        { "late_night", new TimeSpan(22, 0, 0) }
    };

    private static readonly Dictionary<string, Regex> TimeExpressionPatterns = new()
    {
        { "tonight", new Regex(@"\b(tonight|this evening)\b", RegexOptions.IgnoreCase) },
        { "tomorrow", new Regex(@"\b(tomorrow|next day)\b", RegexOptions.IgnoreCase) },
        { "this_morning", new Regex(@"\b(this morning|today morning)\b", RegexOptions.IgnoreCase) },
        { "this_afternoon", new Regex(@"\b(this afternoon|today afternoon)\b", RegexOptions.IgnoreCase) },
        { "this_evening", new Regex(@"\b(this evening|tonight)\b", RegexOptions.IgnoreCase) },
        { "now", new Regex(@"\b(now|right now|immediately)\b", RegexOptions.IgnoreCase) },
        { "later", new Regex(@"\b(later|later today)\b", RegexOptions.IgnoreCase) },
        { "next_week", new Regex(@"\b(next week)\b", RegexOptions.IgnoreCase) },
        { "this_weekend", new Regex(@"\b(this weekend|weekend)\b", RegexOptions.IgnoreCase) }
    };

    public TemporalContextService(HostrDbContext context, ILogger<TemporalContextService> logger, ITenantCacheService tenantCache)
    {
        _context = context;
        _logger = logger;
        _tenantCache = tenantCache;
    }

    public async Task<TimeContext> GetCurrentTimeContextAsync(int tenantId)
    {
        try
        {
            var timezone = await _tenantCache.GetTenantTimezoneAsync(tenantId);
            return await GetCurrentTimeContextAsync(tenantId, timezone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting time context for tenant {TenantId}", tenantId);
            return GetDefaultTimeContext();
        }
    }

    public async Task<TimeContext> GetCurrentTimeContextAsync(int tenantId, string timezone)
    {
        try
        {

            var utcNow = DateTime.UtcNow;
            var localTime = ConvertToTenantTime(utcNow, timezone);

            var context = new TimeContext
            {
                CurrentTime = localTime,
                LocalTime = localTime,
                Timezone = timezone,
                DayOfWeek = localTime.DayOfWeek,
                IsWeekend = localTime.DayOfWeek == DayOfWeek.Saturday || localTime.DayOfWeek == DayOfWeek.Sunday
            };

            context.MealPeriod = await GetMealPeriodAsync(tenantId);
            context.TimeOfDayDescription = GetTimeOfDayDescription(localTime);
            // Calculate business hours inline to avoid circular dependency
            var currentTimeOfDay = localTime.TimeOfDay;
            var defaultStart = new TimeSpan(6, 0, 0);  // 6 AM
            var defaultEnd = new TimeSpan(22, 0, 0);   // 10 PM
            context.IsBusinessHours = currentTimeOfDay >= defaultStart && currentTimeOfDay <= defaultEnd;

            // Check service availability using cached services
            var services = await _tenantCache.GetTenantServicesAsync(tenantId);

            foreach (var service in services)
            {
                // Calculate service availability directly to avoid circular dependency
                bool isAvailable = true;
                if (!string.IsNullOrEmpty(service.AvailableHours))
                {
                    var (start, end) = ParseServiceHours(service.AvailableHours);
                    isAvailable = currentTimeOfDay >= start && currentTimeOfDay <= end;
                }
                context.ServiceAvailability[service.Name] = isAvailable;
            }

            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting temporal context for tenant {TenantId}", tenantId);
            return GetDefaultTimeContext();
        }
    }

    public async Task<bool> IsWithinBusinessHoursAsync(int tenantId, string serviceType)
    {
        try
        {
            // Fix circular dependency - get timezone and calculate time directly
            var timezone = await _tenantCache.GetTenantTimezoneAsync(tenantId);
            var localTime = ConvertToTenantTime(DateTime.UtcNow, timezone);
            var currentTime = localTime.TimeOfDay;

            // Default business hours if no specific service hours found
            var defaultStart = new TimeSpan(6, 0, 0);  // 6 AM
            var defaultEnd = new TimeSpan(22, 0, 0);   // 10 PM

            // Check if there are specific service hours using cached services
            var services = await _tenantCache.GetTenantServicesAsync(tenantId);
            var service = services.FirstOrDefault(s => s.Name.ToLower().Contains(serviceType.ToLower()));

            if (service?.AvailableHours != null && !string.IsNullOrEmpty(service.AvailableHours))
            {
                var (start, end) = ParseServiceHours(service.AvailableHours);
                return currentTime >= start && currentTime <= end;
            }

            return currentTime >= defaultStart && currentTime <= defaultEnd;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking business hours for tenant {TenantId}, service {ServiceType}", tenantId, serviceType);
            return true; // Default to available if error
        }
    }

    public async Task<DateTime> ResolveTimeReferenceAsync(string timeExpression, int tenantId)
    {
        try
        {
            var timeContext = await GetCurrentTimeContextAsync(tenantId);
            var baseTime = timeContext.CurrentTime;

            foreach (var pattern in TimeExpressionPatterns)
            {
                if (pattern.Value.IsMatch(timeExpression))
                {
                    return pattern.Key switch
                    {
                        "tonight" => baseTime.Date.AddHours(19), // 7 PM tonight
                        "tomorrow" => baseTime.Date.AddDays(1).AddHours(9), // 9 AM tomorrow
                        "this_morning" => baseTime.Date.AddHours(8), // 8 AM today
                        "this_afternoon" => baseTime.Date.AddHours(14), // 2 PM today
                        "this_evening" => baseTime.Date.AddHours(18), // 6 PM today
                        "now" => baseTime,
                        "later" => baseTime.AddHours(2), // 2 hours from now
                        "next_week" => baseTime.AddDays(7 - (int)baseTime.DayOfWeek + 1), // Next Monday
                        "this_weekend" => GetNextWeekendDate(baseTime),
                        _ => baseTime
                    };
                }
            }

            // Try to parse specific time mentions (e.g., "at 3 PM", "by 8:30")
            var timeMatch = Regex.Match(timeExpression, @"\b(\d{1,2})(?::(\d{2}))?\s*(am|pm)?\b", RegexOptions.IgnoreCase);
            if (timeMatch.Success)
            {
                if (int.TryParse(timeMatch.Groups[1].Value, out int hour))
                {
                    int minute = 0;
                    if (timeMatch.Groups[2].Success && int.TryParse(timeMatch.Groups[2].Value, out int parsedMinute))
                    {
                        minute = parsedMinute;
                    }

                    var isPm = timeMatch.Groups[3].Value.ToLower() == "pm";
                    if (isPm && hour != 12) hour += 12;
                    if (!isPm && hour == 12) hour = 0;

                    return baseTime.Date.AddHours(hour).AddMinutes(minute);
                }
            }

            return baseTime; // Return current time if no pattern matches
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving time reference '{TimeExpression}' for tenant {TenantId}", timeExpression, tenantId);
            var fallbackContext = await GetCurrentTimeContextAsync(tenantId);
            return fallbackContext.CurrentTime;
        }
    }

    public async Task<MealPeriod> GetMealPeriodAsync(int tenantId)
    {
        try
        {
            var timezone = await _tenantCache.GetTenantTimezoneAsync(tenantId);
            var localTime = ConvertToTenantTime(DateTime.UtcNow, timezone);
            var currentHour = localTime.Hour;

            return currentHour switch
            {
                >= 6 and < 11 => MealPeriod.Breakfast,
                >= 11 and < 16 => MealPeriod.Lunch,
                >= 16 and < 22 => MealPeriod.Dinner,
                _ => MealPeriod.LateNight
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting meal period for tenant {TenantId}", tenantId);
            return MealPeriod.None;
        }
    }

    public async Task<bool> IsServiceAvailableAsync(int tenantId, string serviceName, DateTime? targetTime = null)
    {
        try
        {
            var services = await _tenantCache.GetTenantServicesAsync(tenantId);
            var service = services.FirstOrDefault(s => s.Name == serviceName);

            if (service == null) return false;

            var checkTime = targetTime ?? (await GetCurrentTimeContextAsync(tenantId)).CurrentTime;
            var checkTimeOfDay = checkTime.TimeOfDay;

            if (!string.IsNullOrEmpty(service.AvailableHours))
            {
                var (start, end) = ParseServiceHours(service.AvailableHours);
                return checkTimeOfDay >= start && checkTimeOfDay <= end;
            }

            return true; // Available if no specific hours set
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking service availability for {ServiceName} at tenant {TenantId}", serviceName, tenantId);
            return true; // Default to available
        }
    }

    public DateTime ConvertToTenantTime(DateTime utcTime, string tenantTimezone)
    {
        try
        {
            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(tenantTimezone);
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, timeZoneInfo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not convert to timezone {Timezone}, using UTC", tenantTimezone);
            return utcTime;
        }
    }

    public DateTime ConvertToUtc(DateTime localTime, string tenantTimezone)
    {
        try
        {
            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(tenantTimezone);
            return TimeZoneInfo.ConvertTimeToUtc(localTime, timeZoneInfo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not convert from timezone {Timezone}, treating as UTC", tenantTimezone);
            return localTime;
        }
    }

    private (TimeSpan start, TimeSpan end) ParseServiceHours(string serviceHours)
    {
        try
        {
            // Parse formats like "9:00 AM - 5:00 PM" or "09:00-17:00"
            var match = Regex.Match(serviceHours, @"(\d{1,2}):?(\d{2})?\s*(am|pm)?\s*-\s*(\d{1,2}):?(\d{2})?\s*(am|pm)?", RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var startHour = int.Parse(match.Groups[1].Value);
                var startMinute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                var startAmPm = match.Groups[3].Value;

                var endHour = int.Parse(match.Groups[4].Value);
                var endMinute = match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : 0;
                var endAmPm = match.Groups[6].Value;

                // Convert to 24-hour format
                if (startAmPm.ToLower() == "pm" && startHour != 12) startHour += 12;
                if (startAmPm.ToLower() == "am" && startHour == 12) startHour = 0;
                if (endAmPm.ToLower() == "pm" && endHour != 12) endHour += 12;
                if (endAmPm.ToLower() == "am" && endHour == 12) endHour = 0;

                return (new TimeSpan(startHour, startMinute, 0), new TimeSpan(endHour, endMinute, 0));
            }

            // Default fallback
            return (new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0));
        }
        catch
        {
            return (new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0));
        }
    }

    private string GetTimeOfDayDescription(DateTime time)
    {
        var hour = time.Hour;
        return hour switch
        {
            >= 5 and < 12 => "morning",
            >= 12 and < 17 => "afternoon",
            >= 17 and < 21 => "evening",
            _ => "night"
        };
    }

    private DateTime GetNextWeekendDate(DateTime baseTime)
    {
        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)baseTime.DayOfWeek + 7) % 7;
        if (daysUntilSaturday == 0 && baseTime.DayOfWeek == DayOfWeek.Saturday)
        {
            return baseTime.Date.AddHours(10); // This Saturday at 10 AM
        }
        return baseTime.Date.AddDays(daysUntilSaturday).AddHours(10); // Next Saturday at 10 AM
    }

    private TimeContext GetDefaultTimeContext()
    {
        var utcNow = DateTime.UtcNow;
        return new TimeContext
        {
            CurrentTime = utcNow,
            Timezone = "UTC",
            MealPeriod = MealPeriod.None,
            TimeOfDayDescription = GetTimeOfDayDescription(utcNow),
            DayOfWeek = utcNow.DayOfWeek,
            IsWeekend = utcNow.DayOfWeek == DayOfWeek.Saturday || utcNow.DayOfWeek == DayOfWeek.Sunday,
            IsBusinessHours = true,
            ServiceAvailability = new Dictionary<string, bool>()
        };
    }
}