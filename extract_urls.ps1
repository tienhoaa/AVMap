$bytes = [System.IO.File]::ReadAllBytes("C:\Program Files\Opencartis\Spatial Manager for AutoCAD\AutoCAD R23.1\Opencartis.SpatialManager.AcadApp.R23.1.dll")
$str = [System.Text.Encoding]::ASCII.GetString($bytes)
[regex]::Matches($str, "https://[a-zA-Z0-9\./\?=_&,-]+") | ForEach-Object { $_.Value } | Select-Object -Unique
