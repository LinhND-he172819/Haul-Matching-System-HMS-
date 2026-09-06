$base = 'http://localhost:5104'
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.accessToken)"; 'Content-Type' = 'application/json' }
$future = (Get-Date).ToUniversalTime().AddDays(3).ToString('o')
$payload = @{
  driverId = '4fc44f23-5d56-44f9-bb79-5847c7a9bad9'
  vehicleId = 'bffbe965-4f96-4a3f-8d60-659f6282cf09'
  originHubId = '11111111-2222-3333-4444-555555555551'
  destHubId = '11111111-2222-3333-4444-555555555554'
  routeLineString = 'LINESTRING (106.7 10.8, 108.15 16.07)'
  currentLoadWeightKg = 0
  currentLoadVolumeCbm = 0
  scheduledDepartureAt = $future
} | ConvertTo-Json
try {
    $t = Invoke-RestMethod -Uri "$base/api/trips" -Method POST -Headers $h -Body $payload
    $t | ConvertTo-Json
} catch {
    Write-Host "StatusCode: $($_.Exception.Response.StatusCode.value__)"
    Write-Host "StatusDesc: $($_.Exception.Response.StatusDescription)"
    $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
    $body = $reader.ReadToEnd()
    Write-Host "Body: $body"
}
