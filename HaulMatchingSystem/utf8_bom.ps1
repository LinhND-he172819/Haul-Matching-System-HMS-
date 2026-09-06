$path = 'C:\Do_An\Haul-Matching-System-HMS-\HaulMatchingSystem\test-trip-shipments.ps1'
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$utf8Bom = New-Object System.Text.UTF8Encoding($true)
[System.IO.File]::WriteAllText($path, $content, $utf8Bom)
Write-Host 'Added UTF-8 BOM to test-trip-shipments.ps1'
