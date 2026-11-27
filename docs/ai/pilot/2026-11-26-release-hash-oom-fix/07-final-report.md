# 07-final-report：发布链 SHA256 certutil 修复

## Validation

- AC1/AC2/AC3 全部通过（证据见 05-evidence.md，等级 Verified）。
- 根因：发布链为长跑 pwsh 进程，签名等多步累积内存压力下 `Get-FileHash` 对 145MB zip 分配大缓冲抛 `OutOfMemoryException`；zip 本体完好，失败点仅在哈希。
- 修复：`release-lib.ps1` 新增 `Get-FileSha256`（certutil 独立子进程），替换 `package-release.ps1`、`release-local.ps1` 两处调用。
- 复证：次日（2026-11-27）全链打包同点 10.8s 顺利通过，updates 复算 verified=1。

## Review

- Final Decision：APPROVED（见 06-review.md）。

## 变更文件

| 文件 | 变更 |
|---|---|
| scripts/release/release-lib.ps1 | 新增 Get-FileSha256（certutil 子进程，中文注释） |
| scripts/release/package-release.ps1 | zip+sha256 步改调新函数 |
| scripts/release/release-local.ps1 | UpdateDir 复算步改调新函数 |
