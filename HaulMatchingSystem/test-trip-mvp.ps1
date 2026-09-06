$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5104'
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.accessToken)"; 'Content-Type' = 'application/json' }

# Create a fresh driver
$ts = Get-Date -Format 'HHmmsfff'
$driverEmail = "drv_$ts@hms.com"
$driverId = $null
try {
    $d = Invoke-RestMethod -Uri "$base/api/identity/users" -Method POST -Headers $h -Body (@{
        email = $driverEmail
        password = 'Test1234!'
        fullName = "Drv $ts"
        phone = "090$ts"
        role = 'Driver'
        hubId = '11111111-2222-3333-4444-555555555551'
    } | ConvertTo-Json)
    $driverId = $d.id
    Write-Host "Created driver $driverEmail = $driverId"
} catch { Write-Host "Driver create FAIL: $($_.ErrorDetails.Message)"; exit 1 }

# Create a fresh vehicle
$vehicleId = $null
try {
    $v = Invoke-RestMethod -Uri "$base/api/vehicles" -Method POST -Headers $h -Body (@{
        code = "V-$ts"
        licensePlate = "V-$ts"
        vehicleType = 'Truck'
        maxWeightKg = 5000
        maxVolumeCbm = 30
        hubId = '11111111-2222-3333-4444-555555555551'
        status = 'Available'
    } | ConvertTo-Json)
    $vehicleId = $v.id
    Write-Host "Created vehicle $($v.licensePlate) = $vehicleId"
} catch { Write-Host "Vehicle create FAIL: $($_.ErrorDetails.Message)"; exit 1 }

# 1. POST trip with future date -> should succeed
$future = (Get-Date).ToUniversalTime().AddDays(3).ToString('o')
$payload = @{
  driverId = $driverId
  vehicleId = $vehicleId
  originHubId = '11111111-2222-3333-4444-555555555551'
  destHubId = '11111111-2222-3333-4444-555555555554'
  routeLineString = 'LINESTRING (106.7 10.8, 108.15 16.07)'
  currentLoadWeightKg = 0
  currentLoadVolumeCbm = 0
  scheduledDepartureAt = $future
} | ConvertTo-Json
Write-Host '=== Test 1: POST /api/trips with future date ==='
try {
    $t = Invoke-RestMethod -Uri "$base/api/trips" -Method POST -Headers $h -Body $payload
    $t | Select-Object id, tripCode, status, scheduledDepartureAt, currentLoadWeightKg | ConvertTo-Json
    $global:newTripId = $t.id
    Write-Host "PASS trip=$global:newTripId"
} catch { Write-Host "FAIL: $($_.Exception.Message) - $($_.ErrorDetails.Message)" ; $global:newTripId = $null }

# 2. POST trip with past date -> 400
$past = (Get-Date).ToUniversalTime().AddHours(-1).ToString('o')
$payload2 = $payload -replace [regex]::Escape($future), $past
Write-Host "`n=== Test 2: POST /api/trips with past date (should 400) ==="
try {
    $r = Invoke-RestMethod -Uri "$base/api/trips" -Method POST -Headers $h -Body $payload2
    Write-Host "FAIL: expected 400 but got $($r.id)"
} catch { Write-Host "PASS 400: $($_.ErrorDetails.Message)" }

# 3. POST trip with null scheduledDepartureAt -> 400
$payload3 = $payload -replace '"scheduledDepartureAt":\s*"[^"]+"', '"scheduledDepartureAt": null'
Write-Host "`n=== Test 3: POST /api/trips with null date (should 400) ==="
try {
    $r = Invoke-RestMethod -Uri "$base/api/trips" -Method POST -Headers $h -Body $payload3
    Write-Host "FAIL: expected 400 but got $($r.id)"
} catch { Write-Host "PASS 400: $($_.ErrorDetails.Message)" }

