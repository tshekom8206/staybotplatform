using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;

namespace Hostr.Api.Services;

public interface IEmergencyService
{
    Task<bool> DetectEmergencyAsync(TenantContext tenantContext, string message, DateTime currentTime);
    Task<(bool IsEmergency, string EmergencyType, double Confidence)> AnalyzeEmergencyWithLLMAsync(
        TenantContext tenantContext,
        string message,
        List<(string Role, string Content)>? conversationHistory = null);
    Task<(bool IsEmergency, EmergencyIncident? Incident)> ProcessEmergencyAsync(
        TenantContext tenantContext,
        string message,
        string reporterPhone,
        int conversationId,
        string? location = null);
    Task<EmergencyIncident> CreateIncidentAsync(
        int tenantId,
        int emergencyTypeId,
        string title,
        string description,
        string reporterPhone,
        int conversationId,
        string? location = null);
    Task ExecuteEmergencyProtocolsAsync(EmergencyIncident incident);
    Task<List<EmergencyIncident>> GetActiveIncidentsAsync(int tenantId);
    Task<bool> ResolveIncidentAsync(int incidentId, int tenantId, string resolutionNotes);
    Task SeedEmergencyDataAsync(int tenantId);
}

public class EmergencyService : IEmergencyService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<EmergencyService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationService _notificationService;
    private readonly IOpenAIService _openAIService;
    private readonly IEmergencyContactService _emergencyContactService;

    public EmergencyService(
        HostrDbContext context,
        ILogger<EmergencyService> logger,
        IServiceProvider serviceProvider,
        INotificationService notificationService,
        IOpenAIService openAIService,
        IEmergencyContactService emergencyContactService)
    {
        _context = context;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _notificationService = notificationService;
        _openAIService = openAIService;
        _emergencyContactService = emergencyContactService;
    }

    public async Task<bool> DetectEmergencyAsync(TenantContext tenantContext, string message, DateTime currentTime)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);

            var messageLower = message.ToLower();

            // Stage 1: Keyword-based screening (fast initial filter)
            var emergencyTypes = await _context.EmergencyTypes
                .Where(et => et.IsActive)
                .ToListAsync();

            var keywordMatch = false;
            foreach (var emergencyType in emergencyTypes)
            {
                if (emergencyType.DetectionKeywords.Any(keyword => messageLower.Contains(keyword.ToLower())))
                {
                    keywordMatch = true;
                    _logger.LogInformation("Keyword match found for emergency type: {EmergencyType}", emergencyType.Name);
                    break;
                }
            }

            // If no keyword match, skip LLM analysis (performance optimization)
            if (!keywordMatch)
            {
                return false;
            }

            // Stage 2: LLM-enhanced analysis for context understanding
            var llmAnalysis = await AnalyzeEmergencyWithLLMAsync(tenantContext, message);

            // Consider it an emergency if LLM confidence is above threshold
            const double confidenceThreshold = 0.6;
            var isEmergency = llmAnalysis.IsEmergency && llmAnalysis.Confidence >= confidenceThreshold;

            if (isEmergency)
            {
                _logger.LogWarning("Emergency detected via LLM analysis: Type={EmergencyType}, Confidence={Confidence} - Message: {Message}",
                    llmAnalysis.EmergencyType, llmAnalysis.Confidence, message);
            }
            else if (keywordMatch)
            {
                _logger.LogInformation("Keyword match found but LLM analysis indicates no emergency (Confidence={Confidence}) - Message: {Message}",
                    llmAnalysis.Confidence, message);
            }

            return isEmergency;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting emergency for tenant {TenantId}", tenantContext.TenantId);

            // Fallback to keyword-only detection if LLM fails
            try
            {
                using var scope = new TenantScope(_context, tenantContext.TenantId);
                var messageLower = message.ToLower();
                var emergencyTypes = await _context.EmergencyTypes
                    .Where(et => et.IsActive)
                    .ToListAsync();

                return emergencyTypes.Any(et =>
                    et.DetectionKeywords.Any(keyword => messageLower.Contains(keyword.ToLower())));
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<(bool IsEmergency, string EmergencyType, double Confidence)> AnalyzeEmergencyWithLLMAsync(
        TenantContext tenantContext,
        string message,
        List<(string Role, string Content)>? conversationHistory = null)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);

            // Get active emergency types for context
            var emergencyTypes = await _context.EmergencyTypes
                .Where(et => et.IsActive)
                .Select(et => new { et.Name, et.Description, et.SeverityLevel })
                .ToListAsync();

            // Build emergency types context for LLM
            var emergencyTypesContext = string.Join("\n", emergencyTypes.Select(et =>
                $"- {et.Name} ({et.SeverityLevel}): {et.Description}"));

            // Create system prompt for emergency detection
            var systemPrompt = @"You are an emergency detection system for a hotel. Analyze guest messages to determine if they represent a genuine emergency situation.

