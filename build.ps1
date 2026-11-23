# HotspotYue 一键编译脚本
#
# 不依赖 .NET SDK，也不依赖 Visual Studio：直接使用 Windows 自带的
# .NET Framework 4.x 编译器（csc.exe）加 WinRT 元数据编译。
# 产物是一个几十 KB、零依赖的绿色 exe。
#
# 用法：powershell -ExecutionPolicy Bypass -File .\build.ps1

param(
    [string]$Output = "",
    [string]$Define = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$srcDir = Join-Path $root "src"
$binDir = Join-Path $root "bin"
if ([string]::IsNullOrEmpty($Output)) {
    $Output = Join-Path $binDir "HotspotYue.exe"
}

$frameworkDir = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $frameworkDir "csc.exe"
if (-not (Test-Path $csc)) {
    throw "找不到 csc.exe，本脚本需要 .NET Framework 4.x：$csc"
}

$facade = Get-ChildItem "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Runtime" `
    -Recurse -Filter "System.Runtime.dll" -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $facade) {
    throw "找不到 System.Runtime 门面程序集（facade）"
}

$winmdDir = Join-Path $env:WINDIR "System32\WinMetadata"
if (-not (Test-Path $winmdDir)) {
    throw "找不到 WinRT 元数据目录：$winmdDir"
}

New-Item -ItemType Directory -Force -Path $binDir | Out-Null

# 生成程序图标（4 个尺寸打包进 ico 过于复杂，这里用单张 64x64）
$iconPath = Join-Path $binDir "HotspotYue.ico"
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 64, 64
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(40, 168, 96))
$graphics.FillEllipse($brush, 4, 4, 56, 56)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 7
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawArc($pen, 19, 19, 26, 26, -45, 270)
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$stream = [System.IO.File]::Create($iconPath)
$icon.Save($stream)
$stream.Close()
$graphics.Dispose()
$bitmap.Dispose()

$sources = Get-ChildItem -Path $srcDir -Filter "*.cs" | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) {
    throw "在 $srcDir 下没有找到任何 .cs 源文件"
}

$arguments = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    ("/win32manifest:" + (Join-Path $srcDir "app.manifest")),
    ("/win32icon:" + $iconPath),
    ("/out:" + $Output),
    ("/lib:" + $frameworkDir),
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    ("/r:" + $facade.FullName),
    ("/r:" + (Join-Path $winmdDir "Windows.Foundation.winmd")),
    ("/r:" + (Join-Path $winmdDir "Windows.Networking.winmd"))
)

foreach ($source in $sources) {
    $arguments += $source
}

if (-not [string]::IsNullOrEmpty($Define)) {
    $arguments = @("/define:" + $Define) + $arguments
}

Write-Host "编译 " $sources.Count " 个源文件..."
& $csc @arguments

if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码 $LASTEXITCODE"
}
if (-not (Test-Path $Output)) {
    throw "编译命令执行完毕，但没有生成 $Output"
}

$sizeKb = [math]::Round((Get-Item $Output).Length / 1KB, 1)
Write-Host ""
Write-Host "编译成功：$Output ($sizeKb KB)" -ForegroundColor Green