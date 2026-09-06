param([string]$TokenFile="$env:TEMP\hms_atk.txt")

$tk = Get-Content $TokenFile -Raw
$h = @{Authorization="Bearer $tk"; "Content-Type"="application/json"}

function Test-Endpoint {
    param($Method, $Url, $Body)
    try {
        $params = @{Uri=$Url; Method=$Method; Headers=$h; UseBasicParsing=$true; TimeoutSec=10}
        if ($Body) { $params.Body = $Body }
        $r = Invoke-WebRequest @params
        return @{ Status=$r.StatusCode; Body=$r.Content.Substring(0, [Math]::Min(150, $r.Content.Length)) }
    } catch {
        $code = 0
        $errBody = ""
        if ($_.Exception.Response) {
            $code = [int]$_.Exception.Response.StatusCode
            try {
                $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $errBody = $sr.ReadToEnd()
                $sr.Close()
            } catch {}
        }
        return @{ Status=$code; Body=$errBody.Substring(0, [Math]::Min(200, $errBody.Length)) }
    }
}

$results = @()

function Test($name, $method, $url, $body=$null) {
    $r = Test-Endpoint $method $url $body
    $icon = if ($r.Status -ge 200 -and $r.Status -lt 300) { "OK" } elseif ($r.Status -ge 400 -and $r.Status -lt 500) { "4xx" } else { "FAIL" }
    Write-Output "$icon $($r.Status) $name"
    $script:results += "$icon $($r.Status) $name"
}

Write-Output "`n========== STAFF PROPOSAL ENDPOINTS =========="
Test "GET staff/proposals" GET "http://localhost:5104/api/staff/proposals"
Test "POST staff/proposals/f04d.../approve (Confirmed)" POST "http://localhost:5104/api/staff/proposals/f04d3a3a-d778-4e65-a2d8-c5a7ab024c3d/approve" '{"notes":"test"}'
Test "POST staff/proposals/f04d.../reject (Confirmed)" POST "http://localhost:5104/api/staff/proposals/f04d3a3a-d778-4e65-a2d8-c5a7ab024c3d/reject" '{"notes":"test"}'

Write-Output "`n========== STAFF QUOTATION ENDPOINTS =========="
Test "GET staff/quotations" GET "http://localhost:5104/api/staff/quotations"
Test "PUT staff/quotations/db60... (Cancelled)" PUT "http://localhost:5104/api/staff/quotations/db60aad2-96f2-4575-9d8c-558c4c0eb077" '{"shippingFee":60000,"depositAmount":25000,"expiresAt":"2026-09-01T00:00:00Z"}'
Test "POST staff/quotations/db60.../send (Cancelled)" POST "http://localhost:5104/api/staff/quotations/db60aad2-96f2-4575-9d8c-558c4c0eb077/send"
Test "POST staff/quotations/db60.../cancel (Cancelled)" POST "http://localhost:5104/api/staff/quotations/db60aad2-96f2-4575-9d8c-558c4c0eb077/cancel"

Write-Output "`n========== STAFF PAYMENT ENDPOINTS =========="
Test "GET staff/payments" GET "http://localhost:5104/api/staff/payments"
Test "GET staff/payments/6782.../timeline" GET "http://localhost:5104/api/staff/payments/6782aae0-06fa-45a8-8d41-7447dfbf8cfe/timeline"
Test "POST staff/payments/6782.../refund" POST "http://localhost:5104/api/staff/payments/6782aae0-06fa-45a8-8d41-7447dfbf8cfe/refund" '{"reason":"Test refund"}'
Test "POST staff/payments/6782.../refund/approve" POST "http://localhost:5104/api/staff/payments/6782aae0-06fa-45a8-8d41-7447dfbf8cfe/refund/approve" '{"approved":true}'

Write-Output "`n========== STAFF INCIDENT ENDPOINTS =========="
Test "GET staff/incidents" GET "http://localhost:5104/api/staff/incidents"
Test "POST staff/incidents/4c94.../take (Resolved)" POST "http://localhost:5104/api/staff/incidents/4c9491bc-ff93-4a54-87db-65d5724e4f40/take"
Test "POST staff/incidents/4c94.../resolve (Resolved)" POST "http://localhost:5104/api/staff/incidents/4c9491bc-ff93-4a54-87db-65d5724e4f40/resolve" '{"resolution":"Already resolved"}'
Test "POST staff/incidents/4c94.../reject (Resolved)" POST "http://localhost:5104/api/staff/incidents/4c9491bc-ff93-4a54-87db-65d5724e4f40/reject" '{"reason":"test"}'

Write-Output "`n========== STAFF FEEDBACK ENDPOINTS =========="
Test "GET staff/feedbacks" GET "http://localhost:5104/api/staff/feedbacks"

Write-Output "`n========== HUB ENDPOINTS =========="
Test "GET hubs" GET "http://localhost:5104/api/hubs"
Test "GET hubs/1111..." GET "http://localhost:5104/api/hubs/11111111-2222-3333-4444-555555555551"

Write-Output "`n========== HUB-INVENTORY ENDPOINTS =========="
Test "GET hub-inventory" GET "http://localhost:5104/api/hub-inventory"
Test "GET hub-inventory/dashboard" GET "http://localhost:5104/api/hub-inventory/dashboard"

Write-Output "`n========== VEHICLE ENDPOINTS =========="
Test "GET vehicles" GET "http://localhost:5104/api/vehicles"

Write-Output "`n========== TRIP POST ENDPOINTS =========="
Test "GET trip-posts" GET "http://localhost:5104/api/trip-posts"
Test "GET trip-posts/kpi" GET "http://localhost:5104/api/trip-posts/kpi"
Test "GET trip-posts/public" GET "http://localhost:5104/api/trip-posts/public"
Test "GET trip-posts/eligible-trips" GET "http://localhost:5104/api/trip-posts/eligible-trips"

Write-Output "`n========== ADMIN DASHBOARD =========="
Test "GET admin/dashboard/stats" GET "http://localhost:5104/api/admin/dashboard/stats"

Write-Output "`n========== PUBLIC ENDPOINTS =========="
Test "GET hubs (public)" GET "http://localhost:5104/api/identity/hubs"
Test "GET users (public)" GET "http://localhost:5104/api/identity/users"
Test "GET trips (public)" GET "http://localhost:5104/api/trips/public"
Test "GET trips/active" GET "http://localhost:5104/api/trips/active"

Write-Output "`n========== GEOCODING =========="
Test "POST geocoding/search" POST "http://localhost:5104/api/geocoding/search" '{"address":"Ho Chi Minh City"}'

Write-Output "`n========== SUMMARY =========="
$ok = ($results | Where-Object { $_ -match "^OK" }).Count
$fail = ($results | Where-Object { $_ -match "^FAIL" }).Count
$xx = ($results | Where-Object { $_ -match "^4xx" }).Count
Write-Output "TOTAL: $($results.Count) | OK: $ok | 4xx: $xx | FAIL: $fail"
if ($fail -gt 0) { Write-Output "`nFAILED ENDPOINTS:"; $results | Where-Object { $_ -match "^FAIL" } | ForEach-Object { Write-Output "  $_" } }
