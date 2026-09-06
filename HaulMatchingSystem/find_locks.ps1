$procs = Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue
foreach ($proc in $procs) {
    foreach ($mod in $proc.Modules) {
        if ($mod.FileName -like '*HMS*') {
            Write-Host "$($proc.Id) $($mod.FileName)"
        }
    }
}
