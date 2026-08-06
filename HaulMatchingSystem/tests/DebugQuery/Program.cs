using Npgsql;

var cs = "Host=localhost;Port=5432;Database=hms_db;Username=postgres;Password=hms_password_123";
await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();

Console.WriteLine("=== 1. Check Matched shipments without trip_shipments ===");
{
    var sql = @"
SELECT s.id, s.qr_code, s.status, s.is_deleted
FROM warehouse.shipments s
WHERE s.status = 'Matched' AND s.is_deleted = FALSE
ORDER BY s.created_at DESC";
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        Console.WriteLine($"  Shipment: {r.GetString(1)} | id: {r.GetGuid(0)} | status: {r.GetString(2)} | deleted: {r.GetBoolean(3)}");
    }
}

Console.WriteLine("\n=== 2. All trip_shipments records ===");
{
    var sql = @"
SELECT ts.id, ts.trip_id, ts.shipment_id, ts.status, ts.is_deleted,
       s.qr_code AS shipment_code
FROM transport.trip_shipments ts
LEFT JOIN warehouse.shipments s ON s.id = ts.shipment_id
WHERE ts.is_deleted = FALSE
ORDER BY ts.created_at DESC";
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        Console.WriteLine($"  TS: {r.GetString(5) ?? "N/A"} | trip: {r.GetGuid(1)} | status: {r.GetString(3)} | deleted: {r.GetBoolean(4)}");
    }
}

Console.WriteLine("\n=== 3. Shipment proposals for Matched shipments ===");
{
    var sql = @"
SELECT s.qr_code AS shipment_code, s.id AS shipment_id,
       sp.id AS proposal_id, sp.trip_post_id, sp.status AS proposal_status, sp.is_deleted AS sp_deleted,
       tp.trip_id, tp.is_deleted AS tp_deleted
FROM warehouse.shipments s
JOIN warehouse.shipment_proposals sp ON sp.shipment_id = s.id
LEFT JOIN transport.trip_posts tp ON tp.id = sp.trip_post_id
WHERE s.status = 'Matched' AND s.is_deleted = FALSE
ORDER BY s.created_at DESC";
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        var qrCode = r.GetString(0);
        var proposalId = r.GetGuid(2);
        var tripPostId = r.IsDBNull(3) ? "NULL" : r.GetGuid(3).ToString();
        var proposalStatus = r.GetString(4);
        var spDeleted = r.GetBoolean(5);
        var tripId = r.IsDBNull(6) ? "NULL" : r.GetGuid(6).ToString();
        var tpDeleted = r.IsDBNull(7) ? "N/A" : r.GetBoolean(7).ToString();
        Console.WriteLine($"  {qrCode} | proposal: {proposalId} (status={proposalStatus}, deleted={spDeleted}) | tripPost: {tripPostId} (deleted={tpDeleted}) | trip: {tripId}");
    }
}

Console.WriteLine("\n=== 4. Check if migration would find any orphans ===");
{
    var sql = @"
SELECT COUNT(*) 
FROM warehouse.shipments s
JOIN warehouse.shipment_proposals sp ON sp.shipment_id = s.id AND sp.is_deleted = FALSE
JOIN transport.trip_posts tp ON tp.id = sp.trip_post_id AND tp.is_deleted = FALSE
WHERE s.status = 'Matched' AND s.is_deleted = FALSE
  AND NOT EXISTS (
      SELECT 1 FROM transport.trip_shipments ts
      WHERE ts.shipment_id = s.id AND ts.is_deleted = FALSE
  )";
    await using var cmd = new NpgsqlCommand(sql, conn);
    var count = await cmd.ExecuteScalarAsync();
    Console.WriteLine($"  Orphans found by migration SQL: {count}");
}

Console.WriteLine("\n=== 5. Check all proposals ===");
{
    var sql = @"
SELECT s.qr_code, sp.id, sp.trip_post_id, sp.status, sp.is_deleted
FROM warehouse.shipment_proposals sp
JOIN warehouse.shipments s ON s.id = sp.shipment_id AND s.is_deleted = FALSE
WHERE sp.is_deleted = FALSE
ORDER BY sp.created_at DESC";
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        var tripPostId = r.IsDBNull(2) ? "NULL" : r.GetGuid(2).ToString();
        Console.WriteLine($"  {r.GetString(0)} | proposal: {r.GetGuid(1)} | tripPost: {tripPostId} | status: {r.GetString(3)} | deleted: {r.GetBoolean(4)}");
    }
}

