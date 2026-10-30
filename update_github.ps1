# ========================================================
#   TIEN TRINH CAP NHAT MA NGUON AVMAP LEN GITHUB
# ========================================================

$baseDir = "C:\Users\GAMING-PC\Documents\AVMap"
Set-Location $baseDir

# Bo sung duong dan Git va GitHub CLI vao PATH
$env:PATH = "C:\Program Files\Git\cmd;C:\Program Files\Git\bin;C:\Program Files\GitHub CLI;" + $env:PATH

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "       TIEN TRINH CAP NHAT MA NGUON AVMAP LEN GITHUB    " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Kiem tra Git
$gitCmd = Get-Command git -ErrorAction SilentlyContinue
if (-not $gitCmd) {
    Write-Host "[LOI] Khong tim thay phan mem Git tren may tinh!" -ForegroundColor Red
    Write-Host "Vui long cai dat Git for Windows tai: https://git-scm.com/" -ForegroundColor Yellow
    Read-Host "Bam Enter de thoat"
    exit 1
}

# 2. Khoi tao Git repo neu chua co
$gitFolder = Join-Path $baseDir ".git"
if (-not (Test-Path $gitFolder)) {
    Write-Host "[1/4] Khoi tao kho Git cuc bo (branch: main)..." -ForegroundColor Yellow
    & git init -b main
    & git config user.name "tienhoaa"
    & git config user.email "tienhoaa@users.noreply.github.com"
} else {
    Write-Host "[1/4] Da nhan dien kho Git cuc bo." -ForegroundColor Green
}

# 3. Kiem tra Remote Origin tren GitHub
$remoteUrl = (& git remote get-url origin 2>$null)
if (-not $remoteUrl) {
    Write-Host ""
    Write-Host "Dang kiem tra ket noi voi tai khoan GitHub (tienhoaa)..." -ForegroundColor Cyan
    
    $ghCmd = Get-Command gh -ErrorAction SilentlyContinue
    $repoExists = $false
    
    if ($ghCmd) {
        $checkView = (& gh repo view tienhoaa/AVMap 2>&1)
        if ($LASTEXITCODE -eq 0) {
            $repoExists = $true
        }
    }
    
    if ($repoExists) {
        Write-Host "-> Da tim thay Repository 'tienhoaa/AVMap' tren GitHub. Dang ket noi..." -ForegroundColor Green
        & git remote add origin "https://github.com/tienhoaa/AVMap.git"
    } else {
        Write-Host "-> Chua co repository tren GitHub. Dang chuan bi tao moi..." -ForegroundColor Yellow
        Write-Host "   Chon che do cho Repository:"
        Write-Host "     [1] Private (Rieng tu - chi minh anh xem) [Mac dinh]"
        Write-Host "     [2] Public  (Cong khai - moi nguoi deu xem duoc)"
        Write-Host "     [3] Nhap lien ket GitHub co san"
        $choice = Read-Host "Nhap lua chon (1, 2 hoac 3 - Enter de chon 1)"
        
        if ($choice -eq "2") {
            Write-Host "Dang tao Repository Public 'AVMap' tren GitHub..." -ForegroundColor Cyan
            & gh repo create AVMap --public --source=. --remote=origin --description "AVMap - Plugin Ban Do Nen Google Maps, Street View 360 va GIS cho AutoCAD/enjiCAD"
        } elseif ($choice -eq "3") {
            $inputUrl = Read-Host "Nhap duong dan GitHub Repo (VD: https://github.com/tienhoaa/AVMap.git)"
            if (-not [string]::IsNullOrWhiteSpace($inputUrl)) {
                & git remote add origin $inputUrl.Trim()
            }
        } else {
            Write-Host "Dang tao Repository Private 'AVMap' tren GitHub..." -ForegroundColor Cyan
            & gh repo create AVMap --private --source=. --remote=origin --description "AVMap - Plugin Ban Do Nen Google Maps, Street View 360 va GIS cho AutoCAD/enjiCAD"
        }
        
        # Neu gh tao xong hoac repo da co san, dam bao da gan origin
        $checkRemoteAgain = (& git remote get-url origin 2>$null)
        if (-not $checkRemoteAgain) {
            & git remote add origin "https://github.com/tienhoaa/AVMap.git"
        }
    }
}

# Dam bao kho tienhoaa/AVMap o che do Public de moi may nguoi dung deu co the tai file cap nhat (tranh loi 404)
$ghCmd = Get-Command gh -ErrorAction SilentlyContinue
if ($ghCmd) {
    Write-Host "Dang chuyen kho 'tienhoaa/AVMap' sang che do PUBLIC (de kich hoat link tai truc tiep cho CAD)..." -ForegroundColor Cyan
    & gh repo edit tienhoaa/AVMap --visibility public --accept-visibility-change-consequences 2>$null
}

