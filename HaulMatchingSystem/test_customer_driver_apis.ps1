$base = "http://localhost:5104"
$headers = @{}

$ok = 0; $fail = 0; $clientErr = 0

function Test-Endpoint {
    param([string]$Method, [string]$Path, [string]$Body, [string]$ContentType = "application/json", [string]$Desc = "")
    $uri = "$base$Path"
    $h = @{ Authorization = $script:authHeader }
    try {
        $params = @{ Uri = $uri; Method = $Method; Headers = $h; UseBasicParsing = $true; TimeoutSec = 10 }
        if ($ContentType) { $params.ContentType = $ContentType }
        if ($Body -and $Body -ne "") { $params.Body = [System.Text.Encoding]::UTF8.GetBytes($Body) }
        $resp = Invoke-WebRequest @params
        $code = [int]$resp.StatusCode
        if ($code -ge 200 -and $code -lt 300) {
            Write-Output "OK $code $Method $Desc"
            $script:ok++
        } else {
            Write-Output "?? $code $Method $Desc"
        }
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        $msg = $_.Exception.Message
        if ($code -ge 400 -and $code -lt 500) {
            Write-Output "4xx $code $Method $Desc"
            $script:clientErr++
        } else {
            Write-Output "FAIL $code $Method $Desc | $msg"
            $script:fail++
        }
    }
}

# === LOGIN AS CUSTOMER ===
Write-Output "`n========== CUSTOMER LOGIN =========="
$loginBody = '{"email":"customer@hms.com","password":"customer123"}'
$uri = "$base/api/auth/login"
try {
    $resp = Invoke-RestMethod -Uri $uri -Method POST -ContentType "application/json" -Body $loginBody
    $token = $resp.accessToken
    $script:authHeader = "Bearer $token"
    Write-Output "Customer login OK"
} catch {
    Write-Output "Customer login FAILED: $($_.Exception.Message)"
    # Try alternate format
    try {
        $resp2 = Invoke-WebRequest -Uri $uri -Method POST -ContentType "application/json" -Body $loginBody -UseBasicParsing
        $json = $resp2.Content | ConvertFrom-Json
        $token = $json.accessToken
        $script:authHeader = "Bearer $token"
        Write-Output "Customer login OK (alt)"
    } catch {
        Write-Output "Customer login FAILED (alt): $($_.Exception.Message)"
        Write-Output "Aborting customer tests."
        return
    }
}

# === CUSTOMER ENDPOINTS ===
Write-Output "`n========== CUSTOMER SHIPMENT ENDPOINTS =========="
Test-Endpoint GET "/api/customer/shipments?page=1&pageSize=5" "" "application/json" "GET customer shipments"

# Get a shipment ID from the list
$shipmentId = $null
$quotationId = $null
$paymentId = $null
try {
    $h = @{ Authorization = $script:authHeader }
    $shipResp = Invoke-RestMethod -Uri "$base/api/customer/shipments?page=1&pageSize=5" -Method GET -Headers $h
    if ($shipResp -and $shipResp.data -and $shipResp.data.Count -gt 0) {
        $shipmentId = $shipResp.data[0].id
        Write-Output "  -> Found shipment: $shipmentId (status: $($shipResp.data[0].status))"
    } elseif ($shipResp -and $shipResp.items -and $shipResp.items.Count -gt 0) {
        $shipmentId = $shipResp.items[0].id
        Write-Output "  -> Found shipment: $shipmentId (status: $($shipResp.items[0].status))"
    } else {
        # Try raw array
        if ($shipResp.Count -gt 0) {
            $shipmentId = $shipResp[0].id
            Write-Output "  -> Found shipment: $shipmentId"
        } else {
            Write-Output "  -> No shipments found"
        }
    }
} catch {
    Write-Output "  -> Could not extract shipment ID"
}

