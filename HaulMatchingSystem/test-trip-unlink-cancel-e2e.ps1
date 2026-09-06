$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5104'
$ORIGIN = '11111111-2222-3333-4444-555555555551'
$DEST = '11111111-2222-3333-4444-555555555554'

function Get-ErrorBody($errorRecord) {
    try {
        $response = $errorRecord.Exception.Response
        $stream = $response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        return $reader.ReadToEnd()
    } catch { return $errorRecord.ErrorDetails.Message }
}

function Assert-Equal($actual, $expected, $message) {
    if ($actual -ne $expected) { throw "$message. Expected '$expected', got '$actual'." }
    Write-Host "PASS $message" -ForegroundColor Green
}

$adminLogin = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json)
$adminHeaders = @{ Authorization = "Bearer $($adminLogin.accessToken)"; 'Content-Type' = 'application/json' }
$stamp = Get-Date -Format 'HHmmssfff'

$staffEmail = "e2e_wh_$stamp@hms.com"
$staff = Invoke-RestMethod -Uri "$base/api/identity/users" -Method POST -Headers $adminHeaders -Body (@{
    email = $staffEmail; password = 'Test1234!'; fullName = "E2E Staff $stamp"; phone = "092$stamp"; role = 'Warehouse_Staff'; hubId = $ORIGIN
} | ConvertTo-Json)
$staffLogin = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = $staffEmail; password = 'Test1234!' } | ConvertTo-Json)
$staffHeaders = @{ Authorization = "Bearer $($staffLogin.accessToken)"; 'Content-Type' = 'application/json' }

$driverEmail = "e2e_drv_$stamp@hms.com"
$driver = Invoke-RestMethod -Uri "$base/api/identity/users" -Method POST -Headers $adminHeaders -Body (@{
    email = $driverEmail; password = 'Test1234!'; fullName = "E2E Driver $stamp"; phone = "090$stamp"; role = 'Driver'; hubId = $ORIGIN
} | ConvertTo-Json)
$vehicle = Invoke-RestMethod -Uri "$base/api/vehicles" -Method POST -Headers $adminHeaders -Body (@{
    code = "E2E-$stamp"; licensePlate = "E2E-$stamp"; vehicleType = 'Truck'; maxWeightKg = 5000; maxVolumeCbm = 30; hubId = $ORIGIN; status = 'Available'
} | ConvertTo-Json)

function New-IntakeShipment($index) {
    $draft = Invoke-RestMethod -Uri "$base/api/shipments/draft" -Method POST -Headers $adminHeaders -Body (@{
        customerId = $adminLogin.userId; cargoType = "E2E cargo $index"; weightKg = 20; volumeCbm = 0.2
        senderName = "Sender $index"; senderPhone = "090$stamp$index"; pickupAddress = "Origin $index"; pickupLat = 10.8; pickupLng = 106.7
        receiverName = "Receiver $index"; receiverPhone = "091$stamp$index"; destAddress = "Destination $index"; destLat = 10.8; destLng = 106.7
    } | ConvertTo-Json)
    Invoke-RestMethod -Uri "$base/api/shipments/$($draft.id)/confirm-intake" -Method PUT -Headers $staffHeaders -Body (@{ actualWeightKg = 20; actualVolumeCbm = 0.2 } | ConvertTo-Json) | Out-Null
    return $draft.id
}

function New-Trip($shipmentId) {
    return Invoke-RestMethod -Uri "$base/api/trips" -Method POST -Headers $adminHeaders -Body (@{
        driverId = $driver.id; vehicleId = $vehicle.id; originHubId = $ORIGIN; destHubId = $DEST
        routeLineString = 'LINESTRING (106.7 10.8, 108.15 16.07)'; currentLoadWeightKg = 0; currentLoadVolumeCbm = 0
        scheduledDepartureAt = (Get-Date).ToUniversalTime().AddDays(2).ToString('o'); warehouseShipmentIds = @($shipmentId)
    } | ConvertTo-Json)
}

function Get-History($shipmentId) {
    return Invoke-RestMethod -Uri "$base/api/shipments/$shipmentId/status-history" -Method GET -Headers $adminHeaders
}

$sUnlink = New-IntakeShipment 1
$sCancel = New-IntakeShipment 2
Write-Host "Created shipments for isolated tests: $sUnlink, $sCancel"

$tripUnlink = New-Trip $sUnlink
$beforeUnlink = @(Invoke-RestMethod -Uri "$base/api/trips/$($tripUnlink.id)/shipments" -Method GET -Headers $adminHeaders)
Assert-Equal $beforeUnlink.Count 1 'unlink trip initially contains one shipment'
Assert-Equal $beforeUnlink[0].status 'Matched' 'unlink candidate is Matched'

$unlinkResponse = Invoke-RestMethod -Uri "$base/api/trips/$($tripUnlink.id)/shipments/$sUnlink" -Method DELETE -Headers $adminHeaders
$afterUnlink = @(Invoke-RestMethod -Uri "$base/api/trips/$($tripUnlink.id)/shipments" -Method GET -Headers $adminHeaders)
Assert-Equal $afterUnlink.Count 1 'DELETE preserves shipment detail for historical trip visibility'
$unlinkHistory = Get-History $sUnlink
$unlinkLast = @($unlinkHistory.history)[-1]
Assert-Equal $unlinkLast.toStatus 'In_Warehouse' 'DELETE history ends at In_Warehouse'

$tripCancel = New-Trip $sCancel
$beforeCancel = @(Invoke-RestMethod -Uri "$base/api/trips/$($tripCancel.id)/shipments" -Method GET -Headers $adminHeaders)
Assert-Equal $beforeCancel.Count 1 'cancel trip initially contains one shipment'
Assert-Equal $beforeCancel[0].status 'Matched' 'cancel candidate is Matched'

$cancelResponse = Invoke-RestMethod -Uri "$base/api/trips/$($tripCancel.id)/status" -Method PATCH -Headers $adminHeaders -Body (@{ status = 'Cancelled' } | ConvertTo-Json)
Assert-Equal $cancelResponse.status 'Cancelled' 'trip cancellation succeeds'
$afterCancel = @(Invoke-RestMethod -Uri "$base/api/trips/$($tripCancel.id)/shipments" -Method GET -Headers $adminHeaders)
Assert-Equal $afterCancel.Count 1 'cancelled trip preserves shipment detail for historical trip visibility'
$cancelHistory = Get-History $sCancel
$cancelLast = @($cancelHistory.history)[-1]
Assert-Equal $cancelLast.toStatus 'In_Warehouse' 'Cancelled history ends at In_Warehouse'

Write-Host "`nALL DELETE/CANCEL E2E TESTS PASSED" -ForegroundColor Green
