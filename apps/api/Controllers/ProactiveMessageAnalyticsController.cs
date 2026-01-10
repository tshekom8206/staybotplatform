using Hostr.Api.Data;
using Hostr.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProactiveMessageAnalyticsController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<ProactiveMessageAnalyticsController> _logger;

    // Cost estimates in ZAR
    private const decimal SMS_COST = 0.90m;
    private const decimal WHATSAPP_COST = 0.09m;
    private const decimal WHATSAPP_FAILED_TO_SMS_COST = 0.99m; // WhatsApp attempt + SMS fallback

    public ProactiveMessageAnalyticsController(
        HostrDbContext context,
        ILogger<ProactiveMessageAnalyticsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get delivery statistics and cost analysis for proactive messages
    /// </summary>
    [HttpGet("delivery-stats")]
    public async Task<ActionResult<DeliveryStatsResponse>> GetDeliveryStats(
        [FromQuery] int? tenantId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var query = _context.ScheduledMessages
                .Where(m => m.Status == ScheduledMessageStatus.Sent);

            // Filter by tenant if specified
            if (tenantId.HasValue)
            {
                query = query.Where(m => m.TenantId == tenantId.Value);
            }

            // Filter by date range if specified
            if (startDate.HasValue)
            {
                query = query.Where(m => m.SentAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(m => m.SentAt <= endDate.Value);
            }

            var messages = await query.ToListAsync();

            // Calculate statistics
            var totalMessagesSent = messages.Count;
            var whatsAppSuccessful = messages.Count(m => m.SuccessfulMethod == DeliveryMethod.WhatsApp);
            var whatsAppFailedToSMS = messages.Count(m => m.SuccessfulMethod == DeliveryMethod.WhatsAppFailedToSMS);
            var smsOnly = messages.Count(m => m.SuccessfulMethod == DeliveryMethod.SMS);

            var whatsAppAdoptionRate = totalMessagesSent > 0
                ? (decimal)(whatsAppSuccessful + whatsAppFailedToSMS) / totalMessagesSent * 100
                : 0;

            var whatsAppSuccessRate = (whatsAppSuccessful + whatsAppFailedToSMS) > 0
                ? (decimal)whatsAppSuccessful / (whatsAppSuccessful + whatsAppFailedToSMS) * 100
                : 0;

            // Calculate costs
            var whatsAppCost = whatsAppSuccessful * WHATSAPP_COST;
            var whatsAppFallbackCost = whatsAppFailedToSMS * WHATSAPP_FAILED_TO_SMS_COST;
            var smsCost = smsOnly * SMS_COST;
            var totalCost = whatsAppCost + whatsAppFallbackCost + smsCost;

            // Calculate what it would have cost if all were SMS
            var smsOnlyCost = totalMessagesSent * SMS_COST;
            var savings = smsOnlyCost - totalCost;
            var savingsPercent = smsOnlyCost > 0 ? savings / smsOnlyCost * 100 : 0;

            var response = new DeliveryStatsResponse
            {
                Summary = new DeliveryStatsSummary
                {
                    TotalMessagesSent = totalMessagesSent,
                    WhatsAppSuccessful = whatsAppSuccessful,
                    WhatsAppFailedFallbackToSMS = whatsAppFailedToSMS,
                    SmsOnly = smsOnly,
                    WhatsAppAdoptionRate = Math.Round(whatsAppAdoptionRate, 2),
                    WhatsAppSuccessRate = Math.Round(whatsAppSuccessRate, 2)
                },
                CostEstimate = new CostEstimate
                {
                    WhatsAppCost = Math.Round(whatsAppCost, 2),
                    WhatsAppFallbackCost = Math.Round(whatsAppFallbackCost, 2),
                    SmsCost = Math.Round(smsCost, 2),
                    TotalCost = Math.Round(totalCost, 2),
                    SmsOnlyCost = Math.Round(smsOnlyCost, 2),
                    Savings = Math.Round(savings, 2),
                    SavingsPercent = Math.Round(savingsPercent, 2)
                },
                DateRange = new DateRange
                {
                    StartDate = startDate ?? messages.Min(m => m.SentAt),
                    EndDate = endDate ?? messages.Max(m => m.SentAt)
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting delivery stats");
            return StatusCode(500, "Error retrieving delivery statistics");
        }
    }

    /// <summary>
    /// Get analysis of WhatsApp failures and common error reasons
    /// </summary>
    [HttpGet("whatsapp-failures")]
    public async Task<ActionResult<WhatsAppFailuresResponse>> GetWhatsAppFailures(
        [FromQuery] int? tenantId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var query = _context.ScheduledMessages
                .Where(m => m.WhatsAppFailureReason != null);

            if (tenantId.HasValue)
            {
                query = query.Where(m => m.TenantId == tenantId.Value);
            }

            if (startDate.HasValue)
            {
                query = query.Where(m => m.SentAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(m => m.SentAt <= endDate.Value);
            }

            var failures = await query
                .GroupBy(m => m.WhatsAppFailureReason)
                .Select(g => new WhatsAppFailureReason
                {
                    Reason = g.Key!,
                    Count = g.Count(),
                    FallbackSuccessful = g.Count(m => m.SuccessfulMethod == DeliveryMethod.WhatsAppFailedToSMS),
                    TotallyFailed = g.Count(m => m.Status == ScheduledMessageStatus.Failed)
                })
                .OrderByDescending(f => f.Count)
                .ToListAsync();

            var totalFailures = failures.Sum(f => f.Count);
            var fallbackSuccessRate = totalFailures > 0
                ? (decimal)failures.Sum(f => f.FallbackSuccessful) / totalFailures * 100
                : 0;

            var response = new WhatsAppFailuresResponse
            {
                TotalWhatsAppFailures = totalFailures,
                FallbackSuccessRate = Math.Round(fallbackSuccessRate, 2),
                FailureReasons = failures
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WhatsApp failure analysis");
            return StatusCode(500, "Error retrieving WhatsApp failure analysis");
        }
    }

    /// <summary>
    /// Get daily delivery trends showing WhatsApp vs SMS usage over time
    /// </summary>
    [HttpGet("delivery-trends")]
    public async Task<ActionResult<DeliveryTrendsResponse>> GetDeliveryTrends(
        [FromQuery] int? tenantId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var query = _context.ScheduledMessages
                .Where(m => m.Status == ScheduledMessageStatus.Sent && m.SentAt != null);

            if (tenantId.HasValue)
            {
                query = query.Where(m => m.TenantId == tenantId.Value);
            }

            if (startDate.HasValue)
            {
                query = query.Where(m => m.SentAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(m => m.SentAt <= endDate.Value);
            }

            var messages = await query.ToListAsync();

            var trends = messages
                .GroupBy(m => m.SentAt!.Value.Date)
                .Select(g => new DailyDeliveryTrend
                {
                    Date = g.Key,
                    WhatsAppSuccessful = g.Count(m => m.SuccessfulMethod == DeliveryMethod.WhatsApp),
                    WhatsAppFailedToSMS = g.Count(m => m.SuccessfulMethod == DeliveryMethod.WhatsAppFailedToSMS),
                    SmsOnly = g.Count(m => m.SuccessfulMethod == DeliveryMethod.SMS),
                    TotalMessages = g.Count()
                })
                .OrderBy(t => t.Date)
                .ToList();

            // Calculate cost for each day
            foreach (var trend in trends)
            {
                var whatsAppCost = trend.WhatsAppSuccessful * WHATSAPP_COST;
                var fallbackCost = trend.WhatsAppFailedToSMS * WHATSAPP_FAILED_TO_SMS_COST;
                var smsCost = trend.SmsOnly * SMS_COST;
                trend.TotalCost = Math.Round(whatsAppCost + fallbackCost + smsCost, 2);
            }

            var response = new DeliveryTrendsResponse
            {
                Trends = trends
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting delivery trends");
            return StatusCode(500, "Error retrieving delivery trends");
        }
    }
}

#region Response DTOs

public class DeliveryStatsResponse
{
    public DeliveryStatsSummary Summary { get; set; } = null!;
    public CostEstimate CostEstimate { get; set; } = null!;
    public DateRange DateRange { get; set; } = null!;
}

public class DeliveryStatsSummary
{
    public int TotalMessagesSent { get; set; }
    public int WhatsAppSuccessful { get; set; }
    public int WhatsAppFailedFallbackToSMS { get; set; }
    public int SmsOnly { get; set; }
    public decimal WhatsAppAdoptionRate { get; set; }
    public decimal WhatsAppSuccessRate { get; set; }
}

public class CostEstimate
{
    public decimal WhatsAppCost { get; set; }
    public decimal WhatsAppFallbackCost { get; set; }
    public decimal SmsCost { get; set; }
    public decimal TotalCost { get; set; }
    public decimal SmsOnlyCost { get; set; }
    public decimal Savings { get; set; }
    public decimal SavingsPercent { get; set; }
}

public class DateRange
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class WhatsAppFailuresResponse
{
    public int TotalWhatsAppFailures { get; set; }
    public decimal FallbackSuccessRate { get; set; }
    public List<WhatsAppFailureReason> FailureReasons { get; set; } = new();
}

public class WhatsAppFailureReason
{
    public string Reason { get; set; } = string.Empty;
    public int Count { get; set; }
    public int FallbackSuccessful { get; set; }
    public int TotallyFailed { get; set; }
}

public class DeliveryTrendsResponse
{
    public List<DailyDeliveryTrend> Trends { get; set; } = new();
}

public class DailyDeliveryTrend
{
    public DateTime Date { get; set; }
    public int WhatsAppSuccessful { get; set; }
    public int WhatsAppFailedToSMS { get; set; }
    public int SmsOnly { get; set; }
    public int TotalMessages { get; set; }
    public decimal TotalCost { get; set; }
}

#endregion