EMERGENCY TYPES:
{emergencyTypesContext}

GUIDELINES:
- Only classify as emergency if there is immediate danger, threat to life/safety, or urgent medical situation
- Consider context and tone - distinguish between genuine emergencies and complaints/requests
- Account for emotional language that might not indicate actual emergency
- Provide confidence score (0.0-1.0) based on urgency and severity indicators
- If emergency detected, specify the most appropriate emergency type

Respond with JSON in this exact format:
{{
  ""isEmergency"": true/false,
  ""emergencyType"": ""emergency type name or null"",
  ""confidence"": 0.0-1.0,
  ""reasoning"": ""brief explanation""
}}";

            systemPrompt = systemPrompt.Replace("{emergencyTypesContext}", emergencyTypesContext);

            // Build conversation context if available
            var conversationContext = "";
            if (conversationHistory?.Any() == true)
            {
                conversationContext = "\nCONVERSATION HISTORY:\n" +
                    string.Join("\n", conversationHistory.TakeLast(5).Select(h => $"{h.Role}: {h.Content}"));
            }

            var fullContext = conversationContext + $"\n\nCURRENT MESSAGE TO ANALYZE: {message}";

            // Call OpenAI for emergency analysis
            var response = await _openAIService.GenerateResponseAsync(
                systemPrompt,
                "",
                "",
                fullContext,
                "emergency_analyzer");

            if (response?.Reply == null)
            {
                _logger.LogWarning("No response from LLM for emergency analysis");
                return (false, "", 0.0);
            }

            // Parse LLM response
            var analysisResult = ParseEmergencyAnalysis(response.Reply);

            _logger.LogInformation("LLM Emergency Analysis - Message: '{Message}' | Emergency: {IsEmergency} | Type: {Type} | Confidence: {Confidence}",
                message, analysisResult.IsEmergency, analysisResult.EmergencyType, analysisResult.Confidence);

            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM emergency analysis for tenant {TenantId}", tenantContext.TenantId);
            return (false, "", 0.0);
        }
    }

    private (bool IsEmergency, string EmergencyType, double Confidence) ParseEmergencyAnalysis(string llmResponse)
    {
        try
        {
            // Try to extract JSON from response
            var startIndex = llmResponse.IndexOf('{');
            var endIndex = llmResponse.LastIndexOf('}');

            if (startIndex >= 0 && endIndex > startIndex)
            {
                var jsonStr = llmResponse.Substring(startIndex, endIndex - startIndex + 1);
                var jsonDoc = JsonDocument.Parse(jsonStr);
                var root = jsonDoc.RootElement;

                var isEmergency = root.TryGetProperty("isEmergency", out var emergencyProp) && emergencyProp.GetBoolean();
                var emergencyType = root.TryGetProperty("emergencyType", out var typeProp) && typeProp.ValueKind != JsonValueKind.Null
                    ? typeProp.GetString() ?? "" : "";
                var confidence = root.TryGetProperty("confidence", out var confidenceProp) ? confidenceProp.GetDouble() : 0.0;

                return (isEmergency, emergencyType, confidence);
            }

            // Fallback parsing if JSON extraction fails
            var responseLower = llmResponse.ToLower();
            var isEmergencyFallback = responseLower.Contains("\"isemergency\": true") || responseLower.Contains("emergency detected");

            return (isEmergencyFallback, "", 0.5);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM emergency analysis response: {Response}", llmResponse);
            return (false, "", 0.0);
        }
    }

    public async Task<(bool IsEmergency, EmergencyIncident? Incident)> ProcessEmergencyAsync(
        TenantContext tenantContext,
        string message,
        string reporterPhone,
        int conversationId,
        string? location = null)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);

            // Use LLM-enhanced detection for better emergency type classification
            var llmAnalysis = await AnalyzeEmergencyWithLLMAsync(tenantContext, message);

            if (!llmAnalysis.IsEmergency || llmAnalysis.Confidence < 0.6)
            {
                _logger.LogInformation("LLM analysis indicates no emergency or low confidence: {Confidence}", llmAnalysis.Confidence);
                return (false, null);
            }

            // Get all emergency types for matching
            var emergencyTypes = await _context.EmergencyTypes
                .Where(et => et.IsActive)
                .ToListAsync();

            // First try to match by LLM-identified emergency type name
            var matchedType = emergencyTypes.FirstOrDefault(et =>
                et.Name.Equals(llmAnalysis.EmergencyType, StringComparison.OrdinalIgnoreCase));

            // Fallback to keyword-based matching if LLM type doesn't match exactly
            if (matchedType == null)
            {
                var messageLower = message.ToLower();
                matchedType = emergencyTypes.FirstOrDefault(et =>
                    et.DetectionKeywords.Any(keyword => messageLower.Contains(keyword.ToLower())));
            }

            // If still no match, use the first emergency type as fallback
            if (matchedType == null)
            {
                matchedType = emergencyTypes.FirstOrDefault();
                _logger.LogWarning("No specific emergency type matched, using default: {EmergencyType}", matchedType?.Name);
            }

            if (matchedType == null)
            {
                _logger.LogError("No active emergency types configured for tenant {TenantId}", tenantContext.TenantId);
                return (false, null);
            }

            // Create emergency incident with enhanced title including LLM confidence
            var enhancedTitle = !string.IsNullOrEmpty(llmAnalysis.EmergencyType)
                ? $"{llmAnalysis.EmergencyType} Emergency (AI Confidence: {llmAnalysis.Confidence:P0})"
                : $"{matchedType.Name} Emergency Reported";

            var incident = await CreateIncidentAsync(
                tenantContext.TenantId,
                matchedType.Id,
                enhancedTitle,
                message,
                reporterPhone,
                conversationId,
                location);

            // Execute emergency protocols if auto-escalation is enabled
            if (matchedType.AutoEscalate)
            {
                _ = Task.Run(async () => await ExecuteEmergencyProtocolsAsync(incident));
            }

            _logger.LogCritical("Emergency incident created via LLM analysis: {IncidentId} - Type: {EmergencyType} - Confidence: {Confidence} - Reporter: {Reporter}",
                incident.Id, matchedType.Name, llmAnalysis.Confidence, reporterPhone);

            return (true, incident);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing emergency for tenant {TenantId}", tenantContext.TenantId);
            return (false, null);
        }
    }

    public async Task<EmergencyIncident> CreateIncidentAsync(
        int tenantId, 
        int emergencyTypeId, 
        string title, 
        string description, 
        string reporterPhone, 
        int conversationId,
        string? location = null)
    {
        using var scope = new TenantScope(_context, tenantId);

        var emergencyType = await _context.EmergencyTypes.FindAsync(emergencyTypeId);
        
        var incident = new EmergencyIncident
        {
            TenantId = tenantId,
            EmergencyTypeId = emergencyTypeId,
            ConversationId = conversationId,
            Title = title,
            Description = description,
            SeverityLevel = emergencyType?.SeverityLevel ?? "High",
            ReportedBy = reporterPhone,
            Location = location,
            Status = "ACTIVE",
            ReportedAt = DateTime.UtcNow
        };

        _context.EmergencyIncidents.Add(incident);
        await _context.SaveChangesAsync();

        // Send real-time emergency notification
        if (emergencyType != null)
        {
            await _notificationService.NotifyEmergencyIncidentAsync(tenantId, incident, emergencyType);
        }

        return incident;
    }

    public async Task ExecuteEmergencyProtocolsAsync(EmergencyIncident incident)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var scopedContext = scope.ServiceProvider.GetRequiredService<HostrDbContext>();
            
            using var tenantScope = new TenantScope(scopedContext, incident.TenantId);

            // Get protocols for this emergency type
            var protocols = await scopedContext.EmergencyProtocols
                .Where(p => p.EmergencyTypeId == incident.EmergencyTypeId && p.IsActive)
                .OrderBy(p => p.ExecutionOrder)
                .ToListAsync();

            var responseActions = new List<object>();

            foreach (var protocol in protocols)
            {
                try
                {
                    // Check trigger condition
                    if (!ShouldExecuteProtocol(protocol, incident))
                        continue;

                    // Notify guests if required
                    if (protocol.NotifyGuests && !string.IsNullOrEmpty(protocol.GuestMessage))
                    {
                        var scopedBroadcastService = scope.ServiceProvider.GetRequiredService<IBroadcastService>();
                        var broadcastResult = await scopedBroadcastService.SendEmergencyBroadcastAsync(
                            incident.TenantId,
                            "emergency",
                            protocol.GuestMessage,
                            null,
                            "Emergency System",
                            BroadcastScope.ActiveOnly);

                        responseActions.Add(new
                        {
                            Action = "GuestNotification",
                            Success = broadcastResult.Success,
                            Message = broadcastResult.Message,
                            Recipients = broadcastResult.BroadcastId
                        });

                        _logger.LogInformation("Emergency guest notification sent: {Result}", broadcastResult.Message);
                    }

                    // Create staff task if required
                    if (protocol.NotifyStaff)
                    {
                        var emergencyRequestItemId = await GetEmergencyRequestItemId(incident.TenantId);
                        
                        if (emergencyRequestItemId.HasValue)
                        {
                            var staffTask = new StaffTask
                            {
                                TenantId = incident.TenantId,
                                ConversationId = incident.ConversationId,
                                RequestItemId = emergencyRequestItemId.Value,
                                TaskType = "emergency_response",
                                Status = "Open",
                                Priority = "Urgent",
                                Notes = $"EMERGENCY: {protocol.Title}\n\n{protocol.ProcedureSteps}\n\nIncident: {incident.Description}",
                                CreatedAt = DateTime.UtcNow
                            };

                            scopedContext.StaffTasks.Add(staffTask);
                            await scopedContext.SaveChangesAsync();

                            // Send real-time staff task notification
                            var scopedNotificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                            await scopedNotificationService.NotifyTaskCreatedAsync(incident.TenantId, staffTask, "Emergency Response");

                            responseActions.Add(new
                            {
                                Action = "StaffTaskCreated",
                                TaskId = staffTask.Id,
                                Priority = "Urgent"
                            });

                            _logger.LogInformation("Emergency staff task created: {TaskId}", staffTask.Id);
                        }
                        else
                        {
                            responseActions.Add(new
                            {
                                Action = "StaffTaskCreated",
                                Status = "Failed",
                                Reason = "No emergency request item configured"
                            });

                            _logger.LogWarning("Cannot create emergency staff task - no emergency request item configured for tenant {TenantId}", incident.TenantId);
                        }
                    }

                    // Contact emergency services if protocol requires it
                    if (protocol.NotifyEmergencyServices)
                    {
                        try
                        {
                            // Build emergency contact list from database
                            var dbContacts = await scopedContext.Set<Models.EmergencyContact>()
                                .Where(ec => ec.TenantId == incident.TenantId)
                                .ToListAsync();

                            var emergencyContacts = dbContacts.Select(ec => new Services.EmergencyContact
                            {
                                Name = ec.Name,
                                PhoneNumber = ec.PhoneNumber,
                                ContactType = "SMS",
                                Priority = 1
                            }).ToList();

                            if (emergencyContacts.Any())
                            {
                                var contactResult = await _emergencyContactService.NotifyEmergencyServicesAsync(
                                    incident,
                                    protocol,
                                    emergencyContacts);

                                responseActions.Add(new
                                {
                                    Action = "EmergencyServicesContact",
                                    Status = contactResult.Success ? "Success" : "Failed",
                                    Attempts = contactResult.Attempts.Count,
                                    AttemptedAt = contactResult.AttemptedAt,
                                    FailureReason = contactResult.FailureReason
                                });

                                if (contactResult.Success)
                                {
                                    _logger.LogInformation("Emergency services successfully contacted for incident {IncidentId}", incident.Id);
                                }
                                else
                                {
                                    _logger.LogError("Failed to contact emergency services for incident {IncidentId}: {Reason}",
                                        incident.Id, contactResult.FailureReason);
                                }
                            }
                            else
                            {
                                responseActions.Add(new
                                {
                                    Action = "EmergencyServicesContact",
                                    Status = "NoContactsConfigured",
                                    Note = "No emergency contacts configured for this tenant"
                                });

                                _logger.LogWarning("No emergency contacts configured for tenant {TenantId}, incident {IncidentId}",
                                    incident.TenantId, incident.Id);
                            }
                        }
                        catch (Exception contactEx)
                        {
                            _logger.LogError(contactEx, "Error contacting emergency services for incident {IncidentId}", incident.Id);
                            responseActions.Add(new
                            {
                                Action = "EmergencyServicesContact",
                                Status = "Error",
                                Error = contactEx.Message
                            });
                        }
                    }

                }
                catch (Exception protocolEx)
                {
                    _logger.LogError(protocolEx, "Error executing protocol {ProtocolId} for incident {IncidentId}", 
                        protocol.Id, incident.Id);
                }
            }

            // Update incident with response actions
            incident.ResponseActions = JsonDocument.Parse(JsonSerializer.Serialize(responseActions));
            await scopedContext.SaveChangesAsync();

            _logger.LogInformation("Emergency protocols executed for incident {IncidentId}: {ActionCount} actions", 
                incident.Id, responseActions.Count);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing emergency protocols for incident {IncidentId}", incident.Id);
        }
    }

    public async Task<List<EmergencyIncident>> GetActiveIncidentsAsync(int tenantId)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        return await _context.EmergencyIncidents
            .Include(i => i.EmergencyType)
            .Include(i => i.Conversation)
            .Where(i => i.Status == "ACTIVE")
            .OrderByDescending(i => i.ReportedAt)
            .ToListAsync();
    }

    public async Task<bool> ResolveIncidentAsync(int incidentId, int tenantId, string resolutionNotes)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantId);
            
            var incident = await _context.EmergencyIncidents.FindAsync(incidentId);
            if (incident == null || incident.TenantId != tenantId)
                return false;

            incident.Status = "RESOLVED";
            incident.ResolvedAt = DateTime.UtcNow;
            incident.ResolutionNotes = resolutionNotes;

            await _context.SaveChangesAsync();

            // Send real-time notification for emergency status update
            var emergencyType = await _context.EmergencyTypes.FindAsync(incident.EmergencyTypeId);
            if (emergencyType != null)
            {
                await _notificationService.NotifyEmergencyStatusUpdatedAsync(tenantId, incident, emergencyType);
            }

            _logger.LogInformation("Emergency incident resolved: {IncidentId} - Notes: {Notes}", 
                incidentId, resolutionNotes);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving incident {IncidentId}", incidentId);
            return false;
        }
    }

    public async Task SeedEmergencyDataAsync(int tenantId)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        // Check if emergency data already exists
        var hasEmergencyTypes = await _context.EmergencyTypes.AnyAsync();
        if (hasEmergencyTypes)
        {
            _logger.LogInformation("Emergency data already exists for tenant {TenantId}", tenantId);
            return;
        }

        _logger.LogInformation("Emergency system initialized for tenant {TenantId} - no default data seeded. Configure emergency types through admin interface.", tenantId);
    }

    private bool ShouldExecuteProtocol(EmergencyProtocol protocol, EmergencyIncident incident)
    {
        return protocol.TriggerCondition switch
        {
            "IMMEDIATE" => true,
            "SEVERITY_HIGH" => incident.SeverityLevel == "High" || incident.SeverityLevel == "Critical",
            "SEVERITY_CRITICAL" => incident.SeverityLevel == "Critical",
            "MANUAL" => false, // Manual protocols require staff intervention
            _ => true
        };
    }

    private async Task<int?> GetEmergencyRequestItemId(int tenantId)
    {
        // Get existing emergency request item - do not create automatically
        var emergencyItem = await _context.RequestItems
            .FirstOrDefaultAsync(ri => ri.Category == "emergency");

        return emergencyItem?.Id;
    }
}