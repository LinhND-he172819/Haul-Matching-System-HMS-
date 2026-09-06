$base = 'http://localhost:5104'
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.accessToken)"; 'Content-Type' = 'application/json' }

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

# Test: explicit null
$body = @{
  driverId = $d.id
  vehicleId = $v.id
  originHubId = '11111111-2222-3333-4444-555555555551'
  destHubId = '11111111-2222-3333-4444-555555555554'
  routeLineString = 'LINESTRING (106.7 10.8, 108.15 16.07)'
  currentLoadWeightKg = 0
  currentLoadVolumeCbm = 0
  scheduledDepartureAt = $null
} | ConvertTo-Json -Depth 5
Write-Host "PAYLOAD (explicit null): $body"
try {
    $t = Invoke-RestMethod -Uri "$base/api/trips" -Method POST -Headers $h -Body $body
    Write-Host "GOT 200: id=$($t.id) scheduledDepartureAt=$($t.scheduledDepartureAt)"
} catch {
    $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
    Write-Host "GOT $($_.Exception.Response.StatusCode.value__): $($reader.ReadToEnd())"
}
