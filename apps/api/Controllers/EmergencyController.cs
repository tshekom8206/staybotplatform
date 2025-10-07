using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmergencyController : ControllerBase
{
    private readonly HostrDbContext _context;

    public EmergencyController(HostrDbContext context)
    {
        _context = context;
    }

    #region Emergency Types

    /// <summary>
    /// Get all emergency types for the current tenant
    /// </summary>
    [HttpGet("types")]
    public async Task<IActionResult> GetEmergencyTypes()
    {
        var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
        {
            return BadRequest("Invalid tenant context");
        }

        var types = await _context.EmergencyTypes
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Description,
                t.DetectionKeywords,
                t.SeverityLevel,
                t.AutoEscalate,
                t.RequiresEvacuation,
                t.ContactEmergencyServices,
                t.IsActive,
                t.UpdatedAt,
                ProtocolCount = t.EmergencyProtocols.Count(),
                IncidentCount = t.EmergencyIncidents.Count()
            })
            .ToListAsync();

        return Ok(new { types });
    }

    /// <summary>
    /// Get a specific emergency type with its protocols
    /// </summary>
    [HttpGet("types/{id:int}")]
    public async Task<IActionResult> GetEmergencyType(int id)
    {
        var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
        {
            return BadRequest("Invalid tenant context");
        }

        var type = await _context.EmergencyTypes
            .Where(t => t.Id == id && t.TenantId == tenantId)
            .Include(t => t.EmergencyProtocols)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Description,
                t.DetectionKeywords,
                t.SeverityLevel,
                t.AutoEscalate,
                t.RequiresEvacuation,
                t.ContactEmergencyServices,
                t.IsActive,
                t.UpdatedAt,
                Protocols = t.EmergencyProtocols.Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.ProcedureSteps,
                    p.TriggerCondition,
                    p.NotifyGuests,
                    p.NotifyStaff,
                    p.IsActive,
                    p.ExecutionOrder
                }).OrderBy(p => p.ExecutionOrder)
            })
            .FirstOrDefaultAsync();

        if (type == null)
        {
            return NotFound("Emergency type not found");
        }

        return Ok(type);
    }

    /// <summary>
    /// Create a new emergency type
    /// </summary>
    [HttpPost("types")]
    public async Task<IActionResult> CreateEmergencyType([FromBody] CreateEmergencyTypeRequest request)
    {
        var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
        {
            return BadRequest("Invalid tenant context");
        }

        var emergencyType = new EmergencyType
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            DetectionKeywords = request.DetectionKeywords ?? Array.Empty<string>(),
            SeverityLevel = request.SeverityLevel ?? "High",
            AutoEscalate = request.AutoEscalate ?? true,
            RequiresEvacuation = request.RequiresEvacuation ?? false,
            ContactEmergencyServices = request.ContactEmergencyServices ?? false,
            IsActive = request.IsActive ?? true,
            UpdatedAt = DateTime.UtcNow
        };

        _context.EmergencyTypes.Add(emergencyType);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEmergencyType), new { id = emergencyType.Id }, emergencyType);
    }

    /// <summary>
    /// Update an existing emergency type
    /// </summary>
    [HttpPut("types/{id:int}")]
    public async Task<IActionResult> UpdateEmergencyType(int id, [FromBody] UpdateEmergencyTypeRequest request)
    {
        var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
        {
            return BadRequest("Invalid tenant context");
        }

        var emergencyType = await _context.EmergencyTypes
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        if (emergencyType == null)
        {
            return NotFound("Emergency type not found");
        }

        emergencyType.Name = request.Name;
        emergencyType.Description = request.Description;
        emergencyType.DetectionKeywords = request.DetectionKeywords ?? Array.Empty<string>();
        emergencyType.SeverityLevel = request.SeverityLevel ?? "High";
        emergencyType.AutoEscalate = request.AutoEscalate ?? true;
        emergencyType.RequiresEvacuation = request.RequiresEvacuation ?? false;
        emergencyType.ContactEmergencyServices = request.ContactEmergencyServices ?? false;
        emergencyType.IsActive = request.IsActive ?? true;
        emergencyType.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(emergencyType);
    }

    /// <summary>
    /// Delete an emergency type (soft delete by setting IsActive to false)
    /// </summary>
    [HttpDelete("types/{id:int}")]
    public async Task<IActionResult> DeleteEmergencyType(int id)
    {
        var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
        {
            return BadRequest("Invalid tenant context");
        }

        var emergencyType = await _context.EmergencyTypes
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        if (emergencyType == null)
        {
            return NotFound("Emergency type not found");
        }

        // Check if type has active incidents
        var hasActiveIncidents = await _context.EmergencyIncidents
            .AnyAsync(i => i.EmergencyTypeId == id && i.Status == "ACTIVE");

        if (hasActiveIncidents)
        {
            // Soft delete - set IsActive to false
            emergencyType.IsActive = false;
            emergencyType.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Emergency type deactivated successfully" });
        }
        else
        {
            // Hard delete if no active incidents
            _context.EmergencyTypes.Remove(emergencyType);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    #endregion

    #region Emergency Incidents

    /// <summary>
    /// Get all emergency incidents with optional filtering
    /// </summary>
    [HttpGet("incidents")]
    public async Task<IActionResult> GetEmergencyIncidents(
        [FromQuery] string? status = null,
        [FromQuery] int? emergencyTypeId = null,
        [FromQuery] string? severityLevel = null)
    {
        var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
        {
            return BadRequest("Invalid tenant context");
        }

        var baseQuery = _context.EmergencyIncidents
            .Where(i => i.TenantId == tenantId);

        if (!string.IsNullOrEmpty(status))
        {
            baseQuery = baseQuery.Where(i => i.Status == status);
        }

        if (emergencyTypeId.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.EmergencyTypeId == emergencyTypeId.Value);
        }

        if (!string.IsNullOrEmpty(severityLevel))
        {
            baseQuery = baseQuery.Where(i => i.SeverityLevel == severityLevel);
        }

        var incidents = await baseQuery
            .Include(i => i.EmergencyType)
            .OrderByDescending(i => i.ReportedAt)
            .Select(i => new
            {
                i.Id,
                i.Title,
                i.Description,
                i.Status,
                i.SeverityLevel,
                i.ReportedBy,
                i.Location,
                i.AffectedAreas,
                i.ReportedAt,
                i.ResolvedAt,
                i.ResolutionNotes,
                EmergencyType = new
                {
                    i.EmergencyType.Id,
                    i.EmergencyType.Name,
                    i.EmergencyType.SeverityLevel
                }
            })
            .ToListAsync();

        return Ok(new { incidents });
    }

    /// <summary>
    /// Resolve an emergency incident
    /// </summary>
    [HttpPut("incidents/{id:int}/resolve")]
    public async Task<IActionResult> ResolveIncident(int id, [FromBody] ResolveIncidentRequest request)
    {
        var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
        {
            return BadRequest("Invalid tenant context");
        }

        var incident = await _context.EmergencyIncidents
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId);

        if (incident == null)
        {
            return NotFound("Emergency incident not found");
        }

        incident.Status = "RESOLVED";
        incident.ResolvedAt = DateTime.UtcNow;
        incident.ResolutionNotes = request.ResolutionNotes;

        await _context.SaveChangesAsync();

        return Ok(incident);
    }

    #endregion

    #region Emergency Contacts

    /// <summary>
    /// Get all emergency contacts for the current tenant
    /// </summary>
    [HttpGet("contacts")]
    public async Task<IActionResult> GetEmergencyContacts([FromQuery] string? contactType = null)
    {
        try
        {
            var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
            if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
            {
                return BadRequest("Invalid tenant context");
            }

            var baseQuery = _context.EmergencyContacts
                .Where(c => c.TenantId == tenantId);

            if (!string.IsNullOrEmpty(contactType))
            {
                baseQuery = baseQuery.Where(c => c.ContactType == contactType);
            }

            var contacts = await baseQuery
                .OrderBy(c => c.ContactType)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return Ok(new { contacts });
        }
        catch (Exception ex)
        {
            // Log the exception details for debugging
            Console.WriteLine($"EmergencyContacts error: {ex}");
            return StatusCode(500, "An error occurred while retrieving emergency contacts");
        }
    }

    /// <summary>
    /// Create a new emergency contact
    /// </summary>
    [HttpPost("contacts")]
    public async Task<IActionResult> CreateEmergencyContact([FromBody] CreateEmergencyContactRequest request)
    {
        var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
        {
            return BadRequest("Invalid tenant context");
        }

        var contact = new EmergencyContact
        {
            TenantId = tenantId,
            Name = request.Name,
            ContactType = request.ContactType,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Address = request.Address,
            Notes = request.Notes,
            IsPrimary = request.IsPrimary ?? false,
            IsActive = request.IsActive ?? true,
            UpdatedAt = DateTime.UtcNow
        };

        _context.EmergencyContacts.Add(contact);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEmergencyContacts), contact);
    }

    /// <summary>
    /// Update an existing emergency contact
    /// </summary>
    [HttpPut("contacts/{id:int}")]
    public async Task<IActionResult> UpdateEmergencyContact(int id, [FromBody] UpdateEmergencyContactRequest request)
    {
        var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
        {
            return BadRequest("Invalid tenant context");
        }

        var contact = await _context.EmergencyContacts
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

        if (contact == null)
        {
            return NotFound("Emergency contact not found");
        }

        contact.Name = request.Name;
        contact.ContactType = request.ContactType;
        contact.PhoneNumber = request.PhoneNumber;
        contact.Email = request.Email;
        contact.Address = request.Address;
        contact.Notes = request.Notes;
        contact.IsPrimary = request.IsPrimary ?? false;
        contact.IsActive = request.IsActive ?? true;
        contact.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(contact);
    }

    /// <summary>
    /// Delete an emergency contact
    /// </summary>
    [HttpDelete("contacts/{id:int}")]
    public async Task<IActionResult> DeleteEmergencyContact(int id)
    {
        var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
        {
            return BadRequest("Invalid tenant context");
        }

        var contact = await _context.EmergencyContacts
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

        if (contact == null)
        {
            return NotFound("Emergency contact not found");
        }

        _context.EmergencyContacts.Remove(contact);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Get emergency statistics for the current tenant
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetEmergencyStats()
    {
        try
        {
            var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
            if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
            {
                return BadRequest("Invalid tenant context");
            }

            var totalTypes = await _context.EmergencyTypes
                .Where(t => t.TenantId == tenantId && t.IsActive)
                .CountAsync();

            var totalIncidents = await _context.EmergencyIncidents
                .Where(i => i.TenantId == tenantId)
                .CountAsync();

            var activeIncidents = await _context.EmergencyIncidents
                .Where(i => i.TenantId == tenantId && i.Status == "ACTIVE")
                .CountAsync();

            var resolvedIncidents = await _context.EmergencyIncidents
                .Where(i => i.TenantId == tenantId && i.Status == "RESOLVED")
                .CountAsync();

            var totalContacts = await _context.EmergencyContacts
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .CountAsync();

            var incidentsBySeverity = await _context.EmergencyIncidents
                .Where(i => i.TenantId == tenantId)
                .GroupBy(i => i.SeverityLevel)
                .Select(g => new
                {
                    SeverityLevel = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            // Optimize the incidents by type query to avoid Include
            var incidentsByType = await _context.EmergencyIncidents
                .Where(i => i.TenantId == tenantId)
                .Join(_context.EmergencyTypes,
                    incident => incident.EmergencyTypeId,
                    type => type.Id,
                    (incident, type) => type.Name)
                .GroupBy(typeName => typeName)
                .Select(g => new
                {
                    EmergencyType = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            return Ok(new
            {
                totalTypes = totalTypes,
                totalIncidents = totalIncidents,
                activeIncidents = activeIncidents,
                resolvedIncidents = resolvedIncidents,
                totalContacts = totalContacts,
                incidentsBySeverity = incidentsBySeverity.Select(x => new { severityLevel = x.SeverityLevel, count = x.Count }).ToArray(),
                incidentsByType = incidentsByType.Select(x => new { emergencyType = x.EmergencyType, count = x.Count }).ToArray()
            });
        }
        catch (Exception ex)
        {
            // Log the exception details for debugging
            Console.WriteLine($"EmergencyStats error: {ex}");
            return StatusCode(500, "An error occurred while retrieving emergency statistics");
        }
    }

    #endregion
}

#region DTOs

public class CreateEmergencyTypeRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public string[]? DetectionKeywords { get; set; }

    [MaxLength(20)]
    public string? SeverityLevel { get; set; } = "High";

    public bool? AutoEscalate { get; set; } = true;
    public bool? RequiresEvacuation { get; set; } = false;
    public bool? ContactEmergencyServices { get; set; } = false;
    public bool? IsActive { get; set; } = true;
}

public class UpdateEmergencyTypeRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public string[]? DetectionKeywords { get; set; }

    [MaxLength(20)]
    public string? SeverityLevel { get; set; } = "High";

    public bool? AutoEscalate { get; set; } = true;
    public bool? RequiresEvacuation { get; set; } = false;
    public bool? ContactEmergencyServices { get; set; } = false;
    public bool? IsActive { get; set; } = true;
}

public class CreateEmergencyContactRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContactType { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool? IsPrimary { get; set; } = false;
    public bool? IsActive { get; set; } = true;
}

public class UpdateEmergencyContactRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContactType { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool? IsPrimary { get; set; } = false;
    public bool? IsActive { get; set; } = true;
}

public class ResolveIncidentRequest
{
    [Required]
    public string ResolutionNotes { get; set; } = string.Empty;
}

#endregion