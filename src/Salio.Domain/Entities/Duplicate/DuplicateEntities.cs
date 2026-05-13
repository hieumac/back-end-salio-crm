using Salio.Domain.Common;
using Salio.Domain.Enums;
using Salio.Domain.Entities.Identity;

namespace Salio.Domain.Entities.Duplicate;

public class DuplicateMatchGroup : TenantEntity
{
    public string EntityType { get; set; } = string.Empty;
    public string MatchField { get; set; } = string.Empty;
    public DupConfidence Confidence { get; set; }
    public decimal ConfidenceScore { get; set; }
    public DupStatus Status { get; set; } = DupStatus.Pending;
    public Guid? MasterRecordId { get; set; }
    public Guid? ResolvedById { get; set; }
    public User? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public ICollection<DuplicateMatchRecord> Records { get; set; } = [];
}

public class DuplicateMatchRecord : AuditableEntity
{
    public Guid MatchGroupId { get; set; }
    public DuplicateMatchGroup? MatchGroup { get; set; }
    public Guid RecordId { get; set; }
    public string? RecordSnapshot { get; set; }  // jsonb
    public bool IsMasterCandidate { get; set; }
}
