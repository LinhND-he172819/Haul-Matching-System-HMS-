using HMS.Modules.Identity.Core.Entities;
using HMS.Modules.Identity.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HMS.Modules.Identity.Infrastructure
{
    public class IdentityDbContext : DbContext, IIdentityDbContext
    {
        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Hub> Hubs { get; set; } = null!;
        public DbSet<Vehicle> Vehicles { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(b =>
            {
                b.ToTable("users", "identity");

                b.HasKey(u => u.Id);
                b.Property(u => u.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

                b.Property(u => u.HubId).HasColumnName("hub_id");
                b.Property(u => u.FullName).HasColumnName("full_name").IsRequired();
                b.Property(u => u.AvatarUrl).HasColumnName("avatar_url");
                b.Property(u => u.Phone).HasColumnName("phone");
                b.Property(u => u.Email).HasColumnName("email");
                b.Property(u => u.PasswordHash).HasColumnName("password_hash");
                b.Property(u => u.GoogleId).HasColumnName("google_id");
                b.Property(u => u.ResetPasswordToken).HasColumnName("reset_password_token");
                b.Property(u => u.ResetTokenExpiresAt).HasColumnName("reset_token_expires_at");
                b.Property(u => u.Role).HasColumnName("role").IsRequired();
                b.Property(u => u.RefreshToken).HasColumnName("refresh_token");
                b.Property(u => u.RefreshTokenExpiryTime)
                    .HasColumnName("refresh_token_expiry_time");
                b.Property(u => u.CreatedAt).HasColumnName("created_at");
                b.Property(u => u.UpdatedAt).HasColumnName("updated_at");
                b.Property(u => u.IsDeleted).HasColumnName("is_deleted");
            });

            modelBuilder.Entity<Hub>(b =>
            {
                b.ToTable("hubs", "identity");

                b.HasKey(h => h.Id);
                b.Property(h => h.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

                b.Property(h => h.Name).HasColumnName("name").IsRequired();
                b.Property(h => h.Address).HasColumnName("address").IsRequired();
                b.Property(h => h.CreatedAt).HasColumnName("created_at");
                b.Property(h => h.UpdatedAt).HasColumnName("updated_at");
                b.Property(h => h.IsDeleted).HasColumnName("is_deleted");
            });

            modelBuilder.Entity<Vehicle>(b =>
            {
                b.ToTable("vehicles", "identity");

                b.HasKey(v => v.Id);
                b.Property(v => v.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

                b.Property(v => v.HubId).HasColumnName("hub_id").IsRequired();
                b.Property(v => v.LicensePlate).HasColumnName("license_plate").IsRequired();
                b.Property(v => v.TruckType).HasColumnName("truck_type").IsRequired();
                b.Property(v => v.MaxWeightKg).HasColumnName("max_weight_kg").IsRequired();
                b.Property(v => v.MaxVolumeCbm).HasColumnName("max_volume_cbm").IsRequired();
                b.Property(v => v.CreatedAt).HasColumnName("created_at");
                b.Property(v => v.UpdatedAt).HasColumnName("updated_at");
                b.Property(v => v.IsDeleted).HasColumnName("is_deleted");
            });
        }
    }
}
