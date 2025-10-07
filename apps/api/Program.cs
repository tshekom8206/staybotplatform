using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using System.Text;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Services;
using Hostr.Api.Middleware;
using Hostr.Api.Hubs;
using Hostr.Api.Configuration;
using Hostr.Api.Jobs;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Database
builder.Services.AddDbContext<HostrDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Azure"),
        o => o.UseVector()));

// Memory Cache
builder.Services.AddMemoryCache();

// Identity
builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<HostrDbContext>()
    .AddDefaultTokenProviders();

// JWT Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        // Allow JWT tokens to be passed via query string for SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Configuration options
builder.Services.Configure<MessageClassificationOptions>(
    builder.Configuration.GetSection(MessageClassificationOptions.SectionName));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));

// Services
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<IMessageRoutingService, MessageRoutingService>();
builder.Services.AddScoped<IOpenAIService, OpenAIService>();
builder.Services.AddScoped<IPlanGuardService, PlanGuardService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IBroadcastService, BroadcastService>();
builder.Services.AddScoped<IEmergencyService, EmergencyService>();
builder.Services.AddHttpClient<IEmergencyContactService, EmergencyContactService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ILostAndFoundService, LostAndFoundService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<ITenantOnboardingService, TenantOnboardingService>();
builder.Services.AddScoped<IActionProcessingService, ActionProcessingService>();
builder.Services.AddScoped<ITenantDepartmentService, TenantDepartmentService>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<ISurveyOrchestrationService, SurveyOrchestrationService>();
builder.Services.AddScoped<IGuestLifecycleService, GuestLifecycleService>();
builder.Services.AddScoped<IDataValidationService, DataValidationService>();
builder.Services.AddSingleton<IWhatsAppRateLimiter, WhatsAppRateLimiter>();

// Agent Transfer and Human Handoff Services
builder.Services.AddScoped<IHumanTransferService, HumanTransferService>();
builder.Services.AddScoped<IConversationHandoffService, ConversationHandoffService>();

// Booking Status Management Services
builder.Services.AddScoped<IBookingStatusUpdateService, BookingStatusUpdateService>();
builder.Services.AddScoped<IBookingManagementService, BookingManagementService>();

// Phase 1: Context-Aware Chatbot Enhancement Services
builder.Services.AddScoped<ITemporalContextService, TemporalContextService>();
builder.Services.AddScoped<IConversationStateService, ConversationStateService>();
builder.Services.AddScoped<IAmbiguityDetectionService, AmbiguityDetectionService>();
builder.Services.AddScoped<ILLMIntentAnalysisService, LLMIntentAnalysisService>();

// Phase 2: Advanced Context and Business Logic Services
builder.Services.AddScoped<IClarificationStrategyService, ClarificationStrategyService>();
builder.Services.AddScoped<IContextRelevanceService, ContextRelevanceService>();
builder.Services.AddScoped<IBusinessRulesEngine, BusinessRulesEngine>();
builder.Services.AddScoped<ILLMBusinessRulesEngine, LLMBusinessRulesEngine>();

// Phase 3: Enhanced Response Generation and Flow Management Services
builder.Services.AddScoped<IEnhancedResponseGeneratorService, EnhancedResponseGeneratorService>();
builder.Services.AddScoped<IConversationFlowManagerService, ConversationFlowManagerService>();

// Phase 4: Accuracy Validation and Human-like Response Pattern Services
builder.Services.AddScoped<IAccuracyValidationService, AccuracyValidationService>();
builder.Services.AddScoped<IHumanResponsePatternService, HumanResponsePatternService>();

