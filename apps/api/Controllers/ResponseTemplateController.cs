using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Contracts.DTOs.Common;
using Hostr.Api.Services;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ResponseTemplateController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<ResponseTemplateController> _logger;
    private readonly IResponseTemplateService _responseTemplateService;

    public ResponseTemplateController(
        HostrDbContext context,
        ILogger<ResponseTemplateController> logger,
        IResponseTemplateService responseTemplateService)
    {
        _context = context;
        _logger = logger;
        _responseTemplateService = responseTemplateService;
    }

    // GET: api/responsetemplate
    [HttpGet]
    public async Task<IActionResult> GetTemplates(
        [FromQuery] string? category = null,
        [FromQuery] string? language = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var query = _context.ResponseTemplates
                .Where(t => t.TenantId == tenantId)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(category))
                query = query.Where(t => t.Category == category);

            if (!string.IsNullOrEmpty(language))
                query = query.Where(t => t.Language == language);

            if (isActive.HasValue)
                query = query.Where(t => t.IsActive == isActive.Value);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(t => t.TemplateKey.Contains(search) ||
                                       t.Template.Contains(search) ||
                                       t.Category.Contains(search));

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination and ordering
            var templates = await query
                .OrderBy(t => t.Category)
                .ThenBy(t => t.Priority)
                .ThenBy(t => t.TemplateKey)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.TemplateKey,
                    t.Category,
                    t.Language,
                    t.Template,
                    t.IsActive,
                    t.Priority,
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .ToListAsync();

            var response = new
            {
                Templates = templates,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving response templates");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    // GET: api/responsetemplate/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTemplate(int id)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var template = await _context.ResponseTemplates
                .Where(t => t.Id == id && t.TenantId == tenantId)
                .Select(t => new
                {
                    t.Id,
                    t.TemplateKey,
                    t.Category,
                    t.Language,
                    t.Template,
                    t.IsActive,
                    t.Priority,
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (template == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Template not found"
                });
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = template
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving response template {Id}", id);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    // POST: api/responsetemplate
    [HttpPost]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateResponseTemplateRequestDto request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            // Check if template key already exists for this tenant
            var existingTemplate = await _context.ResponseTemplates
                .FirstOrDefaultAsync(t => t.TenantId == tenantId &&
                                        t.TemplateKey == request.TemplateKey &&
                                        t.Language == request.Language);

            if (existingTemplate != null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = "A template with this key and language already exists"
                });
            }

            var template = new ResponseTemplate
            {
                TenantId = tenantId,
                TemplateKey = request.TemplateKey,
                Category = request.Category,
                Language = request.Language ?? "en",
                Template = request.Template,
                IsActive = request.IsActive ?? true,
                Priority = request.Priority ?? 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ResponseTemplates.Add(template);
            await _context.SaveChangesAsync();

            var createdTemplate = new
            {
                template.Id,
                template.TemplateKey,
                template.Category,
                template.Language,
                template.Template,
                template.IsActive,
                template.Priority,
                template.CreatedAt,
                template.UpdatedAt
            };

            return CreatedAtAction(nameof(GetTemplate), new { id = template.Id },
                new ApiResponse<object>
                {
                    Success = true,
                    Data = createdTemplate
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating response template");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    // PUT: api/responsetemplate/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] UpdateResponseTemplateRequestDto request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var template = await _context.ResponseTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

            if (template == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Template not found"
                });
            }

            // Check if changing template key would create a duplicate
            if (request.TemplateKey != template.TemplateKey || request.Language != template.Language)
            {
                var existingTemplate = await _context.ResponseTemplates
                    .FirstOrDefaultAsync(t => t.TenantId == tenantId &&
                                            t.Id != id &&
                                            t.TemplateKey == request.TemplateKey &&
                                            t.Language == request.Language);

                if (existingTemplate != null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Error = "A template with this key and language already exists"
                    });
                }
            }

            // Update template
            template.TemplateKey = request.TemplateKey;
            template.Category = request.Category;
            template.Language = request.Language ?? template.Language;
            template.Template = request.Template;
            template.IsActive = request.IsActive ?? template.IsActive;
            template.Priority = request.Priority ?? template.Priority;
            template.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var updatedTemplate = new
            {
                template.Id,
                template.TemplateKey,
                template.Category,
                template.Language,
                template.Template,
                template.IsActive,
                template.Priority,
                template.CreatedAt,
                template.UpdatedAt
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = updatedTemplate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating response template {Id}", id);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    // DELETE: api/responsetemplate/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var template = await _context.ResponseTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

            if (template == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Template not found"
                });
            }

            _context.ResponseTemplates.Remove(template);
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { Message = "Template deleted successfully" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting response template {Id}", id);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    // GET: api/responsetemplate/categories
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var categories = await _context.ResponseTemplates
                .Where(t => t.TenantId == tenantId)
                .Select(t => t.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(new ApiResponse<List<string>>
            {
                Success = true,
                Data = categories
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template categories");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    // GET: api/responsetemplate/variables
    [HttpGet("variables")]
    public async Task<IActionResult> GetVariables([FromQuery] string? category = null)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var query = _context.ResponseVariables
                .Where(v => v.TenantId == tenantId && v.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(category))
                query = query.Where(v => v.Category == category);

            var variables = await query
                .OrderBy(v => v.Category)
                .ThenBy(v => v.VariableName)
                .Select(v => new
                {
                    v.Id,
                    v.VariableName,
                    v.VariableValue,
                    v.Category,
                    v.IsActive,
                    v.CreatedAt
                })
                .ToListAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = variables
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving response variables");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    // POST: api/responsetemplate/variables
    [HttpPost("variables")]
    public async Task<IActionResult> CreateOrUpdateVariable([FromBody] CreateResponseVariableRequest request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            // Check if variable already exists
            var existingVariable = await _context.ResponseVariables
                .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.VariableName == request.VariableName);

            if (existingVariable != null)
            {
                // Update existing variable
                existingVariable.VariableValue = request.VariableValue;
                existingVariable.Category = request.Category;
                existingVariable.IsActive = request.IsActive ?? existingVariable.IsActive;
            }
            else
            {
                // Create new variable
                var variable = new ResponseVariable
                {
                    TenantId = tenantId,
                    VariableName = request.VariableName,
                    VariableValue = request.VariableValue,
                    Category = request.Category,
                    IsActive = request.IsActive ?? true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ResponseVariables.Add(variable);
            }

            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { Message = "Variable saved successfully" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating response variable");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    // DELETE: api/responsetemplate/variables/{id}
    [HttpDelete("variables/{id}")]
    public async Task<IActionResult> DeleteVariable(int id)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var variable = await _context.ResponseVariables
                .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == tenantId);

            if (variable == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Variable not found"
                });
            }

            _context.ResponseVariables.Remove(variable);
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { Message = "Variable deleted successfully" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting response variable {Id}", id);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    // POST: api/responsetemplate/process
    [HttpPost("process")]
    public async Task<IActionResult> ProcessTemplate([FromBody] ProcessTemplateRequest request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            if (tenantId <= 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Tenant not found"
                });
            }

            var processedTemplate = await _responseTemplateService.ProcessTemplateAsync(
                tenantId,
                request.TemplateKey,
                request.Variables,
                request.Language ?? "en"
            );

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new
                {
                    processedContent = processedTemplate.Content,
                    usedVariables = processedTemplate.UsedVariables,
                    missingVariables = processedTemplate.MissingVariables
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }
}

// DTOs for ResponseTemplate operations
public class CreateResponseTemplateRequestDto
{
    [Required]
    [MaxLength(100)]
    public string TemplateKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Language { get; set; }

    [Required]
    public string Template { get; set; } = string.Empty;

    public bool? IsActive { get; set; }

    public int? Priority { get; set; }
}

public class UpdateResponseTemplateRequestDto
{
    [Required]
    [MaxLength(100)]
    public string TemplateKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Language { get; set; }

    [Required]
    public string Template { get; set; } = string.Empty;

    public bool? IsActive { get; set; }

    public int? Priority { get; set; }
}

public class CreateResponseVariableRequest
{
    [Required]
    [MaxLength(50)]
    public string VariableName { get; set; } = string.Empty;

    [Required]
    public string VariableValue { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Category { get; set; }

    public bool? IsActive { get; set; }
}

public class ProcessTemplateRequest
{
    [Required]
    [MaxLength(100)]
    public string TemplateKey { get; set; } = string.Empty;

    [Required]
    public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();

    [MaxLength(10)]
    public string? Language { get; set; }
}