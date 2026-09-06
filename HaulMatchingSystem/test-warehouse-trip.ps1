$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5104'
$ORIGIN = '11111111-2222-3333-4444-555555555551'
$DEST   = '11111111-2222-3333-4444-555555555554'

# ===== LOGIN AS ADMIN =====
$adminLogin = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json)
$ah = @{ Authorization = "Bearer $($adminLogin.accessToken)"; 'Content-Type' = 'application/json' }
Write-Host "=== Admin login OK ==="

# ===== CREATE WAREHOUSE STAFF =====
$ts = Get-Date -Format 'HHmmsfff'
$staffEmail = "whst_$ts@hms.com"
$staffBody = @{
    email = $staffEmail
    password = 'Test1234!'
    fullName = "WH Staff $ts"
    phone = "092$ts"
    role = 'Warehouse_Staff'
    hubId = $ORIGIN
} | ConvertTo-Json
$usr = Invoke-RestMethod -Uri "$base/api/identity/users" -Method POST -Headers $ah -Body $staffBody
$sl = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = $staffEmail; password = 'Test1234!' } | ConvertTo-Json)
$sh = @{ Authorization = "Bearer $($sl.accessToken)"; 'Content-Type' = 'application/json' }
Write-Host "Created warehouse staff $staffEmail = $($usr.id)"

# ===== CREATE DRIVER =====
$driverEmail = "drv_$ts@hms.com"
$d = Invoke-RestMethod -Uri "$base/api/identity/users" -Method POST -Headers $ah -Body (@{
    email = $driverEmail
    password = 'Test1234!'
    fullName = "Drv $ts"
    phone = "090$ts"
    role = 'Driver'
    hubId = $ORIGIN
} | ConvertTo-Json)
$driverId = $d.id
Write-Host "Created driver $driverEmail = $driverId"

# ===== CREATE VEHICLE =====
$v = Invoke-RestMethod -Uri "$base/api/vehicles" -Method POST -Headers $ah -Body (@{
    code = "V-$ts"
    licensePlate = "V-$ts"
    vehicleType = 'Truck'
    maxWeightKg = 5000
    maxVolumeCbm = 30
    hubId = $ORIGIN
    status = 'Available'
} | ConvertTo-Json)
$vehicleId = $v.id
Write-Host "Created vehicle $($v.licensePlate) = $vehicleId"

# ===== CREATE 2 WAREHOUSE SHIPMENTS via admin (draft) + staff (confirm-intake) =====
function New-WarehouseShipment($idx) {
    $qr = Invoke-RestMethod -Uri "$base/api/shipments/draft" -Method POST -Headers $ah -Body (@{
        customerId = $adminLogin.userId
        cargoType = "Hang $idx"
        weightKg = 100
        volumeCbm = 1
        senderName = "Nguoi Gui $idx"
        senderPhone = "090$ts$idx"
        pickupAddress = "Kho origin $idx"
        pickupLat = 10.8
        pickupLng = 106.7
        receiverName = "Nguoi Nhan $idx"
        receiverPhone = "091$ts$idx"
        destAddress = "Dia chi $idx"
        destLat = 10.8
        destLng = 106.7
    } | ConvertTo-Json)
    $sid = $qr.id
    Invoke-RestMethod -Uri "$base/api/shipments/${sid}/confirm-intake" -Method PUT -Headers $sh -Body (@{
        actualWeightKg = 100
        actualVolumeCbm = 1
    } | ConvertTo-Json) | Out-Null
    return $sid
}

$s1 = New-WarehouseShipment 1
$s2 = New-WarehouseShipment 2
Write-Host "Created warehouse shipments: $s1, $s2"

# ===== VERIFY HUB INVENTORY =====
$inv = Invoke-RestMethod -Uri "$base/api/hub-inventory?hubId=$ORIGIN&status=In_Warehouse&pageSize=200" -Method GET -Headers $ah
Write-Host "=== Hub inventory In_Warehouse at origin: $($inv.totalCount) ==="

# ===== TEST A: POST trip WITH warehouseShipmentIds =====
$future = (Get-Date).ToUniversalTime().AddDays(3).ToString('o')
$payload = @{
    driverId = $driverId
    vehicleId = $vehicleId
    originHubId = $ORIGIN
    destHubId = $DEST
    routeLineString = 'LINESTRING (106.7 10.8, 108.15 16.07)'
    currentLoadWeightKg = 0
    currentLoadVolumeCbm = 0
    scheduledDepartureAt = $future
    warehouseShipmentIds = @($s1, $s2)
} | ConvertTo-Json

