using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Salio.Domain.Entities.Identity;

namespace Salio.Infrastructure.Persistence.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("organizations");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Plan).HasMaxLength(40);
        b.Property(x => x.Locale).HasMaxLength(10);
        b.Property(x => x.Settings).HasColumnType("jsonb");
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.AvatarUrl).HasMaxLength(500);
        b.HasQueryFilter(u => u.DeletedAt == null);
    }
}

public class OrgMemberConfiguration : IEntityTypeConfiguration<OrgMember>
{
    public void Configure(EntityTypeBuilder<OrgMember> b)
    {
        b.ToTable("org_members");
        b.HasIndex(x => new { x.OrgId, x.UserId }).IsUnique();
        b.HasOne(x => x.Organization).WithMany(o => o.Members).HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany(u => u.Memberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Title).HasMaxLength(120);
    }
}
