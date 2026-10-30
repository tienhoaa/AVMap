[System.Reflection.Assembly]::LoadFrom("C:\Program Files\Autodesk\AutoCAD 2023\accoremgd.dll") | Out-Null
[System.Reflection.Assembly]::LoadFrom("C:\Program Files\Autodesk\AutoCAD 2023\acdbmgd.dll") | Out-Null
[System.Reflection.Assembly]::LoadFrom("C:\Program Files\Autodesk\AutoCAD 2023\acmgd.dll") | Out-Null

$asm = [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Opencartis\Spatial Manager for AutoCAD\AutoCAD R23.1\Opencartis.SpatialManager.AcadApp.R23.1.dll")
foreach ($t in $asm.GetTypes()) {
    foreach ($m in $t.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
        $attrs = $m.GetCustomAttributes($false)
        foreach ($a in $attrs) {
            if ($a.GetType().Name -eq "CommandMethodAttribute") {
                $cmdName = $a.GlobalName
                if ($cmdName -like "*STREETVIEW*") {
                    Write-Output "Found Command: $cmdName in $($t.FullName).$($m.Name)"
                }
            }
        }
    }
}
