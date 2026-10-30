$url = "https://mt1.google.com/vt/lyrs=svv|cb_client:apiv3&x=52607&y=29891&z=16"
$out = "C:\Users\GAMING-PC\Documents\AVMap\test_apiv3.png"
Invoke-WebRequest -Uri $url -OutFile $out -Headers @{"User-Agent"="Mozilla/5.0"}
Get-Item $out | Select-Object Length, LastWriteTime

$url2 = "https://mt1.google.com/mapslt?lyrs=svv&x=52607&y=29891&z=16&w=256&h=256&hl=en&style=40,18"
$out2 = "C:\Users\GAMING-PC\Documents\AVMap\test_mapslt.png"
Invoke-WebRequest -Uri $url2 -OutFile $out2 -Headers @{"User-Agent"="Mozilla/5.0"}
Get-Item $out2 | Select-Object Length, LastWriteTime
