$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5104'
$ORIGIN = '11111111-2222-3333-4444-555555555551'
$DEST = '11111111-2222-3333-4444-555555555554'

$adminLogin = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json)
$ah = @{ Authorization = "Bearer $($adminLogin.accessToken)"; 'Content-Type' = 'application/json' }
Write-Host "=== Admin login OK ==="

$ts = Get-Date -Format 'HHmmsfff'

# Create driver
$driverEmail = "drv_brk_$ts@hms.com"
$d = Invoke-RestMethod -Uri "$base/api/identity/users" -Method POST -Headers $ah -Body (@{
    email = $driverEmail
    password = 'Test1234!'
    fullName = "Drv Brk $ts"
    phone = "090$ts"
    role = 'Driver'
    hubId = $ORIGIN
} | ConvertTo-Json)
$driverId = $d.id

# Create vehicle
$v = Invoke-RestMethod -Uri "$base/api/vehicles" -Method POST -Headers $ah -Body (@{
    code = "VB-$ts"
    licensePlate = "VB-$ts"
    vehicleType = 'Truck'
    maxWeightKg = 5000
    maxVolumeCbm = 30
    hubId = $ORIGIN
    status = 'Available'
} | ConvertTo-Json)
$vehicleId = $v.id

# Create trip in Scheduled state
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
} | ConvertTo-Json

function Set-TripStatus($id, $status) {
    try {
        $r = Invoke-RestMethod -Uri "$base/api/trips/$id/status" -Method PATCH -Headers $ah -Body (@{ status = $status } | ConvertTo-Json)
        Write-Host "    -> $($r.status)"
        return $true
    } catch {
        $sc = $_.Exception.Response.StatusCode.value__
        $body = ''
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $body = $reader.ReadToEnd()
        } catch {}
        Write-Host "    FAIL ($sc): $body"
        return $false
    }
}

# === Trip 1: Full happy path through Breakdown -> Cancelled ===
$t = Invoke-RestMethod -Uri "$base/api/trips" -Method POST -Headers $ah -Body $payload
$tripId = $t.id
Write-Host "=== Created trip $tripId in Scheduled state ==="

Write-Host "=== Walk: Scheduled -> Ready -> InProgress ==="
if (-not (Set-TripStatus $tripId 'Ready')) { exit 1 }
if (-not (Set-TripStatus $tripId 'InProgress')) { exit 1 }

# Test 1: InProgress -> Breakdown should now succeed
Write-Host "`n=== Test 1: InProgress -> Breakdown (should 200) ==="
if (-not (Set-TripStatus $tripId 'Breakdown')) { exit 1 }

# Test 2: Breakdown -> Cancelled should succeed
Write-Host "`n=== Test 2: Breakdown -> Cancelled (should 200) ==="
if (-not (Set-TripStatus $tripId 'Cancelled')) { exit 1 }

# Test 3: Cancelled is terminal
Write-Host "`n=== Test 3: Cancelled is terminal (should 409) ==="
$blocked = $true
try {
    Invoke-RestMethod -Uri "$base/api/trips/$tripId/status" -Method PATCH -Headers $ah -Body (@{ status = 'InProgress' } | ConvertTo-Json) | Out-Null
    $blocked = $false
} catch {}
if ($blocked) { Write-Host "    PASS - blocked" } else { Write-Host "    FAIL - not blocked" }

# === Trip 2: Breakdown -> Completed is NOT allowed ===
$t2 = Invoke-RestMethod -Uri "$base/api/trips" -Method POST -Headers $ah -Body $payload
$tripId2 = $t2.id
Write-Host "`n=== Created trip2 $tripId2 ==="
Set-TripStatus $tripId2 'Ready' | Out-Null
Set-TripStatus $tripId2 'InProgress' | Out-Null
Set-TripStatus $tripId2 'Breakdown' | Out-Null

Write-Host "`n=== Test 4: Breakdown -> Completed (should 409) ==="
$blocked = $true
try {
    Invoke-RestMethod -Uri "$base/api/trips/$tripId2/status" -Method PATCH -Headers $ah -Body (@{ status = 'Completed' } | ConvertTo-Json) | Out-Null
    $blocked = $false
} catch {}
if ($blocked) { Write-Host "    PASS - blocked" } else { Write-Host "    FAIL - not blocked" }

Write-Host "`n=== ALL TESTS DONE ==="
