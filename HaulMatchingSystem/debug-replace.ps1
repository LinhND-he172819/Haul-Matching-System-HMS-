$future = '2026-09-07T12:37:34.6021644+00:00'
$payload = @{
  driverId = 'X'
  scheduledDepartureAt = $future
} | ConvertTo-Json
Write-Host "BEFORE: $payload"
$payload3 = $payload -replace '"scheduledDepartureAt":"[^"]+"', '"scheduledDepartureAt":null'
Write-Host "AFTER: $payload3"
