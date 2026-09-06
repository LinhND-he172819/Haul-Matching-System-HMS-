$tokens = $null
$errors = $null
$code = @'
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
'@
[System.Management.Automation.Language.Parser]::ParseInput($code, [ref]$tokens, [ref]$errors) | Out-Null
if ($errors.Count -gt 0) {
    Write-Host 'Errors:'
    foreach ($e in $errors) { Write-Host ('  Line ' + $e.Extent.StartLineNumber + ': ' + $e.Message) }
} else {
    Write-Host 'No errors'
}
