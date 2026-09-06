Get-Process -Name 'HMS.API' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
Write-Host "Done. HMS.API processes killed."
