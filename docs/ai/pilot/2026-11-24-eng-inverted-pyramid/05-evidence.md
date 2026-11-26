# 05-evidence · 验证证据

> 全部为实跑结果。标注：Verified = 本会话实跑；Inferred = 推断；Unknown = 未验证。

## 门禁实跑（核心证据，Verified）
- **证据A**：`verify-pilot-feature-writeback.ps1 -TaskId 2026-11-24-pnpm-nontty-purge-confirm`（真实 mini-task 目录）→ `OK...轻量任务（mini-task.md 无 YAML 头部，跳过）` **exit 0**（原行为 exit 1 假红，即输入27 被迫 `--no-verify` 的根因）。
- **证据B**：模拟暂存 mini-task 批 22 文件 → `sh .git/hooks/pre-commit` **exit 0** 五道全过（卫生 PASS 710 行 0 命中 / 工程规则 PASS / PILOT 工件链轻量 PASS / feature-writeback OK 跳过）→ `sh .git/hooks/commit-msg`（带 Pilot trailer）**exit 0**。演示后 `git reset` 恢复，未真提交。
- **hook 安装**：临时 git 仓库实跑 `install-git-hooks.ps1` → exit 0，pre-commit + commit-msg 双装且可执行（输出「已安装 commit-msg」）。

## 自动化验证（Verified）
| 项 | 结果 |
|---|---|
| `pnpm run check` | 全绿（vue-tsc / eslint 0 err / component-conventions / theme-check 568 token / check-engineering-rules PASS） |
| `pnpm run test` | 123 文件 1277 测试全过 |
| `dotnet build` | 0 错误 |
| 本任务守卫测试（4 类） | 15/15 全绿（PilotWriteback 3 / InstallGitHooks 1 / EngineeringRules 4 / CommitHygiene 7） |
| 全量 `dotnet test` | 4034 总计 / 4002 过 / 32 失败——32 全为既有失败（与本任务改动面交集为空；抽查 RootDevCommand 证实系 dev 启动器文本漂移），已记 TODO |

## 过程证据（守卫抓真 bug）
- `InstallGitHooksGuardTests` 曾红：补 BOM 用 `Set-Content` 文本往返把 `install-git-hooks.ps1` 毁成 mojibake（ParserError:51）。修复：`git checkout --` 恢复 + 字节级前缀加 BOM，守卫转绿。
- `CommitHygieneGuardTests` 新回归用例暴露 `Read-TextLines` 单行文件逐字符扫描 bug，修复后单条 denylist 规则可命中。
