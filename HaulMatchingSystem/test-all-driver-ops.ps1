$ErrorActionPreference = "Stop"
$base = "http://localhost:5104"

# Login as driver
Write-Host "=== LOGIN ===" -ForegroundColor Yellow
$loginResp = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType "application/json" -Body '{"email":"linhndhe172819@fpt.edu.vn","password":"driver123"}'
$token = $loginResp.accessToken
Write-Host "Login OK: $($loginResp.fullName)" -ForegroundColor Green

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}

# Get trips
Write-Host "`n=== DRIVER TRIPS ===" -ForegroundColor Yellow
$trips = Invoke-RestMethod -Uri "$base/api/driver/trips" -Headers $headers -Method GET
$firstTrip = $null
$firstShipment = $null
foreach ($t in $trips) {
    Write-Host "Trip $($t.id): status=$($t.status)" -ForegroundColor Cyan
    if ($t.shipments) {
        foreach ($s in $t.shipments) {
            Write-Host "  Shipment $($s.id): status=$($s.status)" -ForegroundColor Green
            if (-not $firstShipment) {
                $firstTrip = $t
                $firstShipment = $s
            }
        }
    }
}

if (-not $firstTrip -or -not $firstShipment) {
    Write-Host "No trips or shipments found! Cannot continue testing." -ForegroundColor Red
    exit 1
}

$tripId = $firstTrip.id
$shipmentId = $firstShipment.id
Write-Host "`nUsing Trip: $tripId, Shipment: $shipmentId (status: $($firstShipment.status))" -ForegroundColor Magenta

# Test: Get trip detail
Write-Host "`n=== TEST: Get Trip Detail ===" -ForegroundColor Yellow
try {
    $detail = Invoke-RestMethod -Uri "$base/api/driver/trips/$tripId" -Headers $headers -Method GET
    Write-Host "OK: Trip $($detail.id) status=$($detail.status)" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test: Get shipment detail
Write-Host "`n=== TEST: Get Shipment Detail ===" -ForegroundColor Yellow
try {
    $sDetail = Invoke-RestMethod -Uri "$base/api/driver/trips/$tripId/shipments/$shipmentId" -Headers $headers -Method GET
    Write-Host "OK: Shipment $($sDetail.code) status=$($sDetail.status)" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test status-dependent endpoints
if ($firstTrip.status -eq "PendingStart") {
    Write-Host "`n=== TEST: Start Trip ===" -ForegroundColor Yellow
    try {
        $startResp = Invoke-RestMethod -Uri "$base/api/driver/trips/$tripId/start" -Headers $headers -Method PUT
        Write-Host "OK: $($startResp | ConvertTo-Json -Compress)" -ForegroundColor Green
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

if ($firstShipment.status -eq "Matched") {
    Write-Host "`n=== TEST: Confirm Pickup ===" -ForegroundColor Yellow
    try {
        $pickupResp = Invoke-RestMethod -Uri "$base/api/driver/shipments/$shipmentId/confirm-pickup" -Headers $headers -Method PUT -Body '{"pickupNote":"Test pickup note"}'
        Write-Host "OK: $($pickupResp | ConvertTo-Json -Compress)" -ForegroundColor Green
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Re-fetch to get updated status
$trips2 = Invoke-RestMethod -Uri "$base/api/driver/trips" -Headers $headers -Method GET
$updatedShipment = $null
foreach ($t in $trips2) {
    if ($t.shipments) {
        foreach ($s in $t.shipments) {
            if ($s.id -eq $shipmentId) {
                $updatedShipment = $s
            }
        }
    }
}

if ($updatedShipment) {
    Write-Host "`nAfter updates, shipment status: $($updatedShipment.status)" -ForegroundColor Magenta

    if ($updatedShipment.status -eq "In_Transit") {
        Write-Host "`n=== TEST: Start Transport ===" -ForegroundColor Yellow
        try {
            $transportResp = Invoke-RestMethod -Uri "$base/api/driver/shipments/$shipmentId/start-transport" -Headers $headers -Method PUT
            Write-Host "OK: $($transportResp | ConvertTo-Json -Compress)" -ForegroundColor Green
        } catch {
            Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    # Re-fetch after start-transport
    $trips3 = Invoke-RestMethod -Uri "$base/api/driver/trips" -Headers $headers -Method GET
    $finalShipment = $null
    foreach ($t in $trips3) {
        if ($t.shipments) {
            foreach ($s in $t.shipments) {
                if ($s.id -eq $shipmentId) {
                    $finalShipment = $s
                }
            }
        }
    }

    if ($finalShipment -and $finalShipment.status -eq "In_Transit") {
        Write-Host "`n=== TEST: Confirm Delivery ===" -ForegroundColor Yellow
        try {
            $deliveryResp = Invoke-RestMethod -Uri "$base/api/driver/shipments/$shipmentId/confirm-delivery" -Headers $headers -Method PUT -Body '{"codAmount":1500000,"deliveryNote":"Giao hàng thành công"}'
            Write-Host "OK: $($deliveryResp | ConvertTo-Json -Compress)" -ForegroundColor Green
        } catch {
            Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host "`n=== ALL TESTS COMPLETE ===" -ForegroundColor Yellow
