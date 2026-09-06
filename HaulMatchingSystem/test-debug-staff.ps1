$base = 'http://localhost:5104'
$lr = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType "application/json" -Body '{"email":"admin@hms.com","password":"admin123"}'
$tk = $lr.accessToken
$h = @{Authorization="Bearer $tk"; 'Content-Type'='application/json'}

# Create a warehouse staff
$ts = Get-Date -Format 'HHmmsfff'
$body = @{
    email = "whst_$ts@hms.com"
    password = 'Test1234!'
    fullName = "WH Staff $ts"
    phone = "092$ts"
    role = 'Warehouse_Staff'
    hubId = '11111111-2222-3333-4444-555555555551'
} | ConvertTo-Json
$usr = Invoke-RestMethod -Uri "$base/api/identity/users" -Method POST -Headers $h -Body $body
Write-Host "Staff ID: $($usr.id)"

# Login as staff
$sl = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType "application/json" -Body (@{email = "whst_$ts@hms.com"; password='Test1234!'} | ConvertTo-Json)
$sh = @{Authorization="Bearer $($sl.accessToken)"; 'Content-Type'='application/json'}
Write-Host "Staff login OK userId=$($sl.userId)"

# Test hub-inventory (should auto-filter to staff's hub)
try {
    $inv = Invoke-RestMethod -Uri "$base/api/hub-inventory?status=In_Warehouse&pageSize=5" -Method GET -Headers $sh
    Write-Host "hub-inventory count: $($inv.totalCount) items: $($inv.items.Count)"
} catch {
    $err = $_.ErrorDetails.Message
    Write-Host "hub-inventory err: $err"
}
