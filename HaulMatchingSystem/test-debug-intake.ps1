$base = 'http://localhost:5104'
$lr = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType "application/json" -Body '{"email":"admin@hms.com","password":"admin123"}'
$tk = $lr.accessToken
$userId = $lr.userId
Write-Host "userId: $userId"

# Create draft
$body = @{
    customerId = $userId
    cargoType = 'Test'
    weightKg = 10
    volumeCbm = 1
    receiverName = 'R'
    receiverPhone = '0912'
    destAddress = 'D'
    destLat = 10.8
    destLng = 106.7
} | ConvertTo-Json
$h = @{Authorization="Bearer $tk"; 'Content-Type'='application/json'}
$qr = Invoke-RestMethod -Uri "$base/api/shipments/draft" -Method POST -Headers $h -Body $body
Write-Host "Draft ID: $($qr.id)"

# Try confirm-intake
try {
    $r = Invoke-RestMethod -Uri "$base/api/shipments/$($qr.id)/confirm-intake" -Method PUT -Headers $h -Body '{"actualWeightKg":10,"actualVolumeCbm":1}'
    Write-Host "OK: $r"
} catch {
    $code = $_.Exception.Response.StatusCode
    Write-Host "Status: $code"
    $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
    Write-Host "Body: $($sr.ReadToEnd())"
    $sr.Close()
}
