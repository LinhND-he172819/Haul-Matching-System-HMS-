$base = 'http://localhost:5104'
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.accessToken)"; 'Content-Type' = 'application/json' }

$ts = Get-Date -Format 'HHmmss'
$body = @{
  code = "TST-$ts"
  licensePlate = "TST-$ts"
  vehicleType = 'Truck'
  maxWeightKg = 5000
  maxVolumeCbm = 30
  hubId = '11111111-2222-3333-4444-555555555551'
  status = 'Available'
} | ConvertTo-Json

$v = Invoke-RestMethod -Uri "$base/api/vehicles" -Method POST -Headers $h -Body $body
$v | Select-Object id, licensePlate, status | Format-List