# 4. PUT update with new future date
if ($global:newTripId) {
    Write-Host "`n=== Test 4: PUT /api/trips/{id} updates scheduledDepartureAt ==="
    $newFuture = (Get-Date).ToUniversalTime().AddDays(5).ToString('o')
    $putPayload = $payload -replace [regex]::Escape($future), $newFuture
    try {
        $upd = Invoke-RestMethod -Uri "$base/api/trips/$global:newTripId" -Method PUT -Headers $h -Body $putPayload
        $upd | Select-Object id, status, scheduledDepartureAt | ConvertTo-Json
        Write-Host 'PASS'
    } catch { Write-Host "FAIL: $($_.ErrorDetails.Message)" }

    Write-Host "`n=== Test 5: GET /api/trips/{id} shows scheduledDepartureAt ==="
    $get = Invoke-RestMethod -Uri "$base/api/trips/$global:newTripId" -Method GET -Headers $h
    $get | Select-Object id, status, scheduledDepartureAt, currentLoadWeightKg | ConvertTo-Json
}

# 6-8: Trip post validations
if ($global:newTripId) {
    # Activate trip first (Scheduled -> Active) so trip-post validation passes
    Write-Host "`n=== (Setup) PATCH trip status -> Active ==="
    try {
        $act = Invoke-RestMethod -Uri "$base/api/trips/$global:newTripId/status" -Method PATCH -Headers $h -Body (@{ status = 'Active' } | ConvertTo-Json)
        $act | Select-Object id, status | ConvertTo-Json
        Write-Host 'Activated'
    } catch { Write-Host "Activation FAIL: $($_.ErrorDetails.Message)" }

    Write-Host "`n=== Test 6: POST /api/trip-posts with accept_until > scheduled_departure (should 400) ==="
    $tooLate = (Get-Date).ToUniversalTime().AddDays(7).ToString('o')
    $postPayload = @{
        tripId = $global:newTripId
        title = 'Test post too late'
        description = 'desc'
        pickupMode = 'Hub'
        acceptUntil = $tooLate
    } | ConvertTo-Json
    try {
        Invoke-RestMethod -Uri "$base/api/trip-posts" -Method POST -Headers $h -Body $postPayload
        Write-Host 'FAIL: expected 400 but got 200'
    } catch { Write-Host "PASS 400: $($_.ErrorDetails.Message)" }

    $past2 = (Get-Date).ToUniversalTime().AddMinutes(-1).ToString('o')
    $postPayload2 = @{
        tripId = $global:newTripId
        title = 'Test post past'
        pickupMode = 'Hub'
        acceptUntil = $past2
    } | ConvertTo-Json
    Write-Host "`n=== Test 7: POST /api/trip-posts with past accept_until (should 400) ==="
    try {
        Invoke-RestMethod -Uri "$base/api/trip-posts" -Method POST -Headers $h -Body $postPayload2
        Write-Host 'FAIL: expected 400 but got 200'
    } catch { Write-Host "PASS 400: $($_.ErrorDetails.Message)" }

    $validAccept = (Get-Date).ToUniversalTime().AddDays(2).ToString('o')
    $postPayload3 = @{
        tripId = $global:newTripId
        title = 'Test post valid'
        pickupMode = 'Hub'
        acceptUntil = $validAccept
    } | ConvertTo-Json
    Write-Host "`n=== Test 8: POST /api/trip-posts with accept_until < scheduled_departure (should 201) ==="
    try {
        $post = Invoke-RestMethod -Uri "$base/api/trip-posts" -Method POST -Headers $h -Body $postPayload3
        $post | Select-Object id, status, acceptUntil, scheduledDepartureAt | ConvertTo-Json
        Write-Host 'PASS'
    } catch {
        Write-Host "FAIL: $($_.Exception.Message)"
        Write-Host "  Detail: $($_.ErrorDetails.Message)"
    }
}

# 9. Login as the new driver
Write-Host "`n=== Test 9: GET /api/driver/trips (login as driver) ==="
try {
    $dl = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = $driverEmail; password = 'Test1234!' } | ConvertTo-Json)
    $dh = @{ Authorization = "Bearer $($dl.accessToken)" }
    $dts = Invoke-RestMethod -Uri "$base/api/driver/trips" -Method GET -Headers $dh
    $dts.items | Select-Object -First 3 id, tripCode, status, scheduledDepartureAt, departureTime | ConvertTo-Json
} catch { Write-Host "FAIL: $($_.ErrorDetails.Message)" }

Write-Host "`n=== ALL TESTS COMPLETE ==="
