# 1. AutoCAD package
$acadSrc = "C:\Users\GAMING-PC\Documents\AVMap\bin\Release\net48"
$acadStage = "C:\Users\GAMING-PC\Documents\AVMap\BAN_GIAO_AUTOCAD_BUILD"

if (Test-Path $acadStage) { Remove-Item $acadStage -Recurse -Force }
New-Item -ItemType Directory -Path $acadStage -Force | Out-Null

Copy-Item (Join-Path $acadSrc "AVMap_AutoCAD.dll") $acadStage -Force
Copy-Item (Join-Path $acadSrc "*.dll") $acadStage -Force
if (Test-Path (Join-Path $acadSrc "runtimes")) {
    Copy-Item (Join-Path $acadSrc "runtimes") $acadStage -Recurse -Force
}
Copy-Item "C:\Users\GAMING-PC\Documents\AVMap\BAN_GIAO_AVMAP_AUTOCAD\HUONG_DAN_SU_DUNG.txt" $acadStage -Force

$zipAcad = "C:\Users\GAMING-PC\Documents\AVMap\AVMap_Cho_AutoCAD.zip"
if (Test-Path $zipAcad) { Remove-Item $zipAcad -Force }
Compress-Archive -Path "$acadStage\*" -DestinationPath $zipAcad -Force
Write-Output "AutoCAD zip created successfully: $(Get-Item $zipAcad | Select-Object -ExpandProperty Length) bytes"

try {
    Copy-Item (Join-Path $acadSrc "AVMap_AutoCAD.dll") "C:\Users\GAMING-PC\Documents\AVMap\BAN_GIAO_AVMAP_AUTOCAD\AVMap_AutoCAD.dll" -Force -ErrorAction Stop
    Write-Output "BAN_GIAO_AVMAP_AUTOCAD updated directly!"
} catch {
    Write-Output "Note: BAN_GIAO_AVMAP_AUTOCAD is currently locked by running AutoCAD process."
}

# 2. enjiCAD package
$enjiSrc = "C:\Users\GAMING-PC\Documents\AVMap\enjiCAD\bin\Release\net8.0-windows"
$enjiStage = "C:\Users\GAMING-PC\Documents\AVMap\BAN_GIAO_ENJICAD_BUILD"

if (Test-Path $enjiStage) { Remove-Item $enjiStage -Recurse -Force }
New-Item -ItemType Directory -Path $enjiStage -Force | Out-Null

Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.dll") $enjiStage -Force
Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.deps.json") $enjiStage -Force
Copy-Item (Join-Path $enjiSrc "*.dll") $enjiStage -Force
if (Test-Path (Join-Path $enjiSrc "runtimes")) {
    Copy-Item (Join-Path $enjiSrc "runtimes") $enjiStage -Recurse -Force
}
Copy-Item "C:\Users\GAMING-PC\Documents\AVMap\BAN_GIAO_AVMAP_ENJICAD\HUONG_DAN_SU_DUNG.txt" $enjiStage -Force

$zipEnji = "C:\Users\GAMING-PC\Documents\AVMap\AVMap_Cho_enjiCAD.zip"
if (Test-Path $zipEnji) { Remove-Item $zipEnji -Force }
Compress-Archive -Path "$enjiStage\*" -DestinationPath $zipEnji -Force
Write-Output "enjiCAD zip created successfully: $(Get-Item $zipEnji | Select-Object -ExpandProperty Length) bytes"

try {
    Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.dll") "C:\Users\GAMING-PC\Documents\AVMap\BAN_GIAO_AVMAP_ENJICAD\AVMap_enjiCAD.dll" -Force -ErrorAction Stop
    Write-Output "BAN_GIAO_AVMAP_ENJICAD updated directly!"
} catch {
    Write-Output "Note: BAN_GIAO_AVMAP_ENJICAD is currently locked by running enjiCAD process. New build is safely staged in BAN_GIAO_ENJICAD_BUILD and compressed into AVMap_Cho_enjiCAD.zip."
}
