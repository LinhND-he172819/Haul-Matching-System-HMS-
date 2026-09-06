#requires -Version 5.1
<#
.SYNOPSIS
    End-to-end test for the new trip shipment endpoints (GET + DELETE)
    AND the new cancel-unlinks-matched-shipments behavior.

.DESCRIPTION
    1. Logs in as admin
    2. Picks an existing non-terminal trip that has Matched shipments
    3. Tests:
        a) GET /api/trips/{id}/shipments (admin view)
        b) DELETE /api/trips/{id}/shipments/{shipmentId} (unlink a single Matched shipment)
        c) Cancel a trip → verify auto-unlink + shipment status flips to In_Warehouse
#>

$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5104'

function Write-Section($title) {
    Write-Host "`n========== $title ==========" -ForegroundColor Cyan
}

# 1. Login
Write-Section 'Login as admin'
$loginBody = @{ email = 'admin@hms.com'; password = 'admin123' } | ConvertTo-Json
$loginResp = Invoke-RestMethod -Method Post -Uri "$base/api/auth/login" -Body $loginBody -ContentType 'application/json' -TimeoutSec 15
$token = $loginResp.accessToken
$headers = @{ Authorization = "Bearer $token" }
Write-Host "OK. UserId=$($loginResp.userId), Role=$($loginResp.role)" -ForegroundColor Green

# 2. List all trips, find a good candidate
Write-Section 'List trips (find a non-terminal trip with shipments)'
$trips = Invoke-RestMethod -Method Get -Uri "$base/api/trips" -Headers $headers -TimeoutSec 15
Write-Host "Total trips: $($trips.Count)"

# Find the first non-terminal trip that contains a Matched shipment.
$candidate = $null
$shipments = @()
foreach ($trip in @($trips | Where-Object { $_.status -in @('Scheduled', 'Ready', 'Active', 'Breakdown') })) {
    $tripShipments = @(Invoke-RestMethod -Method Get -Uri "$base/api/trips/$($trip.id)/shipments" -Headers $headers -TimeoutSec 15)
    if (@($tripShipments | Where-Object { $_.status -eq 'Matched' }).Count -gt 0) {
        $candidate = $trip
        $shipments = $tripShipments
        break
    }
}

if (-not $candidate) {
    Write-Host 'SKIP: No non-terminal trip with a Matched shipment exists; no data was modified.' -ForegroundColor Yellow
    exit 0
}

Write-Host "Selected trip: $($candidate.id) status=$($candidate.status)" -ForegroundColor Green

# 3. Test: GET shipments
Write-Section "GET /api/trips/$($candidate.id)/shipments"
Write-Host "Returned $($shipments.Count) shipments"
$shipments | Select-Object qrCode, status, weightKg, volumeCbm | Format-Table -AutoSize

if ($false) {
    Write-Host 'Seed shipment block disabled; tests use existing data only.' -ForegroundColor Yellow
    # Retained only as documentation for the required seed workflow.
    $users = Invoke-RestMethod -Method Get -Uri "$base/api/identity/users" -Headers $headers -TimeoutSec 15
    $driver = $users | Where-Object { $_.role -eq 'Driver' } | Select-Object -First 1
    $vehicles = Invoke-RestMethod -Method Get -Uri "$base/api/vehicles" -Headers $headers -TimeoutSec 15
    $vehicle = $vehicles | Where-Object { $_.status -eq 'Available' } | Select-Object -First 1
    $departure = (Get-Date).AddDays(2).ToString('o')
    $createBody = @{
        driverId = $driver.id
        vehicleId = $vehicle.id
        originHubId = $originHub.id
        destHubId = $destHub.id
        currentLoadWeightKg = 0
        currentLoadVolumeCbm = 0
        routeLineString = "LINESTRING (106.6667 10.8380, 108.1530 16.0710)"
        scheduledDepartureAt = $departure
        warehouseShipmentIds = @()
    } | ConvertTo-Json
    $candidate = Invoke-RestMethod -Method Post -Uri "$base/api/trips" -Body $createBody -ContentType 'application/json' -Headers $headers -TimeoutSec 15
    Write-Host "Created new trip: $($candidate.id)"
}

Write-Host "Selected trip: $($candidate.id) status=$($candidate.status)" -ForegroundColor Green

# 3. Test: GET shipments
Write-Section "GET /api/trips/$($candidate.id)/shipments"
Write-Host "Returned $($shipments.Count) shipments"
$shipments | Select-Object qrCode, status, weightKg, volumeCbm | Format-Table -AutoSize

