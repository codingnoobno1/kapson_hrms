using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.Identity.Candidate;

namespace Vispl.HRManagement.Identity.Admin;

public sealed class AdminIdentityDbContext(DbContextOptions<AdminIdentityDbContext> options)
    : IdentityDbContext<AdminUser, HrRole, string>(options)
{
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<IdentityAuditEntity> IdentityAudits => Set<IdentityAuditEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<CandidateActivationEntity> CandidateActivations => Set<CandidateActivationEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("Users", "identity");
            entity.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.TenantKey).HasMaxLength(80);
            entity.Property(item => item.EmployeeNumber).HasMaxLength(60);
            entity.Property(item => item.CreatedByUserId).HasMaxLength(450);
            entity.HasIndex(item => new { item.TenantKey, item.Portal });
            entity.HasIndex(item => item.CandidateId).HasFilter("[CandidateId] IS NOT NULL");
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeNumber })
                .HasFilter("[EmployeeNumber] IS NOT NULL");
        });
        builder.Entity<HrRole>(entity =>
        {
            entity.ToTable("Roles", "identity");
            entity.Property(item => item.TenantKey).HasMaxLength(80);
            entity.Property(item => item.RoleCode).HasMaxLength(60).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(300);
            entity.HasIndex(item => new { item.TenantKey, item.Portal, item.RoleCode })
                .IsUnique()
                .HasFilter("[TenantKey] IS NOT NULL");
        });
        builder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("RefreshTokens", "identity");
            entity.HasKey(item => item.Id);
            // Lookup is by hash, so it must be unique and indexed.
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => item.FamilyId);
            entity.HasIndex(item => new { item.UserId, item.ExpiresUtc });
            entity.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.UserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.TenantKey).HasMaxLength(80);
            entity.Property(item => item.FamilyId).HasMaxLength(64).IsRequired();
            entity.Property(item => item.DeviceId).HasMaxLength(200);
            entity.Property(item => item.RevokedReason).HasMaxLength(200);
        });

        builder.Entity<CandidateActivationEntity>(entity =>
        {
            entity.ToTable("CandidateActivations", "identity");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.CandidateId });
            entity.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(256).IsRequired();
        });

        builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles", "identity");
        builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims", "identity");
        builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins", "identity");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims", "identity");
        builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens", "identity");

        builder.Entity<RolePermissionEntity>(entity =>
        {
            entity.ToTable("RolePermissions", "identity");
            entity.Property(item => item.RoleId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.PermissionKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => new { item.RoleId, item.PermissionKey }).IsUnique();
            entity.HasOne(item => item.Role)
                .WithMany()
                .HasForeignKey(item => item.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IdentityAuditEntity>(entity =>
        {
            entity.ToTable("IdentityAudit", "identity");
            entity.Property(item => item.TenantKey).HasMaxLength(80);
            entity.Property(item => item.ActorUserId).HasMaxLength(450);
            entity.Property(item => item.Action).HasMaxLength(80).IsRequired();
            entity.Property(item => item.SubjectType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.SubjectId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.DetailJson).HasMaxLength(2000);
            entity.HasIndex(item => new { item.TenantKey, item.OccurredUtc });
        });
    }
}
