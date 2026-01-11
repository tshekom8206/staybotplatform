$items = Get-ChildItem -Path 'C:\Users\Administrator\Downloads\hostr\apps\api\bin\Release\net8.0\publish' -Recurse -Name
foreach ($item in $items) {
    if ($item.Length -gt 200) {
        Write-Host "$($item.Length): $item"
    }
}
