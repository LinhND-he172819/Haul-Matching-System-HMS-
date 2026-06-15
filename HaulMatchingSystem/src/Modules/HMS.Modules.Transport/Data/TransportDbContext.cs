using HMS.Modules.Transport.Entities;
using Microsoft.EntityFrameworkCore;

namespace HMS.Modules.Transport.Data
{
    public class TransportDbContext : DbContext
    {
        public TransportDbContext(DbContextOptions<TransportDbContext> options) : base(options)
        {
        }

        public DbSet<GpsLog> GpsLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Bắt buộc cấu hình Schema mặc định cho toàn bộ bảng trong module này
            modelBuilder.HasDefaultSchema("transport");

            // Cấu hình Unique Index cho cột IdempotencyKey của bảng GpsLogs
            modelBuilder.Entity<GpsLog>()
                .HasIndex(g => g.IdempotencyKey)
                .IsUnique();
        }
    }
}
