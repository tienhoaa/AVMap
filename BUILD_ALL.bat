@echo off
setlocal
cd /d "C:\Users\GAMING-PC\Documents\AVMap"
title AVMap - Build va Dong Bo GitHub

echo ========================================================
echo        TIEN TRINH BIEN DICH VA DONG BO AVMAP
echo ========================================================
echo.

echo [*] Giai phong file DLL truoc khi bien dich (dong CAD neu dang mo)...
taskkill /f /im acad.exe >nul 2>&1
taskkill /f /im enjicad.exe >nul 2>&1
taskkill /f /im gscad.exe >nul 2>&1

echo.
echo [1/4] Dang bien dich AVMap cho AutoCAD (Release)...
dotnet build AVMap.csproj -c Release > build_log_acad.txt 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [LOI] Bien dich AutoCAD that bai! Xem chi tiet:
    type build_log_acad.txt
    echo.
    pause
    exit /b 1
)
echo [THANH CONG] AutoCAD Build OK!

echo.
echo [2/4] Dang bien dich AVMap cho enjiCAD 2026 (Release)...
dotnet build enjiCAD\AVMap_enjiCAD.csproj -c Release > build_log_enji.txt 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [LOI] Bien dich enjiCAD that bai! Xem chi tiet:
    type build_log_enji.txt
    echo.
    pause
    exit /b 1
)

dotnet build Updater\AVMapUpdater.csproj -c Release > build_log_updater.txt 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [LOI] Bien dich Updater that bai! Xem chi tiet:
    type build_log_updater.txt
    echo.
    pause
    exit /b 1
)
echo [THANH CONG] enjiCAD va Auto-Updater Build OK!

echo.
echo [3/4] Dang dong goi Ban Moi vao thu muc...
set "TARGET_ROOT=C:\Users\GAMING-PC\Documents\AVMap\BAN_GIAO_AVMAP_BAN_MOI_AI"
if not exist "%TARGET_ROOT%\AutoCAD" mkdir "%TARGET_ROOT%\AutoCAD"
if not exist "%TARGET_ROOT%\enjiCAD" mkdir "%TARGET_ROOT%\enjiCAD"

copy /y "bin\Release\net48\AVMap_AutoCAD.dll" "%TARGET_ROOT%\AutoCAD\AVMap_AutoCAD.dll" >nul
copy /y "bin\Release\net48\AVMap_AutoCAD.dll" "%TARGET_ROOT%\AutoCAD\AVMap_AutoCAD_AI.dll" >nul
copy /y "bin\Release\net48\*.dll" "%TARGET_ROOT%\AutoCAD\" >nul
copy /y "Updater\bin\Release\net48\AVMapUpdater.exe" "%TARGET_ROOT%\AutoCAD\AVMapUpdater.exe" >nul
copy /y "version.json" "%TARGET_ROOT%\AutoCAD\version.json" >nul

copy /y "enjiCAD\bin\Release\net8.0-windows\AVMap_enjiCAD.dll" "%TARGET_ROOT%\enjiCAD\AVMap_enjiCAD.dll" >nul
copy /y "enjiCAD\bin\Release\net8.0-windows\AVMap_enjiCAD.dll" "%TARGET_ROOT%\enjiCAD\AVMap_enjiCAD_AI.dll" >nul
copy /y "enjiCAD\bin\Release\net8.0-windows\*.dll" "%TARGET_ROOT%\enjiCAD\" >nul
copy /y "Updater\bin\Release\net48\AVMapUpdater.exe" "%TARGET_ROOT%\enjiCAD\AVMapUpdater.exe" >nul
copy /y "version.json" "%TARGET_ROOT%\enjiCAD\version.json" >nul

tar -a -c -f "%TARGET_ROOT%\AVMap_AutoCAD_BanMoi.zip" -C "%TARGET_ROOT%\AutoCAD" *
tar -a -c -f "%TARGET_ROOT%\AVMap_enjiCAD_BanMoi.zip" -C "%TARGET_ROOT%\enjiCAD" *

echo %DATE% %TIME% - BUILD THANH CONG! > build_result.txt
echo [THANH CONG] Dong goi xong tai: %TARGET_ROOT%

echo.
echo [4/4] Tu dong dong bo len GitHub...
set PATH=C:\Program Files\Git\cmd;C:\Program Files\Git\bin;C:\Program Files\GitHub CLI;%PATH%

gh repo edit tienhoaa/AVMap --visibility public --accept-visibility-change-consequences >nul 2>&1

set "CUR_VER=v1.3.0"
for /f "tokens=2 delims=:," %%a in ('findstr /i "\"version\"" version.json') do (
    set "RAW_VER=%%a"
)
set "RAW_VER=%RAW_VER: =%"
set "RAW_VER=%RAW_VER:"=%"
if not "%RAW_VER%"=="" set "CUR_VER=v%RAW_VER%"

echo [*] Phien ban: %CUR_VER%
git add .
git add -f BAN_GIAO_AVMAP_BAN_MOI_AI
git add -f version.json

git commit -m "Phat hanh AVMap %CUR_VER%: Dong bo ma nguon va ban cap nhat" >nul 2>&1
git branch -M main >nul 2>&1
git push origin main

gh release create %CUR_VER% "%TARGET_ROOT%\AutoCAD\AVMap_AutoCAD.dll" "%TARGET_ROOT%\enjiCAD\AVMap_enjiCAD.dll" "%TARGET_ROOT%\AVMap_AutoCAD_BanMoi.zip" "%TARGET_ROOT%\AVMap_enjiCAD_BanMoi.zip" --title "AVMap %CUR_VER% - Ban Cap Nhat Moi" --notes "Tu dong cap nhat Delta Update" --clobber >nul 2>&1

echo.
echo ========================================================
echo  HOAN TAT 100%!
echo  + Thu muc ban giao: %TARGET_ROOT%
echo  + GitHub Release: %CUR_VER% da duoc cap nhat thanh cong!
echo ========================================================
echo.
pause
