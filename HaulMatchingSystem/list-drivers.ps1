$base = 'http://localhost:5104'
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.accessToken)" }
$users = Invoke-RestMethod -Uri "$base/api/identity/users" -Method GET -Headers $h
$drivers = $users | Where-Object { $_.roles -contains 'Driver' -or $_.role -eq 'Driver' }
$drivers | Select-Object id, email | Format-Table -AutoSize
Write-Host "--- VEHICLES ---"
$vehicles = Invoke-RestMethod -Uri "$base/api/vehicles" -Method GET -Headers $h
$vehicles | Select-Object id, licensePlate, status | Format-Table -AutoSize
Write-Host "--- ACTIVE TRIPS ---"
$trips = Invoke-RestMethod -Uri "$base/api/trips" -Method GET -Headers $h
$trips | Where-Object { $_.status -in 'Active','Scheduled','Ready','InProgress' } | Select-Object id, tripCode, driverId, vehicleId, status, scheduledDepartureAt | Format-Table -AutoSize
