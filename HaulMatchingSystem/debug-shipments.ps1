# Debug script to check orphaned shipments
$connectionString = "Host=localhost;Port=5432;Database=hms_db;Username=postgres;Password=hms_password_123"

# We need to use dotnet-script or a C# snippet to query PostgreSQL
# Let's use the existing API with a simple admin login and API call

$lb = '{"email":"admin@hms.com","password":"admin123"}'
$lr = Invoke-RestMethod -Uri "http://localhost:5104/api/auth/login" -Method POST -ContentType "application/json" -Body $lb
$at = $lr.accessToken
$h = @{Authorization = "Bearer $at"}

Write-Output "=== Checking all Matched shipments ==="
$allResp = Invoke-RestMethod -Uri "http://localhost:5104/api/customer/shipments?page=1&pageSize=50" -Method GET -Headers $h

# Check each matched shipment's proposals
$shipmentIds = @(
    "76c281b2-2af9-45ab-ac77-bfd739cfd8bc",
    "b02b977b-6467-4454-a0ea-ac3a72a54cbc"
)

foreach ($sid in $shipmentIds) {
    Write-Output "`n--- Checking shipment $sid ---"
    try {
        $detail = Invoke-RestMethod -Uri "http://localhost:5104/api/staff/shipments/$sid" -Headers $h -ErrorAction Stop
        Write-Output "Status: $($detail.status)"
        Write-Output "TripCode: $($detail.tripCode)"
        if ($detail.proposals) { 
            foreach ($p in $detail.proposals) {
                Write-Output "Proposal: $($p.id) Status: $($p.status) TripPostId: $($p.tripPostId)"
            }
        }
    } catch {
        Write-Output "Error: $($_.Exception.Message)"
    }
}