Write-Host "`n=== Test A: POST /api/trips with warehouseShipmentIds ==="
try {
    $t = Invoke-RestMethod -Uri "$base/api/trips" -Method POST -Headers $ah -Body $payload
    $tripId = $t.id
    Write-Host "PASS trip=$tripId status=$($t.status) load=$($t.currentLoadWeightKg)kg/$($t.currentLoadVolumeCbm)cbm"
} catch {
    Write-Host "FAIL: $($_.Exception.Message) - $($_.ErrorDetails.Message)"
    exit 1
}

# Verify shipments linked via driver endpoint
$dl2 = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = $driverEmail; password = 'Test1234!' } | ConvertTo-Json)
$dh2 = @{ Authorization = "Bearer $($dl2.accessToken)"; 'Content-Type' = 'application/json' }
$linked = Invoke-RestMethod -Uri "$base/api/driver/trips/$tripId" -Method GET -Headers $dh2
Write-Host "=== Trip shipments linked: $($linked.shipments.Count) ==="
$linked.shipments | ForEach-Object { Write-Host ("   - id={0} status={1} weight={2}kg qr={3}" -f $_.id, $_.status, $_.Weight, $_.ShipmentCode) }

# ===== ACTIVATE: Scheduled -> Ready -> InProgress =====
Write-Host "`n=== Test B: Ready -> InProgress ==="
$rd = Invoke-RestMethod -Uri "$base/api/trips/$tripId/status" -Method PATCH -Headers $ah -Body (@{ status = 'Ready' } | ConvertTo-Json)
Write-Host "  -> Ready OK ($($rd.status))"
$ip = Invoke-RestMethod -Uri "$base/api/trips/$tripId/status" -Method PATCH -Headers $ah -Body (@{ status = 'InProgress' } | ConvertTo-Json)
Write-Host "  -> InProgress OK ($($ip.status))"

# ===== TEST C: Try to complete trip with non-delivered shipments (should FAIL) =====
Write-Host "`n=== Test C: Complete trip with undelivered shipments (should 400) ==="
try {
    Invoke-RestMethod -Uri "$base/api/trips/$tripId/status" -Method PATCH -Headers $ah -Body (@{ status = 'Completed' } | ConvertTo-Json)
    Write-Host "FAIL: expected error but trip completed"
    exit 1
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    # Try to get the actual response body
    $body = ''
    try {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
    } catch {}
    Write-Host "PASS blocked ($statusCode): $body"
    if ($body -notmatch 'Kh.ng th.*ho.n th.nh chuy.n') {
        Write-Host "WARNING: Expected message about undelivered shipments, got: $body"
    } else {
        Write-Host "  -> message matches expected 'Khong the hoan thanh chuyen'"
    }
}

# ===== TEST D: Mark shipments Delivered (via driver confirm-delivery) then complete =====
Write-Host "`n=== Login as driver ==="
$dl = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = $driverEmail; password = 'Test1234!' } | ConvertTo-Json)
$dh = @{ Authorization = "Bearer $($dl.accessToken)"; 'Content-Type' = 'application/json' }
Write-Host "  Driver login OK"

# Confirm pickup for each shipment (Matched -> In_Transit)
foreach ($sid in @($s1, $s2)) {
    try {
        $r = Invoke-RestMethod -Uri "$base/api/driver/shipments/${sid}/confirm-pickup" -Method PUT -Headers $dh -Body '{}'
        Write-Host "  Picked up ${sid}: $($r.message)"
    } catch {
        $err = $_.ErrorDetails.Message
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "  confirm-pickup for ${sid} ($statusCode): $err"
    }
}

# Then confirm-delivery
foreach ($sid in @($s1, $s2)) {
    try {
        $r = Invoke-RestMethod -Uri "$base/api/driver/shipments/${sid}/confirm-delivery" -Method PUT -Headers $dh -Body '{}'
        Write-Host "  Delivered ${sid}: $($r.message)"
    } catch {
        $err = $_.ErrorDetails.Message
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "  confirm-delivery for ${sid} ($statusCode): $err"
    }
}

# Verify all shipments are Delivered before trying to complete trip
$linked2 = Invoke-RestMethod -Uri "$base/api/driver/trips/$tripId" -Method GET -Headers $dh2
Write-Host "=== Shipment statuses before completion ==="
$linked2.shipments | ForEach-Object { Write-Host ("   - {0}: {1}" -f $_.id, $_.Status) }

# Now try to complete the trip
Write-Host "`n=== Test D: Complete trip (all shipments delivered) ==="
try {
    $done = Invoke-RestMethod -Uri "$base/api/trips/$tripId/status" -Method PATCH -Headers $ah -Body (@{ status = 'Completed' } | ConvertTo-Json)
    Write-Host "PASS trip completed: $($done.status)"
} catch {
    $err = $_.ErrorDetails.Message
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "FAIL complete ($statusCode): $err"
}

Write-Host "`n=== ALL TESTS DONE ==="
