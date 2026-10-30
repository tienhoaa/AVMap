[System.Reflection.Assembly]::LoadFrom("C:\Program Files\Autodesk\AutoCAD 2023\accoremgd.dll") | Out-Null
[System.Reflection.Assembly]::LoadFrom("C:\Program Files\Autodesk\AutoCAD 2023\acdbmgd.dll") | Out-Null
[System.Reflection.Assembly]::LoadFrom("C:\Program Files\Autodesk\AutoCAD 2023\acmgd.dll") | Out-Null

$asm = [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Opencartis\Spatial Manager for AutoCAD\AutoCAD R23.1\Opencartis.SpatialManager.AcadApp.R23.1.dll")
$types = $asm.GetTypes() | Where-Object { $_.Name -like "*StreetView*" -or $_.Name -like "*Pegman*" }
foreach ($t in $types) {
    Write-Output "Type: $($t.FullName)"
    foreach ($m in $t.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        Write-Output "   Method: $($m.Name)"
    }
}
