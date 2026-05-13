using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Salio.Domain.Entities.Auth;

namespace Salio.Infrastructure.Persistence.Configurations;

public class AuthIdentityConfiguration : IEntityTypeConfiguration<AuthIdentity>
{
    public void Configure(EntityTypeBuilder<AuthIdentity> b)
    {
        b.ToTable("auth_identities");
        b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ProviderUserId).HasMaxLength(200);
        b.Property(x => x.ProviderMetadata).HasColumnType("jsonb");
        b.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique().HasFilter("\"ProviderUserId\" IS NOT NULL");
        b.HasIndex(x => new { x.UserId, x.Provider });
        b.HasOne(x => x.User).WithMany(u => u.AuthIdentities).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> b)
    {
        b.ToTable("user_sessions");
        b.Property(x => x.SessionToken).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.SessionToken).IsUnique();
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.Property(x => x.DeviceFingerprint).HasMaxLength(200);
        b.Property(x => x.DeviceName).HasMaxLength(120);
        b.HasOne(x => x.User).WithMany(u => u.Sessions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Session).WithMany(s => s.RefreshTokens).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> b)
    {
        b.ToTable("email_verification_tokens");
        b.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
    }
}

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> b)
    {
        b.ToTable("password_reset_tokens");
        b.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.IpAddress).HasMaxLength(64);
    }
}

public class MfaFactorConfiguration : IEntityTypeConfiguration<MfaFactor>
{
    public void Configure(EntityTypeBuilder<MfaFactor> b)
    {
        b.ToTable("mfa_factors");
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Label).HasMaxLength(120);
        b.Property(x => x.PhoneNumber).HasMaxLength(40);
        b.HasOne(x => x.User).WithMany(u => u.MfaFactors).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MfaChallengeConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> b)
    {
        b.ToTable("mfa_challenges");
        b.Property(x => x.CodeHash).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Factor).WithMany(f => f.Challenges).HasForeignKey(x => x.FactorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> b)
    {
        b.ToTable("login_attempts");
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.Result).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.Property(x => x.FailureReason).HasMaxLength(200);
        b.HasIndex(x => new { x.Email, x.CreatedAt });
        b.HasOne(x => x.User).WithMany(u => u.LoginAttempts).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> b)
    {
        b.ToTable("api_keys");
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.KeyPrefix).HasMaxLength(20).IsRequired();
        b.Property(x => x.KeyHash).HasMaxLength(200).IsRequired();
        b.Property(x => x.Scopes).HasColumnType("jsonb");
        b.HasIndex(x => x.KeyHash).IsUnique();
        b.HasIndex(x => new { x.OrgId, x.KeyPrefix });
        b.HasOne(x => x.CreatedBy).WithMany(u => u.CreatedApiKeys).HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> b)
    {
        b.ToTable("invitations");
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.Token).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => new { x.OrgId, x.Email });
        b.HasOne(x => x.InvitedBy).WithMany(u => u.InvitationsSent).HasForeignKey(x => x.InvitedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AcceptedByUser).WithMany(u => u.InvitationsAccepted).HasForeignKey(x => x.AcceptedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
