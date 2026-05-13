using Salio.Domain.Common;
using Salio.Domain.Enums;
using Salio.Domain.Entities.Identity;
using Salio.Domain.Entities.Library;

namespace Salio.Domain.Entities.Chat;

public class ChatConversation : TenantEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ContextType { get; set; }
    public Guid? ContextId { get; set; }
    public bool Pinned { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = [];
}

public class ChatMessage : AuditableEntity
{
    public Guid ConversationId { get; set; }
    public ChatConversation? Conversation { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ContentTokens { get; set; }
    public string? Model { get; set; }
    public int? LatencyMs { get; set; }
    public string? Metadata { get; set; }

    public ICollection<ChatMessageSource> Sources { get; set; } = [];
}

public class ChatMessageSource : AuditableEntity
{
    public Guid MessageId { get; set; }
    public ChatMessage? Message { get; set; }
    public Guid ChunkId { get; set; }
    public DocumentChunk? Chunk { get; set; }
    public decimal Score { get; set; }
    public string? Label { get; set; }
}