# 4. Kiem tra va Add file
Write-Host ""
Write-Host "[2/4] Dang quet va them cac file ma nguon moi..." -ForegroundColor Cyan
& git add .
& git add -f BAN_GIAO_AVMAP_BAN_MOI_AI
& git add -f version.json

# 5. Commit
$defaultMsg = "Cap nhat AVMap: Hien thi tinh thanh CRS, Xu ly vet xanh Street View, Xuat Block KMZ, GPS EXIF cho anh hien truong"
Write-Host ""
Write-Host "[3/4] Tao commit ma nguon..." -ForegroundColor Cyan
Write-Host "Ghi chu commit mac dinh: '$defaultMsg'" -ForegroundColor Gray
$userMsg = Read-Host "Nhap ghi chu commit cua anh (Hoac bam ENTER de lay mac dinh)"

$commitMsg = $defaultMsg
if (-not [string]::IsNullOrWhiteSpace($userMsg)) {
    $commitMsg = $userMsg
}

& git commit -m "$commitMsg"

# 6. Push len GitHub
Write-Host ""
Write-Host "[4/4] Dang day (push) toan bo ma nguon len GitHub..." -ForegroundColor Cyan
& git branch -M main
& git push -u origin main

if ($LASTEXITCODE -eq 0) {
    $finalUrl = (& git remote get-url origin 2>$null)
    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host "  [THANH CONG] DA DAY TOAN BO MA NGUON LEN GITHUB!" -ForegroundColor Green
    Write-Host "  Link repo: $finalUrl" -ForegroundColor Yellow
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host ""

    # Kiem tra file DLL va file ZIP ban giao de tao GitHub Release
    $acadDll = Join-Path $baseDir "BAN_GIAO_AVMAP_BAN_MOI_AI\AutoCAD\AVMap_AutoCAD.dll"
    $enjiDll = Join-Path $baseDir "BAN_GIAO_AVMAP_BAN_MOI_AI\enjiCAD\AVMap_enjiCAD.dll"
    $zipAcad = Join-Path $baseDir "BAN_GIAO_AVMAP_BAN_MOI_AI\AVMap_AutoCAD_BanMoi.zip"
    $zipEnji = Join-Path $baseDir "BAN_GIAO_AVMAP_BAN_MOI_AI\AVMap_enjiCAD_BanMoi.zip"
    
    if ((Test-Path $acadDll) -and (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Host "Phat hien cac file ban giao (DLL cap nhat & ZIP) san sang." -ForegroundColor Cyan
        $relChoice = Read-Host "Ban co muon phat hanh Release v1.2.0 len GitHub de kich hoat tu dong cap nhat khong? (Y/N) [Mac dinh: Y]"
        if ([string]::IsNullOrWhiteSpace($relChoice) -or $relChoice -eq "Y" -or $relChoice -eq "y") {
            Write-Host "Dang phat hanh Release v1.2.0 len GitHub (Dinh kem DLL va ZIP)..." -ForegroundColor Cyan
            
            $releaseAssets = @()
            if (Test-Path $acadDll) { $releaseAssets += "$acadDll" }
            if (Test-Path $enjiDll) { $releaseAssets += "$enjiDll" }
            if (Test-Path $zipAcad) { $releaseAssets += "$zipAcad" }
            if (Test-Path $zipEnji) { $releaseAssets += "$zipEnji" }
            
            & gh release create v1.2.0 @releaseAssets --title "AVMap v1.2.0 - Ban Cap Nhat Moi" --notes "Cap nhat moi: Hien thi tinh thanh CRS, Fix vet xanh Street View, Xuat Block KMZ, GPS EXIF cho anh hien truong, Tu dong cap nhat Auto-Updater (chi tai DLL can thiet)" --clobber
            if ($LASTEXITCODE -eq 0) {
                Write-Host "-> Da dang tai Release thanh cong! Link tai DLL truc tiep da duoc kich hoat." -ForegroundColor Green
            }
        }
    }
} else {
    Write-Host ""
    Write-Host "[LUU Y] Phat hien nhanh tren GitHub co du lieu tu truoc, dang thuc hien pull va rebase..." -ForegroundColor Yellow
    & git pull origin main --rebase
    & git push -u origin main
    
    if ($LASTEXITCODE -eq 0) {
        $finalUrl = (& git remote get-url origin 2>$null)
        Write-Host ""
        Write-Host "========================================================" -ForegroundColor Green
        Write-Host "  [THANH CONG] DA DONG BO VA DAY MA NGUON LEN GITHUB!" -ForegroundColor Green
        Write-Host "  Link repo: $finalUrl" -ForegroundColor Yellow
        Write-Host "========================================================" -ForegroundColor Green
        Write-Host ""
    } else {
        Write-Host ""
        Write-Host "[THONG BAO] Khong the day tu dong. Vui long kiem tra quyen truy cap tai khoan GitHub." -ForegroundColor Red
    }
}
