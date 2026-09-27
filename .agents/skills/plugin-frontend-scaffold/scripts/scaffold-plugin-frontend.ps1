param(
  [Parameter(Mandatory = $true)][string]$Plugin,
  [string]$Template = 'AIAgent',
  [switch]$Force
)
$ErrorActionPreference = 'Stop'

# Resolve repo root: scripts/ -> plugin-frontend-scaffold/ -> skills/ -> .agents/ -> repo
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
$src = Join-Path $repo "ForgeSelf.Api/Plugins/$Template/web"
$dst = Join-Path $repo "ForgeSelf.Api/Plugins/$Plugin/web"

if (Test-Path $dst) {
  if (-not $Force) { Write-Error "Destination already exists: $dst. Use -Force to overwrite."; exit 1 }
  Remove-Item $dst -Recurse -Force
}
if (-not (Test-Path $src)) { Write-Error "Template web not found: $src"; exit 1 }

Write-Host "[scaffold] copy $Template/web -> $Plugin/web (exclude node_modules, dist)"
Copy-Item $src $dst -Recurse -Exclude @('node_modules', 'dist')

Write-Host "[scaffold] refresh shared dependency shims (host-side)"
$shimScript = Join-Path $repo "ForgeSelf.Web/scripts/generate-shared-shims.mjs"
if (Test-Path $shimScript) { node $shimScript } else { Write-Warning "shim script missing: $shimScript" }

Write-Host "[scaffold] install + build plugin frontend"
Push-Location $dst
try {
  pnpm i
  pnpm run build
}
finally { Pop-Location }

Write-Host "[scaffold] run pluginViewLoader unit test (host-side)"
Push-Location (Join-Path $repo "ForgeSelf.Web")
try { pnpm run test src/utils/__tests__/pluginViewLoader.test.ts }
finally { Pop-Location }

Write-Host "[scaffold] done. Next steps:"
Write-Host "  1. Edit components in $dst"
Write-Host "  2. Register 'frontend' in Plugins/$Plugin/plugin.json (see docs/05-guides/plugin-frontend-development.md)"
Write-Host "  3. Publish & verify with .agents/skills/plugin-publish-verify/"