if ($shipmentId) {
    Test-Endpoint GET "/api/customer/shipments/$shipmentId" "" "application/json" "GET customer shipment detail"
    Test-Endpoint PUT "/api/customer/shipments/$shipmentId" '{"warehouseAddress":"Test Address 123"}' "application/json" "PUT customer shipment update"
    Test-Endpoint GET "/api/customer/shipments/$shipmentId/feedback" "" "application/json" "GET shipment feedback"
}

Write-Output "`n========== CUSTOMER QUOTATION ENDPOINTS =========="
# Try to get quotation ID from staff quotations list
try {
    $h = @{ Authorization = $script:authHeader }
    # Also try customer's own quotation
    $qResp = Invoke-RestMethod -Uri "$base/api/customer/quotations/$shipmentId" -Method GET -Headers $h -ErrorAction SilentlyContinue
} catch {}

# Get quotation from staff endpoint (admin)
$adminLoginBody = '{"email":"admin@hms.com","password":"admin123"}'
try {
    $adminResp = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType "application/json" -Body $adminLoginBody
    $adminHeader = "Bearer $($adminResp.accessToken)"
    $hAdmin = @{ Authorization = $adminHeader }
    $qList = Invoke-RestMethod -Uri "$base/api/staff/quotations?page=1&pageSize=5" -Method GET -Headers $hAdmin
    if ($qList.data -and $qList.data.Count -gt 0) {
        $quotationId = $qList.data[0].id
        Write-Output "Found quotation from staff list: $quotationId"
    } elseif ($qList.items -and $qList.items.Count -gt 0) {
        $quotationId = $qList.items[0].id
        Write-Output "Found quotation from staff list: $quotationId"
    }
} catch {
    Write-Output "Could not get quotation from staff list"
}

# Switch back to customer token
$script:authHeader = "Bearer $token"

if ($quotationId) {
    Test-Endpoint GET "/api/customer/quotations/$quotationId" "" "application/json" "GET customer quotation"
    Test-Endpoint GET "/api/customer/quotations/$quotationId/payment-history" "" "application/json" "GET quotation payment history"
} else {
    Write-Output "No quotation ID available, skipping quotation tests"
}

Write-Output "`n========== CUSTOMER PAYMENT ENDPOINTS =========="
# Try to get payment ID from staff payments
try {
    $pList = Invoke-RestMethod -Uri "$base/api/staff/payments?page=1&pageSize=5" -Method GET -Headers $hAdmin
    if ($pList.data -and $pList.data.Count -gt 0) {
        $paymentId = $pList.data[0].id
        Write-Output "Found payment from staff list: $paymentId"
    } elseif ($pList.items -and $pList.items.Count -gt 0) {
        $paymentId = $pList.items[0].id
        Write-Output "Found payment from staff list: $paymentId"
    }
} catch {
    Write-Output "Could not get payment from staff list"
}

# Switch back to customer token
$script:authHeader = "Bearer $token"

if ($paymentId) {
    Test-Endpoint GET "/api/customer/payments/$paymentId" "" "application/json" "GET customer payment detail"
    Test-Endpoint GET "/api/customer/payments/$paymentId/timeline" "" "application/json" "GET customer payment timeline"
    Test-Endpoint POST "/api/customer/payments/$paymentId/retry" "" "application/json" "POST customer retry payment"
    Test-Endpoint POST "/api/customer/payments/$paymentId/cancel" "" "application/json" "POST customer cancel payment"
} else {
    Write-Output "No payment ID available, skipping payment tests"
}

Write-Output "`n========== CUSTOMER FEEDBACK ENDPOINTS =========="
if ($shipmentId) {
    Test-Endpoint POST "/api/customer/shipments/$shipmentId/feedback" '{"rating":5,"comment":"Great service!","feedbackType":"DeliveryQuality"}' "application/json" "POST customer feedback"
} else {
    Write-Output "No shipment ID available, skipping feedback tests"
}

Write-Output "`n========== CUSTOMER DASHBOARD =========="
Test-Endpoint GET "/api/customer/dashboard/stats" "" "application/json" "GET customer dashboard stats"

