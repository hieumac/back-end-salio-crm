using Salio.Domain.Common;
using Salio.Domain.Enums;
using Salio.Domain.Entities.Identity;
using Pgvector;

namespace Salio.Domain.Entities.Library;

public class LibraryNode : TenantEntity
{
    public Organization? Organization { get; set; }
    public Guid? ParentId { get; set; }
    public LibraryNode? Parent { get; set; }
    public LibraryRootType RootType { get; set; }
    public LibraryNodeType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public LibraryStatus Status { get; set; } = LibraryStatus.Active;
    public string? FileId { get; set; }
    public string? FileUrl { get; set; }
    public string? FileMime { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? Path { get; set; }
    public bool IsSystem { get; set; }
    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }

    public ICollection<LibraryNode> Children { get; set; } = [];
    public ICollection<LibraryPermission> Permissions { get; set; } = [];
    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}

public class LibraryPermission : AuditableEntity
{
    public Guid NodeId { get; set; }
    public LibraryNode? Node { get; set; }
    public string PrincipalType { get; set; } = "user";  // user | team | role
    public Guid PrincipalId { get; set; }
    public string Permission { get; set; } = "view";  // view | edit | manage
}

public class DocumentChunk : AuditableEntity
{
    public Guid NodeId { get; set; }
    public LibraryNode? Node { get; set; }
    public Guid OrgId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ContentTokens { get; set; }

    /// <summary>Vector embedding (pgvector). Dimension typically 1536 (OpenAI ada-002) or 768.</summary>
    public Vector? Embedding { get; set; }
    public string? Metadata { get; set; }
}
