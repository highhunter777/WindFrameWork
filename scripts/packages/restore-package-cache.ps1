<#
.SYNOPSIS
    把 export-package-cache.ps1 导出的缓存还原回 Library/PackageCache，用于离线解析。

.DESCRIPTION
    还原前会校验包目录名中的 commit 标记与 packages-lock.json 中的 hash 是否一致：
    不一致说明缓存与锁文件不同源，此时还原会让工程进入「包内容与锁文件不符」的状态，
    因此直接中止并要求重新导出。

    还原完成后仍需让 UPM 认可这些包：正常情况下 UPM 会直接复用 Library/PackageCache 中
    已存在的目录；若仍尝试访问远端，请改用 manifest 中的 file: 引用（见工程 README）。

.PARAMETER ProjectPath
    Unity 工程根目录。默认为本脚本上级两级目录。

.PARAMETER CachePath
    缓存目录。默认为 <工程根>/.cache/packages。

.EXAMPLE
    pwsh scripts/packages/restore-package-cache.ps1
#>
[CmdletBinding()]
param(
    [string] $ProjectPath,
    [string] $CachePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ProjectPath) { $ProjectPath = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path }
if (-not $CachePath) { $CachePath = Join-Path $ProjectPath '.cache\packages' }

$manifestPath = Join-Path $CachePath 'cache-manifest.json'
if (-not (Test-Path $manifestPath)) { throw "未找到缓存清单：$manifestPath" }

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$packageCache = Join-Path $ProjectPath 'Library\PackageCache'

foreach ($entry in $manifest.packages) {
    $source = Join-Path $CachePath $entry.name
    if (-not (Test-Path $source)) { throw "缓存中缺少包：$($entry.name)" }

    $expectedPrefix = $entry.name + '@' + $entry.hash.Substring(0, 10)
    if ($entry.marker -ne $expectedPrefix) {
        throw ("缓存标记与锁文件不一致：{0} 的标记为 {1}，锁文件 hash 前 10 位为 {2}。" -f `
            $entry.name, $entry.marker, $expectedPrefix.Substring($entry.name.Length + 1))
    }

    $target = Join-Path $packageCache $entry.marker
    if (Test-Path $target) {
        Write-Host ("已存在，跳过 {0}" -f $entry.marker)
        continue
    }

    Copy-Item $source -Destination $target -Recurse -Force
    Write-Host ("已还原 {0}" -f $entry.marker)
}

Write-Host ''
Write-Host '还原完成。若 UPM 仍尝试访问远端，请在 manifest 中改用 file: 引用。'