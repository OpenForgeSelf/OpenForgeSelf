# Final Report — 发布链 pnpm install (host-web) 恒败修复（输入5 2026-11-23）

## 结论

**COMPLETED** — 根因修复（PC-043 首发 + G2 守卫落地），用户发布命令本机恢复可跑（原位复现 exit=1 → 修复后 exit=0）；中档门禁全量跑毕，3 处本批自引入红当批修复转绿，34 处既有基线红逐类归属记 TODO。

## 根因（Verified）

`scripts/install-git-hooks.ps1` 的 `Test-IsExecutable` **bash 分支先于 `if ($IsWindows)` 短路**：本机 git-bash 在 PATH 时，MSYS 内建 `test -x` 收 `盘符:\` 形态 Windows 路径恒 false → postinstall 钩子被误判不可执行 → `:94 throw` → ELIFECYCLE 1。自 4a3f67f（postinstall 自动装 hook）起，本机（Windows + git-bash）一切 `pnpm install` 必挂；CI（linux）不受影响。

## 验证（全部 Verified）

| 项 | 结果 |
|---|---|
| 红灯（修复前） | 主仓原位 `pnpm install --frozen-lockfile` **exit=1**（`:94 throw hook 安装失败`） |
| 绿灯（修复后） | 同命令 **exit=0**，「安装校验通过」「Done in 3.1s」 |
| G2 守卫 | `RepositoryScriptTests.跨平台脚本平台短路必须先于POSIX探测_安装钩子` 全量套件中通过 |
| 全量后端（中档） | **3985 通过 / 37 失败 / 4022 总 / 21 分钟**；37 失败 = 3 本批自引入（BOM 丢失 ×1、case 漏登索引 ×1、PC-024 悬空引用 ×2 处）当批修复 + 34 既有基线/环境红（逐类归属记 TODO） |
| 过滤复跑 | `--filter RepositoryScriptTests\|ProblemClassRegistryTests` = **40/41**（唯一余红 = 既有 RootDevCommand 陈旧守卫） |
| 机检 | check-problem-classes OK（42 PC 含 PC-043）；hygiene -Staged **PASS files=41 hits=0**（终态） |

## 问题类归档 / 重构提案 / 悬空教训（三节）

- **问题类归档**：**PC-043**（跨平台脚本平台短路必须先于 POSIX 探测）+ case `2026-11-23-release-pnpm-install-hook-fail.md` + 台账/索引行齐备；编号经重编号避让主仓在途 PC-042（hover-reveal，另一会话未提交占用），双 PC-042 覆辙（PC-034 前车）未重蹈。
- **重构提案**：`Set-ExecutableBit`/`Test-IsExecutable` 的 Windows 语义可再简化（NTFS 无 +x 概念，整段可提前 return），已并入 PC-043 守卫面；本批不改结构（最小修复原则）。
- **悬空教训**：① **编辑工具会静默剥 UTF-8 BOM**（PS1 中文脚本被守卫拦下当批修复；已记 TODO 候选=编辑后 BOM 断言机制化）；② **PC 编号是跨会话共享资源**——归档前必须先扫全仓在途引用（含他人未提交工作区）再占号；③ 仓库级守卫（BOM/注册表/顺序断言）本次全部命中真实回归，中档门禁对 scripts/** 的触发条件被再次验证必要。

## Validation / Review（验证复核）

Review（06-review.md）Final Decision = **APPROVED**（自审 2026-11-23）：八问全过，无 CHANGES_REQUIRED 项；本节上表为 Validation 汇总，全部条目为 Verified 等级。

## 风险 / 遗留

- 主仓 checkout 的同 patch 热修（未提交工作区改动）在本提交合入主分支后即为冗余，可 `git checkout -- scripts/install-git-hooks.ps1` 或随合并消失；用户重跑发布命令不受影响。
- 34 处既有基线红（ProjectWorkspace×15 陈旧断言、RootDevCommand 陈旧守卫、PC-034 双文件、PC-011/PC-027 迁移欠账、WorkflowPlanning 404×6、AIService 夹具×3 等）→ TODO 一次对账批。
- dev-stack 已重启供验收（后端 :7301 / 前端 :7399）。