# === LOGIN AS DRIVER ===
Write-Output "`n========== DRIVER LOGIN =========="
$driverLoginBody = '{"email":"driver@hms.com","password":"driver123"}'
try {
    $dResp = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -ContentType "application/json" -Body $driverLoginBody
    $driverToken = $dResp.accessToken
    $script:authHeader = "Bearer $driverToken"
    Write-Output "Driver login OK"
} catch {
    Write-Output "Driver login FAILED: $($_.Exception.Message)"
    try {
        $dResp2 = Invoke-WebRequest -Uri "$base/api/auth/login" -Method POST -ContentType "application/json" -Body $driverLoginBody -UseBasicParsing
        $dJson = $dResp2.Content | ConvertFrom-Json
        $driverToken = $dJson.accessToken
        $script:authHeader = "Bearer $driverToken"
        Write-Output "Driver login OK (alt)"
    } catch {
        Write-Output "Driver login FAILED completely. Aborting driver tests."
        Write-Output "`n========== SUMMARY =========="
        Write-Output "TOTAL: $(($ok + $fail + $clientErr)) | OK: $ok | 4xx: $clientErr | FAIL: $fail"
        return
    }
}

Write-Output "`n========== DRIVER MATCHING ENDPOINTS =========="
Test-Endpoint GET "/api/drivers/me/matching-suggestions" "" "application/json" "GET driver matching suggestions"

Write-Output "`n========== DRIVER PROPOSAL ENDPOINTS =========="
Test-Endpoint GET "/api/driver/proposals/pending" "" "application/json" "GET driver pending proposals"

# Get a proposal ID
$proposalId = $null
try {
    $h = @{ Authorization = $script:authHeader }
    $propResp = Invoke-RestMethod -Uri "$base/api/driver/proposals/pending" -Method GET -Headers $h
    if ($propResp -and $propResp.Count -gt 0) {
        $proposalId = $propResp[0].id
        Write-Output "  -> Found proposal: $proposalId"
    } elseif ($propResp.data -and $propResp.data.Count -gt 0) {
        $proposalId = $propResp.data[0].id
        Write-Output "  -> Found proposal: $proposalId"
    }
} catch {
    Write-Output "  -> No pending proposals found"
}

Write-Output "`n========== DRIVER TRIP ENDPOINTS =========="
Test-Endpoint GET "/api/driver/trips?page=1&pageSize=5" "" "application/json" "GET driver trips"

# Get a trip ID
$tripId = $null
try {
    $h = @{ Authorization = $script:authHeader }
    $tripResp = Invoke-RestMethod -Uri "$base/api/driver/trips?page=1&pageSize=5" -Method GET -Headers $h
    if ($tripResp.data -and $tripResp.data.Count -gt 0) {
        $tripId = $tripResp.data[0].id
        Write-Output "  -> Found trip: $tripId (status: $($tripResp.data[0].status))"
    } elseif ($tripResp.items -and $tripResp.items.Count -gt 0) {
        $tripId = $tripResp.items[0].id
        Write-Output "  -> Found trip: $tripId"
    } elseif ($tripResp.Count -gt 0) {
        $tripId = $tripResp[0].id
        Write-Output "  -> Found trip: $tripId"
    }
} catch {
    Write-Output "  -> No trips found"
}

if ($tripId) {
    Test-Endpoint GET "/api/driver/trips/$tripId" "" "application/json" "GET driver trip detail"
}

Write-Output "`n========== DRIVER EXTERNAL SHIPMENT ENDPOINTS =========="
Test-Endpoint GET "/api/driver/external-shipments?page=1&pageSize=5" "" "application/json" "GET driver external shipments"

Write-Output "`n========== DRIVER COD ENDPOINTS =========="
if ($paymentId) {
    # Switch to driver for COD
    Test-Endpoint POST "/api/driver/payments/$paymentId/confirm-cod" "" "application/json" "POST driver confirm COD"
} else {
    Write-Output "No payment ID for COD test"
}

Write-Output "`n========== SUMMARY =========="
Write-Output "TOTAL: $(($ok + $fail + $clientErr)) | OK: $ok | 4xx: $clientErr | FAIL: $fail"
