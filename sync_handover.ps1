# 1. Update BAN_GIAO_AVMAP_AUTOCAD
$acadSrc = "C:\Users\GAMING-PC\Documents\AVMap\bin\Release\net48"
$acadDst = "C:\Users\GAMING-PC\Documents\AVMap\BAN_GIAO_AVMAP_AUTOCAD"

Remove-Item (Join-Path $acadDst "AVMap_AutoCAD_v27_Fixed.dll") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $acadDst "AVMap_AutoCAD_v*.dll") -Force -ErrorAction SilentlyContinue

Copy-Item (Join-Path $acadSrc "AVMap_AutoCAD.dll") $acadDst -Force
Copy-Item (Join-Path $acadSrc "*.dll") $acadDst -Force
Copy-Item (Join-Path $acadSrc "runtimes") $acadDst -Recurse -Force

# Also update root and Release_AutoCAD for direct netloading
Copy-Item (Join-Path $acadSrc "AVMap_AutoCAD.dll") "C:\Users\GAMING-PC\Documents\AVMap\AVMap_AutoCAD.dll" -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $acadSrc "AVMap_AutoCAD.dll") "C:\Users\GAMING-PC\Documents\AVMap\Release_AutoCAD\AVMap_AutoCAD.dll" -Force -ErrorAction SilentlyContinue

# 2. Update BAN_GIAO_AVMAP_ENJICAD
$enjiSrc = "C:\Users\GAMING-PC\Documents\AVMap\enjiCAD\bin\Release\net8.0-windows"
$enjiDst = "C:\Users\GAMING-PC\Documents\AVMap\BAN_GIAO_AVMAP_ENJICAD"

Remove-Item (Join-Path $enjiDst "AVMap_enjiCAD_v*.dll") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $enjiDst "AVMap_enjiCAD_v*.deps.json") -Force -ErrorAction SilentlyContinue

Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.dll") $enjiDst -Force
Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.deps.json") $enjiDst -Force
Copy-Item (Join-Path $enjiSrc "Magick*.dll") $enjiDst -Force
Copy-Item (Join-Path $enjiSrc "Microsoft.Web.WebView2*.dll") $enjiDst -Force
Copy-Item (Join-Path $enjiSrc "Newtonsoft.Json.dll") $enjiDst -Force
Copy-Item (Join-Path $enjiSrc "runtimes") $enjiDst -Recurse -Force

# Also update root and Release_enjiCAD
Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.dll") "C:\Users\GAMING-PC\Documents\AVMap\AVMap_enjiCAD.dll" -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.deps.json") "C:\Users\GAMING-PC\Documents\AVMap\AVMap_enjiCAD.deps.json" -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.dll") "C:\Users\GAMING-PC\Documents\AVMap\Release_enjiCAD\AVMap_enjiCAD.dll" -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.deps.json") "C:\Users\GAMING-PC\Documents\AVMap\Release_enjiCAD\AVMap_enjiCAD.deps.json" -Force -ErrorAction SilentlyContinue

# 3. Create fresh zip packages
$zipAcad = "C:\Users\GAMING-PC\Documents\AVMap\AVMap_Cho_AutoCAD.zip"
$zipEnji = "C:\Users\GAMING-PC\Documents\AVMap\AVMap_Cho_enjiCAD.zip"

if (Test-Path $zipAcad) { Remove-Item $zipAcad -Force }
if (Test-Path $zipEnji) { Remove-Item $zipEnji -Force }

Compress-Archive -Path "$acadDst\*" -DestinationPath $zipAcad -Force
Compress-Archive -Path "$enjiDst\*" -DestinationPath $zipEnji -Force

Write-Output "ALL SYNC & PACKAGING COMPLETED!"
