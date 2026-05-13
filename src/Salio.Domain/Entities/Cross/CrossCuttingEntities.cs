using Salio.Domain.Common;
using Salio.Domain.Entities.Identity;
using System.Net;

namespace Salio.Domain.Entities.Cross;

public class Notification : AuditableEntity
{
    public Guid OrgId { get; set; }
    public Guid RecipientId { get; set; }
    public User? Recipient { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public class AuditLog : AuditableEntity
{
    public Guid OrgId { get; set; }
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Before { get; set; }  // jsonb
    public string? After { get; set; }   // jsonb
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
