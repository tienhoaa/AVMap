@echo off
chcp 65001 >nul
echo ========================================================
echo        TIEN TRINH BIEN DICH VA DONG GOI AVMAP
echo ========================================================
echo.
cd /d "C:\Users\GAMING-PC\Documents\AVMap"

echo [1/3] Dang bien dich AVMap cho AutoCAD (Release)...
dotnet build AVMap.csproj -c Release > build_log_acad.txt 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [LOI] Bien dich AutoCAD that bai! Xem chi tiet trong build_log_acad.txt
    type build_log_acad.txt
    pause
    exit /b %ERRORLEVEL%
)
echo [THANH CONG] AutoCAD Build OK!
echo.

echo [2/3] Dang bien dich AVMap cho enjiCAD 2026 (Release)...
dotnet build enjiCAD\AVMap_enjiCAD.csproj -c Release > build_log_enji.txt 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [LOI] Bien dich enjiCAD that bai! Xem chi tiet trong build_log_enji.txt
    type build_log_enji.txt
    pause
    exit /b %ERRORLEVEL%
)
echo [THANH CONG] enjiCAD Build OK!
echo.

echo [3/3] Dang dong goi Ban Moi vao thu muc rieng biet (TUYET DOI KHONG DE LEN BAN CU)...
powershell -NoProfile -ExecutionPolicy Bypass -File package_new_version.ps1 > build_log_package.txt 2>&1
type build_log_package.txt

echo %DATE% %TIME% - BUILD THANH CONG! > build_result.txt
echo.
echo ========================================================
echo  HOAN TAT! BAN MOI DUOC LUU TACH BIET HOAN TOAN TAI:
echo  Thu muc: C:\Users\GAMING-PC\Documents\AVMap\BAN_GIAO_AVMAP_BAN_MOI_AI
echo  + AutoCAD: BAN_GIAO_AVMAP_BAN_MOI_AI\AutoCAD\AVMap_AutoCAD.dll
echo  + enjiCAD: BAN_GIAO_AVMAP_BAN_MOI_AI\enjiCAD\AVMap_enjiCAD.dll
echo  + 2 file ZIP rieng: AVMap_AutoCAD_BanMoi.zip va AVMap_enjiCAD_BanMoi.zip
echo.
echo  * CAC BAN CU VAN DUOC GIU NGUYEN 100%, KHONG BI ANH HUONG!
echo ========================================================
echo.
set /p PUSH_GIT="Ban co muon dong bo cap nhat ma nguon len GitHub luon khong? (Y/N) [Mac dinh: N]: "
if /i "%PUSH_GIT%"=="Y" (
    call UPDATE_GITHUB.bat
) else (
    echo [Bo qua] Khong cap nhat GitHub.
    pause
)
