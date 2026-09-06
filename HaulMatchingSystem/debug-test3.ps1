$base = 'http://localhost:5104'
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.accessToken)"; 'Content-Type' = 'application/json' }

$payload = @{
  driverId = '4fc44f23-5d56-44f9-bb79-5847c7a9bad9'
  vehicleId = '5427534d-cff9-458b-bce5-886c9e109342'
  originHubId = '11111111-2222-3333-4444-555555555551'
  destHubId = '11111111-2222-3333-4444-555555555554'
  routeLineString = 'LINESTRING (106.7 10.8, 108.15 16.07)'
  currentLoadWeightKg = 0
  currentLoadVolumeCbm = 0
  scheduledDepartureAt = $null
} | ConvertTo-Json

Write-Host "PAYLOAD: $payload"
try {
    $t = Invoke-RestMethod -Uri "$base/api/trips" -Method POST -Headers $h -Body $payload
    Write-Host "GOT 200: $($t.id)"
} catch {
    Write-Host "StatusCode: $($_.Exception.Response.StatusCode.value__)"
    $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
    Write-Host "Body: $($reader.ReadToEnd())"
}
