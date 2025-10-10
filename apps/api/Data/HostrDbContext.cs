using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Hostr.Api.Models;
using Pgvector.EntityFrameworkCore;

namespace Hostr.Api.Data;

public class HostrDbContext : IdentityDbContext<User, IdentityRole<int>, int>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public HostrDbContext(DbContextOptions<HostrDbContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<UserTenant> UserTenants { get; set; }
    public DbSet<WhatsAppNumber> WhatsAppNumbers { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<FAQ> FAQs { get; set; }
    public DbSet<KnowledgeBaseChunk> KnowledgeBaseChunks { get; set; }
    public DbSet<UpsellItem> UpsellItems { get; set; }
    public DbSet<GuideItem> GuideItems { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Rating> Ratings { get; set; }
    public DbSet<RequestItem> RequestItems { get; set; }
    public DbSet<StaffTask> StaffTasks { get; set; }
    public DbSet<StockEvent> StockEvents { get; set; }
    public DbSet<WhatsAppTemplate> WhatsAppTemplates { get; set; }
    public DbSet<QuickReply> QuickReplies { get; set; }
    public DbSet<UsageDaily> UsageDaily { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<MenuCategory> MenuCategories { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<MenuSpecial> MenuSpecials { get; set; }
    public DbSet<BusinessInfo> BusinessInfo { get; set; }
    public DbSet<InformationItem> InformationItems { get; set; }
    public DbSet<BroadcastMessage> BroadcastMessages { get; set; }
    public DbSet<BroadcastRecipient> BroadcastRecipients { get; set; }
    public DbSet<BroadcastTemplate> BroadcastTemplates { get; set; }
    public DbSet<ServiceCategory> ServiceCategories { get; set; }
    public DbSet<ConciergeService> ConciergeServices { get; set; }
    public DbSet<LocalProvider> LocalProviders { get; set; }
    public DbSet<EmergencyType> EmergencyTypes { get; set; }
    public DbSet<EmergencyIncident> EmergencyIncidents { get; set; }
    public DbSet<EmergencyProtocol> EmergencyProtocols { get; set; }
    public DbSet<EmergencyContact> EmergencyContacts { get; set; }
    public DbSet<EmergencyContactAttempt> EmergencyContactAttempts { get; set; }

    // Maintenance models
    public DbSet<MaintenanceItem> MaintenanceItems { get; set; }
    public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
    public DbSet<MaintenanceHistory> MaintenanceHistory { get; set; }
    
    // Booking modification models
    public DbSet<BookingModification> BookingModifications { get; set; }

    // Hotel information models
    public DbSet<HotelInfo> HotelInfos { get; set; }
    public DbSet<HotelCategory> HotelCategories { get; set; }
    public DbSet<SupportedLanguage> SupportedLanguages { get; set; }
    public DbSet<HotelFeature> HotelFeatures { get; set; }
    public DbSet<SupportedCurrency> SupportedCurrencies { get; set; }
    public DbSet<BookingChangeHistory> BookingChangeHistory { get; set; }

    // Agent management models - DISABLED: Using Users table instead
    // public DbSet<Agent> Agents { get; set; }
    // public DbSet<AgentSession> AgentSessions { get; set; }

    // Hotel Services
    public DbSet<Service> Services { get; set; }
    public DbSet<ServiceIcon> ServiceIcons { get; set; }

    // Business Rules
    public DbSet<ServiceBusinessRule> ServiceBusinessRules { get; set; }
    public DbSet<RequestItemRule> RequestItemRules { get; set; }

    // Tenant Department Configuration
    public DbSet<TenantDepartment> TenantDepartments { get; set; }
    public DbSet<ServiceDepartmentMapping> ServiceDepartmentMappings { get; set; }
    
    // Lost and Found models
    public DbSet<LostItem> LostItems { get; set; }
    public DbSet<FoundItem> FoundItems { get; set; }
    public DbSet<LostAndFoundMatch> LostAndFoundMatches { get; set; }
    public DbSet<LostAndFoundCategory> LostAndFoundCategories { get; set; }
    public DbSet<LostAndFoundNotification> LostAndFoundNotifications { get; set; }

    // Conversation State Tracking
    public DbSet<ConversationStateRecord> ConversationStateRecords { get; set; }

    // Welcome Message models
    public DbSet<WelcomeMessage> WelcomeMessages { get; set; }

    // Guest Rating models
    public DbSet<GuestRating> GuestRatings { get; set; }
    public DbSet<PostStaySurvey> PostStaySurveys { get; set; }

    // Business Metrics models
    public DbSet<GuestBusinessMetrics> GuestBusinessMetrics { get; set; }

    // Upsell Metrics models
    public DbSet<UpsellMetric> UpsellMetrics { get; set; }

    // Phase 3: Conversation Flow Management
    public DbSet<ConversationFlow> ConversationFlows { get; set; }
    public DbSet<FlowStep> FlowSteps { get; set; }

    // Response Template models
    public DbSet<ResponseTemplate> ResponseTemplates { get; set; }
    public DbSet<ResponseVariable> ResponseVariables { get; set; }

    // Agent Transfer models
    public DbSet<ConversationTransfer> ConversationTransfers { get; set; }

    // Push Notification models
    public DbSet<PushSubscription> PushSubscriptions { get; set; }

    // User Notification Read tracking
    public DbSet<UserNotificationRead> UserNotificationReads { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Vector extension
        builder.HasPostgresExtension("vector");
        builder.HasPostgresExtension("pg_trgm");

        // Configure entities
        ConfigureTenancy(builder);
        ConfigureConversations(builder);
        ConfigureCatalog(builder);
        ConfigureTasks(builder);
        ConfigureAgents(builder);
        ConfigureTemplates(builder);
        ConfigureMenu(builder);
        ConfigureConciergeServices(builder);
        ConfigureEmergency(builder);
        ConfigureMaintenance(builder);
        ConfigureBookingModifications(builder);
        ConfigureLostAndFound(builder);
        ConfigureWelcomeMessages(builder);
        ConfigureDepartments(builder);
        ConfigureBusinessMetrics(builder);
        ConfigureBusinessRules(builder);
        ConfigureAgentTransfer(builder);
        ConfigureIndexes(builder);

        // Global query filters for tenant isolation
        ApplyGlobalFilters(builder);
    }

    private void ConfigureTenancy(ModelBuilder builder)
    {
        builder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<UserTenant>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.TenantId });
            entity.HasOne(e => e.User).WithMany(e => e.UserTenants).HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Tenant).WithMany(e => e.UserTenants).HasForeignKey(e => e.TenantId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<WhatsAppNumber>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.WhatsAppNumbers).HasForeignKey(e => e.TenantId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureConversations(ModelBuilder builder)
    {
        builder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.Conversations).HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.AssignedAgent)
                .WithMany()
                .HasForeignKey(e => e.AssignedAgentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.Messages).HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.Conversation).WithMany(e => e.Messages).HasForeignKey(e => e.ConversationId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<FAQ>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.FAQs).HasForeignKey(e => e.TenantId);
            entity.Property(e => e.Tags)
                .HasColumnType("text[]");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<KnowledgeBaseChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.KnowledgeBaseChunks).HasForeignKey(e => e.TenantId);
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureCatalog(ModelBuilder builder)
    {
        builder.Entity<UpsellItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.UpsellItems).HasForeignKey(e => e.TenantId);
            entity.Property(e => e.Categories)
                .HasColumnType("text[]"); // Use PostgreSQL array type directly
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<GuideItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.GuideItems).HasForeignKey(e => e.TenantId);
            entity.Property(e => e.LocationJson).HasColumnType("jsonb");
            entity.Property(e => e.OpenHoursJson).HasColumnType("jsonb");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.Bookings).HasForeignKey(e => e.TenantId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<Rating>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.Ratings).HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.Booking).WithMany(e => e.Ratings).HasForeignKey(e => e.BookingId);
            entity.HasOne(e => e.Conversation).WithMany(e => e.Ratings).HasForeignKey(e => e.ConversationId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureTasks(ModelBuilder builder)
    {
        builder.Entity<RequestItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.RequestItems).HasForeignKey(e => e.TenantId);
            entity.Property(e => e.ServiceHours).HasColumnType("jsonb");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<StaffTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.StaffTasks).HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.Conversation).WithMany(e => e.StaffTasks).HasForeignKey(e => e.ConversationId);
            entity.HasOne(e => e.RequestItem).WithMany(e => e.StaffTasks).HasForeignKey(e => e.RequestItemId);
            entity.HasOne(e => e.Booking).WithMany().HasForeignKey(e => e.BookingId);
            entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedBy);
            entity.HasOne(e => e.AssignedToUser).WithMany().HasForeignKey(e => e.AssignedToId);
            entity.HasOne(e => e.CompletedByUser).WithMany().HasForeignKey(e => e.CompletedBy);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<StockEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.RequestItem).WithMany(e => e.StockEvents).HasForeignKey(e => e.RequestItemId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureAgents(ModelBuilder builder)
    {
        builder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.Skills)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Metadata.SetValueComparer(new ValueComparer<string[]>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToArray()));
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<AgentSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Agent).WithMany(e => e.Sessions).HasForeignKey(e => e.AgentId);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.SessionStarted).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastHeartbeat).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureTemplates(ModelBuilder builder)
    {
        builder.Entity<WhatsAppTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.WhatsAppTemplates).HasForeignKey(e => e.TenantId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<QuickReply>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.QuickReplies).HasForeignKey(e => e.TenantId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<UsageDaily>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.UsageDaily).HasForeignKey(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.Date }).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(e => e.AuditLogs).HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.ActorUser).WithMany().HasForeignKey(e => e.ActorUserId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureMenu(ModelBuilder builder)
    {
        builder.Entity<MenuCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.MenuCategory).WithMany(e => e.MenuItems).HasForeignKey(e => e.MenuCategoryId);
            entity.Property(e => e.Tags)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Metadata.SetValueComparer(new ValueComparer<string[]>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToArray()));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<MenuSpecial>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.MenuItem).WithMany().HasForeignKey(e => e.MenuItemId);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<BusinessInfo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.Tags)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Metadata.SetValueComparer(new ValueComparer<string[]>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToArray()));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<InformationItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.Keywords)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Metadata.SetValueComparer(new ValueComparer<string[]>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToArray()));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureConciergeServices(ModelBuilder builder)
    {
        builder.Entity<ServiceCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<ConciergeService>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.ServiceCategory).WithMany(e => e.ConciergeServices).HasForeignKey(e => e.ServiceCategoryId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<LocalProvider>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.ServiceCategory).WithMany(e => e.LocalProviders).HasForeignKey(e => e.ServiceCategoryId);
            entity.HasOne(e => e.ConciergeService).WithMany(e => e.LocalProviders).HasForeignKey(e => e.ConciergeServiceId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureIndexes(ModelBuilder builder)
    {
        // Conversations
        builder.Entity<Conversation>()
            .HasIndex(e => new { e.TenantId, e.WaUserPhone });

        // Messages
        builder.Entity<Message>()
            .HasIndex(e => new { e.TenantId, e.ConversationId, e.CreatedAt });

        // FAQs with trigram index
        builder.Entity<FAQ>()
            .HasIndex(e => e.Question)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        // Knowledge base chunks with vector index
        builder.Entity<KnowledgeBaseChunk>()
            .HasIndex(e => e.Embedding)
            .HasMethod("ivfflat")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("lists", 100);

        // Ratings
        builder.Entity<Rating>()
            .HasIndex(e => new { e.TenantId, e.ReceivedAt });
        builder.Entity<Rating>()
            .HasIndex(e => new { e.TenantId, e.Source });

        // Guest Ratings - Optimized for reports and queries
        builder.Entity<GuestRating>()
            .HasIndex(e => new { e.TenantId, e.CreatedAt });
        builder.Entity<GuestRating>()
            .HasIndex(e => new { e.TenantId, e.Department });
        builder.Entity<GuestRating>()
            .HasIndex(e => new { e.TenantId, e.Rating });
        builder.Entity<GuestRating>()
            .HasIndex(e => new { e.TenantId, e.CollectionMethod });
        builder.Entity<GuestRating>()
            .HasIndex(e => e.TaskId);

        // Post Stay Surveys - Optimized for completion tracking
        builder.Entity<PostStaySurvey>()
            .HasIndex(e => new { e.TenantId, e.IsCompleted });
        builder.Entity<PostStaySurvey>()
            .HasIndex(e => new { e.TenantId, e.CompletedAt })
            .HasFilter("\"CompletedAt\" IS NOT NULL");
        builder.Entity<PostStaySurvey>()
            .HasIndex(e => e.SurveyToken)
            .IsUnique();
        builder.Entity<PostStaySurvey>()
            .HasIndex(e => new { e.TenantId, e.SentSuccessfully });
        builder.Entity<PostStaySurvey>()
            .HasIndex(e => new { e.TenantId, e.SentAt });

        // Business Metrics - Optimized for reporting
        builder.Entity<GuestBusinessMetrics>()
            .HasIndex(e => new { e.TenantId, e.LifetimeValue });
        builder.Entity<GuestBusinessMetrics>()
            .HasIndex(e => new { e.TenantId, e.AverageSatisfaction });
        builder.Entity<GuestBusinessMetrics>()
            .HasIndex(e => new { e.TenantId, e.TotalStays });
    }

    private void ApplyGlobalFilters(ModelBuilder builder)
    {
        var tenantId = GetCurrentTenantId();
        
        if (tenantId.HasValue)
        {
            builder.Entity<Conversation>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<Message>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<FAQ>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<KnowledgeBaseChunk>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<UpsellItem>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<GuideItem>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<Booking>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<Rating>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<RequestItem>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<StaffTask>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<StockEvent>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<WhatsAppTemplate>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<QuickReply>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<UsageDaily>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<AuditLog>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<WhatsAppNumber>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<MenuCategory>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<MenuItem>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<MenuSpecial>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<BusinessInfo>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<InformationItem>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<ServiceCategory>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<ConciergeService>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<LocalProvider>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<EmergencyType>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<EmergencyIncident>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<EmergencyProtocol>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<EmergencyContact>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<WelcomeMessage>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<GuestBusinessMetrics>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<ServiceBusinessRule>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<RequestItemRule>().HasQueryFilter(e => e.TenantId == tenantId);
        }
    }

    private void ConfigureEmergency(ModelBuilder builder)
    {
        builder.Entity<EmergencyType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.DetectionKeywords)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Metadata.SetValueComparer(new ValueComparer<string[]>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToArray()));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        builder.Entity<EmergencyIncident>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.EmergencyType).WithMany(e => e.EmergencyIncidents).HasForeignKey(e => e.EmergencyTypeId);
            entity.HasOne(e => e.Conversation).WithMany().HasForeignKey(e => e.ConversationId);
            entity.Property(e => e.AffectedAreas)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Metadata.SetValueComparer(new ValueComparer<string[]>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToArray()));
            entity.Property(e => e.ResponseActions).HasColumnType("jsonb");
            entity.Property(e => e.ReportedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        builder.Entity<EmergencyProtocol>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.EmergencyType).WithMany(e => e.EmergencyProtocols).HasForeignKey(e => e.EmergencyTypeId);
            entity.Property(e => e.EmergencyContacts)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Metadata.SetValueComparer(new ValueComparer<string[]>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToArray()));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        builder.Entity<EmergencyContact>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private int? GetCurrentTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items["TenantId"] is int tenantId)
        {
            return tenantId;
        }
        return null;
    }

    private void ConfigureMaintenance(ModelBuilder builder)
    {
        builder.Entity<MaintenanceItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<MaintenanceRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.MaintenanceItem).WithMany(i => i.MaintenanceRequests).HasForeignKey(e => e.MaintenanceItemId);
            entity.HasOne(e => e.Conversation).WithMany().HasForeignKey(e => e.ConversationId);
            entity.HasOne(e => e.AssignedToUser).WithMany().HasForeignKey(e => e.AssignedTo);
            entity.Property(e => e.ReportedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<MaintenanceHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.MaintenanceItem).WithMany(i => i.MaintenanceHistory).HasForeignKey(e => e.MaintenanceItemId);
            entity.HasOne(e => e.MaintenanceRequest).WithMany().HasForeignKey(e => e.MaintenanceRequestId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureBookingModifications(ModelBuilder builder)
    {
        builder.Entity<BookingModification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.Booking).WithMany().HasForeignKey(e => e.BookingId);
            entity.HasOne(e => e.Conversation).WithMany().HasForeignKey(e => e.ConversationId);
            entity.HasOne(e => e.ProcessedByUser).WithMany().HasForeignKey(e => e.ProcessedBy);
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<BookingChangeHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.Booking).WithMany().HasForeignKey(e => e.BookingId);
            entity.HasOne(e => e.BookingModification).WithMany().HasForeignKey(e => e.BookingModificationId);
            entity.Property(e => e.ChangedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureLostAndFound(ModelBuilder builder)
    {
        builder.Entity<LostItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.Conversation).WithMany().HasForeignKey(e => e.ConversationId);
            entity.Property(e => e.ReportedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<FoundItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.FoundAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<LostAndFoundMatch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.LostItem).WithMany(l => l.LostItemMatches).HasForeignKey(e => e.LostItemId);
            entity.HasOne(e => e.FoundItem).WithMany(f => f.FoundItemMatches).HasForeignKey(e => e.FoundItemId);
            entity.HasOne(e => e.VerifiedByUser).WithMany().HasForeignKey(e => e.VerifiedBy);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<LostAndFoundCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.Keywords)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Metadata.SetValueComparer(new ValueComparer<string[]>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToArray()));
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<LostAndFoundNotification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.LostItem).WithMany().HasForeignKey(e => e.LostItemId);
            entity.HasOne(e => e.FoundItem).WithMany().HasForeignKey(e => e.FoundItemId);
            entity.HasOne(e => e.Match).WithMany().HasForeignKey(e => e.MatchId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureWelcomeMessages(ModelBuilder builder)
    {
        builder.Entity<WelcomeMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureDepartments(ModelBuilder builder)
    {
        builder.Entity<TenantDepartment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Unique constraint: One department per tenant
            entity.HasIndex(e => new { e.TenantId, e.DepartmentName }).IsUnique();
        });

        builder.Entity<ServiceDepartmentMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Department)
                .WithMany(d => d.ServiceMappings)
                .HasForeignKey(e => e.TargetDepartment)
                .HasPrincipalKey(d => d.DepartmentName)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Unique constraint: One mapping per service category per tenant
            entity.HasIndex(e => new { e.TenantId, e.ServiceCategory }).IsUnique();
        });
    }

    private void ConfigureBusinessMetrics(ModelBuilder builder)
    {
        builder.Entity<GuestBusinessMetrics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Unique constraint: One metrics record per phone number per tenant
            entity.HasIndex(e => new { e.TenantId, e.PhoneNumber }).IsUnique();
        });

        // Update Booking entity to include foreign key relationships
        builder.Entity<Booking>()
            .HasOne(e => e.PreviousBooking)
            .WithMany()
            .HasForeignKey(e => e.PreviousBookingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Booking>()
            .HasOne(e => e.ExtendedFromBooking)
            .WithMany()
            .HasForeignKey(e => e.ExtendedFromBookingId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureBusinessRules(ModelBuilder builder)
    {
        builder.Entity<ServiceBusinessRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Service).WithMany(s => s.BusinessRules).HasForeignKey(e => e.ServiceId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<RequestItemRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.RequestItem).WithMany(r => r.BusinessRules).HasForeignKey(e => e.RequestItemId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private void ConfigureAgentTransfer(ModelBuilder builder)
    {
        builder.Entity<ConversationTransfer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Conversation)
                .WithMany()
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FromAgent)
                .WithMany()
                .HasForeignKey(e => e.FromAgentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ToAgent)
                .WithMany()
                .HasForeignKey(e => e.ToAgentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(e => e.TransferredAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => new { e.ConversationId, e.TransferredAt });
            entity.HasIndex(e => new { e.TenantId, e.Status });
        });
    }
}