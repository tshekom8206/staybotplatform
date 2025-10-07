using System;
using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class GuestRating
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    // Link to the conversation/guest
    public int? ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    // Link to specific task if rating is for a specific service
    public int? TaskId { get; set; }
    public StaffTask? Task { get; set; }

    // Link to booking
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }

    // Rating details
    [Range(1, 5)]
    public int Rating { get; set; } // 1-5 stars

    public string? Department { get; set; } // Which department/service is being rated

    public string? Comment { get; set; } // Optional text feedback

    public string RatingType { get; set; } = "Service"; // Service, Stay, Department

    public string? GuestName { get; set; }

    public string? GuestPhone { get; set; }

    public string? RoomNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // For tracking if this was collected via chat or survey
    public string CollectionMethod { get; set; } = "Chat"; // Chat, Survey, Manual

    // For post-stay surveys
    public bool WouldRecommend { get; set; } // NPS question

    public int? NpsScore { get; set; } // 0-10 Net Promoter Score
}

public class PostStaySurvey
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public string GuestName { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
    public string? GuestPhone { get; set; }

    // Overall ratings
    public int OverallRating { get; set; } // 1-5
    public int CleanlinessRating { get; set; } // 1-5
    public int ServiceRating { get; set; } // 1-5
    public int AmenitiesRating { get; set; } // 1-5
    public int ValueRating { get; set; } // 1-5

    // Department-specific ratings
    public int? FrontDeskRating { get; set; }
    public int? HousekeepingRating { get; set; }
    public int? MaintenanceRating { get; set; }
    public int? FoodServiceRating { get; set; }

    // Feedback
    public string? WhatWentWell { get; set; }
    public string? WhatCouldImprove { get; set; }
    public string? AdditionalComments { get; set; }

    // NPS
    [Range(0, 10)]
    public int NpsScore { get; set; } // How likely to recommend (0-10)

    // Survey metadata
    public DateTime SentAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string SurveyToken { get; set; } = Guid.NewGuid().ToString();
    public bool IsCompleted { get; set; }

    // Survey tracking fields
    public bool SentSuccessfully { get; set; } = false;
    public DateTime? OpenedAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }
    public int ClickCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// For aggregating ratings by department or service
public class RatingsSummary
{
    public string Period { get; set; } = "All Time"; // Today, Week, Month, All Time
    public int TotalRatings { get; set; }
    public double AverageRating { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new(); // 1-5 star counts
    public double? NpsScore { get; set; } // Net Promoter Score
    public int PromoterCount { get; set; } // NPS 9-10
    public int PassiveCount { get; set; } // NPS 7-8
    public int DetractorCount { get; set; } // NPS 0-6

    // Department breakdowns
    public Dictionary<string, DepartmentRating> DepartmentRatings { get; set; } = new();
}

public class DepartmentRating
{
    public string Department { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AverageRating { get; set; }
    public string Trend { get; set; } = "stable"; // improving, declining, stable
}