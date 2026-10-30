Add-Type -Path 'C:\Program Files\CIC\enjiCAD2026\GcDbMgd.dll'
Add-Type -Path 'C:\Program Files\CIC\enjiCAD2026\GcMgd.dll'
Add-Type -Path 'C:\Program Files\CIC\enjiCAD2026\GcCoreMgd.dll'

$pl = New-Object Gssoft.Gscad.DatabaseServices.Polyline
$pl.AddVertexAt(0, (New-Object Gssoft.Gscad.Geometry.Point2d(0, 0)), 0.5, 0, 0)
$pl.AddVertexAt(1, (New-Object Gssoft.Gscad.Geometry.Point2d(10, 0)), 0, 0, 0)

$segType = $pl.GetSegmentType(0)
Write-Output "enjiCAD SegmentType: $segType"

$arc = $pl.GetArcSegment2dAt(0)
Write-Output "enjiCAD Arc Center: $($arc.Center) Radius: $($arc.Radius)"

$samples = $arc.GetSamplePoints(6)
foreach ($pt in $samples) {
    Write-Output "  enjiCAD Sample: $pt"
}
