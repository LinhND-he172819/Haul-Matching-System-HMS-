using HMS.Modules.Identity.Core.Entities;
using HMS.Modules.Identity.Core.Interfaces;
using HMS.Modules.Matching.Core.Models;
using Microsoft.EntityFrameworkCore;
using Vehicle = HMS.Modules.Matching.Core.Models.Vehicle;

namespace HMS.Modules.Matching.Infrastructure
{
    public class MatchingDbContext : DbContext, IIdentityDbContext
    {
        public MatchingDbContext(DbContextOptions<MatchingDbContext> options)
            : base(options) { }

        public DbSet<Trip> Trips { get; set; } = null!;
        public DbSet<Vehicle> Vehicles { get; set; } = null!;
        public DbSet<Shipment> Shipments { get; set; } = null!;
        public DbSet<TripShipment> TripShipments { get; set; } = null!;
        public DbSet<Hub> Hubs { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Trip>(b =>
            {
                b.ToTable("trips", "transport");
                b.Property(p => p.Version).IsRowVersion();
                b.HasIndex(p => p.DriverId);
                b.HasIndex(p => p.VehicleId);
                b.HasOne<Vehicle>()
                    .WithMany()
                    .HasForeignKey(p => p.VehicleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Vehicle>(b => b.ToTable("vehicles", "identity"));
            modelBuilder.Entity<Shipment>(b => b.ToTable("shipments", "warehouse"));

            modelBuilder.Entity<TripShipment>(b =>
            {
                b.ToTable("trip_shipments", "transport");
                b.HasIndex(p => p.TripId);
                b.HasIndex(p => p.ShipmentId);
                b.HasOne<Trip>()
                    .WithMany()
                    .HasForeignKey(p => p.TripId)
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasOne<Shipment>()
                    .WithMany()
                    .HasForeignKey(p => p.ShipmentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
