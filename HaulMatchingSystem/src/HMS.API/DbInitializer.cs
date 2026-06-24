using HMS.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HMS.API;

public static class DbInitializer
{
    private const string DefaultHubId = "11111111-2222-3333-4444-555555555551";

    public static void Initialize(IdentityDbContext identityDb)
    {
        try
        {
            EnsureSchema(identityDb);
            SeedDefaultHubs(identityDb);
            SeedDefaultUsers(identityDb);

            Console.WriteLine("Database seed completed. Admin login: admin@hms.com / admin123");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Database initialization failed: " + ex.Message);
        }
    }

    private static void EnsureSchema(IdentityDbContext identityDb)
    {
        identityDb.Database.ExecuteSqlRaw(
            """
            CREATE EXTENSION IF NOT EXISTS pgcrypto;
            CREATE EXTENSION IF NOT EXISTS postgis;
            CREATE SCHEMA IF NOT EXISTS identity;

            CREATE TABLE IF NOT EXISTS identity.hubs (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                name text NOT NULL,
                address text NOT NULL,
                geo_location geography(Point, 4326),
                created_at timestamp with time zone DEFAULT now(),
                updated_at timestamp with time zone DEFAULT now(),
                is_deleted boolean DEFAULT false
            );

            CREATE TABLE IF NOT EXISTS public.hubs (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                name text NOT NULL,
                address text NOT NULL,
                geo_location geography(Point, 4326),
                created_at timestamp with time zone DEFAULT now(),
                updated_at timestamp with time zone DEFAULT now(),
                is_deleted boolean DEFAULT false
            );

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
            ALTER TABLE identity.hubs ADD COLUMN IF NOT EXISTS geo_location geography(Point, 4326);
            ALTER TABLE public.hubs ADD COLUMN IF NOT EXISTS geo_location geography(Point, 4326);
            ALTER TABLE identity.hubs ALTER COLUMN geo_location DROP NOT NULL;
            ALTER TABLE public.hubs ALTER COLUMN geo_location DROP NOT NULL;
            """
        );
    }

    private static void SeedDefaultHubs(IdentityDbContext identityDb)
    {
        identityDb.Database.ExecuteSqlRaw(
            """
            WITH seed_hubs(id, name, address, longitude, latitude) AS (
                VALUES
                    ('11111111-2222-3333-4444-555555555551'::uuid, 'Kho Go Vap - TP.HCM', '12 Nguyen Oanh, Go Vap, HCMC', 106.6667, 10.8380),
                    ('11111111-2222-3333-4444-555555555552'::uuid, 'Kho Tan Binh - TP.HCM', '45 Cong Hoa, Tan Binh, HCMC', 106.6520, 10.8010),
                    ('11111111-2222-3333-4444-555555555553'::uuid, 'Kho Ha Noi', '102 Giai Phong, Dong Da, Ha Noi', 105.8412, 21.0035),
                    ('11111111-2222-3333-4444-555555555554'::uuid, 'Kho Da Nang', '88 Nguyen Luong Bang, Lien Chieu, Da Nang', 108.1530, 16.0710)
            )
            INSERT INTO identity.hubs (id, name, address, geo_location, created_at, updated_at, is_deleted)
            SELECT
                id,
                name,
                address,
                ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)::geography,
                now(),
                now(),
                false
            FROM seed_hubs
            ON CONFLICT (id) DO UPDATE
            SET
                name = EXCLUDED.name,
                address = EXCLUDED.address,
                geo_location = EXCLUDED.geo_location,
                updated_at = now(),
                is_deleted = false;

            WITH seed_hubs(id, name, address, longitude, latitude) AS (
                VALUES
                    ('11111111-2222-3333-4444-555555555551'::uuid, 'Kho Go Vap - TP.HCM', '12 Nguyen Oanh, Go Vap, HCMC', 106.6667, 10.8380),
                    ('11111111-2222-3333-4444-555555555552'::uuid, 'Kho Tan Binh - TP.HCM', '45 Cong Hoa, Tan Binh, HCMC', 106.6520, 10.8010),
                    ('11111111-2222-3333-4444-555555555553'::uuid, 'Kho Ha Noi', '102 Giai Phong, Dong Da, Ha Noi', 105.8412, 21.0035),
                    ('11111111-2222-3333-4444-555555555554'::uuid, 'Kho Da Nang', '88 Nguyen Luong Bang, Lien Chieu, Da Nang', 108.1530, 16.0710)
            )
            INSERT INTO public.hubs (id, name, address, geo_location, created_at, updated_at, is_deleted)
            SELECT
                id,
                name,
                address,
                ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)::geography,
                now(),
                now(),
                false
            FROM seed_hubs
            ON CONFLICT (id) DO UPDATE
            SET
                name = EXCLUDED.name,
                address = EXCLUDED.address,
                geo_location = EXCLUDED.geo_location,
                updated_at = now(),
                is_deleted = false;
            """
        );
    }

    private static void SeedDefaultUsers(IdentityDbContext identityDb)
    {
        UpsertUser(
            identityDb,
            email: "admin@hms.com",
            fullName: "System Admin",
            password: "admin123",
            role: "Admin",
            phone: "0987654321",
            hubId: null);

        UpsertUser(
            identityDb,
            email: "driver@hms.com",
            fullName: "Driver Nguyen",
            password: "driver123",
            role: "Driver",
            phone: "0912345678",
            hubId: Guid.Parse(DefaultHubId));

        UpsertUser(
            identityDb,
            email: "customer@hms.com",
            fullName: "Customer An",
            password: "customer123",
            role: "Customer",
            phone: "0909090909",
            hubId: null);

        identityDb.SaveChanges();
    }

    private static void UpsertUser(
        IdentityDbContext identityDb,
        string email,
        string fullName,
        string password,
        string role,
        string phone,
        Guid? hubId)
    {
        var user = identityDb.Users.FirstOrDefault(u => u.Email == email);
        if (user is null)
        {
            identityDb.Users.Add(new HMS.Modules.Identity.Core.Entities.User
            {
                Id = Guid.NewGuid(),
                Email = email,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            });
            user = identityDb.Users.Local.First(u => u.Email == email);
        }

        user.FullName = fullName;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        user.Role = role;
        user.Phone = phone;
        user.HubId = hubId;
        user.UpdatedAt = DateTime.UtcNow;
        user.IsDeleted = false;
    }
}