if ($false) {
    Write-Host 'Seed workflow intentionally disabled; tests use existing data only.'
    # Fetch a hub and a customer
    $hubs = Invoke-RestMethod -Method Get -Uri "$base/api/hubs" -Headers $headers -TimeoutSec 15
    $originHub = $hubs | Where-Object { $_.id -eq '11111111-2222-3333-4444-555555555551' } | Select-Object -First 1
    # We need a customer. Look at users.
    $users = Invoke-RestMethod -Method Get -Uri "$base/api/identity/users" -Headers $headers -TimeoutSec 15
    $customer = $users | Where-Object { $_.role -eq 'Customer' } | Select-Object -First 1
    if (-not $customer) {
        Write-Host 'No customer found; aborting.' -ForegroundColor Red
        exit 1
    }
    $shipmentBody = @{
        senderCustomerId = $customer.id
        senderName = 'Test Sender'
        senderPhone = '0901234567'
        pickupAddress = 'Test pickup address'
        pickupHubId = $originHub.id
        receiverName = 'Test Receiver'
        receiverPhone = '0909876543'
        deliveryAddress = 'Test delivery address'
        destHubId = '11111111-2222-3333-4444-555555555554'
        cargoType = 'General'
        weightKg = 25
        volumeCbm = 0.5
        declaredValue = 1000000
        codAmount = 0
        isCod = $false
    } | ConvertTo-Json
    try {
        $newShipment = Invoke-RestMethod -Method Post -Uri "$base/api/shipments" -Body $shipmentBody -ContentType 'application/json' -Headers $headers -TimeoutSec 15
        Write-Host "Created shipment: $($newShipment.qrCode)"
    } catch {
        Write-Host "Create shipment failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
    # Move shipment to In_Warehouse if needed
    $currentStatus = $newShipment.status
    Write-Host "New shipment status: $currentStatus"
    # Recreate the trip with the new shipment linked
    $hubs = Invoke-RestMethod -Method Get -Uri "$base/api/hubs" -Headers $headers -TimeoutSec 15
    $originHub2 = $hubs | Where-Object { $_.id -eq '11111111-2222-3333-4444-555555555551' } | Select-Object -First 1
    $destHub2 = $hubs | Where-Object { $_.id -eq '11111111-2222-3333-4444-555555555554' } | Select-Object -First 1
    $driver2 = ($users | Where-Object { $_.role -eq 'Driver' } | Select-Object -First 1)
    $vehicle2 = (Invoke-RestMethod -Method Get -Uri "$base/api/vehicles" -Headers $headers -TimeoutSec 15 | Where-Object { $_.status -eq 'Available' } | Select-Object -First 1)
    $departure2 = (Get-Date).AddDays(2).ToString('o')
    $createBody2 = @{
        driverId = $driver2.id
        vehicleId = $vehicle2.id
        originHubId = $originHub2.id
        destHubId = $destHub2.id
        currentLoadWeightKg = 25
        currentLoadVolumeCbm = 0.5
        routeLineString = "LINESTRING (106.6667 10.8380, 108.1530 16.0710)"
        scheduledDepartureAt = $departure2
        warehouseShipmentIds = @($newShipment.id)
    } | ConvertTo-Json
    $candidate = Invoke-RestMethod -Method Post -Uri "$base/api/trips" -Body $createBody2 -ContentType 'application/json' -Headers $headers -TimeoutSec 15
    Write-Host "Created trip with linked shipment: $($candidate.id)"
    # Refetch
    $shipments = Invoke-RestMethod -Method Get -Uri "$base/api/trips/$($candidate.id)/shipments" -Headers $headers -TimeoutSec 15
}

# 4. Test: DELETE /shipments/{id} on a Matched shipment
Write-Section 'DELETE /api/trips/{id}/shipments/{shipmentId} (unlink a Matched shipment)'
$matched = $shipments | Where-Object { $_.status -eq 'Matched' } | Select-Object -First 1
if ($matched) {
    Write-Host "Unlinking shipment $($matched.qrCode) (id=$($matched.shipmentId))"
    try {
        $unlinkResult = Invoke-RestMethod -Method Delete -Uri "$base/api/trips/$($candidate.id)/shipments/$($matched.shipmentId)" -Headers $headers -TimeoutSec 15
        Write-Host "Unlinked OK. Trip load is now: $($unlinkResult.trip.currentLoadWeightKg)kg / $($unlinkResult.trip.currentLoadVolumeCbm)CBM" -ForegroundColor Green
    } catch {
        Write-Host "Unlink failed: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host 'No Matched shipment to unlink' -ForegroundColor Yellow
}

# 5. Test: Try unlink an In_Transit shipment (should fail with 409)
Write-Section 'Test 409: cannot unlink a non-Matched shipment'
$nonMatched = $shipments | Where-Object { $_.status -ne 'Matched' } | Select-Object -First 1
if ($nonMatched) {
    Write-Host "Trying to unlink non-Matched shipment $($nonMatched.qrCode) (status=$($nonMatched.status))"
    try {
        Invoke-RestMethod -Method Delete -Uri "$base/api/trips/$($candidate.id)/shipments/$($nonMatched.shipmentId)" -Headers $headers -TimeoutSec 15
        Write-Host 'ERROR: Should have returned 409' -ForegroundColor Red
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 409) {
            Write-Host "OK: got 409 Conflict as expected" -ForegroundColor Green
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $body = $reader.ReadToEnd()
            Write-Host "Body: $body"
        } else {
            Write-Host "Unexpected status: $statusCode" -ForegroundColor Red
        }
    }
} else {
    Write-Host 'No non-Matched shipment available for this test' -ForegroundColor Yellow
}

# 6. Test: Cancel a trip with Matched shipments → should auto-unlink
Write-Section 'Cancel a trip with Matched shipments → should auto-unlink'
$cancelCandidate = $null
$cancelShipments = @()
foreach ($trip in @($trips | Where-Object { $_.status -in @('Scheduled', 'Ready', 'Active', 'Breakdown') })) {
    $tripShipments = @(Invoke-RestMethod -Method Get -Uri "$base/api/trips/$($trip.id)/shipments" -Headers $headers -TimeoutSec 15)
    if (@($tripShipments | Where-Object { $_.status -eq 'Matched' }).Count -gt 0) {
        $cancelCandidate = $trip
        $cancelShipments = $tripShipments
        break
    }
}
if ($cancelCandidate) {
    # Check shipments on this trip
    $matchedBeforeCancel = @($cancelShipments | Where-Object { $_.status -eq 'Matched' })
    $matchedCount = $matchedBeforeCancel.Count
    Write-Host "Trip $($cancelCandidate.id) status=$($cancelCandidate.status) has $matchedCount Matched shipment(s)"

    if ($matchedCount -eq 0) {
        Write-Host 'SKIP: Candidate trip has no Matched shipment; cancellation auto-unlink cannot be verified.' -ForegroundColor Yellow
        return
    }

    Write-Host "Cancelling trip..."
    $cancelBody = @{ status = 'Cancelled'; occurredAt = (Get-Date).ToString('o') } | ConvertTo-Json
    try {
        $cancelled = Invoke-RestMethod -Method Patch -Uri "$base/api/trips/$($cancelCandidate.id)/status" -Body $cancelBody -ContentType 'application/json' -Headers $headers -TimeoutSec 15
        Write-Host "Trip cancelled. New status=$($cancelled.status)" -ForegroundColor Green

        # Refetch shipments → Matched ones should be gone (either unlinked, or their status should be In_Warehouse)
        $afterShipments = Invoke-RestMethod -Method Get -Uri "$base/api/trips/$($cancelCandidate.id)/shipments" -Headers $headers -TimeoutSec 15
        Write-Host "Shipments still listed on trip: $($afterShipments.Count)"
        $afterShipments | Select-Object qrCode, status | Format-Table -AutoSize

        # Check one previously-Matched shipment through the status-history endpoint.
        $previouslyMatched = $matchedBeforeCancel | Select-Object -First 1
        if ($previouslyMatched) {
            $historyResp = Invoke-RestMethod -Method Get -Uri "$base/api/shipments/$($previouslyMatched.shipmentId)/status-history" -Headers $headers -TimeoutSec 15
            $lastTransition = @($historyResp.history) | Select-Object -Last 1
            Write-Host "Shipment $($previouslyMatched.qrCode) last status transition: $($lastTransition.fromStatus) -> $($lastTransition.toStatus)" -ForegroundColor Green
            if ($lastTransition.toStatus -eq 'In_Warehouse') {
                Write-Host 'PASS: Shipment reverted to In_Warehouse as expected' -ForegroundColor Green
            } else {
                Write-Host "FAIL: Shipment last status is $($lastTransition.toStatus), expected In_Warehouse" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "Cancel failed: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host 'No candidate trip to cancel' -ForegroundColor Yellow
}

Write-Host "`n========== DONE ==========" -ForegroundColor Cyan
