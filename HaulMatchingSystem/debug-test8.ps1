$base = 'http://localhost:5104'
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.accessToken)"; 'Content-Type' = 'application/json' }

# Find an existing trip with scheduledDepartureAt
$trips = Invoke-RestMethod -Uri "$base/api/trips" -Method GET -Headers $h
$trip = $trips | Where-Object { $_.status -eq 'Scheduled' -and $null -ne $_.scheduledDepartureAt } | Select-Object -First 1
if (-not $trip) {
    Write-Host "No scheduled trip found, creating one..."
    $ts = Get-Date -Format 'HHmmsfff'
    $d = Invoke-RestMethod -Uri "$base/api/identity/users" -Method POST -Headers $h -Body (@{
        email = "drv_$ts@hms.com"; password = 'Test1234!'; fullName = "Drv $ts"
        phone = "090$ts"; role = 'Driver'; hubId = '11111111-2222-3333-4444-555555555551'
    } | ConvertTo-Json)
    $v = Invoke-RestMethod -Uri "$base/api/vehicles" -Method POST -Headers $h -Body (@{
        code = "V-$ts"; licensePlate = "V-$ts"; vehicleType = 'Truck'
        maxWeightKg = 5000; maxVolumeCbm = 30
        hubId = '11111111-2222-3333-4444-555555555551'; status = 'Available'
    } | ConvertTo-Json)
    $future = (Get-Date).ToUniversalTime().AddDays(3).ToString('o')
    $trip = Invoke-RestMethod -Uri "$base/api/trips" -Method POST -Headers $h -Body (@{
        driverId = $d.id; vehicleId = $v.id
        originHubId = '11111111-2222-3333-4444-555555555551'
        destHubId = '11111111-2222-3333-4444-555555555554'
        routeLineString = 'LINESTRING (106.7 10.8, 108.15 16.07)'
        currentLoadWeightKg = 0; currentLoadVolumeCbm = 0
        scheduledDepartureAt = $future
    } | ConvertTo-Json)
}
Write-Host "Using trip: $($trip.id) status=$($trip.status) scheduledDepartureAt=$($trip.scheduledDepartureAt)"

# Create valid trip post
$validAccept = (Get-Date).ToUniversalTime().AddDays(2).ToString('o')
$payload = @{
    tripId = $trip.id
    title = 'Test post valid'
    pickupMode = 'Hub'
    acceptUntil = $validAccept
} | ConvertTo-Json
Write-Host "POST payload: $payload"
try {
    $p = Invoke-RestMethod -Uri "$base/api/trip-posts" -Method POST -Headers $h -Body $payload
    $p | Select-Object id, status, acceptUntil | ConvertTo-Json
} catch {
    $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
    Write-Host "GOT $($_.Exception.Response.StatusCode.value__): $($reader.ReadToEnd())"
}
