<#
.SYNOPSIS
    把工程中由 Git URL 引入的包从 Library/PackageCache 导出为可复用缓存。

.DESCRIPTION
    UPM 每次冷解析 Git 依赖都要访问远端。本机对 github.com 的 443 直连并不稳定，
    解析失败会让整个工程停在「包解析失败」状态，而此时 Library/PackageCache 中的内容
    才是唯一可用副本。

    本脚本把已解析的包连同其来源信息一并导出到工程根下的 .cache/packages（不入库），
    配合 restore-package-cache.ps1 可在离线机器上还原，配合 packages-lock.json 中锁定的
    hash 实现可复现构建。

.PARAMETER ProjectPath
    Unity 工程根目录。默认为本脚本上级两级目录。

.PARAMETER OutputPath
    缓存输出目录。默认为 <工程根>/.cache/packages。

.PARAMETER PackageName
    仅导出指定包。省略时导出 packages-lock.json 中全部 git 来源的包。

.EXAMPLE
    pwsh scripts/packages/export-package-cache.ps1
#>
[CmdletBinding()]
param(
    [string] $ProjectPath,
    [string] $OutputPath,
    [string] $PackageName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ProjectPath) { $ProjectPath = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path }
if (-not $OutputPath) { $OutputPath = Join-Path $ProjectPath '.cache\packages' }

$lockPath = Join-Path $ProjectPath 'Packages\packages-lock.json'
$packageCache = Join-Path $ProjectPath 'Library\PackageCache'

if (-not (Test-Path $lockPath)) { throw "未找到 packages-lock.json：$lockPath" }
if (-not (Test-Path $packageCache)) { throw "未找到包缓存目录：$packageCache（请先在 Unity 中完成一次依赖解析）" }

$lock = Get-Content $lockPath -Raw | ConvertFrom-Json
$gitDependencies = $lock.dependencies.PSObject.Properties |
    Where-Object { $_.Value.source -eq 'git' } |
    ForEach-Object { [PSCustomObject]@{ Name = $_.Name; Hash = $_.Value.hash; Url = $_.Value.version } }

if ($PackageName) {
    $gitDependencies = $gitDependencies | Where-Object { $_.Name -eq $PackageName }
    if (-not $gitDependencies) { throw "packages-lock.json 中不存在名为 $PackageName 的 git 依赖。" }
}

if (-not $gitDependencies) {
    Write-Host 'packages-lock.json 中没有 git 来源的依赖，无需导出。'
    return
}

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

$manifestPath = Join-Path $OutputPath 'cache-manifest.json'
$manifest = if (Test-Path $manifestPath) { Get-Content $manifestPath -Raw | ConvertFrom-Json } else { $null }
$entries = @()

foreach ($dependency in $gitDependencies) {
    $source = Get-ChildItem $packageCache -Directory |
        Where-Object { $_.Name -eq $dependency.Name -or $_.Name.StartsWith($dependency.Name + '@') } |
        Select-Object -First 1

    if (-not $source) {
        Write-Warning "跳过 $($dependency.Name)：Library/PackageCache 中未找到对应目录。"
        continue
    }

    $target = Join-Path $OutputPath $dependency.Name
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Copy-Item $source.FullName -Destination $target -Recurse -Force

    # 包目录名形如 <name>@<hash 前 10 位>，是校验缓存是否与 lock 一致的唯一凭据。
    $marker = Split-Path $source.Name -Leaf
    $entries += [PSCustomObject]@{
        name    = $dependency.Name
        url     = $dependency.Url
        hash    = $dependency.Hash
        marker  = $marker
    }

    Write-Host ("已导出 {0}  ({1})" -f $dependency.Name, $marker)
}

[PSCustomObject]@{
    generatedAt = (Get-Date).ToUniversalTime().ToString('o')
    packages     = $entries
} | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host ''
Write-Host ("缓存目录：{0}" -f $OutputPath)
Write-Host ("已导出 {0} 个 git 来源的包。" -f $entries.Count)