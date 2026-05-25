using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using Salio.Domain.Entities.Ai;
using Salio.Domain.Entities.Auth;
using Salio.Domain.Entities.Chat;
using Salio.Domain.Entities.Crm;
using Salio.Domain.Entities.Cross;
using Salio.Domain.Entities.Duplicate;
using Salio.Domain.Entities.Identity;
using Salio.Domain.Entities.Library;
using Salio.Domain.Entities.Rbac;
using TaskModel = Salio.Domain.Entities.Models.Task;

namespace Salio.Infrastructure.Persistence;

public class SalioDbContext : DbContext, ISalioDbContext
{
    private readonly ICurrentUserService? _currentUser;

    public SalioDbContext(DbContextOptions<SalioDbContext> options) : base(options) { }

    public SalioDbContext(DbContextOptions<SalioDbContext> options, ICurrentUserService currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }


    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OrgMember> OrgMembers => Set<OrgMember>();

    public DbSet<AuthIdentity> AuthIdentities => Set<AuthIdentity>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<MfaFactor> MfaFactors => Set<MfaFactor>();
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<SystemFunction> SystemFunctions => Set<SystemFunction>();
    public DbSet<SystemAction> SystemActions => Set<SystemAction>();
    public DbSet<FunctionAction> FunctionActions => Set<FunctionAction>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<DealActivity> DealActivities => Set<DealActivity>();
    public DbSet<DealStageHistory> DealStageHistories => Set<DealStageHistory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<DealProduct> DealProducts => Set<DealProduct>();
    public DbSet<TaskModel> Tasks => Set<TaskModel>();
    public DbSet<DealFollower> DealFollowers => Set<DealFollower>();

    public DbSet<DuplicateMatchGroup> DuplicateMatchGroups => Set<DuplicateMatchGroup>();
    public DbSet<DuplicateMatchRecord> DuplicateMatchRecords => Set<DuplicateMatchRecord>();

    public DbSet<AiInsight> AiInsights => Set<AiInsight>();
    public DbSet<AiScoreHistory> AiScoreHistories => Set<AiScoreHistory>();

    public DbSet<LibraryNode> LibraryNodes => Set<LibraryNode>();
    public DbSet<LibraryPermission> LibraryPermissions => Set<LibraryPermission>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatMessageSource> ChatMessageSources => Set<ChatMessageSource>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasPostgresExtension("uuid-ossp");
        builder.HasPostgresExtension("vector");
        builder.HasPostgresExtension("pg_trgm");

        builder.ApplyConfigurationsFromAssembly(typeof(SalioDbContext).Assembly);

        // Chuyển toàn bộ column name chưa map sang snake_case cho khớp với SQL migrations
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var col = property.GetColumnName();
                if (!string.IsNullOrEmpty(col))
                    property.SetColumnName(ToSnakeCase(col));
            }
        }

        base.OnModelCreating(builder);
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && !char.IsUpper(input[i - 1]))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = _currentUser?.UserId;

        // ── Auditable: tự fill CreatedAt/CreatedBy/UpdatedAt/UpdatedBy ──
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                if (entry.Entity.CreatedBy is null) entry.Entity.CreatedBy = userId;
            }
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = userId;
            }
        }

        // ── Soft delete: chuyển Delete → Modified + set DeletedAt/DeletedBy ──
        foreach (var entry in ChangeTracker.Entries<SoftDeletableEntity>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.DeletedAt = now;
                entry.Entity.DeletedBy = userId;
                entry.Entity.IsActive = false; // tắt luôn cờ active
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
