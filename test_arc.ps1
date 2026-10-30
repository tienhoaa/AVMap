Add-Type -Path 'C:\Program Files\Autodesk\AutoCAD 2023\acdbmgd.dll'
Add-Type -Path 'C:\Program Files\Autodesk\AutoCAD 2023\acmgd.dll'
Add-Type -Path 'C:\Program Files\Autodesk\AutoCAD 2023\accoremgd.dll'

$pl = New-Object Autodesk.AutoCAD.DatabaseServices.Polyline
$pl.AddVertexAt(0, (New-Object Autodesk.AutoCAD.Geometry.Point2d(0, 0)), 0.5, 0, 0)
$pl.AddVertexAt(1, (New-Object Autodesk.AutoCAD.Geometry.Point2d(10, 0)), 0, 0, 0)

$segType = $pl.GetSegmentType(0)
Write-Output "SegmentType: $segType"

$arc = $pl.GetArcSegment2dAt(0)
Write-Output "Arc Center: $($arc.Center) Radius: $($arc.Radius)"
Write-Output "Arc StartPoint: $($arc.StartPoint) EndPoint: $($arc.EndPoint)"
Write-Output "Arc IsClockWise: $($arc.IsClockWise)"

$samples = $arc.GetSamplePoints(6)
foreach ($pt in $samples) {
    Write-Output "  Sample: $pt"
}

$pl2 = New-Object Autodesk.AutoCAD.DatabaseServices.Polyline
$pl2.AddVertexAt(0, (New-Object Autodesk.AutoCAD.Geometry.Point2d(0, 0)), -0.5, 0, 0)
$pl2.AddVertexAt(1, (New-Object Autodesk.AutoCAD.Geometry.Point2d(10, 0)), 0, 0, 0)

$arc2 = $pl2.GetArcSegment2dAt(0)
Write-Output "--- Bulge -0.5 ---"
Write-Output "Arc2 Center: $($arc2.Center) Radius: $($arc2.Radius)"
Write-Output "Arc2 StartPoint: $($arc2.StartPoint) EndPoint: $($arc2.EndPoint)"
Write-Output "Arc2 IsClockWise: $($arc2.IsClockWise)"

$samples2 = $arc2.GetSamplePoints(6)
foreach ($pt in $samples2) {
    Write-Output "  Sample: $pt"
}
