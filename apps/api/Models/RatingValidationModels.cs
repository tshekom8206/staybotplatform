using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class CreateRatingRequest
{
    [Required]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
    public int Rating { get; set; }

    [MaxLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters")]
    public string? Comment { get; set; }
}

public class CollectRatingRequest
{
    [Required]
    public int ConversationId { get; set; }

    [Required]
    [MaxLength(500, ErrorMessage = "Message text cannot exceed 500 characters")]
    public string MessageText { get; set; }
}

public class PostStaySurveySubmissionRequest
{
    [Required]
    public string SurveyToken { get; set; } = string.Empty;

    [Required]
    [Range(1, 5, ErrorMessage = "Overall rating must be between 1 and 5")]
    public int OverallRating { get; set; }

    [Required]
    [Range(1, 5, ErrorMessage = "Cleanliness rating must be between 1 and 5")]
    public int CleanlinessRating { get; set; }

    [Required]
    [Range(1, 5, ErrorMessage = "Service rating must be between 1 and 5")]
    public int ServiceRating { get; set; }

    [Required]
    [Range(1, 5, ErrorMessage = "Amenities rating must be between 1 and 5")]
    public int AmenitiesRating { get; set; }

    [Required]
    [Range(1, 5, ErrorMessage = "Value rating must be between 1 and 5")]
    public int ValueRating { get; set; }

    [Range(1, 5, ErrorMessage = "Front desk rating must be between 1 and 5")]
    public int? FrontDeskRating { get; set; }

    [Range(1, 5, ErrorMessage = "Housekeeping rating must be between 1 and 5")]
    public int? HousekeepingRating { get; set; }

    [Range(1, 5, ErrorMessage = "Maintenance rating must be between 1 and 5")]
    public int? MaintenanceRating { get; set; }

    [Range(1, 5, ErrorMessage = "Food service rating must be between 1 and 5")]
    public int? FoodServiceRating { get; set; }

    [Required]
    [Range(0, 10, ErrorMessage = "NPS score must be between 0 and 10")]
    public int NpsScore { get; set; }

    [MaxLength(2000, ErrorMessage = "What went well cannot exceed 2000 characters")]
    public string? WhatWentWell { get; set; }

    [MaxLength(2000, ErrorMessage = "What could improve cannot exceed 2000 characters")]
    public string? WhatCouldImprove { get; set; }

    [MaxLength(2000, ErrorMessage = "Additional comments cannot exceed 2000 characters")]
    public string? AdditionalComments { get; set; }
}