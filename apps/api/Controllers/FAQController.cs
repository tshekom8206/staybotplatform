using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FAQController : ControllerBase
{
    private readonly HostrDbContext _context;

    public FAQController(HostrDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all FAQs for the current tenant
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFAQs()
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var faqs = await _context.FAQs
            .Where(f => f.TenantId == tenantId)
            .OrderBy(f => f.Question)
            .Select(f => new
            {
                f.Id,
                f.Question,
                f.Answer,
                f.Language,
                f.Tags,
                f.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { faqs });
    }

    /// <summary>
    /// Get a specific FAQ by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetFAQ(int id)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var faq = await _context.FAQs
            .Where(f => f.Id == id && f.TenantId == tenantId)
            .Select(f => new
            {
                f.Id,
                f.Question,
                f.Answer,
                f.Language,
                f.Tags,
                f.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (faq == null)
        {
            return NotFound("FAQ not found");
        }

        return Ok(new { faq });
    }

    /// <summary>
    /// Create a new FAQ
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateFAQ([FromBody] CreateFAQRequest request)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var faq = new FAQ
        {
            TenantId = tenantId,
            Question = request.Question,
            Answer = request.Answer,
            Language = request.Language ?? "en",
            Tags = request.Tags ?? Array.Empty<string>(),
            UpdatedAt = DateTime.UtcNow
        };

        _context.FAQs.Add(faq);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetFAQ), new { id = faq.Id }, new { faq });
    }

    /// <summary>
    /// Update an existing FAQ
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFAQ(int id, [FromBody] UpdateFAQRequest request)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var faq = await _context.FAQs
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId);

        if (faq == null)
        {
            return NotFound("FAQ not found");
        }

        faq.Question = request.Question;
        faq.Answer = request.Answer;
        faq.Language = request.Language ?? "en";
        faq.Tags = request.Tags ?? Array.Empty<string>();
        faq.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { faq });
    }

    /// <summary>
    /// Delete an FAQ
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFAQ(int id)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var faq = await _context.FAQs
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId);

        if (faq == null)
        {
            return NotFound("FAQ not found");
        }

        _context.FAQs.Remove(faq);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Get FAQ statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetFAQStats()
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var stats = await _context.FAQs
            .Where(f => f.TenantId == tenantId)
            .GroupBy(f => f.Language)
            .Select(g => new
            {
                Language = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        var totalCount = await _context.FAQs
            .Where(f => f.TenantId == tenantId)
            .CountAsync();

        var faqs = await _context.FAQs
            .Where(f => f.TenantId == tenantId && f.Tags != null)
            .Select(f => f.Tags)
            .ToListAsync();

        var uniqueTags = faqs
            .SelectMany(tags => tags)
            .Distinct()
            .ToList();

        return Ok(new {
            total = totalCount,
            byLanguage = stats,
            uniqueTags = uniqueTags
        });
    }

    /// <summary>
    /// Search FAQs by question or tag
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchFAQs([FromQuery] string query, [FromQuery] string? language = null)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        if (string.IsNullOrEmpty(query))
        {
            return BadRequest("Search query is required");
        }

        var faqQuery = _context.FAQs
            .Where(f => f.TenantId == tenantId);

        if (!string.IsNullOrEmpty(language))
        {
            faqQuery = faqQuery.Where(f => f.Language == language);
        }

        var allFaqs = await faqQuery
            .Select(f => new
            {
                f.Id,
                f.Question,
                f.Answer,
                f.Language,
                f.Tags,
                f.UpdatedAt
            })
            .ToListAsync();

        var faqs = allFaqs
            .Where(f =>
                f.Question.ToLower().Contains(query.ToLower()) ||
                f.Answer.ToLower().Contains(query.ToLower()) ||
                (f.Tags != null && f.Tags.Any(t => t.ToLower().Contains(query.ToLower()))))
            .OrderBy(f => f.Question)
            .ToList();

        return Ok(new { faqs, query, language });
    }
}

public class CreateFAQRequest
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Language { get; set; } = "en";
    public string[]? Tags { get; set; }
}

public class UpdateFAQRequest
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Language { get; set; } = "en";
    public string[]? Tags { get; set; }
}