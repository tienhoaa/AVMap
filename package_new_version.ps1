# Dong goi ban moi vao thu muc rieng biet (TUYET DOI KHONG DE LEN BAN CU)

$baseDir = "C:\Users\GAMING-PC\Documents\AVMap"
$targetRoot = Join-Path $baseDir "BAN_GIAO_AVMAP_BAN_MOI_AI"

$acadStage = Join-Path $targetRoot "AutoCAD"
$enjiStage = Join-Path $targetRoot "enjiCAD"

if (!(Test-Path $targetRoot)) { New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null }
if (!(Test-Path $acadStage)) { New-Item -ItemType Directory -Path $acadStage -Force | Out-Null }
if (!(Test-Path $enjiStage)) { New-Item -ItemType Directory -Path $enjiStage -Force | Out-Null }

# 1. Dong goi AutoCAD
$acadSrc = Join-Path $baseDir "bin\Release\net48"
try {
    Copy-Item (Join-Path $acadSrc "AVMap_AutoCAD.dll") $acadStage -Force -ErrorAction SilentlyContinue
} catch {
    Write-Warning "File AVMap_AutoCAD.dll dang bi AutoCAD khoa. Se tao ban AVMap_AutoCAD_AI.dll de NETLOAD truc tiep!"
}

# Luon luon tao them ban AVMap_AutoCAD_AI.dll de nguoi dung NETLOAD ngay ma khong can tat AutoCAD!
Copy-Item (Join-Path $acadSrc "AVMap_AutoCAD.dll") (Join-Path $acadStage "AVMap_AutoCAD_AI.dll") -Force -ErrorAction SilentlyContinue

# Sao chep tat ca dll phu tro
Get-ChildItem -Path $acadSrc -Filter "*.dll" | ForEach-Object {
    try {
        Copy-Item $_.FullName $acadStage -Force -ErrorAction SilentlyContinue
    } catch {}
}

if (Test-Path (Join-Path $acadSrc "runtimes")) {
    Copy-Item (Join-Path $acadSrc "runtimes") $acadStage -Recurse -Force -ErrorAction SilentlyContinue
}

# Sao chep AVMapUpdater.exe va version.json vao goi AutoCAD
$updaterExe = Join-Path $baseDir "Updater\bin\Release\net48\AVMapUpdater.exe"
if (Test-Path $updaterExe) {
    Copy-Item $updaterExe $acadStage -Force -ErrorAction SilentlyContinue
}
$versionFile = Join-Path $baseDir "version.json"
if (Test-Path $versionFile) {
    Copy-Item $versionFile $acadStage -Force -ErrorAction SilentlyContinue
}

$hdsdAcad = @"
=================================================================================
HUONG DAN SU DUNG BAN MOI TICH HOP AI STREET VIEW (AVMAP - AUTOCAD)
=================================================================================

* LUU Y QUAN TRONG NEU AUTOCAD DANG MO:
  Do AutoCAD khoa file DLL trong bo nho, neu anh dang mo AutoCAD:
  1. Go lenh: NETLOAD
  2. Chon file: AVMap_AutoCAD_AI.dll (File nay duoc tao rieng de nap ngay ma khong can tat CAD!)
  3. Hoac neu muon dung file goc AVMap_AutoCAD.dll, vui long tat han AutoCAD roi chay lai BUILD_ALL.bat.

