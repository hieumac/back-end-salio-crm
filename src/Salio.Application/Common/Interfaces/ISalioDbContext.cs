using Microsoft.EntityFrameworkCore;
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

namespace Salio.Application.Common.Interfaces;

/// <summary>
/// Cổng truy cập DB từ Application layer (Domain không biết về EF Core).
/// </summary>
public interface ISalioDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<User> Users { get; }
    DbSet<OrgMember> OrgMembers { get; }

    DbSet<AuthIdentity> AuthIdentities { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<EmailVerificationToken> EmailVerificationTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<MfaFactor> MfaFactors { get; }
    DbSet<MfaChallenge> MfaChallenges { get; }
    DbSet<LoginAttempt> LoginAttempts { get; }
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<Invitation> Invitations { get; }

    DbSet<SystemFunction> SystemFunctions { get; }
    DbSet<SystemAction> SystemActions { get; }
    DbSet<FunctionAction> FunctionActions { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<Role> Roles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<PermissionGrant> PermissionGrants { get; }
    DbSet<Team> Teams { get; }
    DbSet<TeamMember> TeamMembers { get; }

    DbSet<Company> Companies { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<Pipeline> Pipelines { get; }
    DbSet<PipelineStage> PipelineStages { get; }
    DbSet<Deal> Deals { get; }
    DbSet<DealActivity> DealActivities { get; }
    DbSet<DealStageHistory> DealStageHistories { get; }
    DbSet<Product> Products { get; }
    DbSet<DealProduct> DealProducts { get; }
    DbSet<TaskModel> Tasks { get; }
    DbSet<DealFollower> DealFollowers { get; }

    DbSet<DuplicateMatchGroup> DuplicateMatchGroups { get; }
    DbSet<DuplicateMatchRecord> DuplicateMatchRecords { get; }

    DbSet<AiInsight> AiInsights { get; }
    DbSet<AiScoreHistory> AiScoreHistories { get; }

    DbSet<LibraryNode> LibraryNodes { get; }
    DbSet<LibraryPermission> LibraryPermissions { get; }
    DbSet<DocumentChunk> DocumentChunks { get; }

    DbSet<ChatConversation> ChatConversations { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<ChatMessageSource> ChatMessageSources { get; }

    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
