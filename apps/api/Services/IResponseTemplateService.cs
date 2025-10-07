using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface IResponseTemplateService
{
    Task<string> GetTemplateAsync(int tenantId, string templateKey, string language = "en");
    Task<ProcessedTemplate> ProcessTemplateAsync(int tenantId, string templateKey, Dictionary<string, object> variables, string language = "en");
    Task<ProcessedTemplate> ProcessTemplateWithFallbackAsync(int tenantId, string templateKey, Dictionary<string, object> variables, string fallbackTemplate, string language = "en");
    Task<Dictionary<string, string>> GetTenantVariablesAsync(int tenantId);
    Task<string> GetVariableValueAsync(int tenantId, string variableName);
    Task SetVariableAsync(int tenantId, string variableName, string value, string? category = null);
    Task<bool> TemplateExistsAsync(int tenantId, string templateKey, string language = "en");
    Task<List<string>> GetMissingVariablesAsync(string template, Dictionary<string, object> providedVariables);
    Task<string> ProcessTemplateStringAsync(string template, Dictionary<string, object> variables);
}