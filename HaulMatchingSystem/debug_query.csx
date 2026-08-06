using Npgsql;
var cs = "Host=localhost;Port=5432;Database=hms_db;Username=postgres;Password=hms_password_123";
await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();

// Check if trip_shipments exist for any of the 6 orphaned shipments
var sql = @"
SELECT s.id, s.qr_code, s.status, 
       ts.id as ts_id, ts.trip_id, ts.status as ts_status,
       sp.id as sp_id, sp.trip_post_id, sp.status as sp_status
FROM warehouse.shipments s
LEFT JOIN transport.trip_shipments ts ON ts.shipment_id = s.id AND ts.is_deleted = FALSE
LEFT JOIN warehouse.shipment_proposals sp ON sp.shipment_id = s.id AND sp.is_deleted = FALSE
WHERE s.status = 'Matched' AND s.is_deleted = FALSE
ORDER BY s.created_at DESC
"@;
await using var cmd = new NpgsqlCommand(sql, conn);
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync()) {
    var shipmentId = reader.GetGuid(0);
    var qrCode = reader.GetString(1);
    var status = reader.GetString(2);
    var tsId = reader.IsDBNull(3) ? "NULL" : reader.GetGuid(3).ToString();
    var tripId = reader.IsDBNull(4) ? "NULL" : reader.GetGuid(4).ToString();
    var tsStatus = reader.IsDBNull(5) ? "NULL" : reader.GetString(5);
    var spId = reader.IsDBNull(6) ? "NULL" : reader.GetGuid(6).ToString();
    var tripPostId = reader.IsDBNull(7) ? "NULL" : reader.GetGuid(7).ToString();
    var spStatus = reader.IsDBNull(8) ? "NULL" : reader.GetString(8);
    Console.WriteLine($"{qrCode} | ts: {tsId} trip: {tripId} tsStatus: {tsStatus} | sp: {spId} tripPost: {tripPostId} spStatus: {spStatus}");
}
