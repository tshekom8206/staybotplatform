using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public class ResponseTemplateService : IResponseTemplateService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<ResponseTemplateService> _logger;
    private readonly Dictionary<int, Dictionary<string, string>> _tenantVariableCache = new();
    private readonly object _cacheLock = new();

    public ResponseTemplateService(HostrDbContext context, ILogger<ResponseTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> GetTemplateAsync(int tenantId, string templateKey, string language = "en")
    {
        try
        {
            var template = await _context.ResponseTemplates
                .Where(rt => rt.TenantId == tenantId && rt.TemplateKey == templateKey && rt.Language == language && rt.IsActive)
                .OrderBy(rt => rt.Priority)
                .FirstOrDefaultAsync();

            if (template == null && language != "en")
            {
                _logger.LogWarning("Template {TemplateKey} not found for tenant {TenantId} in language {Language}, falling back to English",
                    templateKey, tenantId, language);

                template = await _context.ResponseTemplates
                    .Where(rt => rt.TenantId == tenantId && rt.TemplateKey == templateKey && rt.Language == "en" && rt.IsActive)
                    .OrderBy(rt => rt.Priority)
                    .FirstOrDefaultAsync();
            }

            if (template == null)
            {
                _logger.LogError("Template {TemplateKey} not found for tenant {TenantId}", templateKey, tenantId);
                return $"Template '{templateKey}' not found for tenant {tenantId}";
            }

            return template.Template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template {TemplateKey} for tenant {TenantId}", templateKey, tenantId);
            return $"Error retrieving template: {ex.Message}";
        }
    }

    public async Task<ProcessedTemplate> ProcessTemplateAsync(int tenantId, string templateKey, Dictionary<string, object> variables, string language = "en")
    {
        try
        {
            var template = await GetTemplateAsync(tenantId, templateKey, language);

            if (template.StartsWith("Template '") || template.StartsWith("Error retrieving"))
            {
                return new ProcessedTemplate
                {
                    Content = template,
                    MissingVariables = new List<string> { templateKey }
                };
            }

            return await ProcessTemplateStringAsync(template, variables, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing template {TemplateKey} for tenant {TenantId}", templateKey, tenantId);
            return new ProcessedTemplate
            {
                Content = $"Error processing template: {ex.Message}",
                MissingVariables = new List<string>()
            };
        }
    }

    public async Task<ProcessedTemplate> ProcessTemplateWithFallbackAsync(int tenantId, string templateKey, Dictionary<string, object> variables, string fallbackTemplate, string language = "en")
    {
        var result = await ProcessTemplateAsync(tenantId, templateKey, variables, language);

        if (result.Content.StartsWith("Template '") || result.Content.StartsWith("Error"))
        {
            _logger.LogWarning("Using fallback template for {TemplateKey} (tenant {TenantId})", templateKey, tenantId);
            return await ProcessTemplateStringAsync(fallbackTemplate, variables, tenantId);
        }

        return result;
    }

    public async Task<Dictionary<string, string>> GetTenantVariablesAsync(int tenantId)
    {
        lock (_cacheLock)
        {
            if (_tenantVariableCache.ContainsKey(tenantId))
            {
                return _tenantVariableCache[tenantId];
            }
        }

        try
        {
            var variables = await _context.ResponseVariables
                .Where(rv => rv.TenantId == tenantId && rv.IsActive)
                .ToDictionaryAsync(rv => rv.VariableName, rv => rv.VariableValue);

            // Add built-in variables from HotelInfo and Tenant
            var hotelInfo = await _context.HotelInfos
                .Where(hi => hi.TenantId == tenantId)
                .FirstOrDefaultAsync();

            var tenant = await _context.Tenants
                .Where(t => t.Id == tenantId)
                .FirstOrDefaultAsync();

            if (hotelInfo != null)
            {
                variables.TryAdd(ResponseVariableNames.Phone, hotelInfo.Phone ?? "");
                variables.TryAdd(ResponseVariableNames.Email, hotelInfo.Email ?? "");
                variables.TryAdd(ResponseVariableNames.Website, hotelInfo.Website ?? "");
            }

            if (tenant != null)
            {
                variables.TryAdd(ResponseVariableNames.HotelName, tenant.Name);
            }

            lock (_cacheLock)
            {
                _tenantVariableCache[tenantId] = variables;
            }

            return variables;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading variables for tenant {TenantId}", tenantId);
            return new Dictionary<string, string>();
        }
    }

    public async Task<string> GetVariableValueAsync(int tenantId, string variableName)
    {
        var variables = await GetTenantVariablesAsync(tenantId);
        return variables.TryGetValue(variableName, out var value) ? value : "";
    }

    public async Task SetVariableAsync(int tenantId, string variableName, string value, string? category = null)
    {
        try
        {
            var existingVariable = await _context.ResponseVariables
                .Where(rv => rv.TenantId == tenantId && rv.VariableName == variableName)
                .FirstOrDefaultAsync();

            if (existingVariable != null)
            {
                existingVariable.VariableValue = value;
                if (category != null)
                    existingVariable.Category = category;
            }
            else
            {
                _context.ResponseVariables.Add(new ResponseVariable
                {
                    TenantId = tenantId,
                    VariableName = variableName,
                    VariableValue = value,
                    Category = category
                });
            }

            await _context.SaveChangesAsync();

            // Clear cache for this tenant
            lock (_cacheLock)
            {
                _tenantVariableCache.Remove(tenantId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting variable {VariableName} for tenant {TenantId}", variableName, tenantId);
        }
    }

    public async Task<bool> TemplateExistsAsync(int tenantId, string templateKey, string language = "en")
    {
        try
        {
            return await _context.ResponseTemplates
                .AnyAsync(rt => rt.TenantId == tenantId && rt.TemplateKey == templateKey && rt.Language == language && rt.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking template existence {TemplateKey} for tenant {TenantId}", templateKey, tenantId);
            return false;
        }
    }

    public async Task<List<string>> GetMissingVariablesAsync(string template, Dictionary<string, object> providedVariables)
    {
        var variablePattern = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);
        var matches = variablePattern.Matches(template);

        var requiredVariables = matches.Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        var missingVariables = requiredVariables
            .Where(variable => !providedVariables.ContainsKey(variable))
            .ToList();

        return await Task.FromResult(missingVariables);
    }

    public async Task<string> ProcessTemplateStringAsync(string template, Dictionary<string, object> variables)
    {
        var result = await ProcessTemplateStringAsync(template, variables, null);
        return result.Content;
    }

    private async Task<ProcessedTemplate> ProcessTemplateStringAsync(string template, Dictionary<string, object> variables, int? tenantId = null)
    {
        try
        {
            var processedTemplate = new ProcessedTemplate
            {
                Content = template,
                UsedVariables = new Dictionary<string, string>(),
                MissingVariables = new List<string>()
            };

            // Get tenant variables if tenantId is provided
            Dictionary<string, string> tenantVariables = new();
            if (tenantId.HasValue)
            {
                tenantVariables = await GetTenantVariablesAsync(tenantId.Value);
            }

            // Add dynamic time variables
            var now = DateTime.UtcNow;
            var allVariables = new Dictionary<string, object>(variables)
            {
                [ResponseVariableNames.CurrentTime] = now.ToString("HH:mm"),
                [ResponseVariableNames.TimeOfDay] = GetTimeOfDay(now)
            };

            // Merge tenant variables
            foreach (var kvp in tenantVariables)
            {
                allVariables.TryAdd(kvp.Key, kvp.Value);
            }

            var variablePattern = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);
            var result = variablePattern.Replace(template, match =>
            {
                var variableName = match.Groups[1].Value;

                if (allVariables.TryGetValue(variableName, out var value))
                {
                    var stringValue = value?.ToString() ?? "";
                    processedTemplate.UsedVariables[variableName] = stringValue;
                    return stringValue;
                }

                processedTemplate.MissingVariables.Add(variableName);
                _logger.LogWarning("Missing variable {VariableName} in template processing for tenant {TenantId}",
                    variableName, tenantId);
                return $"{{{variableName}}}"; // Keep original placeholder
            });

            processedTemplate.Content = result;
            return processedTemplate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing template string for tenant {TenantId}", tenantId);
            return new ProcessedTemplate
            {
                Content = $"Error processing template: {ex.Message}",
                MissingVariables = new List<string>()
            };
        }
    }

    private static string GetTimeOfDay(DateTime dateTime)
    {
        var hour = dateTime.Hour;
        return hour switch
        {
            >= 5 and < 12 => "morning",
            >= 12 and < 17 => "afternoon",
            >= 17 and < 21 => "evening",
            _ => "night"
        };
    }
}