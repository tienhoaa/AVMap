Get-ChildItem -Path "C:\Program Files\Opencartis\Spatial Manager for AutoCAD" -Recurse -Filter "*.dll" | ForEach-Object {
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    $str = [System.Text.Encoding]::ASCII.GetString($bytes)
    $m = [regex]::Matches($str, "(https://mt[0-9]\.google\.com/[^\x00-\x1F\s""]+|https://maps\.googleapis\.com/[^\x00-\x1F\s""]+|mapslt\?lyrs=[^\x00-\x1F\s""]+)")
    if ($m.Count -gt 0) {
        Write-Output "File: $($_.Name)"
        $m | ForEach-Object { Write-Output "   $($_.Value)" }
    }
}
