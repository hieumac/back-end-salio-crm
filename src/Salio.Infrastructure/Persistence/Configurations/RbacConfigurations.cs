using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Salio.Domain.Entities.Rbac;

namespace Salio.Infrastructure.Persistence.Configurations;

public class SystemFunctionConfiguration : IEntityTypeConfiguration<SystemFunction>
{
    public void Configure(EntityTypeBuilder<SystemFunction> b)
    {
        b.ToTable("system_functions");
        b.Property(x => x.Code).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.ModuleGroup).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Path).HasMaxLength(200);
        b.Property(x => x.Icon).HasMaxLength(60);
        b.Property(x => x.RiskLevel).HasConversion<string>().HasMaxLength(20);
    }
}

public class SystemActionConfiguration : IEntityTypeConfiguration<SystemAction>
{
    public void Configure(EntityTypeBuilder<SystemAction> b)
    {
        b.ToTable("system_actions");
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
    }
}

public class FunctionActionConfiguration : IEntityTypeConfiguration<FunctionAction>
{
    public void Configure(EntityTypeBuilder<FunctionAction> b)
    {
        b.ToTable("function_actions");
        b.HasIndex(x => new { x.FunctionId, x.ActionId }).IsUnique();
        b.HasOne(x => x.Function).WithMany(f => f.FunctionActions).HasForeignKey(x => x.FunctionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Action).WithMany(a => a.FunctionActions).HasForeignKey(x => x.ActionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("permissions");
        b.Property(x => x.Code).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.FunctionId, x.ActionId, x.Scope }).IsUnique();
        b.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.Function).WithMany(f => f.Permissions).HasForeignKey(x => x.FunctionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Action).WithMany(a => a.Permissions).HasForeignKey(x => x.ActionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.Property(x => x.Code).HasMaxLength(60).IsRequired();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        b.HasOne(x => x.Organization).WithMany(o => o.Roles).HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ParentRole).WithMany(r => r.ChildRoles).HasForeignKey(x => x.ParentRoleId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.CreatedBy).WithMany(u => u.CreatedRoles).HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions");
        b.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
        b.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_roles");
        b.HasIndex(x => new { x.UserId, x.OrgId, x.RoleId }).IsUnique();
        b.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.AssignedBy).WithMany(u => u.AssignedUserRoles).HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PermissionGrantConfiguration : IEntityTypeConfiguration<PermissionGrant>
{
    public void Configure(EntityTypeBuilder<PermissionGrant> b)
    {
        b.ToTable("permission_grants");
        b.Property(x => x.Effect).HasConversion<string>().HasMaxLength(10);
        b.HasIndex(x => new { x.UserId, x.OrgId, x.PermissionId }).IsUnique();
        b.HasOne(x => x.User).WithMany(u => u.PermissionGrants).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Permission).WithMany(p => p.Grants).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.GrantedBy).WithMany(u => u.GrantedPermissions).HasForeignKey(x => x.GrantedById).OnDelete(DeleteBehavior.SetNull);
    }
}

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.ToTable("teams");
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        b.HasOne(x => x.Organization).WithMany(o => o.Teams).HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Manager).WithMany(u => u.ManagedTeams).HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.ParentTeam).WithMany(t => t.ChildTeams).HasForeignKey(x => x.ParentTeamId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> b)
    {
        b.ToTable("team_members");
        b.Property(x => x.RoleType).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.TeamId, x.UserId }).IsUnique();
        b.HasOne(x => x.Team).WithMany(t => t.Members).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany(u => u.TeamMemberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
