using Npgsql;
var cs = "Host=localhost;Port=5432;Database=hms_db;Username=postgres;Password=hms_password_123";
using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();
using var cmd = new NpgsqlCommand("SELECT email, password_hash, role FROM identity.users WHERE email LIKE '%driver%' OR email LIKE '%test%'", conn);
using var r = await cmd.ExecuteReaderAsync();
while (await r.ReadAsync())
    Console.WriteLine($"  {r.GetString(0)} | role={r.GetString(2)} | hash={r.GetString(1)[..30]}...");
