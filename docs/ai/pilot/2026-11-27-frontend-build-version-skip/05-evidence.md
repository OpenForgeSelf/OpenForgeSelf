# 05 Evidence — 前端构建版本感知增量

## Changed Files

| 文件 | 变更 |
|---|---|
| `ForgeSelf.Web/package.json` | version 0.0.0 → 2.4.22（对齐 csproj VersionPrefix） |
| `scripts/release/build-frontend.ps1` | 新增 manifest 读写/跳过判断/回写（5 函数）；`Invoke-PnpmBuild` 加 Key/Artifact 参数；宿主步骤前加版本一致性警告 |
| `artifacts/frontend-build-versions.json` | 运行时产物（gitignored）：16 条记录（宿主 + 15 插件） |
| `docs/ai/pilot/2026-11-27-frontend-build-version-skip/*` | 本工件链 |

## 验证（全部 Verified，真实命令真实输出）

| # | 场景 | 命令 | 结果 |
|---|---|---|---|
| ① | 宿主首跑（manifest 无记录） | `build-frontend.ps1 -HostOnly` | rc=0，重建 54.3s，manifest 写入 `ForgeSelf.Web/2.4.22` |
| ② | 宿主二跑 | 同上 | rc=0，`[skip] host-web: version 2.4.22 unchanged, reusing artifact`，0.0s |
| ③a | 插件首跑（AIAgent） | `-PluginsOnly -Plugin AIAgent` | rc=0，重建 12.1s，manifest 写入 |
| ③b | 插件二跑 | 同上 | rc=0，`[skip]` 0.0s |
| ③c | 版本变更（1.3.0→1.3.1） | node 改版本后同命令 | rc=0，重建 10.2s（无 skip），manifest 更新 1.3.1；还原 1.3.0 后重建，manifest 回 1.3.0 |
| ④ | 宿主版本 ≠ csproj（2.4.23 vs 2.4.22） | 临时改宿主版本跑任意构建 | 输出黄色 WARNING 提示两处同步 bump；已还原 2.4.22 |
| ⑤ | 全量 16 单元终验 | `build-frontend.ps1`（无参） | rc=0，16 条 `[skip]`，宿主 0.0s + 插件 0.1s |

## 过程记录（诚实留痕）

- 验证 ③c 首次尝试用 PowerShell 5.1 `Set-Content -Encoding utf8` 改 package.json，**写入了 BOM 导致 PostCSS/JSON 解析炸**（`Unexpected token \u0000`）——验证手段问题，非脚本缺陷；已 `git checkout` 还原文件，改用 node 读写后通过。教训：改 JSON 必须用 node 或无 BOM 写入（本机 PowerShell 5.1 utf8 带 BOM）。
- manifest 中 14 个插件的 `builtAt` 补录为 2026-11-26 09:41（昨日分段构建的真实完成时间），非本次构建时间——dist 为昨日真实产物、版本未变，补录等价于"构建后记录"。

## Known Limitations

- 改码未 bump 版本 → 不会触发重建（用户明确选择人工版本纪律，弃用源码指纹方案）。
- manifest 是单机文件（artifacts/ 不入库）；新 clone / 清 artifacts 后首跑全量重建一次，属预期。

## Unresolved Issues

无。
