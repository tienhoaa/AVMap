@echo off
setlocal
title AVMap - Huy Tu Dong Khoi Dong Cung CAD

echo ========================================================
echo        HUY TU DONG KHOI DONG AVMAP CUNG CAD
echo ========================================================
echo.

set "BUNDLE_DIR=%APPDATA%\Autodesk\ApplicationPlugins\AVMap.bundle"
set "ENJI_BUNDLE_1=%APPDATA%\enjiCAD\ApplicationPlugins\AVMap_enji.bundle"
set "ENJI_BUNDLE_2=%APPDATA%\Gstarsoft\ApplicationPlugins\AVMap_enji.bundle"

echo [*] Dang go bo cau hinh khoi dong AutoCAD...
if exist "%BUNDLE_DIR%\PackageContents.xml" del /f /q "%BUNDLE_DIR%\PackageContents.xml" >nul 2>&1
if exist "%BUNDLE_DIR%" rmdir /s /q "%BUNDLE_DIR%" >nul 2>&1

echo [*] Dang go bo cau hinh khoi dong enjiCAD / GstarCAD...
if exist "%ENJI_BUNDLE_1%\PackageContents.xml" del /f /q "%ENJI_BUNDLE_1%\PackageContents.xml" >nul 2>&1
if exist "%ENJI_BUNDLE_1%" rmdir /s /q "%ENJI_BUNDLE_1%" >nul 2>&1
if exist "%ENJI_BUNDLE_2%\PackageContents.xml" del /f /q "%ENJI_BUNDLE_2%\PackageContents.xml" >nul 2>&1
if exist "%ENJI_BUNDLE_2%" rmdir /s /q "%ENJI_BUNDLE_2%" >nul 2>&1

echo [*] Dang xoa dang ky Registry enjiCAD...
reg delete "HKCU\Software\enjiCAD\enjiCAD 2026\Applications\AVMap" /f >nul 2>&1
reg delete "HKCU\Software\enjiCAD\enjiCAD2026\Applications\AVMap" /f >nul 2>&1

echo.
echo ========================================================
echo  [THANH CONG] DA HUY KHOI DONG CUNG CAD!
echo ========================================================
echo.
echo  AutoCAD va enjiCAD da tro ve trang thai nguyen ban sach se 100%%.
echo  Lần sau khi mở CAD se KHONG tu dong nap AVMap nua.
echo.
echo  Khi nao can su dung, ban chi viec go lenh NETLOAD
echo  nhu binh thuong!
echo.
pause