// Message Processing Enhancement Services
builder.Services.AddScoped<IMessageNormalizationService, MessageNormalizationService>();
builder.Services.AddScoped<ISmartContextManagerService, SmartContextManagerService>();
builder.Services.AddScoped<IResponseValidationService, ResponseValidationService>();
builder.Services.AddScoped<IConfigurationBasedResponseService, ConfigurationBasedResponseService>();
builder.Services.AddScoped<IDataSourcePriorityService, DataSourcePriorityService>();
builder.Services.AddScoped<IConfigurationFirstPipelineService, ConfigurationFirstPipelineService>();
builder.Services.AddScoped<IResponseMonitoringService, ResponseMonitoringService>();
builder.Services.AddScoped<IResponseDeduplicationService, ResponseDeduplicationService>();

// Agent Transfer and Handoff Services
builder.Services.AddScoped<IAgentAvailabilityService, AgentAvailabilityService>();

// Caching Service
builder.Services.AddScoped<ITenantCacheService, TenantCacheService>();
builder.Services.AddScoped<IResponseTemplateService, ResponseTemplateService>();

// FAQ and Knowledge Base Services
builder.Services.AddScoped<IFAQService, FAQService>();

// Audit Service
builder.Services.AddScoped<IAuditService, AuditService>();

// Upselling Service
builder.Services.AddScoped<IUpsellRecommendationService, UpsellRecommendationService>();

// TODO: Add these services when they are implemented
// builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();
// builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddHttpClient<IWhatsAppApiClient, WhatsAppApiClient>();

// Background Jobs - Quartz configuration with survey processing and guest metrics
builder.Services.AddQuartz(configure =>
{
    var surveyJobKey = new JobKey("SurveyProcessingJob");
    configure.AddJob<SurveyProcessingJob>(surveyJobKey)
        .AddTrigger(opts => opts
            .ForJob(surveyJobKey)
            .WithIdentity("SurveyProcessingJob-trigger")
            .WithCronSchedule("0 0 * * * ?") // Every hour
        );

    var metricsJobKey = new JobKey("GuestMetricsJob");
    configure.AddJob<GuestMetricsJob>(metricsJobKey)
        .AddTrigger(opts => opts
            .ForJob(metricsJobKey)
            .WithIdentity("GuestMetricsJob-trigger")
            .WithCronSchedule("0 0 2 * * ?") // Daily at 2 AM
        );

    var bookingStatusJobKey = new JobKey("BookingStatusUpdateJob");
    configure.AddJob<BookingStatusUpdateJob>(bookingStatusJobKey)
        .AddTrigger(opts => opts
            .ForJob(bookingStatusJobKey)
            .WithIdentity("BookingStatusUpdateJob-trigger")
            .WithCronSchedule("0 0 * * * ?") // Every hour
        );
});
builder.Services.AddQuartzHostedService();

// SignalR
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Hostr API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:4200",
                "http://localhost:4201",  // Angular admin UI
                "https://localhost:3000",
                "https://localhost:4200",
                "https://localhost:4201",
                "http://127.0.0.1:5500",  // For HTML test client
                "http://localhost:5500")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseMiddleware<JsonErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// SignalR Hubs
app.MapHub<StaffTaskHub>("/hubs/stafftask");
app.MapHub<TaskHub>("/hubs/tasks");

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<HostrDbContext>();
    await context.Database.MigrateAsync();
    
    // Seed mock menu data and initialize services for tenants
    var menuService = scope.ServiceProvider.GetRequiredService<IMenuService>();
    var emergencyService = scope.ServiceProvider.GetRequiredService<IEmergencyService>();
    var lostAndFoundService = scope.ServiceProvider.GetRequiredService<ILostAndFoundService>();
    var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
    
    try 
    {
        // Get all tenants and seed mock data for each one
        var tenants = await context.Tenants.ToListAsync();
        foreach (var tenant in tenants)
        {
            await menuService.SeedMockDataAsync(tenant.Id);
            await emergencyService.SeedEmergencyDataAsync(tenant.Id);
            await lostAndFoundService.SeedLostAndFoundDataAsync(tenant.Id);
        }
        
        Log.Information("Menu, emergency, and lost & found systems initialization completed successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error during system initialization");
    }
}

app.Run();