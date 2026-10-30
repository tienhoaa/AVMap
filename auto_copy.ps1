for ($i = 0; $i -lt 600; $i++) {
    try {
        Copy-Item -Path "C:\Users\GAMING-PC\Documents\AVMap\Release_AutoCAD\AVMap_AutoCAD.dll" -Destination "C:\Users\GAMING-PC\Documents\AVMap\AVMap_AutoCAD.dll" -Force -ErrorAction Stop
        Write-Output "SUCCESS: Root AVMap_AutoCAD.dll updated!"
        break
    } catch {
        Start-Sleep -Seconds 1
    }
}
