$runtimesPath = 'C:\Users\Administrator\Downloads\hostr\apps\apipublish\runtimes'
$keepRuntimes = @('win', 'win-x64')

Get-ChildItem $runtimesPath -Directory | ForEach-Object {
    if ($keepRuntimes -notcontains $_.Name) {
        Write-Host "Removing $($_.Name)"
        Remove-Item $_.FullName -Recurse -Force
    } else {
        Write-Host "Keeping $($_.Name)"
    }
}

Write-Host "Done"
