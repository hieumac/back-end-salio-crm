using Salio.Domain.Common;
using Salio.Domain.Enums;
using Salio.Domain.Entities.Crm;

namespace Salio.Domain.Entities.Ai;

public class AiInsight : TenantEntity
{
    public string ScopeType { get; set; } = string.Empty;
    public Guid ScopeId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? Priority { get; set; }
    public string? SuggestedAction { get; set; }  // jsonb
    public string? Model { get; set; }
    public AiInsightStatus Status { get; set; } = AiInsightStatus.Active;
    public DateTimeOffset? ExpiresAt { get; set; }
    public Guid? DismissedById { get; set; }
}

public class AiScoreHistory : AuditableEntity
{
    public Guid DealId { get; set; }
    public Deal? Deal { get; set; }
    public int Score { get; set; }
    public string? Reasons { get; set; }  // jsonb
    public string? Model { get; set; }
}
