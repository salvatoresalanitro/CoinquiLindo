using CoinquiLindo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Mapping for House entity
            modelBuilder.Entity<House>( b =>
            {
                b.HasKey(h => h.Id);
                b.Metadata.FindNavigation(nameof(House.Tenants))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
                b.Metadata.FindNavigation(nameof(House.Rooms))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            // Mapping for Room entity
            modelBuilder.Entity<Room>(b =>
            {
                b.HasKey(r => r.Id);
                b.Metadata.FindNavigation(nameof(Room.CleaningTasks))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            // Mapping for Shift entity
            modelBuilder.Entity<Shift>(b =>
            {
                b.HasKey(s => s.Id);
                b.HasOne(s => s.AssignedTenant).WithMany();
                b.HasOne(s => s.AssignedRoom).WithMany();

                b.Metadata.FindNavigation(nameof(Shift.Checklist))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
                b.Metadata.FindNavigation(nameof(Shift.Reports))!
                    .SetPropertyAccessMode(PropertyAccessMode.Field);
            });
        }
    }
}
