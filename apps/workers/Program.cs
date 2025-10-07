using Microsoft.EntityFrameworkCore;
using Serilog;
using Hostr.Workers.Data;
using Hostr.Workers.Services;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

// Database
builder.Services.AddDbContext<WorkersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Azure"),
        o => o.UseVector()));

// Services
builder.Services.AddScoped<IEmbeddingsService, EmbeddingsService>();
builder.Services.AddScoped<IRatingsService, RatingsService>();
builder.Services.AddScoped<IRetentionService, RetentionService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// Quartz
builder.Services.AddQuartz(q =>
{
    
    // Embeddings worker - runs every hour
    var embeddingsJobKey = new JobKey("EmbeddingsWorker");
    q.AddJob<EmbeddingsWorker>(opts => opts.WithIdentity(embeddingsJobKey));
    q.AddTrigger(opts => opts
        .ForJob(embeddingsJobKey)
        .WithIdentity("EmbeddingsWorker-trigger")
        .WithCronSchedule("0 0 * * * ?")); // Every hour
    
    // Ratings scheduler - runs every 15 minutes
    var ratingsJobKey = new JobKey("RatingsScheduler");
    q.AddJob<RatingsScheduler>(opts => opts.WithIdentity(ratingsJobKey));
    q.AddTrigger(opts => opts
        .ForJob(ratingsJobKey)
        .WithIdentity("RatingsScheduler-trigger")
        .WithCronSchedule("0 */15 * * * ?")); // Every 15 minutes
    
    // Retention worker - runs daily at 2 AM
    var retentionJobKey = new JobKey("RetentionWorker");
    q.AddJob<RetentionWorker>(opts => opts.WithIdentity(retentionJobKey));
    q.AddTrigger(opts => opts
        .ForJob(retentionJobKey)
        .WithIdentity("RetentionWorker-trigger")
        .WithCronSchedule("0 0 2 * * ?")); // Daily at 2 AM
    
    // Analytics rollup - runs daily at 1 AM
    var analyticsJobKey = new JobKey("AnalyticsRollupWorker");
    q.AddJob<AnalyticsRollupWorker>(opts => opts.WithIdentity(analyticsJobKey));
    q.AddTrigger(opts => opts
        .ForJob(analyticsJobKey)
        .WithIdentity("AnalyticsRollupWorker-trigger")
        .WithCronSchedule("0 0 1 * * ?")); // Daily at 1 AM
});

builder.Services.AddQuartzHostedService();

var host = builder.Build();

// Apply migrations on startup
using (var scope = host.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<WorkersDbContext>();
    await context.Database.MigrateAsync();
}

await host.RunAsync();