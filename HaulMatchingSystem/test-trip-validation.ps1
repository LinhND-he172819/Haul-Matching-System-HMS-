$t = Get-Content "$env:TEMP\hms_atk.txt"
$headers = @{ "Authorization" = "Bearer $t"; "Content-Type" = "application/json" }
$base = "http://localhost:5104/api/trips"

Write-Host "=== Test 1: Weight 6000 > max 5000 (bffbe965) ==="
$body = '{"driverId":"7d6487b3-1b42-4781-b838-efd7be0d1319","vehicleId":"bffbe965-4f96-4a3f-8d60-659f6282cf09","originHubId":"11111111-2222-3333-4444-555555555551","destHubId":"11111111-2222-3333-4444-555555555553","routeLineString":"LINESTRING (106.6667 10.838, 105.8412 21.0035)","currentLoadWeightKg":6000,"currentLoadVolumeCbm":20}'
try {
    Invoke-RestMethod -Uri $base -Method POST -Headers $headers -Body $body | Out-Null
    Write-Host "  FAIL: Should have been rejected!" -ForegroundColor Red
} catch {
    $errBody = $_.ErrorDetails.Message
    if ($errBody -match "vượt quá tải trọng") {
        Write-Host "  PASS: $errBody" -ForegroundColor Green
    } else {
        Write-Host "  UNEXPECTED: $errBody" -ForegroundColor Yellow
    }
}

Write-Host "=== Test 2: Volume 55 > max 50 (bffbe965: 50CBM) ==="
$body = '{"driverId":"7d6487b3-1b42-4781-b838-efd7be0d1319","vehicleId":"bffbe965-4f96-4a3f-8d60-659f6282cf09","originHubId":"11111111-2222-3333-4444-555555555551","destHubId":"11111111-2222-3333-4444-555555555553","routeLineString":"LINESTRING (106.6667 10.838, 105.8412 21.0035)","currentLoadWeightKg":200,"currentLoadVolumeCbm":55}'
Test-Trip -Name 'Volume too high' -Body $body -ExpectReject 'volumeCbm'

Write-Host "=== Test 3: Both valid (200kg, 30 CBM) - should PASS ==="
$body = '{"driverId":"7d6487b3-1b42-4781-b838-efd7be0d1319","vehicleId":"bffbe965-4f96-4a3f-8d60-659f6282cf09","originHubId":"11111111-2222-3333-4444-555555555551","destHubId":"11111111-2222-3333-4444-555555555553","routeLineString":"LINESTRING (106.6667 10.838, 105.8412 21.0035)","currentLoadWeightKg":200,"currentLoadVolumeCbm":30}'
Test-Trip -Name 'Valid trip' -Body $body

Write-Host "`n=== Test 4: Weight 5001 > max 5000 (bffbe965) ==="
$body = '{"driverId":"7d6487b3-1b42-4781-b838-efd7be0d1319","vehicleId":"bffbe965-4f96-4a3f-8d60-659f6282cf09","originHubId":"11111111-2222-3333-4444-555555555551","destHubId":"11111111-2222-3333-4444-555555555553","routeLineString":"LINESTRING (106.6667 10.838, 105.8412 21.0035)","currentLoadWeightKg":5001,"currentLoadVolumeCbm":10}'
Test-Trip -Name 'Weight 1kg over' -Body $body -ExpectReject 'weightKg'

Write-Host "`n=== Test 5: Exact limit (5000kg, 50 CBM) - should PASS ==="
$body = '{"driverId":"7d6487b3-1b42-4781-b838-efd7be0d1319","vehicleId":"bffbe965-4f96-4a3f-8d60-659f6282cf09","originHubId":"11111111-2222-3333-4444-555555555551","destHubId":"11111111-2222-3333-4444-555555555553","routeLineString":"LINESTRING (106.6667 10.838, 105.8412 21.0035)","currentLoadWeightKg":5000,"currentLoadVolumeCbm":50}'
Test-Trip -Name 'Exact limit' -Body $body
