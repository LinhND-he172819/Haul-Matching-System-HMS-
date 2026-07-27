using HMS.Modules.Matching.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HMS.Modules.Matching.Infrastructure
{
    public class MatchingDbContext : DbContext
    {
        public MatchingDbContext(DbContextOptions<MatchingDbContext> options)
            : base(options) { }

        public DbSet<Trip> Trips { get; set; } = null!;
        public DbSet<Vehicle> Vehicles { get; set; } = null!;
        public DbSet<Shipment> Shipments { get; set; } = null!;
        public DbSet<TripShipment> TripShipments { get; set; } = null!;
        public DbSet<ShipmentProposal> ShipmentProposals { get; set; } = null!;
        public DbSet<Quotation> Quotations { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Trip>(b =>
            {
                b.ToTable("trips", "transport");
                b.Property(p => p.Version).IsConcurrencyToken();
                b.HasIndex(p => p.DriverId);
                b.HasIndex(p => p.VehicleId);
                b.HasOne<Vehicle>()
                    .WithMany()
                    .HasForeignKey(p => p.VehicleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Vehicle>(b => b.ToTable("vehicles", "transport"));
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

            modelBuilder.Entity<ShipmentProposal>(b =>
            {
                b.ToTable("shipment_proposals", "warehouse");
                b.HasIndex(p => p.ShipmentId);
                b.HasIndex(p => p.TripPostId);
                b.HasIndex(p => p.CustomerId);
                b.HasIndex(p => p.Status);
                // Unique constraint: one pending/approved proposal per shipment per trip post
                b.HasIndex(p => new { p.ShipmentId, p.TripPostId }).IsUnique()
                    .HasFilter("status IN ('PendingReview', 'Approved')");
            });

            modelBuilder.Entity<Quotation>(b =>
            {
                b.ToTable("quotations", "warehouse");
                b.HasIndex(p => p.ProposalId);
                b.HasIndex(p => p.Status);
            });

            modelBuilder.Entity<Payment>(b =>
            {
                b.ToTable("payments", "warehouse");
                b.HasIndex(p => p.QuotationId);
                b.HasIndex(p => p.ShipmentId);
                b.HasIndex(p => p.CustomerId);
                b.HasIndex(p => p.TransactionReference).IsUnique();
                b.HasIndex(p => p.IdempotencyKey).IsUnique();
            });
        }
    }
}