Console.WriteLine("\n=== 6. Trip details for linked trips ===");
{
    var sql = @"
SELECT t.id, t.trip_code, t.status, t.is_deleted,
       t.origin_hub_id, t.dest_hub_id
FROM transport.trips t
WHERE t.id IN (
    SELECT DISTINCT ts.trip_id FROM transport.trip_shipments ts
    WHERE ts.shipment_id IN (
        '76c281b2-2af9-45ab-ac77-bfd739cfd8bc',
        '3e3d364b-61e7-45c2-98d0-87ee67ff0d19',
        'b02b977b-6467-4454-a0ea-ac3a72a54cbc',
        '1a012b17-b5fb-41b2-8931-3dd0946450b3',
        'e2ee03a4-91e0-4434-afe7-14e3fb8c4c9c',
        'bf42d22e-8f2f-49dd-9433-346e0d26c94b'
    ) AND ts.is_deleted = FALSE
)
ORDER BY t.created_at DESC";
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        var tripCode = r.IsDBNull(1) ? "NULL" : r.GetString(1);
        Console.WriteLine($"  Trip: {r.GetGuid(0)} | tripCode: {tripCode} | status: {r.GetString(2)} | deleted: {r.GetBoolean(3)} | origin: {r.GetGuid(4)} | dest: {r.GetGuid(5)}");
    }
}

Console.WriteLine("\n=== 7. Verify: customer SQL JOIN for tripCode ===");
{
    var sql = @"
SELECT s.qr_code, s.status, ts.status as ts_status, t.trip_code
FROM warehouse.shipments s
LEFT JOIN transport.trip_shipments ts ON ts.shipment_id = s.id AND ts.is_deleted = FALSE
LEFT JOIN transport.trips t ON t.id = ts.trip_id AND t.is_deleted = FALSE
WHERE s.status = 'Matched' AND s.is_deleted = FALSE
ORDER BY s.created_at DESC";
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        var tripCode = r.IsDBNull(3) ? "NULL" : r.GetString(3);
        var tsStatus = r.IsDBNull(2) ? "NULL" : r.GetString(2);
        Console.WriteLine($"  {r.GetString(0)} | status: {r.GetString(1)} | ts: {tsStatus} | tripCode: {tripCode}");
    }
}

Console.WriteLine("\nDone.");

Console.WriteLine("\n=== 8. Driver info for active trips ===");
{
    var sql = @"
SELECT t.id AS trip_id, t.trip_code, t.status, t.driver_id,
       u.email, u.full_name
FROM transport.trips t
JOIN identity.users u ON u.id = t.driver_id
WHERE t.id IN (
    'f86fbcca-dfc2-4305-95c3-646db62351b4',
    'afb49a8f-933a-4722-8cc6-86153e3e0dc4',
    'f52ed2d2-2cac-4a01-bfc1-1ba9a3c06f35'
)";
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        Console.WriteLine($"  Trip: {r.GetString(1)} | status: {r.GetString(2)} | driver: {r.GetString(4)} ({r.GetString(5)})");
    }
}

Console.WriteLine("\n=== 9. Driver trip detail simulation ===");
{
    var sql = @"
SELECT ts.id, ts.trip_id, ts.shipment_id, ts.delivery_sequence, ts.status,
       s.qr_code, s.receiver_name, s.dest_address
FROM transport.trip_shipments ts
JOIN warehouse.shipments s ON s.id = ts.shipment_id AND s.is_deleted = FALSE
WHERE ts.trip_id = 'f86fbcca-dfc2-4305-95c3-646db62351b4' AND ts.is_deleted = FALSE
ORDER BY ts.delivery_sequence";
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        Console.WriteLine($"  #{r.GetInt32(3)} {r.GetString(5)} | status: {r.GetString(4)} | to: {r.GetString(6)} @ {r.GetString(7)}");
    }
}

Console.WriteLine("\nDone.");
