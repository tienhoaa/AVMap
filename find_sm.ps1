Get-ChildItem -Path "c:\Program Files\Opencartis\Spatial Manager for AutoCAD" -Recurse -Filter "*.dll" | ForEach-Object {
    $m = Select-String -Path $_.FullName -Pattern "StreetView" -SimpleMatch -Quiet
    if ($m) { Write-Output $_.FullName }
}
