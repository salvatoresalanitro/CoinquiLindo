using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CoinquiLindo.Infrastructure
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<House> Houses => Set<House>();
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<CleaningTask> CleaningTasks => Set<CleaningTask>();
        public DbSet<Shift> Shifts => Set<Shift>();
        public DbSet<Report> Reports => Set<Report>();
        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<RotationPlan> RotationPlans => Set<RotationPlan>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Mapping for House entity
            modelBuilder.Entity<House>( b =>
            {
                b.HasKey(h => h.Id);
                b.Property(h => h.InviteCode).HasMaxLength(House.InviteCodeLength);
                b.Property(h => h.TimeZoneId).HasMaxLength(House.MaxTimeZoneIdLength);
                b.HasIndex(h => h.InviteCode).IsUnique();
                b.Metadata.FindNavigation(nameof(House.Tenants))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
                b.Metadata.FindNavigation(nameof(House.Rooms))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            // Mapping for Room entity
            modelBuilder.Entity<Room>(b =>
            {
                b.HasKey(r => r.Id);
                // Required: a task removed from the room is deleted, not left behind with a NULL RoomId
                b.HasMany(r => r.CleaningTasks).WithOne().HasForeignKey("RoomId").IsRequired().OnDelete(DeleteBehavior.Cascade);
                b.Metadata.FindNavigation(nameof(Room.CleaningTasks))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            // Mapping for Shift entity
            modelBuilder.Entity<Shift>(b =>
            {
                b.HasKey(s => s.Id);
                b.HasOne<House>().WithMany().HasForeignKey(s => s.HouseId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(s => s.AssignedTenant).WithMany();
                b.HasOne(s => s.AssignedRoom).WithMany();

                // SQLite has no time zone info: without this the deadline is read back as Unspecified
                // and serialized without "Z", so clients would treat it as local time
                b.Property(s => s.DueDate).HasConversion(
                    value => value,
                    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
                b.HasIndex(s => s.DueDate);

                // Plain reference to the rotation plan (no FK): the plan can be deleted, the shifts stay as history
                b.Property(s => s.RotationPlanId);
                // One generated shift per room and week of a plan: makes the job and concurrent saves idempotent
                b.HasIndex(nameof(Shift.RotationPlanId), "AssignedRoomId", nameof(Shift.StartDate))
                    .IsUnique()
                    .HasFilter("\"RotationPlanId\" IS NOT NULL")
                    .HasDatabaseName("IX_Shifts_OneRotationShiftPerRoomAndWeek");

                b.HasOne(s => s.HelperTenant).WithMany().HasForeignKey("HelperTenantId")
                    .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

                // Required: items removed from the checklist (or of a deleted shift) are deleted, not orphaned
                b.HasMany(s => s.Checklist).WithOne().HasForeignKey("ShiftId").IsRequired().OnDelete(DeleteBehavior.Cascade);
                b.Metadata.FindNavigation(nameof(Shift.Checklist))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
                b.Metadata.FindNavigation(nameof(Shift.Reports))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            // One plan per house; the ordered ids are JSON arrays (EF primitive collections): order is the business rule
            modelBuilder.Entity<RotationPlan>(b =>
            {
                b.ToTable("RotationPlans");
                b.HasKey(p => p.Id);
                b.HasOne<House>().WithMany().HasForeignKey(p => p.HouseId).OnDelete(DeleteBehavior.Cascade);
                b.HasIndex(p => p.HouseId).IsUnique();
                b.PrimitiveCollection(p => p.TenantIds).HasField("_tenantIds")
                    .UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();
                b.PrimitiveCollection(p => p.RoomIds).HasField("_roomIds")
                    .UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();
            });

            // Report photo is mandatory: NOT NULL plus a CHECK so blank keys are rejected by the database too
            modelBuilder.Entity<Report>(b =>
            {
                b.HasKey(r => r.Id);
                b.Property(r => r.PhotoKey).HasMaxLength(64).IsRequired();
                b.ToTable(t => t.HasCheckConstraint("CK_Reports_PhotoKey_NotBlank", "length(trim(\"PhotoKey\")) > 0"));

                b.HasMany(r => r.Votes).WithOne().HasForeignKey("ReportId").IsRequired().OnDelete(DeleteBehavior.Cascade);
                b.Metadata.FindNavigation(nameof(Report.Votes))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            // One vote per tenant per report, enforced by the database against concurrent requests
            modelBuilder.Entity<ReportVote>(b =>
            {
                b.HasKey(v => v.Id);
                b.HasOne<Tenant>().WithMany().HasForeignKey(v => v.VoterTenantId).OnDelete(DeleteBehavior.Restrict);
                b.HasIndex("ReportId", nameof(ReportVote.VoterTenantId)).IsUnique();
                b.ToTable("ReportVotes", t => t.HasCheckConstraint("CK_ReportVotes_Type", "\"Type\" IN (-1, 1)"));
            });

            // Mapping for User aggregate (account linked to Google)
            modelBuilder.Entity<User>(b =>
            {
                b.HasKey(u => u.Id);
                b.Property(u => u.GoogleSubject).HasMaxLength(255);
                b.HasIndex(u => u.GoogleSubject).IsUnique();
                b.Property(u => u.Email).HasMaxLength(320);
                b.Property(u => u.DisplayName).HasMaxLength(200);

                b.HasMany(u => u.RefreshTokens).WithOne().HasForeignKey("UserId").IsRequired().OnDelete(DeleteBehavior.Cascade);
                b.Metadata.FindNavigation(nameof(User.RefreshTokens))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);

                b.HasMany(u => u.PushDevices).WithOne().HasForeignKey("UserId").IsRequired().OnDelete(DeleteBehavior.Cascade);
                b.Metadata.FindNavigation(nameof(User.PushDevices))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            // A token identifies one app install: it can belong to a single account at a time
            modelBuilder.Entity<PushDevice>(b =>
            {
                b.ToTable("PushDevices");
                b.HasKey(d => d.Id);
                b.Property(d => d.Token).HasMaxLength(PushDevice.MaxTokenLength);
                b.HasIndex(d => d.Token).IsUnique();
            });

            modelBuilder.Entity<RefreshToken>(b =>
            {
                b.HasKey(t => t.Id);
                b.Property(t => t.TokenHash).HasMaxLength(64);
                b.HasIndex(t => t.TokenHash).IsUnique();
            });

            // Tenant -> User is optional: tenants added by name have no account
            modelBuilder.Entity<Tenant>(b =>
            {
                b.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
                b.HasIndex("HouseId", nameof(Tenant.UserId)).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
            });

            // ShiftId/ReportId are plain references (no FK): notifications are a log, not part of those aggregates
            modelBuilder.Entity<Notification>(b =>
            {
                b.HasKey(n => n.Id);
                b.HasOne<User>().WithMany().HasForeignKey(n => n.RecipientUserId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne<House>().WithMany().HasForeignKey(n => n.HouseId).OnDelete(DeleteBehavior.Cascade);
                b.Property(n => n.Title).HasMaxLength(Notification.MaxTitleLength);
                b.Property(n => n.Body).HasMaxLength(Notification.MaxBodyLength);
                b.HasIndex(n => new { n.RecipientUserId, n.ReadAt, n.CreatedAt });

                // Idempotency guards against races and repeated job runs (values of NotificationType)
                b.HasIndex(n => new { n.RecipientUserId, n.Type, n.ShiftId })
                    .IsUnique()
                    .HasFilter($"\"Type\" = {(int)NotificationType.ShiftDueSoon}")
                    .HasDatabaseName("IX_Notifications_DueSoonOncePerShift");
                b.HasIndex(n => new { n.RecipientUserId, n.Type, n.ReportId })
                    .IsUnique()
                    .HasFilter($"\"Type\" = {(int)NotificationType.ReportVoted} AND \"ReadAt\" IS NULL")
                    .HasDatabaseName("IX_Notifications_OneUnreadVoteSummaryPerReport");
                b.HasIndex(n => new { n.RecipientUserId, n.Type, n.HouseId })
                    .IsUnique()
                    .HasFilter($"\"Type\" = {(int)NotificationType.CleaningTasksChanged} AND \"ReadAt\" IS NULL")
                    .HasDatabaseName("IX_Notifications_OneUnreadTaskSummaryPerHouse");
            });

            // Ids are generated by the domain: EF must treat children with a set key as new (Added), not existing
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.FindPrimaryKey()?.Properties is [var key] && key.ClrType == typeof(Guid))
                {
                    key.ValueGenerated = ValueGenerated.Never;
                }
            }
        }
    }
}