* TINH NANG MOI:
  - HE THONG TU DONG CAP NHAT 1-CLICK (FAST AUTO-UPDATER):
    + Nguoi dung chi can giai nen bo cai lan dau tien!
    + Cac lan cap nhat sau: Ngay sau khi NETLOAD hoac tren thanh Ribbon se co thong bao [🔔 CO BAN MOI].
    + Khi bam cap nhat: Form cap nhat se tu dong dong CAD, chi tai DUY NHAT cac file can thiet (~600 KB sieu nhanh), ghi de truc tiep vao thu muc netload va mo lai AutoCAD ngay lap tuc!
  - THE HIEN RO TINH THANH / HE TOA DO DANG AP DUNG CHO BAN VE:
    + Hop thoai cai dat he toa do (lenh AVCRS) bo sung the thong tin mau xanh noi bat ngay tren dau.
    + Thong bao ro rang ten tinh thanh, kinh tuyen truc va trang thai [✔ DANG SU DUNG].
    + Danh sach he toa do tu dong cuon va boi xanh dung tinh thanh hien tai voi cot trang thai [✔ Dang dung].
  - TACH BIET HOAN TOAN VET DUONG XANH GOOGLE STREET VIEW:
    + Khi bat ban do nen ve tinh thong thuong (lenh AVMAPON), nen anh ve tinh sach bong 100%, tuyet doi khong bi lan vet duong xanh Street View.
    + Mang luoi duong mau xanh Street View chi xuat hien duy nhat khi chay lenh AVSTREETVIEW de nguoi dung nhan biet va click chon diem.
    + Khi tat cua so Street View hoac ESC, ban ve tu dong hoan tra nen ve tinh sach ban dau.
  - HO TRO XUAT BLOCK SANG GOOGLE EARTH (KMZ / KML):
    + Ho tro 100% cac loai Block: Block tinh, Dynamic Block, Block thuoc tinh Attributes (moc trac dia, coc GPMB, ho ga, cay xanh...).
    + Tu dong ghim vi tri (Placemark Pin) tai toa do chen cua Block voi ten hien thi theo thuoc tinh (So hieu, Ten diem...).
    + Tu dong trich xuat toan bo thuoc tinh Attributes thanh bang HTML chi tiet trong Balloon thong tin tren Google Earth.
    + Tu dong ra va ve toan bo hinh hoc net ve (Linework, Polylines, Circles, Hatches) ben trong Block xuong mat dat ve tinh.
  - TU DONG NHUNG TOA DO GPS & EXIF VAO ANH CHUP STREET VIEW:
    + Moi buc anh chup tu Street View tu dong duoc nhung sieu du lieu EXIF GPS chuan quoc te
      (Vi do, Kinh do WGS-84, goc nhin la ban Compass Heading, ngay gio chup, toa do VN-2000).
    + Tuong thich 100% voi tinh nang 'Chen Anh Khao Sat Hien Truong' (lenh AVPHOTO / AVSURVEYPHOTO):
      Phan mem tu dong nhan dien toa do GPS, chuyen ve toa do VN-2000 va dat anh dung vi tri tren tuyen duong CAD!
    + Tu dong ve mui ten chi huong nhin cua camera theo goc la ban thuc te khi chen vao CAD!
    + Tuong thich keo tha vao Google Earth, QGIS, ArcGIS hoac xem trong Windows Properties.
  - NUT [ 📸 Chup Anh Hien Trang ]: 1 nut duy nhat tinh gon, tu dong an sach se hop tim kiem,
    the ngay thang, la ban, pegman, map details, tooltip; cat bo dai ban quyen Google;
    xoa sach vet chu chim © Google tren mat duong; khong chen bat ky text/toa do nao len anh.
    Anh xuat ra sach bong tu nhien nhu chup bang dien thoai thuc dia!
  - CUA SO [ ⚙ Cai Dat ]: Da keo rong thoang dang, mac dinh KHONG dong dau trac dia,
    mac dinh BAT xoa chu chim tren mat duong.
  - DOI THOI DIEM XEM LICH SU (TIME TRAVEL): Click truc tiep vao dong 'Xem them ngay'
    (hoac 'See more dates') duoi ten dia diem o goc tren-trai de chon nam chup cu hon!
=================================================================================
"@
Set-Content -Path (Join-Path $acadStage "HUONG_DAN_SU_DUNG.txt") -Value $hdsdAcad -Encoding UTF8

$zipAcad = Join-Path $targetRoot "AVMap_AutoCAD_BanMoi.zip"
if (Test-Path $zipAcad) { Remove-Item $zipAcad -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$acadStage\*" -DestinationPath $zipAcad -Force
Write-Output "AutoCAD Ban Moi: Da dong goi thanh cong vao $zipAcad va thu muc $acadStage"

# 2. Dong goi enjiCAD
$enjiSrc = Join-Path $baseDir "enjiCAD\bin\Release\net8.0-windows"
try {
    Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.dll") $enjiStage -Force -ErrorAction SilentlyContinue
} catch {}

Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.dll") (Join-Path $enjiStage "AVMap_enjiCAD_AI.dll") -Force -ErrorAction SilentlyContinue

Get-ChildItem -Path $enjiSrc -Filter "*.dll" | ForEach-Object {
    try {
        Copy-Item $_.FullName $enjiStage -Force -ErrorAction SilentlyContinue
    } catch {}
}
if (Test-Path (Join-Path $enjiSrc "AVMap_enjiCAD.deps.json")) {
    Copy-Item (Join-Path $enjiSrc "AVMap_enjiCAD.deps.json") $enjiStage -Force -ErrorAction SilentlyContinue
}
if (Test-Path (Join-Path $enjiSrc "runtimes")) {
    Copy-Item (Join-Path $enjiSrc "runtimes") $enjiStage -Recurse -Force -ErrorAction SilentlyContinue
}

# Sao chep AVMapUpdater.exe va version.json vao goi enjiCAD
if (Test-Path $updaterExe) {
    Copy-Item $updaterExe $enjiStage -Force -ErrorAction SilentlyContinue
}
if (Test-Path $versionFile) {
    Copy-Item $versionFile $enjiStage -Force -ErrorAction SilentlyContinue
}

$hdsdEnji = @"
=================================================================================
HUONG DAN SU DUNG BAN MOI TICH HOP AI STREET VIEW (AVMAP - ENJICAD)
=================================================================================
* Go lenh: NETLOAD
* Chon file: AVMap_enjiCAD.dll (hoac AVMap_enjiCAD_AI.dll)
=================================================================================
"@
Set-Content -Path (Join-Path $enjiStage "HUONG_DAN_SU_DUNG.txt") -Value $hdsdEnji -Encoding UTF8

$zipEnji = Join-Path $targetRoot "AVMap_enjiCAD_BanMoi.zip"
if (Test-Path $zipEnji) { Remove-Item $zipEnji -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path "$enjiStage\*" -DestinationPath $zipEnji -Force
Write-Output "enjiCAD Ban Moi: Da dong goi thanh cong vao $zipEnji va thu muc $enjiStage"

Write-Output "Tat ca ban moi duoc luu rieng biet tai: $targetRoot"
