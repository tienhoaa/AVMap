@echo off
setlocal EnableDelayedExpansion
title AVMap - Bat Tu Dong Khoi Dong Cung CAD

echo ========================================================
echo        BAT TU DONG KHOI DONG AVMAP CUNG CAD (.BUNDLE)
echo ========================================================
echo.

set "BUNDLE_DIR=%APPDATA%\Autodesk\ApplicationPlugins\AVMap.bundle"
set "CONTENTS_DIR=%BUNDLE_DIR%\Contents"
set "SRC_DIR=%~dp0"

echo [*] Dang tao thu muc .bundle Autodesk...
if not exist "%CONTENTS_DIR%" mkdir "%CONTENTS_DIR%"

echo [*] Dang sao chep cac file AVMap vao thu muc .bundle...
if exist "%SRC_DIR%AVMap_AutoCAD.dll" (
    copy /y "%SRC_DIR%*.dll" "%CONTENTS_DIR%\" >nul
    copy /y "%SRC_DIR%*.json" "%CONTENTS_DIR%\" >nul
) else if exist "%SRC_DIR%AutoCAD\AVMap_AutoCAD.dll" (
    copy /y "%SRC_DIR%AutoCAD\*.dll" "%CONTENTS_DIR%\" >nul
    copy /y "%SRC_DIR%AutoCAD\*.json" "%CONTENTS_DIR%\" >nul
) else if exist "%SRC_DIR%..\AutoCAD\AVMap_AutoCAD.dll" (
    copy /y "%SRC_DIR%..\AutoCAD\*.dll" "%CONTENTS_DIR%\" >nul
    copy /y "%SRC_DIR%..\AutoCAD\*.json" "%CONTENTS_DIR%\" >nul
) else (
    echo [CANH BAO] Khong tim thay file AVMap_AutoCAD.dll canh file BAT!
)

echo [*] Dang tao file cau hinh PackageContents.xml...
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<ApplicationPackage SchemaVersion="1.0" AppVersion="1.3.0" Author="AVMap Team" ProductCode="{0F72D04A-D8A8-4F11-8BD4-73891461A639}" Name="AVMap" Description="AVMap - Ban Do Nen &amp; GIS AutoCAD"^>
echo   ^<CompanyDetails Name="AVMap" Url="https://github.com/tienhoaa/AVMap" /^>
echo   ^<Components Description="CAD Components"^>
echo     ^<RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R19.0" /^>
echo     ^<ComponentEntry AppName="AVMap" ModuleName="./Contents/AVMap_AutoCAD.dll" AppType=".NET" LoadOnAutoCADStartup="True" /^>
echo   ^<Components^>
echo ^</ApplicationPackage^>
) > "%BUNDLE_DIR%\PackageContents.xml"

echo.
echo ========================================================
echo  [THANH CONG] DA BAT TU DONG KHOI DONG CUNG CAD!
echo ========================================================
echo.
echo  Tu lan mo AutoCAD tiep theo, AVMap se tu dong nạp 100%%
echo  ma ban KHONG CAN go lenh NETLOAD nua!
echo.
echo  Ban co the dong CAD va mo lai de kiem tra.
echo.
pause
