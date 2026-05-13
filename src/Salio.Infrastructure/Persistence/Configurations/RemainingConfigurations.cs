using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Salio.Domain.Entities.Ai;
using Salio.Domain.Entities.Chat;
using Salio.Domain.Entities.Cross;
using Salio.Domain.Entities.Duplicate;
using Salio.Domain.Entities.Library;

namespace Salio.Infrastructure.Persistence.Configurations;

public class DuplicateMatchGroupConfiguration : IEntityTypeConfiguration<DuplicateMatchGroup>
{
    public void Configure(EntityTypeBuilder<DuplicateMatchGroup> b)
    {
        b.ToTable("dup_match_groups");
        b.Property(x => x.EntityType).HasMaxLength(40).IsRequired();
        b.Property(x => x.MatchField).HasMaxLength(60).IsRequired();
        b.Property(x => x.Confidence).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ConfidenceScore).HasPrecision(5, 4);
        b.HasIndex(x => new { x.OrgId, x.EntityType, x.Status });
        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public class DuplicateMatchRecordConfiguration : IEntityTypeConfiguration<DuplicateMatchRecord>
{
    public void Configure(EntityTypeBuilder<DuplicateMatchRecord> b)
    {
        b.ToTable("dup_match_records");
        b.Property(x => x.RecordSnapshot).HasColumnType("jsonb");
        b.HasOne(x => x.MatchGroup).WithMany(g => g.Records).HasForeignKey(x => x.MatchGroupId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AiInsightConfiguration : IEntityTypeConfiguration<AiInsight>
{
    public void Configure(EntityTypeBuilder<AiInsight> b)
    {
        b.ToTable("ai_insights");
        b.Property(x => x.ScopeType).HasMaxLength(40).IsRequired();
        b.Property(x => x.Type).HasMaxLength(60).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Priority).HasMaxLength(20);
        b.Property(x => x.Model).HasMaxLength(80);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.SuggestedAction).HasColumnType("jsonb");
        b.HasIndex(x => new { x.OrgId, x.Status, x.CreatedAt });
        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public class AiScoreHistoryConfiguration : IEntityTypeConfiguration<AiScoreHistory>
{
    public void Configure(EntityTypeBuilder<AiScoreHistory> b)
    {
        b.ToTable("ai_score_history");
        b.Property(x => x.Reasons).HasColumnType("jsonb");
        b.Property(x => x.Model).HasMaxLength(80);
        b.HasIndex(x => new { x.DealId, x.CreatedAt });
        b.HasOne(x => x.Deal).WithMany(d => d.AiScoreHistory).HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LibraryNodeConfiguration : IEntityTypeConfiguration<LibraryNode>
{
    public void Configure(EntityTypeBuilder<LibraryNode> b)
    {
        b.ToTable("library_nodes");
        b.Property(x => x.RootType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.Property(x => x.FileId).HasMaxLength(120);
        b.Property(x => x.FileMime).HasMaxLength(80);
        b.Property(x => x.Path).HasMaxLength(2000);
        b.HasIndex(x => new { x.OrgId, x.RootType, x.ParentId });
        b.HasQueryFilter(x => x.DeletedAt == null);
        b.HasOne(x => x.Organization).WithMany(o => o.LibraryNodes).HasForeignKey(x => x.OrgId);
        b.HasOne(x => x.Parent).WithMany(p => p.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class LibraryPermissionConfiguration : IEntityTypeConfiguration<LibraryPermission>
{
    public void Configure(EntityTypeBuilder<LibraryPermission> b)
    {
        b.ToTable("library_permissions");
        b.Property(x => x.PrincipalType).HasMaxLength(20).IsRequired();
        b.Property(x => x.Permission).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.NodeId, x.PrincipalType, x.PrincipalId }).IsUnique();
        b.HasOne(x => x.Node).WithMany(n => n.Permissions).HasForeignKey(x => x.NodeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> b)
    {
        b.ToTable("document_chunks");
        b.Property(x => x.Content).IsRequired();
        b.Property(x => x.Metadata).HasColumnType("jsonb");
        b.Property(x => x.Embedding).HasColumnType("vector(1536)");
        b.HasIndex(x => new { x.NodeId, x.ChunkIndex }).IsUnique();
        b.HasIndex(x => x.OrgId);
        b.HasOne(x => x.Node).WithMany(n => n.Chunks).HasForeignKey(x => x.NodeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> b)
    {
        b.ToTable("chat_conversations");
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.ContextType).HasMaxLength(40);
        b.HasIndex(x => new { x.OrgId, x.UserId, x.LastMessageAt });
        b.HasQueryFilter(x => x.DeletedAt == null);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.ToTable("chat_messages");
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Model).HasMaxLength(80);
        b.Property(x => x.Metadata).HasColumnType("jsonb");
        b.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        b.HasOne(x => x.Conversation).WithMany(c => c.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChatMessageSourceConfiguration : IEntityTypeConfiguration<ChatMessageSource>
{
    public void Configure(EntityTypeBuilder<ChatMessageSource> b)
    {
        b.ToTable("chat_message_sources");
        b.Property(x => x.Score).HasPrecision(5, 4);
        b.Property(x => x.Label).HasMaxLength(200);
        b.HasOne(x => x.Message).WithMany(m => m.Sources).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Chunk).WithMany().HasForeignKey(x => x.ChunkId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.Property(x => x.Type).HasMaxLength(60).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.LinkUrl).HasMaxLength(500);
        b.Property(x => x.EntityType).HasMaxLength(60);
        b.HasIndex(x => new { x.RecipientId, x.ReadAt });
        b.HasOne(x => x.Recipient).WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.Property(x => x.Action).HasMaxLength(80).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.Before).HasColumnType("jsonb");
        b.Property(x => x.After).HasColumnType("jsonb");
        b.HasIndex(x => new { x.OrgId, x.EntityType, x.EntityId });
        b.HasIndex(x => new { x.OrgId, x.CreatedAt });
        b.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
    }
}
