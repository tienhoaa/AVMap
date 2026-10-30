$acad = [System.Runtime.InteropServices.Marshal]::GetActiveObject("AutoCAD.Application")
$doc = $acad.ActiveDocument
Write-Host "Active Doc Name: $($doc.Name)"
Write-Host "Active Doc Path: $($doc.Path)"
$viewCtr = $doc.GetVariable("VIEWCTR")
Write-Host "VIEWCTR: $($viewCtr[0]), $($viewCtr[1]), $($viewCtr[2])"
$viewSize = $doc.GetVariable("VIEWSIZE")
Write-Host "VIEWSIZE: $viewSize"
$insunits = $doc.GetVariable("INSUNITS")
Write-Host "INSUNITS: $insunits"
$extmin = $doc.GetVariable("EXTMIN")
Write-Host "EXTMIN: $($extmin[0]), $($extmin[1])"
$extmax = $doc.GetVariable("EXTMAX")
Write-Host "EXTMAX: $($extmax[0]), $($extmax[1])"