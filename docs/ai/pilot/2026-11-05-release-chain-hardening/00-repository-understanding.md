# 00 Repository Understanding（task-13 发行链加固）

- 发行链=release-local.ps1（编排）→ build-frontend/publish-host/publish-bootstrapper/package-release/make-release-notes → UpdateDir 三件套拷贝。release-lib.ps1=共享函数（Get-ReleaseRepoRoot=PSScriptRoot\..\..）。
- SHA256SUMS.txt 由 package-release.ps1 生成（`<hash>  <zip名>` ascii 单条目）；UpdateDir 拷贝在 release-local L99-105（拷 zip/SHA256SUMS/RELEASE-NOTES）。
- worktree 布局：wt-<hash> 建于主仓 .temp\ 下 → Get-ReleaseRepoRoot 的 Split-Path -Parent 落到主仓 .temp（updates 盲区）。
- 环境面：harness 会话进程带 ESBUILD_BINARY_PATH=全局 esbuild 0.25.12；插件 web pnpm install 的 esbuild@0.25.0 postinstall 按它校验即炸（patch-esbuild.mjs 中和在 install 后，救不了 install）。
