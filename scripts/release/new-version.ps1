# new-version.ps1 - 版本串辅助入口（2026-10-02 版本规则，真源 docs/04-standards/packaging-upgrade-backup.md §1.1）
# 输入三段发行号 → 输出完整发行串（补 10 位时间码 yyMMddHHmm）与可直接执行的 git tag 命令。
# 只打印，不做任何 git 写操作：提交 / 打 tag / 推送一律由人执行（agent 未经明确指令不得代劳）。
#
# Examples:
#   ./new-version.ps1 -Version 2.3.0
#     → 2.3.0.2610021630
#       git tag -a v2.3.0.2610021630 -m "ForgeSelf v2.3.0.2610021630"
#   ./new-version.ps1 -Version 2.3.0.2609161125   # 已含时间码时幂等原样返回

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')

$full = Get-FullReleaseVersion $Version

Write-Host ("{0}" -f $full)
Write-Host ('git tag -a v{0} -m "ForgeSelf v{0}"' -f $full)
