<#
.SYNOPSIS
  以「出树构建」方式编译插件自带前端（沙箱兜底路径，见 plugin-development §3.2）。
.DESCRIPTION
  插件目录内的 web/node_modules 在本环境是残缺副本，且 pnpm install 重建会被沙箱 safe-delete 拦截，
  因此把插件 web/src + vite.config.ts 暂存到宿主树内（ForgeSelf.Web/.plugin-build-<id>/），
  用宿主已装好的 vite / @vitejs/plugin-vue / @tailwindcss/vite 驱动构建。
  两条硬约束（都实证过会静默出错）：
    - publicDir 必须 false，否则宿主 public/ 的 favicon/logo/shared 会被误拷进插件产物；
    - --outDir 必须给绝对路径，因为 outDir 相对 cwd 而非 config 位置。
  产物只应是 index.js + style.css。临时目录用完即删。
.EXAMPLE
  pwsh scripts/build-plugin-web.ps1 -Plugin FileTools
#>
param(
  [Parameter(Mandatory)][string]$Plugin,
  [string]$PluginDirName   # 缺省与 -Plugin 相同（目录名 PascalCase）
)

$ErrorActionPreference = 'Stop'
if (-not $PluginDirName) { $PluginDirName = $Plugin }

$repo   = Split-Path -Parent $PSScriptRoot
$web    = Join-Path $repo 'ForgeSelf.Web'
$src    = Join-Path $repo "Plugins/$PluginDirName/web"
$tmp    = Join-Path $web ".plugin-build-$($Plugin.ToLower())"
$outDir = Join-Path $src 'dist'

if (-not (Test-Path (Join-Path $src 'vite.config.ts'))) { throw "插件前端不存在: $src" }

if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp }
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
Copy-Item -Recurse (Join-Path $src 'src') (Join-Path $tmp 'src')
Copy-Item (Join-Path $src 'vite.config.ts') (Join-Path $tmp 'vite.config.ts')

$wrapper = @'
import { defineConfig } from 'vite'
import baseConfig from './vite.config'
export default defineConfig({ ...baseConfig, publicDir: false })
'@
Set-Content -Path (Join-Path $tmp 'vite.wrapper.config.ts') -Value $wrapper -Encoding utf8

Push-Location $web
try {
  Write-Host "[build-plugin-web] $Plugin -> $outDir" -ForegroundColor Cyan
  node node_modules/vite/bin/vite.js build `
    --config (Join-Path ".plugin-build-$($Plugin.ToLower())" 'vite.wrapper.config.ts') `
    --outDir $outDir --emptyOutDir
  $code = $LASTEXITCODE
}
finally {
  Pop-Location
  if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp }
}

if ($code -eq 0) {
  Write-Host "[build-plugin-web] 产物：" -ForegroundColor Green
  Get-ChildItem $outDir | ForEach-Object { Write-Host ("  {0}  {1} bytes" -f $_.Name, $_.Length) }
} else {
  Write-Host "[build-plugin-web] 构建失败 exit=$code" -ForegroundColor Red
}
exit $code
