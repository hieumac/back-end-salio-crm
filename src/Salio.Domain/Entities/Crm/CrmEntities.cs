using Salio.Domain.Common;
using Salio.Domain.Enums;
using Salio.Domain.Entities.Identity;
using DealTaskStatus = Salio.Domain.Enums.TaskStatus;

namespace Salio.Domain.Entities.Crm;

/// <summary>
/// Doanh nghiệp (khách hàng B2B).
/// </summary>
public class Company : TenantEntity
{
    public Organization? Organization { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? Industry { get; set; }
    public string? Size { get; set; }
    public string? Website { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }
    public string? CustomFields { get; set; }  // jsonb

    public ICollection<Contact> Contacts { get; set; } = [];
    public ICollection<Deal> Deals { get; set; } = [];
}

/// <summary>
/// Liên hệ (người) — thuộc Company.
/// </summary>
public class Contact : TenantEntity
{
    public Organization? Organization { get; set; }
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Title { get; set; }
    public bool IsPrimary { get; set; }
    public string? CustomFields { get; set; }

    public ICollection<Deal> Deals { get; set; } = [];
}

public class Pipeline : TenantEntity
{
    public Organization? Organization { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int Order { get; set; }

    public ICollection<PipelineStage> Stages { get; set; } = [];
    public ICollection<Deal> Deals { get; set; } = [];
}

public class PipelineStage : AuditableEntity
{
    public Guid PipelineId { get; set; }
    public Pipeline? Pipeline { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public int DefaultProbability { get; set; }
    public bool IsWon { get; set; }
    public bool IsLost { get; set; }
    public string? Color { get; set; }

    public ICollection<Deal> Deals { get; set; } = [];
}

/// <summary>
/// Cơ hội bán hàng (deal).
/// </summary>
public class Deal : TenantEntity
{
    public Organization? Organization { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public Guid PipelineId { get; set; }
    public Pipeline? Pipeline { get; set; }
    public Guid StageId { get; set; }
    public PipelineStage? Stage { get; set; }

    public decimal Value { get; set; }
    public string Currency { get; set; } = "VND";
    public int Probability { get; set; }
    public DealSource Source { get; set; }

    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid? ContactId { get; set; }
    public Contact? Contact { get; set; }

    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public DateOnly? ExpectedCloseDate { get; set; }
    public DateTimeOffset? ActualCloseDate { get; set; }

    public int? AiScore { get; set; }
    public string? AiScoreReasons { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }

    public ICollection<DealActivity> Activities { get; set; } = [];
    public ICollection<DealStageHistory> StageHistory { get; set; } = [];
    public ICollection<DealProduct> Products { get; set; } = [];
    public ICollection<Models.Task> Tasks { get; set; } = [];
    public ICollection<DealFollower> Followers { get; set; } = [];
    public ICollection<Ai.AiScoreHistory> AiScoreHistory { get; set; } = [];
}

public class DealActivity : AuditableEntity
{
    public Guid DealId { get; set; }
    public Deal? Deal { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Metadata { get; set; }
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }
}

public class DealStageHistory : AuditableEntity
{
    public Guid DealId { get; set; }
    public Deal? Deal { get; set; }
    public Guid? FromStageId { get; set; }
    public PipelineStage? FromStage { get; set; }
    public Guid ToStageId { get; set; }
    public PipelineStage? ToStage { get; set; }
    public long DurationInPrevStageSeconds { get; set; }
    public Guid? ChangedById { get; set; }
    public User? ChangedBy { get; set; }
}

public class Product : TenantEntity
{
    public Organization? Organization { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = "unit";
    public string Currency { get; set; } = "VND";
    // IsActive đã inherit từ AuditableEntity (base field)

    public ICollection<DealProduct> DealProducts { get; set; } = [];
}

public class DealProduct : AuditableEntity
{
    public Guid DealId { get; set; }
    public Deal? Deal { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal Total { get; set; }
}

public class DealFollower : BaseEntity
{
    public Guid DealId { get; set; }
    public Deal? Deal { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public DateTimeOffset FollowedAt { get; set; } = DateTimeOffset.UtcNow;
}
