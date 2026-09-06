$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile('C:\Do_An\Haul-Matching-System-HMS-\HaulMatchingSystem\test-trip-shipments.ps1', [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) {
    Write-Host 'Errors:'
    foreach ($e in $errors) { Write-Host ('  Line ' + $e.Extent.StartLineNumber + ', Col ' + $e.Extent.StartColumnNumber + ': ' + $e.Message) }
} else {
    Write-Host 'No parse errors. File is valid.'
}
