using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace HMS.Modules.Transport.Data
{
    public class TransportDbContext : DbContext
    {
        public TransportDbContext(DbContextOptions<TransportDbContext> options) : base(options)
        {
        }

        public DbSet<GpsLog> GpsLogs { get; set; }
        public DbSet<Trip> Trips { get; set; }
        public DbSet<TripException> TripExceptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("transport");

            modelBuilder.Entity<Trip>(b =>
            {
                b.ToTable("trips");

                b.HasKey(x => x.Id);

                b.Property(x => x.Id).HasColumnName("id");

                b.Property(x => x.DriverId).HasColumnName("driver_id");

                b.Property(x => x.VehicleId).HasColumnName("vehicle_id");

                b.Property(x => x.OriginHubId).HasColumnName("origin_hub_id");

                b.Property(x => x.DestHubId).HasColumnName("dest_hub_id");

                b.Property(x => x.RouteLineString).HasColumnName("route_linestring");

                b.Property(x => x.CurrentLoadWeightKg).HasColumnName("current_load_weight");

                b.Property(x => x.CurrentLoadVolumeCbm).HasColumnName("current_load_volume");

                b.Property(x => x.StartedAt).HasColumnName("started_at");

                b.Property(x => x.FinishedAt).HasColumnName("finished_at");

                b.Property(x => x.Version).HasColumnName("version");

                b.Property(x => x.Status)
                    .HasColumnName("status")
                    .HasConversion<string>();

                b.Property(x => x.CreatedAt).HasColumnName("created_at");

                b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            });

            modelBuilder.Entity<GpsLog>(b =>
            {
                b.ToTable("gps_logs");

                b.HasKey(x => x.Id);

                b.Property(x => x.Id).HasColumnName("id");
                b.Property(x => x.TripId).HasColumnName("trip_id");
                b.Property(x => x.Lat).HasColumnName("lat");
                b.Property(x => x.Lng).HasColumnName("lng");
                b.Property(x => x.Speed).HasColumnName("speed");
                b.Property(x => x.DeviceTimestamp).HasColumnName("device_timestamp");
                b.Property(x => x.ServerReceivedAt).HasColumnName("server_received_at");
                b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");
            });

            modelBuilder.Entity<StaleTripQueryResult>().HasNoKey();
        }
    }
}
