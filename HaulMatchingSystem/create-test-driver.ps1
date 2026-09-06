$base = 'http://localhost:5104'
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType 'application/json' -Body (@{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.accessToken)"; 'Content-Type' = 'application/json' }

$ts = Get-Date -Format 'HHmmss'
$email = "testdrv_$ts@hms.com"
$body = @{
  email = $email
  password = 'Test1234!'
  fullName = "Test Driver $ts"
  phone = "0901234$ts"
  role = 'Driver'
  hubId = '11111111-2222-3333-4444-555555555551'
} | ConvertTo-Json

$u = Invoke-RestMethod -Uri "$base/api/identity/users" -Method POST -Headers $h -Body $body
$u | Select-Object id, email, role | Format-List
Write-Host "EMAIL: $email"
