Add-Type -AssemblyName System.Drawing

$lat = 12.2592
$lon = 108.9965

foreach ($z in 11..16) {
    $x = [math]::Floor(($lon + 180.0) / 360.0 * [math]::Pow(2.0, $z))
    $latRad = $lat * [math]::PI / 180.0
    $y = [math]::Floor((1.0 - [math]::Log([math]::Tan($latRad) + 1.0 / [math]::Cos($latRad)) / [math]::PI) / 2.0 * [math]::Pow(2.0, $z))
    $url = "https://mt1.google.com/mapslt?lyrs=svv&x=$x&y=$y&z=$z&w=256&h=256&hl=en&style=40,18"
    $out = "C:\Users\GAMING-PC\Documents\AVMap\test_svv_z$z.png"
    Invoke-WebRequest -Uri $url -OutFile $out -Headers @{"User-Agent"="Mozilla/5.0"}
    $bmp = [System.Drawing.Bitmap]::FromFile($out)
    $nonZero = 0
    for ($px=0; $px -lt 256; $px++) {
        for ($py=0; $py -lt 256; $py++) {
            if ($bmp.GetPixel($px, $py).A -gt 10) { $nonZero++ }
        }
    }
    $bmp.Dispose()
    Write-Output "Zoom $z : Tile ($x, $y) - NonZero Pixels: $nonZero - FileSize: $((Get-Item $out).Length)"
}
