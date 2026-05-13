using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Salio.Domain.Entities.Crm;
using TaskModel = Salio.Domain.Entities.Models.Task;

namespace Salio.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("companies");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.TaxCode).HasMaxLength(40);
        b.Property(x => x.Industry).HasMaxLength(80);
        b.Property(x => x.Size).HasMaxLength(40);
        b.Property(x => x.Website).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.CustomFields).HasColumnType("jsonb");
        b.HasIndex(x => new { x.OrgId, x.TaxCode });
        b.HasIndex(x => new { x.OrgId, x.Name });
        b.HasQueryFilter(x => x.DeletedAt == null);
        b.HasOne(x => x.Organization).WithMany(o => o.Companies).HasForeignKey(x => x.OrgId);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> b)
    {
        b.ToTable("contacts");
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Title).HasMaxLength(120);
        b.Property(x => x.CustomFields).HasColumnType("jsonb");
        b.HasIndex(x => new { x.OrgId, x.Email });
        b.HasIndex(x => new { x.OrgId, x.Phone });
        b.HasQueryFilter(x => x.DeletedAt == null);
        b.HasOne(x => x.Organization).WithMany(o => o.Contacts).HasForeignKey(x => x.OrgId);
        b.HasOne(x => x.Company).WithMany(c => c.Contacts).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
{
    public void Configure(EntityTypeBuilder<Pipeline> b)
    {
        b.ToTable("pipelines");
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.HasIndex(x => new { x.OrgId, x.Name });
        b.HasQueryFilter(x => x.DeletedAt == null);
        b.HasOne(x => x.Organization).WithMany(o => o.Pipelines).HasForeignKey(x => x.OrgId);
    }
}

public class PipelineStageConfiguration : IEntityTypeConfiguration<PipelineStage>
{
    public void Configure(EntityTypeBuilder<PipelineStage> b)
    {
        b.ToTable("pipeline_stages");
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Color).HasMaxLength(20);
        b.HasIndex(x => new { x.PipelineId, x.Code }).IsUnique();
        b.HasOne(x => x.Pipeline).WithMany(p => p.Stages).HasForeignKey(x => x.PipelineId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> b)
    {
        b.ToTable("deals");
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Value).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.AiScoreReasons).HasColumnType("jsonb");
        b.Property(x => x.CustomFields).HasColumnType("jsonb");
        b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.OrgId, x.StageId });
        b.HasIndex(x => new { x.OrgId, x.AssigneeId });
        b.HasQueryFilter(x => x.DeletedAt == null);

        b.HasOne(x => x.Organization).WithMany(o => o.Deals).HasForeignKey(x => x.OrgId);
        b.HasOne(x => x.Pipeline).WithMany(p => p.Deals).HasForeignKey(x => x.PipelineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Stage).WithMany(s => s.Deals).HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Company).WithMany(c => c.Deals).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Contact).WithMany(c => c.Deals).HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Assignee).WithMany(u => u.AssignedDeals).HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class DealActivityConfiguration : IEntityTypeConfiguration<DealActivity>
{
    public void Configure(EntityTypeBuilder<DealActivity> b)
    {
        b.ToTable("deal_activities");
        b.Property(x => x.Type).HasMaxLength(40).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Metadata).HasColumnType("jsonb");
        b.HasIndex(x => new { x.DealId, x.CreatedAt });
        b.HasOne(x => x.Deal).WithMany(d => d.Activities).HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class DealStageHistoryConfiguration : IEntityTypeConfiguration<DealStageHistory>
{
    public void Configure(EntityTypeBuilder<DealStageHistory> b)
    {
        b.ToTable("deal_stage_history");
        b.HasIndex(x => new { x.DealId, x.CreatedAt });
        b.HasOne(x => x.Deal).WithMany(d => d.StageHistory).HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.FromStage).WithMany().HasForeignKey(x => x.FromStageId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.ToStage).WithMany().HasForeignKey(x => x.ToStageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ChangedBy).WithMany().HasForeignKey(x => x.ChangedById).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products");
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(20);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        b.HasQueryFilter(x => x.DeletedAt == null);
        b.HasOne(x => x.Organization).WithMany(o => o.Products).HasForeignKey(x => x.OrgId);
    }
}

public class DealProductConfiguration : IEntityTypeConfiguration<DealProduct>
{
    public void Configure(EntityTypeBuilder<DealProduct> b)
    {
        b.ToTable("deal_products");
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.DiscountPct).HasPrecision(5, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.HasOne(x => x.Deal).WithMany(d => d.Products).HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Product).WithMany(p => p.DealProducts).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TaskConfiguration : IEntityTypeConfiguration<TaskModel>
{
    public void Configure(EntityTypeBuilder<TaskModel> b)
    {
        b.ToTable("tasks");
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.OrgId, x.AssigneeId, x.Status });
        b.HasIndex(x => new { x.OrgId, x.DealId });
        b.HasQueryFilter(x => x.DeletedAt == null);
        b.HasOne(x => x.Organization).WithMany(o => o.Tasks).HasForeignKey(x => x.OrgId);
        b.HasOne(x => x.Assignee).WithMany(u => u.AssignedTasks).HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Deal).WithMany(d => d.Tasks).HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DealFollowerConfiguration : IEntityTypeConfiguration<DealFollower>
{
    public void Configure(EntityTypeBuilder<DealFollower> b)
    {
        b.ToTable("deal_followers");
        b.HasKey(x => new { x.DealId, x.UserId });
        b.HasOne(x => x.Deal).WithMany(d => d.Followers).HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
