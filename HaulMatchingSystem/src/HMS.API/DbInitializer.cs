using System;
using System.Linq;
using HMS.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HMS.API
{
    public static class DbInitializer
    {
        public static void Initialize(IdentityDbContext identityDb)
        {
            try
            {
                // 1. Chạy SQL vá Database (Schema & Cột)
                identityDb.Database.ExecuteSqlRaw(
                    @"
                    CREATE SCHEMA IF NOT EXISTS identity;

                    CREATE TABLE IF NOT EXISTS identity.users (
                        id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                        hub_id uuid,
                        full_name text NOT NULL,
                        avatar_url text,
                        phone text,
                        email text,
                        password_hash text,
                        google_id text,
                        reset_password_token text,
                        reset_token_expires_at timestamp with time zone,
                        role text NOT NULL,
                        refresh_token text,
                        refresh_token_expiry_time timestamp with time zone,
                        created_at timestamp with time zone DEFAULT now(),
                        updated_at timestamp with time zone DEFAULT now(),
                        is_deleted boolean DEFAULT false
                    );

                    CREATE TABLE IF NOT EXISTS identity.hubs (
                        id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                        name text NOT NULL,
                        address text NOT NULL,
                        created_at timestamp with time zone DEFAULT now(),
                        updated_at timestamp with time zone DEFAULT now(),
                        is_deleted boolean DEFAULT false
                    );

                    CREATE TABLE IF NOT EXISTS identity.vehicles (
                        id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                        hub_id uuid NOT NULL,
                        license_plate text NOT NULL,
                        truck_type text NOT NULL,
                        max_weight_kg numeric NOT NULL,
                        max_volume_cbm numeric NOT NULL,
                        created_at timestamp with time zone DEFAULT now(),
                        updated_at timestamp with time zone DEFAULT now(),
                        is_deleted boolean DEFAULT false
                    );

                    ALTER TABLE identity.users ADD COLUMN IF NOT EXISTS refresh_token text;
                    ALTER TABLE identity.users ADD COLUMN IF NOT EXISTS refresh_token_expiry_time timestamp with time zone;

                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 
                            FROM information_schema.columns 
                            WHERE table_schema='identity' AND table_name='hubs' AND column_name='geo_location'
                        ) THEN
                            ALTER TABLE identity.hubs ALTER COLUMN geo_location DROP NOT NULL;
                        END IF;
                    END $$;
                "
                );

                // 2. Seed dữ liệu Hubs mặc định
                if (!identityDb.Hubs.Any())
                {
                    identityDb.Hubs.AddRange(
                        new HMS.Modules.Identity.Core.Entities.Hub
                        {
                            Id = Guid.Parse("11111111-2222-3333-4444-555555555551"),
                            Name = "Kho Gò Vấp - TP.HCM",
                            Address = "12 Nguyễn Oanh, Gò Vấp, HCMC",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                        },
                        new HMS.Modules.Identity.Core.Entities.Hub
                        {
                            Id = Guid.Parse("11111111-2222-3333-4444-555555555552"),
                            Name = "Kho Tân Bình - TP.HCM",
                            Address = "45 Cộng Hòa, Tân Bình, HCMC",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                        },
                        new HMS.Modules.Identity.Core.Entities.Hub
                        {
                            Id = Guid.Parse("11111111-2222-3333-4444-555555555553"),
                            Name = "Kho Hà Nội",
                            Address = "102 Giải Phóng, Đống Đa, Hà Nội",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                        },
                        new HMS.Modules.Identity.Core.Entities.Hub
                        {
                            Id = Guid.Parse("11111111-2222-3333-4444-555555555554"),
                            Name = "Kho Đà Nẵng",
                            Address = "88 Nguyễn Lương Bằng, Liên Chiểu, Đà Nẵng",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                        }
                    );
                    identityDb.SaveChanges();
                    Console.WriteLine("Đã seed thành công 4 kho hàng mặc định vào database.");
                }

                // 3. Seed / Cập nhật tài khoản mặc định
                // Admin
                var existingAdmin = identityDb.Users.FirstOrDefault(u =>
                    u.Email == "admin@hms.com" || u.Email == "admin@gmail.com"
                );
                if (existingAdmin != null)
                {
                    existingAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123");
                    existingAdmin.Role = "Admin";
                    existingAdmin.FullName = "System Admin";
                    identityDb.Users.Update(existingAdmin);
                }
                else
                {
                    identityDb.Users.Add(
                        new HMS.Modules.Identity.Core.Entities.User
                        {
                            Id = Guid.NewGuid(),
                            FullName = "System Admin",
                            Email = "admin@hms.com",
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                            Role = "Admin",
                            Phone = "0987654321",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsDeleted = false,
                        }
                    );
                }

                // Driver
                var existingDriver = identityDb.Users.FirstOrDefault(u =>
                    u.Email == "driver@hms.com"
                );
                if (existingDriver != null)
                {
                    existingDriver.PasswordHash = BCrypt.Net.BCrypt.HashPassword("driver123");
                    existingDriver.Role = "Driver";
                    identityDb.Users.Update(existingDriver);
                }
                else
                {
                    identityDb.Users.Add(
                        new HMS.Modules.Identity.Core.Entities.User
                        {
                            Id = Guid.NewGuid(),
                            FullName = "Driver Nguyen",
                            Email = "driver@hms.com",
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("driver123"),
                            Role = "Driver",
                            Phone = "0912345678",
                            HubId = Guid.Parse("11111111-2222-3333-4444-555555555551"),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsDeleted = false,
                        }
                    );
                }

                // Customer
                var existingCustomer = identityDb.Users.FirstOrDefault(u =>
                    u.Email == "customer@hms.com"
                );
                if (existingCustomer != null)
                {
                    existingCustomer.PasswordHash = BCrypt.Net.BCrypt.HashPassword("customer123");
                    existingCustomer.Role = "Customer";
                    identityDb.Users.Update(existingCustomer);
                }
                else
                {
                    identityDb.Users.Add(
                        new HMS.Modules.Identity.Core.Entities.User
                        {
                            Id = Guid.NewGuid(),
                            FullName = "Customer An",
                            Email = "customer@hms.com",
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("customer123"),
                            Role = "Customer",
                            Phone = "0909090909",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsDeleted = false,
                        }
                    );
                }

                identityDb.SaveChanges();
                Console.WriteLine(
                    "Đã cấu hình & cập nhật tài khoản mẫu thành công: admin@hms.com / admin123"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khởi tạo database: " + ex.Message);
            }
        }
    }
}
