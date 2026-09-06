$ErrorActionPreference = "Stop"
$body = '{"email":"linhndhe172819@fpt.edu.vn","password":"driver123"}'
try {
    $resp = Invoke-RestMethod -Method POST -Uri "http://localhost:5104/api/auth/login" -ContentType "application/json" -Body $body
    Write-Host "Login OK: $($resp.fullName)"
    $token = $resp.accessToken
} catch {
    Write-Host "Login failed: $_"
    exit 1
}

$reqBody = '{"pickupNote":"test from script"}'
$headers = @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" }
$uri = "http://localhost:5104/api/driver/shipments/16cfc6b2-fada-419a-a501-56457f78b37b/confirm-pickup"
try {
    $result = Invoke-WebRequest -Method PUT -Uri $uri -Headers $headers -Body $reqBody -UseBasicParsing -ErrorAction Stop
    Write-Host "SUCCESS: $($result.StatusCode) $($result.Content)"
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
    $errBody = $reader.ReadToEnd()
    $reader.Close()
    Write-Host "ERROR [$statusCode]: $errBody"
}
