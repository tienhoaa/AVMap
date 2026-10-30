Add-Type -AssemblyName System.Drawing
$cache = [System.IO.Path]::Combine($env:LOCALAPPDATA, 'AVMap', 'Cache')
$files = Get-ChildItem -Path $cache -Filter 's_stitched*' | Sort-Object LastWriteTime -Descending
if ($files.Count -gt 0) {
    $latest = $files[0].FullName
    Write-Host "File: $latest"
    $bmp = New-Object System.Drawing.Bitmap($latest)
    Write-Host "Size: $($bmp.Width) x $($bmp.Height)"
    $cp = $bmp.GetPixel([int]($bmp.Width / 2), [int]($bmp.Height / 2))
    Write-Host "Center Pixel: R=$($cp.R), G=$($cp.G), B=$($cp.B)"
    $bmp.Dispose()
}