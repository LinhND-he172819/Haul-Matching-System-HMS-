$ErrorActionPreference = "Stop"

# Login
$json = '{"email":"admin@hms.com","password":"admin123"}'
$jsonPath = "$env:TEMP\login.json"
[System.IO.File]::WriteAllText($jsonPath, $json, [System.Text.Encoding]::UTF8)

$loginResp = curl.exe -s -X POST "http://localhost:5104/api/auth/login" -H "Content-Type: application/json" -d "@$jsonPath"
Write-Host "LOGIN RESPONSE: $loginResp"

$loginObj = $loginResp | ConvertFrom-Json
$token = $loginObj.accessToken
Write-Host "TOKEN: $token"

# Create quotation
$quotationJson = '{"shippingFee":50000,"depositAmount":15000,"currency":"VND","expiresAt":"2026-08-13T12:00:00Z"}'
$quotationPath = "$env:TEMP\quotation.json"
[System.IO.File]::WriteAllText($quotationPath, $quotationJson, [System.Text.Encoding]::UTF8)

$result = curl.exe -s -w "`nHTTP_STATUS:%{http_code}" -X POST "http://localhost:5104/api/staff/proposals/f04d3a3a-d778-4e65-a2d8-c5a7ab024c3d/quotations" -H "Content-Type: application/json" -H "Authorization: Bearer $token" -d "@$quotationPath"
Write-Host "`nQUOTATION RESPONSE: $result"
