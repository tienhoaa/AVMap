@echo off
setlocal EnableDelayedExpansion
title AVMap - Bat Tu Dong Khoi Dong Cung enjiCAD 2026

echo ========================================================
echo     BAT TU DONG KHOI DONG AVMAP CUNG ENJICAD 2026
echo ========================================================
echo.

set "BUNDLE_DIR=%APPDATA%\enjiCAD\ApplicationPlugins\AVMap_enji.bundle"
set "CONTENTS_DIR=%BUNDLE_DIR%\Contents"
set "GSTAR_BUNDLE=%APPDATA%\Gstarsoft\ApplicationPlugins\AVMap_enji.bundle\Contents"
set "SRC_DIR=%~dp0"

echo [*] Dang tao thu muc .bundle cho enjiCAD...
if not exist "%CONTENTS_DIR%" mkdir "%CONTENTS_DIR%"
if not exist "%GSTAR_BUNDLE%" mkdir "%GSTAR_BUNDLE%"

echo [*] Dang sao chep file AVMap_enjiCAD vao thu muc khoi dong...
if exist "%SRC_DIR%AVMap_enjiCAD.dll" (
    copy /y "%SRC_DIR%*.dll" "%CONTENTS_DIR%\" >nul 2>&1
    copy /y "%SRC_DIR%*.json" "%CONTENTS_DIR%\" >nul 2>&1
    copy /y "%SRC_DIR%*.dll" "%GSTAR_BUNDLE%\" >nul 2>&1
    copy /y "%SRC_DIR%*.json" "%GSTAR_BUNDLE%\" >nul 2>&1
) else if exist "%SRC_DIR%enjiCAD\AVMap_enjiCAD.dll" (
    copy /y "%SRC_DIR%enjiCAD\*.dll" "%CONTENTS_DIR%\" >nul 2>&1
    copy /y "%SRC_DIR%enjiCAD\*.json" "%CONTENTS_DIR%\" >nul 2>&1
    copy /y "%SRC_DIR%enjiCAD\*.dll" "%GSTAR_BUNDLE%\" >nul 2>&1
    copy /y "%SRC_DIR%enjiCAD\*.json" "%GSTAR_BUNDLE%\" >nul 2>&1
) else if exist "%SRC_DIR%..\enjiCAD\AVMap_enjiCAD.dll" (
    copy /y "%SRC_DIR%..\enjiCAD\*.dll" "%CONTENTS_DIR%\" >nul 2>&1
    copy /y "%SRC_DIR%..\enjiCAD\*.json" "%CONTENTS_DIR%\" >nul 2>&1
    copy /y "%SRC_DIR%..\enjiCAD\*.dll" "%GSTAR_BUNDLE%\" >nul 2>&1
    copy /y "%SRC_DIR%..\enjiCAD\*.json" "%GSTAR_BUNDLE%\" >nul 2>&1
)

echo [*] Dang tao file cau hinh PackageContents.xml cho enjiCAD...
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<ApplicationPackage SchemaVersion="1.0" AppVersion="1.4.0" Author="AVMap Team" ProductCode="{0F72D04A-D8A8-4F11-8BD4-73891461A639}" Name="AVMap" Description="AVMap - Ban Do Nen &amp; GIS enjiCAD"^>
echo   ^<CompanyDetails Name="AVMap" Url="https://github.com/tienhoaa/AVMap" /^>
echo   ^<Components Description="enjiCAD Components"^>
echo     ^<RuntimeRequirements OS="Win64" Platform="enjiCAD*" SeriesMin="R19.0" /^>
echo     ^<ComponentEntry AppName="AVMap" ModuleName="./Contents/AVMap_enjiCAD.dll" AppType=".NET" LoadOnAutoCADStartup="True" /^>
echo   ^<Components^>
echo ^</ApplicationPackage^>
) > "%BUNDLE_DIR%\PackageContents.xml"

copy /y "%BUNDLE_DIR%\PackageContents.xml" "%APPDATA%\Gstarsoft\ApplicationPlugins\AVMap_enji.bundle\PackageContents.xml" >nul 2>&1

echo [*] Dang dang ky khoa khoi dong Registry cho enjiCAD 2026...
reg add "HKCU\Software\enjiCAD\enjiCAD 2026\Applications\AVMap" /v DESCRIPTION /t REG_SZ /d "AVMap - Ban Do Nen & GIS enjiCAD" /f >nul 2>&1
reg add "HKCU\Software\enjiCAD\enjiCAD 2026\Applications\AVMap" /v LOADCTRLS /t REG_DWORD /d 2 /f >nul 2>&1
reg add "HKCU\Software\enjiCAD\enjiCAD 2026\Applications\AVMap" /v LOADER /t REG_SZ /d "%CONTENTS_DIR%\AVMap_enjiCAD.dll" /f >nul 2>&1
reg add "HKCU\Software\enjiCAD\enjiCAD 2026\Applications\AVMap" /v MANAGED /t REG_DWORD /d 1 /f >nul 2>&1

reg add "HKCU\Software\enjiCAD\enjiCAD2026\Applications\AVMap" /v DESCRIPTION /t REG_SZ /d "AVMap - Ban Do Nen & GIS enjiCAD" /f >nul 2>&1
reg add "HKCU\Software\enjiCAD\enjiCAD2026\Applications\AVMap" /v LOADCTRLS /t REG_DWORD /d 2 /f >nul 2>&1
reg add "HKCU\Software\enjiCAD\enjiCAD2026\Applications\AVMap" /v LOADER /t REG_SZ /d "%CONTENTS_DIR%\AVMap_enjiCAD.dll" /f >nul 2>&1
reg add "HKCU\Software\enjiCAD\enjiCAD2026\Applications\AVMap" /v MANAGED /t REG_DWORD /d 1 /f >nul 2>&1

echo.
echo ========================================================
echo  [THANH CONG] DA BAT TU DONG KHOI DONG CUNG ENJICAD 2026!
echo ========================================================
echo.
echo  Tu lan mo enjiCAD tiep theo, AVMap se tu dong nạp 100%%
echo  ma ban KHONG CAN go lenh NETLOAD nua!
echo.
echo  Ban co the dong enjiCAD va mo lai de kiem tra.
echo.
pause
