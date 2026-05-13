using Salio.Domain.Common;
using Salio.Domain.Entities.Crm;
using Salio.Domain.Entities.Library;
using Salio.Domain.Entities.Rbac;

namespace Salio.Domain.Entities.Identity;

/// <summary>
/// Tổ chức (tenant) — đơn vị cấp cao nhất chứa users, deals, products...
/// </summary>
public class Organization : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Plan { get; set; } = "free";
    public string Locale { get; set; } = "vi-VN";
    public string? Settings { get; set; }  // jsonb

    public ICollection<OrgMember> Members { get; set; } = [];
    public ICollection<Company> Companies { get; set; } = [];
    public ICollection<Contact> Contacts { get; set; } = [];
    public ICollection<Pipeline> Pipelines { get; set; } = [];
    public ICollection<Deal> Deals { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<Models.Task> Tasks { get; set; } = [];
    public ICollection<LibraryNode> LibraryNodes { get; set; } = [];
    public ICollection<Role> Roles { get; set; } = [];
    public ICollection<Team> Teams { get; set; } = [];
    public ICollection<Invitation> Invitations { get; set; } = [];
}
